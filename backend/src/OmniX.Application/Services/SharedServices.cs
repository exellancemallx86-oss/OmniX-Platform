using OmniX.Domain.Entities.Auth;
using OmniX.Domain.Enums;
using OmniX.Infrastructure.Data;
using OtpNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace OmniX.Application.Services;

// ════════════════════════════════════════════════════════════════════════════
//  ApiResponse<T> — Standard API Response wrapper
// ════════════════════════════════════════════════════════════════════════════
public class ApiResponse<T>
{
    public bool    Success { get; init; }
    public string? Message { get; init; }
    public T?      Data    { get; init; }
    public List<string>? Errors { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null)
        => new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message, List<string>? errors = null)
        => new() { Success = false, Message = message, Errors = errors };
}

public class ApiResponse
{
    public bool    Success { get; init; }
    public string? Message { get; init; }

    public static ApiResponse Ok(string? message = null)
        => new() { Success = true, Message = message };

    public static ApiResponse Fail(string message)
        => new() { Success = false, Message = message };
}

// ════════════════════════════════════════════════════════════════════════════
//  ITotpService — TOTP 2FA (RFC 6238) — من Ultra v6
// ════════════════════════════════════════════════════════════════════════════
public interface ITotpService
{
    (string secret, string qrUri) GenerateSecret(string email);
    bool ValidateCode(string secret, string code);
    string GenerateCode(string secret);
}

public class TotpService : ITotpService
{
    private const string ISSUER = "OmniX Platform";

    public (string secret, string qrUri) GenerateSecret(string email)
    {
        var key     = KeyGeneration.GenerateRandomKey(20);
        var secret  = Base32Encoding.ToString(key);
        var encoded = Uri.EscapeDataString(email);
        var qrUri   = $"otpauth://totp/{Uri.EscapeDataString(ISSUER)}:{encoded}" +
                      $"?secret={secret}&issuer={Uri.EscapeDataString(ISSUER)}&algorithm=SHA1&digits=6&period=30";
        return (secret, qrUri);
    }

    public bool ValidateCode(string secret, string code)
    {
        try
        {
            var totp = new Totp(Base32Encoding.ToBytes(secret));
            return totp.VerifyTotp(code, out _, VerificationWindow.RfcSpecifiedNetworkDelay);
        }
        catch { return false; }
    }

    public string GenerateCode(string secret)
    {
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.ComputeTotp();
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  IAuditService — Audit Logging — من Ultra v6
// ════════════════════════════════════════════════════════════════════════════
public interface IAuditService
{
    Task LogAsync(Guid? tenantId, Guid? userId, AuditAction action,
        string entity, Guid? entityId,
        string? oldValues = null, string? newValues = null,
        string? details = null, string? ip = null,
        bool success = true, string? error = null,
        CancellationToken ct = default);
}

public class AuditService : IAuditService
{
    private readonly OmniXDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AuditService> _log;

    public AuditService(OmniXDbContext db, ICurrentUserService currentUser,
        ILogger<AuditService> log)
    { _db = db; _currentUser = currentUser; _log = log; }

    public async Task LogAsync(Guid? tenantId, Guid? userId, AuditAction action,
        string entity, Guid? entityId,
        string? oldValues = null, string? newValues = null,
        string? details = null, string? ip = null,
        bool success = true, string? error = null,
        CancellationToken ct = default)
    {
        try
        {
            var log = new AuditLog
            {
                TenantId   = tenantId ?? _currentUser.TenantId ?? Guid.Empty,
                UserId     = userId   ?? _currentUser.UserId,
                Action     = action,
                Entity     = entity,
                EntityId   = entityId,
                OldValues  = oldValues,
                NewValues  = newValues ?? details,
                IpAddress  = ip,
                IsSuccess  = success,
                ErrorMessage = error,
            };
            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to write audit log: {Action} {Entity}", action, entity);
        }
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  IFeatureFlagService — Feature Flags per-Tenant
// ════════════════════════════════════════════════════════════════════════════
public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(Guid tenantId, string feature, CancellationToken ct = default);
    Task SetAsync(Guid tenantId, string feature, bool enabled, CancellationToken ct = default);
}

public class FeatureFlagService : IFeatureFlagService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public FeatureFlagService(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public async Task<bool> IsEnabledAsync(Guid tenantId, string feature, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OmniXDbContext>();
        var flag = await db.FeatureFlags.AsNoTracking()
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Feature == feature, ct);
        return flag?.IsEnabled ?? false;
    }

    public async Task SetAsync(Guid tenantId, string feature, bool enabled, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OmniXDbContext>();
        var flag = await db.FeatureFlags
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Feature == feature, ct);

        if (flag == null)
        {
            db.FeatureFlags.Add(new FeatureFlag
            {
                TenantId  = tenantId,
                Feature   = feature,
                IsEnabled = enabled,
                EnabledAt = enabled ? DateTime.UtcNow : null,
            });
        }
        else
        {
            flag.IsEnabled = enabled;
            flag.EnabledAt = enabled ? DateTime.UtcNow : flag.EnabledAt;
        }
        await db.SaveChangesAsync(ct);
    }
}

// ── Placeholder interfaces (implemented in their own files) ──────────────
public interface ICurrentUserService { Guid? UserId { get; } Guid? TenantId { get; } string? Role { get; } bool IsAuthenticated { get; } }
public interface IPosService { }
public interface IInventoryService { }
public interface ICustomerService { }
public interface IDashboardService { }
public interface ISyncService { }
public interface IRestaurantService { }
public interface IRentalService { }
public interface IMallService { }
public interface IMallOrderService { }
public interface ILoyaltyService { }
public interface IWalletService { }
public interface IPaymobService { }
public interface IDeliveryService { }
public interface ILicenseService { }
public interface IAIService { }
public interface IMallAuthService { }

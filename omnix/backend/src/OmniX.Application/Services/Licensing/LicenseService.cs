using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniX.Application.Services.Auth;
using OmniX.Domain.Entities.Licensing;
using OmniX.Domain.Enums;
using OmniX.Infrastructure.Data;
using OmniX.Infrastructure.Caching;

namespace OmniX.Application.Services.Licensing;

// ════════════════════════════════════════════════════════════════════════════
//  DTOs
// ════════════════════════════════════════════════════════════════════════════
public record LicenseStatusDto(
    Guid TenantId, PlanType PlanType, LicenseStatus Status,
    DateTime ActivatedAt, DateTime? ExpiresAt, bool SyncAddonActive,
    int MaxBranches, int MaxUsers, int MaxProducts,
    bool IsExpired, int DaysRemaining);

public record ActivateLicenseRequest(string LicenseKey, string DeviceFingerprint);

public interface ILicenseService
{
    Task<ApiResponse<LicenseStatusDto>> GetStatusAsync(CancellationToken ct = default);
    Task<ApiResponse<LicenseStatusDto>> ActivateAsync(ActivateLicenseRequest req, CancellationToken ct = default);
    Task<bool>                          HasModuleAsync(TenantModules module, CancellationToken ct = default);
    Task<bool>                          WithinLimitAsync(string limitName, CancellationToken ct = default);
}

// ════════════════════════════════════════════════════════════════════════════
//  LicenseService — من Ultra v6
// ════════════════════════════════════════════════════════════════════════════
public class LicenseService : ILicenseService
{
    private readonly OmniXDbContext      _db;
    private readonly ITenantProvider     _tenant;
    private readonly ICacheService       _cache;
    private readonly ILogger<LicenseService> _log;

    public LicenseService(OmniXDbContext db, ITenantProvider tenant,
        ICacheService cache, ILogger<LicenseService> log)
    { _db=db; _tenant=tenant; _cache=cache; _log=log; }

    public async Task<ApiResponse<LicenseStatusDto>> GetStatusAsync(CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new UnauthorizedAccessException();
        var cacheKey = $"license:{tenantId}";

        var cached = await _cache.GetAsync<LicenseStatusDto>(cacheKey);
        if (cached is not null) return ApiResponse<LicenseStatusDto>.Ok(cached);

        var license = await _db.Licenses
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && !l.IsDeleted, ct);

        if (license is null)
        {
            // Default: Trial
            var trial = new LicenseStatusDto(tenantId, PlanType.Trial, LicenseStatus.Active,
                DateTime.UtcNow, DateTime.UtcNow.AddDays(14), false, 1, 2, 50,
                false, 14);
            return ApiResponse<LicenseStatusDto>.Ok(trial);
        }

        var now = DateTime.UtcNow;
        var isExpired  = license.ExpiresAt.HasValue && license.ExpiresAt < now;
        var daysLeft   = license.ExpiresAt.HasValue
            ? Math.Max(0, (int)(license.ExpiresAt.Value - now).TotalDays) : 9999;

        var dto = new LicenseStatusDto(
            tenantId, license.PlanType, isExpired ? LicenseStatus.Expired : license.Status,
            license.ActivatedAt, license.ExpiresAt, license.SyncAddonActive,
            license.MaxBranches, license.MaxUsers, license.MaxProducts,
            isExpired, daysLeft);

        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));
        return ApiResponse<LicenseStatusDto>.Ok(dto);
    }

    public async Task<ApiResponse<LicenseStatusDto>> ActivateAsync(
        ActivateLicenseRequest req, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new UnauthorizedAccessException();
        var keyHash  = SecurityHelper.HashSha256(req.LicenseKey);

        var license = await _db.Licenses
            .FirstOrDefaultAsync(l => l.LicenseKeyHash == keyHash, ct);
        if (license is null) return ApiResponse<LicenseStatusDto>.Fail("مفتاح الترخيص غير صحيح");
        if (license.Status == LicenseStatus.Revoked) return ApiResponse<LicenseStatusDto>.Fail("هذا الترخيص ملغى");

        license.TenantId              = tenantId;
        license.Status                = LicenseStatus.Active;
        license.ActivatedAt           = DateTime.UtcNow;
        license.DeviceFingerprintHash = SecurityHelper.HashSha256(req.DeviceFingerprint);
        license.UpdatedAt             = DateTime.UtcNow;

        // Update tenant plan
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, ct);
        if (tenant is not null) { tenant.Plan = license.PlanType; tenant.UpdatedAt = DateTime.UtcNow; }

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"license:{tenantId}");

        _log.LogInformation("[License] Activated Plan={Plan} for Tenant={TenantId}",
            license.PlanType, tenantId);

        return await GetStatusAsync(ct);
    }

    public async Task<bool> HasModuleAsync(TenantModules module, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        if (!tenantId.HasValue) return false;
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId.Value, ct);
        return tenant?.ActiveModules.HasFlag(module) ?? false;
    }

    public async Task<bool> WithinLimitAsync(string limitName, CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (!status.Success || status.Data is null) return false;
        var lic = status.Data;

        return limitName switch {
            "branches" => await _db.Branches.CountAsync(b => b.TenantId == lic.TenantId && !b.IsDeleted, ct) < lic.MaxBranches || lic.MaxBranches < 0,
            "users"    => await _db.Users.CountAsync(u => u.TenantId == lic.TenantId && !u.IsDeleted, ct) < lic.MaxUsers || lic.MaxUsers < 0,
            "products" => await _db.Products.CountAsync(p => p.TenantId == lic.TenantId && !p.IsDeleted, ct) < lic.MaxProducts || lic.MaxProducts < 0,
            _          => true
        };
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  FeatureFlagService — per-Tenant feature toggles (من Ultra v6)
// ════════════════════════════════════════════════════════════════════════════
public interface IFeatureFlagService
{
    Task<bool>   IsEnabledAsync(Guid tenantId, string feature, CancellationToken ct = default);
    Task         SetAsync(Guid tenantId, string feature, bool enabled, string? config = null, CancellationToken ct = default);
    Task<object> GetAllAsync(Guid tenantId, CancellationToken ct = default);
}

public class FeatureFlagService : IFeatureFlagService
{
    private readonly IServiceScopeFactory _scope;
    private readonly ICacheService        _cache;

    public FeatureFlagService(IServiceScopeFactory scope, ICacheService cache)
    { _scope = scope; _cache = cache; }

    public async Task<bool> IsEnabledAsync(Guid tenantId, string feature, CancellationToken ct = default)
    {
        var cacheKey = $"ff:{tenantId}:{feature}";
        var cached   = await _cache.GetAsync<bool?>(cacheKey);
        if (cached.HasValue) return cached.Value;

        using var scope = _scope.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<OmniXDbContext>();
        var now = DateTime.UtcNow;

        var flag = await db.FeatureFlags
            .FirstOrDefaultAsync(f => f.TenantId == tenantId
                && f.Feature == feature && !f.IsDeleted
                && (f.ExpiresAt == null || f.ExpiresAt > now), ct);

        var enabled = flag?.IsEnabled ?? false;
        await _cache.SetAsync(cacheKey, (bool?)enabled, TimeSpan.FromMinutes(5));
        return enabled;
    }

    public async Task SetAsync(Guid tenantId, string feature, bool enabled,
        string? config = null, CancellationToken ct = default)
    {
        using var scope = _scope.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<OmniXDbContext>();

        var flag = await db.FeatureFlags
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Feature == feature, ct);

        if (flag is null)
            db.FeatureFlags.Add(new OmniX.Domain.Entities.Auth.FeatureFlag {
                TenantId = tenantId, Feature = feature, IsEnabled = enabled,
                Config = config, EnabledAt = enabled ? DateTime.UtcNow : null,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
        else
        {
            flag.IsEnabled = enabled; flag.Config = config ?? flag.Config;
            flag.EnabledAt = enabled ? DateTime.UtcNow : flag.EnabledAt;
            flag.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"ff:{tenantId}:{feature}");
    }

    public async Task<object> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var scope = _scope.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OmniXDbContext>();
        return await db.FeatureFlags
            .Where(f => f.TenantId == tenantId && !f.IsDeleted)
            .Select(f => new { f.Feature, f.IsEnabled, f.Config, f.EnabledAt })
            .ToListAsync(ct);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  LicenseController
//  Route: /api/v1/license
// ════════════════════════════════════════════════════════════════════════════
namespace OmniX.API.Controllers.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/license")]
public class LicenseController : OmniXBaseController
{
    private readonly ILicenseService _license;
    public LicenseController(ILicenseService license) => _license = license;

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
        => Ok(await _license.GetStatusAsync(ct));

    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateLicenseRequest req, CancellationToken ct)
    {
        var res = await _license.ActivateAsync(req, ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }

    [HttpGet("check/{limitName}")]
    public async Task<IActionResult> CheckLimit(string limitName, CancellationToken ct)
        => Ok(new { allowed = await _license.WithinLimitAsync(limitName, ct) });
}

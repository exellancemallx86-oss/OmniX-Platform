using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OmniX.Domain.Entities.Auth;
using OmniX.Domain.Enums;
using OmniX.Infrastructure.Data;

namespace OmniX.Application.Services.Auth;

// ════════════════════════════════════════════════════════════════════════════
//  DTOs
// ════════════════════════════════════════════════════════════════════════════
public record LoginRequest(string? Username, string? Email, string Password, string? MfaCode);
public record RegisterRequest(string Username, string Email, string Password,
    string FirstName, string LastName, string? Phone, Guid TenantId,
    Guid? BranchId, UserRole Role = UserRole.Cashier);
public record ChangePasswordRequest(string OldPassword, string NewPassword, string ConfirmPassword);
public record MfaVerifyRequest(string Code);
public record RefreshRequest(string Token);

public record AuthResponse(string AccessToken, string RefreshToken,
    int ExpiresIn, UserDto User, bool RequiresMfa = false);
public record MfaSetupResponse(string Secret, string QrUrl, List<string> BackupCodes);
public record UserDto(Guid Id, string Username, string Email,
    string FirstName, string LastName, string FullName,
    string? Phone, string? AvatarUrl, UserRole Role,
    Guid? BranchId, string? BranchName, bool IsActive,
    bool TwoFactorEnabled, DateTime? LastLoginAt, DateTime CreatedAt);

public record ApiResponse<T>(bool Success, string Message, T? Data = default)
{
    public static ApiResponse<T> Ok(T data, string msg = "success") => new(true, msg, data);
    public static ApiResponse<T> Fail(string msg) => new(false, msg, default);
}
public record ApiResponse(bool Success, string Message)
{
    public static ApiResponse Ok(string msg = "success") => new(true, msg);
    public static ApiResponse Fail(string msg) => new(false, msg);
}

// ════════════════════════════════════════════════════════════════════════════
//  Interfaces
// ════════════════════════════════════════════════════════════════════════════
public interface IAuthService
{
    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest req, string ip, string ua, CancellationToken ct = default);
    Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default);
    Task<ApiResponse<AuthResponse>> RefreshTokenAsync(string rawToken, string ip, CancellationToken ct = default);
    Task<ApiResponse>               LogoutAsync(string rawToken, CancellationToken ct = default);
    Task<ApiResponse<UserDto>>      GetCurrentUserAsync(CancellationToken ct = default);
    Task<ApiResponse>               ChangePasswordAsync(ChangePasswordRequest req, CancellationToken ct = default);
    Task<ApiResponse<MfaSetupResponse>> SetupMfaAsync(CancellationToken ct = default);
    Task<ApiResponse>               VerifyMfaAsync(MfaVerifyRequest req, CancellationToken ct = default);
    Task<ApiResponse>               DisableMfaAsync(MfaVerifyRequest req, CancellationToken ct = default);
}

public interface ITotpService
{
    (string Secret, string QrUrl) GenerateSecret(string email, string issuer);
    bool   ValidateCode(string secret, string code);
    string GenerateBackupCode();
}

public interface IAuditService
{
    Task LogAsync(Guid tenantId, Guid? userId, AuditAction action,
        string entity, Guid? entityId, bool isSuccess = true,
        string? error = null, CancellationToken ct = default);
}

public interface ICurrentUserService
{
    Guid?   UserId       { get; }
    Guid?   TenantId     { get; }
    string? Username     { get; }
    string? Role         { get; }
    bool    IsAuthenticated { get; }
}

// ════════════════════════════════════════════════════════════════════════════
//  SecurityHelper
// ════════════════════════════════════════════════════════════════════════════
public static class SecurityHelper
{
    public static string HashPassword(string p) => BCrypt.Net.BCrypt.HashPassword(p, 12);
    public static bool VerifyPassword(string p, string h) => BCrypt.Net.BCrypt.Verify(p, h);

    public static (string Raw, string Hash, string Salt) GenerateRefreshToken()
    {
        var raw  = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (raw, HashWithSalt(raw, salt), salt);
    }

    public static bool VerifyRefreshToken(string raw, string hash, string salt)
        => HashWithSalt(raw, salt) == hash;

    public static string HashSha256(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLower();

    private static string HashWithSalt(string v, string s) => HashSha256(v + s);

    public static (bool Ok, string? Error) ValidatePasswordPolicy(string pw)
    {
        if (pw.Length < 8)         return (false, "كلمة المرور يجب أن تكون 8 أحرف على الأقل");
        if (!pw.Any(char.IsUpper)) return (false, "يجب أن تحتوي على حرف كبير");
        if (!pw.Any(char.IsDigit)) return (false, "يجب أن تحتوي على رقم");
        return (true, null);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  TotpService — RFC 6238
// ════════════════════════════════════════════════════════════════════════════
public class TotpService : ITotpService
{
    public (string Secret, string QrUrl) GenerateSecret(string email, string issuer)
    {
        var secret = Base32Encode(RandomNumberGenerator.GetBytes(20));
        var qr = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}" +
                 $"?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";
        return (secret, qr);
    }

    public bool ValidateCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code)) return false;
        try
        {
            var totp = new OtpNet.Totp(Base32Decode(secret));
            return totp.VerifyTotp(code, out _, new OtpNet.VerificationWindow(1, 1));
        }
        catch { return false; }
    }

    public string GenerateBackupCode()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLower();

    private static string Base32Encode(byte[] data)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new StringBuilder();
        for (int i = 0; i < data.Length; i += 5)
        {
            int b0 = data[i], b1 = i+1<data.Length?data[i+1]:0,
                b2 = i+2<data.Length?data[i+2]:0, b3 = i+3<data.Length?data[i+3]:0,
                b4 = i+4<data.Length?data[i+4]:0;
            sb.Append(chars[(b0>>3)&31]).Append(chars[((b0&7)<<2)|(b1>>6)]);
            sb.Append(chars[(b1>>1)&31]).Append(chars[((b1&1)<<4)|(b2>>4)]);
            sb.Append(chars[((b2&15)<<1)|(b3>>7)]).Append(chars[(b3>>2)&31]);
            sb.Append(chars[((b3&3)<<3)|(b4>>5)]).Append(chars[b4&31]);
        }
        return sb.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        input = input.TrimEnd('=').ToUpper();
        var bits = new StringBuilder();
        foreach (var c in input)
            bits.Append(Convert.ToString(chars.IndexOf(c), 2).PadLeft(5, '0'));
        var result = new byte[bits.Length / 8];
        for (int i = 0; i < result.Length; i++)
            result[i] = Convert.ToByte(bits.ToString(i * 8, 8), 2);
        return result;
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  AuditService
// ════════════════════════════════════════════════════════════════════════════
public class AuditService : IAuditService
{
    private readonly OmniXDbContext _db;
    public AuditService(OmniXDbContext db) => _db = db;

    public async Task LogAsync(Guid tenantId, Guid? userId, AuditAction action,
        string entity, Guid? entityId, bool isSuccess = true,
        string? error = null, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId, UserId = userId,
            Action = action, Entity = entity, EntityId = entityId,
            IsSuccess = isSuccess, ErrorMessage = error,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  AuthService — B2B (Ultra v6 — TOTP + Lockout + Token Rotation)
// ════════════════════════════════════════════════════════════════════════════
public class AuthService : IAuthService
{
    private readonly OmniXDbContext       _db;
    private readonly IConfiguration       _cfg;
    private readonly IAuditService        _audit;
    private readonly ITotpService         _totp;
    private readonly ICurrentUserService  _me;
    private readonly ILogger<AuthService> _log;

    private const int MAX_FAIL = 5, LOCK_MIN = 15, JWT_MIN = 60, REF_DAYS = 7;

    public AuthService(OmniXDbContext db, IConfiguration cfg, IAuditService audit,
        ITotpService totp, ICurrentUserService me, ILogger<AuthService> log)
    { _db=db; _cfg=cfg; _audit=audit; _totp=totp; _me=me; _log=log; }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(
        LoginRequest req, string ip, string ua, CancellationToken ct = default)
    {
        const string BAD = "بيانات الدخول غير صحيحة";
        var user = await _db.Users.Include(u => u.Branch)
            .FirstOrDefaultAsync(u =>
                (u.Username == req.Username || u.Email == req.Email) && !u.IsDeleted, ct);

        if (user == null)
        {
            await _audit.LogAsync(Guid.Empty, null, AuditAction.LoginFailed, "User", null, false, "not found", ct);
            return ApiResponse<AuthResponse>.Fail(BAD);
        }
        if (user.IsLockedOut)
        {
            var min = (int)Math.Ceiling((user.LockoutEnd!.Value - DateTime.UtcNow).TotalMinutes);
            return ApiResponse<AuthResponse>.Fail($"الحساب مقفول. حاول بعد {min} دقيقة");
        }
        if (!SecurityHelper.VerifyPassword(req.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MAX_FAIL)
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(LOCK_MIN);
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync(user.TenantId, user.Id, AuditAction.LoginFailed, "User", user.Id, false, "wrong pw", ct);
            return ApiResponse<AuthResponse>.Fail(BAD);
        }
        if (!user.IsActive) return ApiResponse<AuthResponse>.Fail("الحساب معطّل");
        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(req.MfaCode))
                return ApiResponse<AuthResponse>.Ok(new AuthResponse("","",0,ToDto(user),true), "أدخل كود التحقق الثنائي");
            if (!_totp.ValidateCode(user.TotpSecret!, req.MfaCode))
                return ApiResponse<AuthResponse>.Fail("كود التحقق الثنائي غير صحيح");
        }

        user.FailedLoginAttempts = 0; user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow; user.LastLoginIp = ip;
        user.LastLoginDevice = ua?[..Math.Min(ua?.Length??0,250)];

        var jwt = BuildJwt(user);
        var (raw, hash, salt) = SecurityHelper.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken {
            TenantId=user.TenantId, UserId=user.Id,
            TokenHash=hash, Salt=salt, IpAddress=ip,
            DeviceInfo=ua?[..Math.Min(ua?.Length??0,250)],
            ExpiresAt=DateTime.UtcNow.AddDays(REF_DAYS),
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(user.TenantId, user.Id, AuditAction.Login, "User", user.Id, ct: ct);
        return ApiResponse<AuthResponse>.Ok(new AuthResponse(jwt, raw, JWT_MIN*60, ToDto(user)));
    }

    public async Task<ApiResponse<AuthResponse>> RegisterAsync(
        RegisterRequest req, CancellationToken ct = default)
    {
        var (ok, err) = SecurityHelper.ValidatePasswordPolicy(req.Password);
        if (!ok) return ApiResponse<AuthResponse>.Fail(err!);
        if (await _db.Users.AnyAsync(u => u.Email == req.Email && !u.IsDeleted, ct))
            return ApiResponse<AuthResponse>.Fail("البريد الإلكتروني مستخدم بالفعل");
        if (await _db.Users.AnyAsync(u => u.Username == req.Username && !u.IsDeleted, ct))
            return ApiResponse<AuthResponse>.Fail("اسم المستخدم مستخدم بالفعل");

        var user = new ApplicationUser {
            TenantId=req.TenantId, BranchId=req.BranchId,
            Username=req.Username, Email=req.Email,
            PasswordHash=SecurityHelper.HashPassword(req.Password),
            FirstName=req.FirstName, LastName=req.LastName,
            Phone=req.Phone, Role=req.Role, IsActive=true,
        };
        _db.Users.Add(user);
        var (raw, hash, salt) = SecurityHelper.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken {
            TenantId=user.TenantId, UserId=user.Id,
            TokenHash=hash, Salt=salt, ExpiresAt=DateTime.UtcNow.AddDays(REF_DAYS),
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(user.TenantId, user.Id, AuditAction.Create, "User", user.Id, ct: ct);
        return ApiResponse<AuthResponse>.Ok(new AuthResponse(BuildJwt(user), raw, JWT_MIN*60, ToDto(user)));
    }

    public async Task<ApiResponse<AuthResponse>> RefreshTokenAsync(
        string rawToken, string ip, CancellationToken ct = default)
    {
        var all = await _db.RefreshTokens.Include(r => r.User).ThenInclude(u => u.Branch)
            .Where(r => !r.IsRevoked && r.ExpiresAt > DateTime.UtcNow).ToListAsync(ct);
        var rt = all.FirstOrDefault(r => SecurityHelper.VerifyRefreshToken(rawToken, r.TokenHash, r.Salt));
        if (rt == null) return ApiResponse<AuthResponse>.Fail("Refresh token غير صالح");
        rt.IsRevoked = true; rt.RevokedAt = DateTime.UtcNow;
        var (newRaw, newHash, newSalt) = SecurityHelper.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken {
            TenantId=rt.TenantId, UserId=rt.UserId,
            TokenHash=newHash, Salt=newSalt, IpAddress=ip, DeviceInfo=rt.DeviceInfo,
            ExpiresAt=DateTime.UtcNow.AddDays(REF_DAYS),
        });
        await _db.SaveChangesAsync(ct);
        return ApiResponse<AuthResponse>.Ok(new AuthResponse(BuildJwt(rt.User), newRaw, JWT_MIN*60, ToDto(rt.User)));
    }

    public async Task<ApiResponse> LogoutAsync(string rawToken, CancellationToken ct = default)
    {
        var all = await _db.RefreshTokens.Where(r => !r.IsRevoked).ToListAsync(ct);
        var rt  = all.FirstOrDefault(r => SecurityHelper.VerifyRefreshToken(rawToken, r.TokenHash, r.Salt));
        if (rt != null) { rt.IsRevoked = true; rt.RevokedAt = DateTime.UtcNow; }
        if (_me.UserId.HasValue && _me.TenantId.HasValue)
            await _audit.LogAsync(_me.TenantId.Value, _me.UserId, AuditAction.Logout, "User", _me.UserId, ct: ct);
        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok("تم تسجيل الخروج");
    }

    public async Task<ApiResponse<UserDto>> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var uid = _me.UserId ?? throw new UnauthorizedAccessException();
        var tid = _me.TenantId ?? throw new UnauthorizedAccessException();
        var user = await _db.Users.Include(u => u.Branch)
            .FirstOrDefaultAsync(u => u.Id == uid && u.TenantId == tid, ct);
        return user == null ? ApiResponse<UserDto>.Fail("غير موجود") : ApiResponse<UserDto>.Ok(ToDto(user));
    }

    public async Task<ApiResponse> ChangePasswordAsync(ChangePasswordRequest req, CancellationToken ct = default)
    {
        var uid = _me.UserId ?? throw new UnauthorizedAccessException();
        var tid = _me.TenantId ?? throw new UnauthorizedAccessException();
        if (req.NewPassword != req.ConfirmPassword) return ApiResponse.Fail("كلمتا المرور غير متطابقتين");
        var (ok, err) = SecurityHelper.ValidatePasswordPolicy(req.NewPassword);
        if (!ok) return ApiResponse.Fail(err!);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid && u.TenantId == tid, ct);
        if (user == null) return ApiResponse.Fail("غير موجود");
        if (!SecurityHelper.VerifyPassword(req.OldPassword, user.PasswordHash)) return ApiResponse.Fail("كلمة المرور الحالية غير صحيحة");
        user.PasswordHash = SecurityHelper.HashPassword(req.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        var old = await _db.RefreshTokens.Where(r => r.UserId == uid && !r.IsRevoked).ToListAsync(ct);
        foreach (var t in old) { t.IsRevoked = true; t.RevokedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(tid, uid, AuditAction.PasswordChange, "User", uid, ct: ct);
        return ApiResponse.Ok("تم تغيير كلمة المرور");
    }

    public async Task<ApiResponse<MfaSetupResponse>> SetupMfaAsync(CancellationToken ct = default)
    {
        var uid = _me.UserId ?? throw new UnauthorizedAccessException();
        var tid = _me.TenantId ?? throw new UnauthorizedAccessException();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid && u.TenantId == tid, ct);
        if (user == null) return ApiResponse<MfaSetupResponse>.Fail("غير موجود");
        if (user.TwoFactorEnabled) return ApiResponse<MfaSetupResponse>.Fail("التحقق الثنائي مفعّل بالفعل");
        var (secret, qrUrl) = _totp.GenerateSecret(user.Email, _cfg["Jwt:Issuer"] ?? "OmniX");
        var codes = Enumerable.Range(0, 8).Select(_ => _totp.GenerateBackupCode()).ToList();
        user.TotpSecret = secret;
        user.BackupCodes = System.Text.Json.JsonSerializer.Serialize(codes.Select(SecurityHelper.HashSha256));
        await _db.SaveChangesAsync(ct);
        return ApiResponse<MfaSetupResponse>.Ok(new MfaSetupResponse(secret, qrUrl, codes));
    }

    public async Task<ApiResponse> VerifyMfaAsync(MfaVerifyRequest req, CancellationToken ct = default)
    {
        var uid = _me.UserId ?? throw new UnauthorizedAccessException();
        var tid = _me.TenantId ?? throw new UnauthorizedAccessException();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid && u.TenantId == tid, ct);
        if (user?.TotpSecret == null) return ApiResponse.Fail("لم يتم إعداد MFA");
        if (!_totp.ValidateCode(user.TotpSecret, req.Code)) return ApiResponse.Fail("الكود غير صحيح");
        user.TwoFactorEnabled = true;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(tid, uid, AuditAction.MfaEnabled, "User", uid, ct: ct);
        return ApiResponse.Ok("تم تفعيل التحقق الثنائي");
    }

    public async Task<ApiResponse> DisableMfaAsync(MfaVerifyRequest req, CancellationToken ct = default)
    {
        var uid = _me.UserId ?? throw new UnauthorizedAccessException();
        var tid = _me.TenantId ?? throw new UnauthorizedAccessException();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid && u.TenantId == tid, ct);
        if (user == null) return ApiResponse.Fail("غير موجود");
        if (!_totp.ValidateCode(user.TotpSecret!, req.Code)) return ApiResponse.Fail("الكود غير صحيح");
        user.TwoFactorEnabled = false; user.TotpSecret = null; user.BackupCodes = null;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(tid, uid, AuditAction.MfaDisabled, "User", uid, ct: ct);
        return ApiResponse.Ok("تم تعطيل التحقق الثنائي");
    }

    private string BuildJwt(ApplicationUser u)
    {
        var secret = _cfg["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret مفقود");
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = int.TryParse(_cfg["Jwt:ExpiryMinutes"], out var e) ? e : JWT_MIN;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   u.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, u.Email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim("tenantId",  u.TenantId.ToString()),
            new Claim("userId",    u.Id.ToString()),
            new Claim("branchId",  u.BranchId?.ToString() ?? ""),
            new Claim(ClaimTypes.Role, u.Role.ToString()),
            new Claim("firstName", u.FirstName),
        };
        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(_cfg["Jwt:Issuer"] ?? "OmniX", _cfg["Jwt:Audience"] ?? "OmniXClient",
                claims, expires: DateTime.UtcNow.AddMinutes(expiry), signingCredentials: creds));
    }

    private static UserDto ToDto(ApplicationUser u) => new(
        u.Id, u.Username, u.Email, u.FirstName, u.LastName, u.FullName,
        u.Phone, u.AvatarUrl, u.Role, u.BranchId, u.Branch?.Name,
        u.IsActive, u.TwoFactorEnabled, u.LastLoginAt, u.CreatedAt);
}

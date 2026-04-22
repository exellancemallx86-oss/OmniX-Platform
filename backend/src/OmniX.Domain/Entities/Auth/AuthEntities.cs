using OmniX.Domain.Entities.Core;
using OmniX.Domain.Enums;

namespace OmniX.Domain.Entities.Auth;

// ════════════════════════════════════════════════════════════════════════════
//  ApplicationUser — المستخدم الموحد
//  مدمج من: Ultra v6 AuthEntities.cs + ApplicationUser.cs
//  إصلاح Bug: TOTP fields كانت في ملفين منفصلين — الآن في entity واحدة
// ════════════════════════════════════════════════════════════════════════════
public class ApplicationUser : BaseEntity
{
    // ── الهوية الأساسية ──────────────────────────────────────────────────────
    public required string Username     { get; set; }
    public required string Email        { get; set; }
    public required string PasswordHash { get; set; }
    public required string FirstName    { get; set; }
    public required string LastName     { get; set; }
    public string FullName              => $"{FirstName} {LastName}";
    public string? Phone                { get; set; }
    public string? AvatarUrl            { get; set; }

    // ── الدور والصلاحيات ────────────────────────────────────────────────────
    public UserRole Role   { get; set; } = UserRole.Cashier;
    public Guid?  BranchId { get; set; }
    public Guid?  RoleId   { get; set; }   // Custom role (ApplicationRole)

    // ── حالة الحساب ─────────────────────────────────────────────────────────
    public bool      IsActive        { get; set; } = true;
    public bool      IsEmailVerified { get; set; }
    public bool      IsPhoneVerified { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }

    // ── تاريخ الدخول ────────────────────────────────────────────────────────
    public DateTime? LastLoginAt     { get; set; }
    public string?   LastLoginIp     { get; set; }
    public string?   LastLoginDevice { get; set; }

    // ── Account Lockout ──────────────────────────────────────────────────────
    public int       FailedLoginAttempts { get; set; }
    public DateTime? LockoutEnd          { get; set; }
    public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow;

    // ── MFA / TOTP (RFC 6238) — من Ultra v6 ─────────────────────────────────
    public bool    TwoFactorEnabled { get; set; }
    public string? TotpSecret       { get; set; }   // AES-256 encrypted
    public string? BackupCodes      { get; set; }   // JSON array of hashed codes

    // ── Password ─────────────────────────────────────────────────────────────
    public DateTime? PasswordChangedAt  { get; set; }
    public bool      MustChangePassword { get; set; }

    // ── Preferences ─────────────────────────────────────────────────────────
    public string Language { get; set; } = "ar";
    public string Theme    { get; set; } = "dark";
    public string Timezone { get; set; } = "Africa/Cairo";

    // ── Navigation ──────────────────────────────────────────────────────────
    public virtual Tenant              Tenant        { get; set; } = null!;
    public virtual Branch?             Branch        { get; set; }
    public virtual ApplicationRole?    CustomRole    { get; set; }
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

// ════════════════════════════════════════════════════════════════════════════
//  RefreshToken — رمز التجديد (من Ultra v6)
// ════════════════════════════════════════════════════════════════════════════
public class RefreshToken : BaseEntity
{
    public Guid    UserId           { get; set; }
    public required string TokenHash { get; set; }
    public required string Salt      { get; set; }
    public string? DeviceInfo        { get; set; }
    public string? IpAddress         { get; set; }
    public bool    IsRevoked         { get; set; }
    public DateTime ExpiresAt        { get; set; }
    public DateTime? RevokedAt       { get; set; }
    public string?  ReplacedByToken  { get; set; }

    public virtual ApplicationUser User { get; set; } = null!;
}

// ════════════════════════════════════════════════════════════════════════════
//  ApplicationRole — الدور المخصص (من Ultra v6)
// ════════════════════════════════════════════════════════════════════════════
public class ApplicationRole : BaseEntity
{
    public required string Name        { get; set; }
    public required string NameAr      { get; set; }
    public string?  Description        { get; set; }
    public bool     IsSystem           { get; set; }
    public bool     IsActive           { get; set; } = true;

    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<ApplicationPermission> Permissions { get; set; } = [];
}

// ════════════════════════════════════════════════════════════════════════════
//  ApplicationPermission — الصلاحية (من Ultra v6)
// ════════════════════════════════════════════════════════════════════════════
public class ApplicationPermission : BaseEntity
{
    public Guid    RoleId              { get; set; }
    public required string Resource   { get; set; }
    public required string Action     { get; set; }
    public bool    CanCreate          { get; set; }
    public bool    CanRead            { get; set; }
    public bool    CanUpdate          { get; set; }
    public bool    CanDelete          { get; set; }
    public bool    CanExport          { get; set; }

    public virtual ApplicationRole Role { get; set; } = null!;
}

// ════════════════════════════════════════════════════════════════════════════
//  FeatureFlag — علامة الميزة per-Tenant (من Ultra v6)
// ════════════════════════════════════════════════════════════════════════════
public class FeatureFlag : BaseEntity
{
    public required string Feature { get; set; }
    public bool    IsEnabled       { get; set; }
    public string? Config          { get; set; }
    public DateTime? EnabledAt    { get; set; }
    public DateTime? ExpiresAt    { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}

// ════════════════════════════════════════════════════════════════════════════
//  AuditLog — سجل التدقيق الغير قابل للتعديل (من Ultra v6)
// ════════════════════════════════════════════════════════════════════════════
public class AuditLog : BaseEntity
{
    public Guid?        UserId       { get; set; }
    public string?      UserName     { get; set; }
    public AuditAction  Action       { get; set; }
    public required string Entity   { get; set; }
    public Guid?        EntityId     { get; set; }
    public string?      OldValues    { get; set; }   // JSON
    public string?      NewValues    { get; set; }   // JSON
    public string?      IpAddress    { get; set; }
    public string?      UserAgent    { get; set; }
    public bool         IsSuccess    { get; set; } = true;
    public string?      ErrorMessage { get; set; }

    public virtual Tenant? Tenant { get; set; }
}

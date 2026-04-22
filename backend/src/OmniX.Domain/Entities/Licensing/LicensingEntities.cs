using OmniX.Domain.Entities.Core;
using OmniX.Domain.Enums;

namespace OmniX.Domain.Entities.Licensing;

// ════════════════════════════════════════════════════════════════════════════
//  OmniX Licensing Entities — من Ultra v6 + MallX Phase 13
//  يشمل: Plans + Subscriptions + License + Device Validation + Plugins
// ════════════════════════════════════════════════════════════════════════════

public class Plan : BaseEntity
{
    public required string Name          { get; set; }
    public required string NameEn        { get; set; }
    public string?   Description         { get; set; }
    public PlanType  Type                { get; set; }
    public bool      IsActive            { get; set; } = true;
    public int       SortOrder           { get; set; }

    // ── الأسعار (بالجنيه المصري) ───────────────────────────────────────
    public decimal   MonthlyPrice        { get; set; }
    public decimal   AnnualPrice         { get; set; }
    public decimal   LifetimePrice       { get; set; }
    public decimal   SetupFee            { get; set; }

    // ── حدود الاستخدام ──────────────────────────────────────────────────
    public int MaxBranches               { get; set; }   // -1 = غير محدود
    public int MaxUsers                  { get; set; }
    public int MaxProducts               { get; set; }
    public int MaxCustomers              { get; set; } = -1;
    public int MaxOrders                 { get; set; } = -1;
    public int DataRetentionDays         { get; set; } = 365;

    // ── الوحدات المفعّلة ────────────────────────────────────────────────
    public bool HasPOS                   { get; set; } = true;
    public bool HasInventory             { get; set; } = true;
    public bool HasSyncEngine            { get; set; }
    public bool HasMultiBranch           { get; set; }
    public bool HasAdvancedReports       { get; set; }
    public bool HasApiAccess             { get; set; }
    public bool HasWhiteLabel            { get; set; }
    public bool HasAi                    { get; set; }
    public bool HasPluginStore           { get; set; }
    public bool HasMarketplace           { get; set; }
    public bool HasDelivery              { get; set; }
    public bool HasWhatsApp              { get; set; }
    public int  MaxWhatsAppNumbers       { get; set; }

    // ── وحدة المطعم ─────────────────────────────────────────────────────
    public bool HasRestaurantModule      { get; set; }
    public int  MaxRestaurantTables      { get; set; }
    public bool HasKDS                   { get; set; }
    public bool HasQRMenu                { get; set; }
    public bool HasTableReservations     { get; set; }
    public bool HasRestaurantAnalytics   { get; set; }

    // ── وحدة التأجير ────────────────────────────────────────────────────
    public bool HasRentalModule          { get; set; }
    public int  MaxRentalAssets          { get; set; }
    public bool HasRentalMarketplace     { get; set; }

    // ── وحدة المول ──────────────────────────────────────────────────────
    public bool HasMallModule            { get; set; }
    public int  MaxMallStores            { get; set; }
    public bool HasLoyaltyProgram        { get; set; }
    public bool HasWallet                { get; set; }

    // ── SLA ─────────────────────────────────────────────────────────────
    public string? SupportLevel          { get; set; }   // basic/priority/dedicated
    public int     UptimeSlaPercent      { get; set; } = 99;

    public virtual ICollection<Subscription> Subscriptions { get; set; } = [];
}

public class Subscription : BaseEntity
{
    public Guid              TenantId                { get; set; }
    public Guid              PlanId                  { get; set; }
    public SubscriptionStatus Status                 { get; set; }
    public BillingCycle      BillingCycle            { get; set; }
    public decimal           Amount                  { get; set; }
    public decimal           DiscountAmount          { get; set; }
    public DateTime          StartDate               { get; set; }
    public DateTime          EndDate                 { get; set; }
    public DateTime?         TrialEndsAt             { get; set; }
    public DateTime?         CancelledAt             { get; set; }
    public string?           CancelReason            { get; set; }
    public bool              AutoRenew               { get; set; } = true;
    public DateTime?         NextBillingDate         { get; set; }
    public string?           PaymentGateway          { get; set; }
    public string?           ExternalSubscriptionId  { get; set; }
    public string?           LastPaymentRef          { get; set; }
    public DateTime?         LastPaymentAt           { get; set; }
    public int               FailedPaymentAttempts   { get; set; }
    public bool              SyncAddonActive         { get; set; }
    public DateTime?         SyncAddonExpiresAt      { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Plan   Plan   { get; set; } = null!;
}

public class License : BaseEntity
{
    public required string LicenseKeyHash       { get; set; }
    public PlanType        PlanType             { get; set; }
    public LicenseStatus   Status               { get; set; }
    public DateTime        ActivatedAt          { get; set; }
    public DateTime?       ExpiresAt            { get; set; }
    public bool            SyncAddonActive      { get; set; }
    public DateTime?       SyncAddonExpiresAt   { get; set; }
    public string?         DeviceFingerprintHash { get; set; }
    public int             MaxBranches          { get; set; } = 1;
    public int             MaxUsers             { get; set; } = 1;
    public int             MaxProducts          { get; set; } = 500;
    public DateTime?       DowngradedAt         { get; set; }
    public string?         DowngradeReason      { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}

public class DeviceRegistration : BaseEntity
{
    public required string DeviceId          { get; set; }
    public required string FingerprintHash   { get; set; }
    public string?         DeviceName        { get; set; }
    public string?         Platform          { get; set; }   // windows/android/ios/web
    public string?         AppVersion        { get; set; }
    public bool            IsActive          { get; set; } = true;
    public DateTime        RegisteredAt      { get; set; } = DateTime.UtcNow;
    public DateTime?       LastSeenAt        { get; set; }
    public DateTime?       RevokedAt         { get; set; }
    public string?         RevokeReason      { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}

public class Plugin : BaseEntity
{
    public required string Name              { get; set; }
    public required string Slug             { get; set; }
    public string?   Description            { get; set; }
    public string?   DeveloperName          { get; set; }
    public string?   DeveloperUrl           { get; set; }
    public string?   IconUrl                { get; set; }
    public string    Version                { get; set; } = "1.0.0";
    public PluginCategory Category          { get; set; }
    public decimal   Price                  { get; set; }
    public bool      IsActive               { get; set; } = true;
    public bool      IsVerified             { get; set; }
    public bool      IsFeatured             { get; set; }
    public int       InstallCount           { get; set; }
    public decimal   Rating                 { get; set; }
    public string?   DocumentationUrl       { get; set; }

    public virtual ICollection<PluginInstallation> Installations { get; set; } = [];
}

public class PluginInstallation : BaseEntity
{
    public Guid    PluginId    { get; set; }
    public bool    IsEnabled   { get; set; } = true;
    public string? Config      { get; set; }
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;

    public virtual Plugin Plugin { get; set; } = null!;
    public virtual Tenant Tenant { get; set; } = null!;
}

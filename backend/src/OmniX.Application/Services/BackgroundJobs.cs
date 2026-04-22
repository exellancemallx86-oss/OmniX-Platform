using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OmniX.Domain.Enums;
using OmniX.Infrastructure.Data;

namespace OmniX.Application.Services;

// ════════════════════════════════════════════════════════════════════════════
//  LicenseEnforcementJob — من Ultra v6
//  يعمل كل ساعة — يُنزّل الخطة لـ Tenants منتهي اشتراكهم
// ════════════════════════════════════════════════════════════════════════════
public class LicenseEnforcementJob : BackgroundService
{
    private readonly IServiceScopeFactory _scope;
    private readonly ILogger<LicenseEnforcementJob> _log;

    public LicenseEnforcementJob(IServiceScopeFactory scope, ILogger<LicenseEnforcementJob> log)
    { _scope = scope; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("LicenseEnforcementJob started");
        while (!ct.IsCancellationRequested)
        {
            try { await RunAsync(ct); }
            catch (Exception ex) { _log.LogError(ex, "LicenseEnforcementJob error"); }
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = _scope.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OmniXDbContext>();

        var expired = await db.Licenses
            .Include(l => l.Tenant)
            .Where(l => l.Status == LicenseStatus.Active
                && l.ExpiresAt.HasValue
                && l.ExpiresAt.Value < DateTime.UtcNow
                && !l.IsDeleted)
            .ToListAsync(ct);

        foreach (var lic in expired)
        {
            lic.Status        = LicenseStatus.Expired;
            lic.DowngradedAt  = DateTime.UtcNow;
            lic.DowngradeReason = "License expired automatically";
            lic.Tenant.Plan   = PlanType.Trial;

            _log.LogWarning("License expired for tenant {TenantId}: {Name}",
                lic.TenantId, lic.Tenant.Name);
        }

        if (expired.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            _log.LogInformation("LicenseEnforcementJob: downgraded {Count} tenants", expired.Count);
        }
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  SyncCleanupJob — من MesterX Pro
//  يعمل كل يوم — يحذف Sync records القديمة (أكثر من 30 يوم)
// ════════════════════════════════════════════════════════════════════════════
public class SyncCleanupJob : BackgroundService
{
    private readonly IServiceScopeFactory _scope;
    private readonly ILogger<SyncCleanupJob> _log;

    public SyncCleanupJob(IServiceScopeFactory scope, ILogger<SyncCleanupJob> log)
    { _scope = scope; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scope.CreateScope();
                var db     = scope.ServiceProvider.GetRequiredService<OmniXDbContext>();
                var cutoff = DateTime.UtcNow.AddDays(-30);

                var deleted = await db.SyncLogs
                    .Where(s => s.CreatedAt < cutoff)
                    .ExecuteDeleteAsync(ct);

                var cleanedQueue = await db.SyncQueue
                    .Where(s => s.Status == SyncStatus.Completed && s.UpdatedAt < cutoff)
                    .ExecuteDeleteAsync(ct);

                _log.LogInformation("SyncCleanup: removed {Logs} logs, {Queue} queue items",
                    deleted, cleanedQueue);
            }
            catch (Exception ex) { _log.LogError(ex, "SyncCleanupJob error"); }

            await Task.Delay(TimeSpan.FromDays(1), ct);
        }
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  LoyaltyExpiryJob — من MallX
//  يعمل كل يوم — يُعيّن النقاط المنتهية
// ════════════════════════════════════════════════════════════════════════════
public class LoyaltyExpiryJob : BackgroundService
{
    private readonly IServiceScopeFactory _scope;
    private readonly ILogger<LoyaltyExpiryJob> _log;

    public LoyaltyExpiryJob(IServiceScopeFactory scope, ILogger<LoyaltyExpiryJob> log)
    { _scope = scope; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scope.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OmniXDbContext>();

                var expiredAccounts = await db.LoyaltyAccounts
                    .Where(l => l.PointsExpireAt.HasValue && l.PointsExpireAt < DateTime.UtcNow
                        && l.AvailablePoints > 0)
                    .ToListAsync(ct);

                foreach (var acc in expiredAccounts)
                {
                    // Mark all available points as redeemed (expired)
                    acc.RedeemedPoints += acc.AvailablePoints;
                    acc.PointsExpireAt  = null;
                }

                if (expiredAccounts.Count > 0)
                {
                    await db.SaveChangesAsync(ct);
                    _log.LogInformation("LoyaltyExpiry: expired points for {Count} accounts",
                        expiredAccounts.Count);
                }
            }
            catch (Exception ex) { _log.LogError(ex, "LoyaltyExpiryJob error"); }

            await Task.Delay(TimeSpan.FromDays(1), ct);
        }
    }
}

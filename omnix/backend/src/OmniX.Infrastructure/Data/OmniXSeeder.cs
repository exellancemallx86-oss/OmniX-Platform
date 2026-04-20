using Microsoft.EntityFrameworkCore;
using OmniX.Domain.Entities.Auth;
using OmniX.Domain.Entities.Core;
using OmniX.Domain.Entities.Licensing;
using OmniX.Domain.Enums;
using OmniX.Infrastructure.Data;

namespace OmniX.Infrastructure.Data;

// ════════════════════════════════════════════════════════════════════════════
//  OmniXSeeder — البيانات الأولية للمنصة
//  يشمل: Plans + SuperAdmin Tenant + Admin User + Demo Data
// ════════════════════════════════════════════════════════════════════════════
public static class OmniXSeeder
{
    public static async Task SeedAsync(OmniXDbContext db)
    {
        // ── 1. Plans ──────────────────────────────────────────────────────
        if (!await db.Plans.AnyAsync())
        {
            var plans = new List<Plan>
            {
                new()
                {
                    Id = Guid.NewGuid(), TenantId = Guid.Empty,
                    Name = "تجريبي", NameEn = "Trial",
                    Type = PlanType.Trial, IsActive = true, SortOrder = 0,
                    MonthlyPrice = 0, AnnualPrice = 0,
                    MaxBranches = 1, MaxUsers = 2, MaxProducts = 50,
                    HasPOS = true, HasInventory = true,
                    DataRetentionDays = 30,
                },
                new()
                {
                    Id = Guid.NewGuid(), TenantId = Guid.Empty,
                    Name = "أساسي", NameEn = "Basic",
                    Type = PlanType.Basic, IsActive = true, SortOrder = 1,
                    MonthlyPrice = 199, AnnualPrice = 1990,
                    MaxBranches = 3, MaxUsers = 10, MaxProducts = 500,
                    HasPOS = true, HasInventory = true, HasMultiBranch = true,
                    HasDelivery = true, HasWhatsApp = true, MaxWhatsAppNumbers = 1,
                    DataRetentionDays = 365,
                },
                new()
                {
                    Id = Guid.NewGuid(), TenantId = Guid.Empty,
                    Name = "احترافي", NameEn = "Professional",
                    Type = PlanType.ProMonthly, IsActive = true, SortOrder = 2,
                    MonthlyPrice = 499, AnnualPrice = 4990,
                    MaxBranches = 10, MaxUsers = 30, MaxProducts = 2000,
                    HasPOS = true, HasInventory = true, HasMultiBranch = true,
                    HasAdvancedReports = true, HasApiAccess = true,
                    HasDelivery = true, HasWhatsApp = true, MaxWhatsAppNumbers = 3,
                    HasSyncEngine = true, HasAi = true,
                    HasRestaurantModule = true, MaxRestaurantTables = 50,
                    HasKDS = true, HasQRMenu = true, HasTableReservations = true,
                    HasRentalModule = true, MaxRentalAssets = 200,
                    DataRetentionDays = 730,
                },
                new()
                {
                    Id = Guid.NewGuid(), TenantId = Guid.Empty,
                    Name = "مؤسسي", NameEn = "Enterprise",
                    Type = PlanType.Enterprise, IsActive = true, SortOrder = 3,
                    MonthlyPrice = 999, AnnualPrice = 9990,
                    MaxBranches = -1, MaxUsers = -1, MaxProducts = -1,
                    HasPOS = true, HasInventory = true, HasMultiBranch = true,
                    HasAdvancedReports = true, HasApiAccess = true, HasWhiteLabel = true,
                    HasDelivery = true, HasWhatsApp = true, MaxWhatsAppNumbers = -1,
                    HasSyncEngine = true, HasAi = true, HasPluginStore = true,
                    HasMarketplace = true,
                    HasRestaurantModule = true, MaxRestaurantTables = -1,
                    HasKDS = true, HasQRMenu = true, HasTableReservations = true,
                    HasRestaurantAnalytics = true,
                    HasRentalModule = true, MaxRentalAssets = -1,
                    HasRentalMarketplace = true,
                    HasMallModule = true, MaxMallStores = -1,
                    HasLoyaltyProgram = true, HasWallet = true,
                    SupportLevel = "dedicated", UptimeSlaPercent = 99,
                    DataRetentionDays = -1,
                },
            };
            db.Plans.AddRange(plans);
            await db.SaveChangesAsync();
        }

        // ── 2. Platform Owner Tenant ──────────────────────────────────────
        var platformTenantId = new Guid("00000000-0000-0000-0000-000000000001");
        if (!await db.Tenants.AnyAsync(t => t.Id == platformTenantId))
        {
            var tenant = new Tenant
            {
                Id           = platformTenantId,
                TenantId     = platformTenantId,
                Name         = "OmniX Platform",
                Slug         = "omnix-platform",
                BusinessType = BusinessType.Services,
                Currency     = "EGP",
                Status       = TenantStatus.Active,
                IsActive     = true,
                IsVerified   = true,
                Plan         = PlanType.Enterprise,
                ActiveModules = TenantModules.POS | TenantModules.Inventory
                    | TenantModules.Restaurant | TenantModules.Rental
                    | TenantModules.Mall | TenantModules.AI,
                ContactEmail = "admin@omnix.app",
            };
            db.Tenants.Add(tenant);

            // Main Branch
            var branch = new Branch
            {
                Id       = new Guid("00000000-0000-0000-0000-000000000002"),
                TenantId = platformTenantId,
                Name     = "Main",
                IsMain   = true,
                IsActive = true,
            };
            db.Branches.Add(branch);

            // Platform Owner User
            var user = new ApplicationUser
            {
                Id           = new Guid("00000000-0000-0000-0000-000000000003"),
                TenantId     = platformTenantId,
                BranchId     = branch.Id,
                Username     = "admin",
                Email        = "admin@omnix.app",
                FirstName    = "OmniX",
                LastName     = "Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456", 12),
                Role         = UserRole.PlatformOwner,
                IsActive     = true,
                IsEmailVerified = true,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
    }
}

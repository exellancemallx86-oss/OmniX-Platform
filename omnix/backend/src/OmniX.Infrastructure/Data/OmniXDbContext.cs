using Microsoft.EntityFrameworkCore;
using OmniX.Domain.Entities.Auth;
using OmniX.Domain.Entities.Core;
using OmniX.Domain.Entities.Licensing;
using OmniX.Domain.Entities.Mall;
using OmniX.Domain.Entities.Rental;
using OmniX.Domain.Entities.Restaurant;
using OmniX.Domain.Entities.Sync;

namespace OmniX.Infrastructure.Data;

// ════════════════════════════════════════════════════════════════════════════
//  OmniXDbContext — قاعدة البيانات الموحّدة
//  مدمجة من: Ultra v6 DbContext + MallX DbContext + Pro Sync entities
//  يشمل: 60+ DbSet تغطي كل modules المنصة
// ════════════════════════════════════════════════════════════════════════════
public class OmniXDbContext : DbContext
{
    public OmniXDbContext(DbContextOptions<OmniXDbContext> options) : base(options) { }

    // ─── Auth ──────────────────────────────────────────────────────────────
    public DbSet<ApplicationUser>       Users               => Set<ApplicationUser>();
    public DbSet<ApplicationRole>       Roles               => Set<ApplicationRole>();
    public DbSet<ApplicationPermission> Permissions         => Set<ApplicationPermission>();
    public DbSet<RefreshToken>          RefreshTokens       => Set<RefreshToken>();
    public DbSet<FeatureFlag>           FeatureFlags        => Set<FeatureFlag>();
    public DbSet<AuditLog>              AuditLogs           => Set<AuditLog>();

    // ─── Core ──────────────────────────────────────────────────────────────
    public DbSet<Tenant>                Tenants             => Set<Tenant>();
    public DbSet<Branch>                Branches            => Set<Branch>();
    public DbSet<Product>               Products            => Set<Product>();
    public DbSet<Category>              Categories          => Set<Category>();
    public DbSet<Customer>              Customers           => Set<Customer>();
    public DbSet<Supplier>              Suppliers           => Set<Supplier>();
    public DbSet<Expense>               Expenses            => Set<Expense>();

    // ─── POS / Inventory ───────────────────────────────────────────────────
    public DbSet<StockItem>             StockItems          => Set<StockItem>();
    public DbSet<StockMovement>         StockMovements      => Set<StockMovement>();
    public DbSet<SaleOrder>             SaleOrders          => Set<SaleOrder>();
    public DbSet<SaleOrderItem>         SaleOrderItems      => Set<SaleOrderItem>();
    public DbSet<Payment>               Payments            => Set<Payment>();
    public DbSet<PurchaseOrder>         PurchaseOrders      => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem>     PurchaseOrderItems  => Set<PurchaseOrderItem>();

    // ─── Licensing ─────────────────────────────────────────────────────────
    public DbSet<Plan>                  Plans               => Set<Plan>();
    public DbSet<Subscription>          Subscriptions       => Set<Subscription>();
    public DbSet<License>               Licenses            => Set<License>();
    public DbSet<DeviceRegistration>    DeviceRegistrations => Set<DeviceRegistration>();
    public DbSet<Plugin>                Plugins             => Set<Plugin>();
    public DbSet<PluginInstallation>    PluginInstallations => Set<PluginInstallation>();

    // ─── Restaurant (من Ultra v6) ──────────────────────────────────────────
    public DbSet<RestaurantCategory>      RestaurantCategories     => Set<RestaurantCategory>();
    public DbSet<RestaurantMenuItem>      RestaurantMenuItems      => Set<RestaurantMenuItem>();
    public DbSet<MenuItemVariant>         MenuItemVariants         => Set<MenuItemVariant>();
    public DbSet<MenuModifierGroup>       MenuModifierGroups       => Set<MenuModifierGroup>();
    public DbSet<MenuModifier>            MenuModifiers            => Set<MenuModifier>();
    public DbSet<BranchMenuOverride>      BranchMenuOverrides      => Set<BranchMenuOverride>();
    public DbSet<BranchItemAvailability>  BranchItemAvailabilities => Set<BranchItemAvailability>();
    public DbSet<FloorPlan>               FloorPlans               => Set<FloorPlan>();
    public DbSet<RestaurantTable>         RestaurantTables         => Set<RestaurantTable>();
    public DbSet<TableSession>            TableSessions            => Set<TableSession>();
    public DbSet<TableReservation>        TableReservations        => Set<TableReservation>();
    public DbSet<SessionPayment>          SessionPayments          => Set<SessionPayment>();
    public DbSet<KitchenStation>          KitchenStations          => Set<KitchenStation>();
    public DbSet<DineInOrder>             DineInOrders             => Set<DineInOrder>();
    public DbSet<DineInOrderItem>         DineInOrderItems         => Set<DineInOrderItem>();
    public DbSet<OrderItemModifier>       OrderItemModifiers       => Set<OrderItemModifier>();
    public DbSet<KitchenTicket>           KitchenTickets           => Set<KitchenTicket>();
    public DbSet<KitchenTicketItem>       KitchenTicketItems       => Set<KitchenTicketItem>();

    // ─── Rental (من Ultra v6) ──────────────────────────────────────────────
    public DbSet<RentalAsset>            RentalAssets             => Set<RentalAsset>();
    public DbSet<RentalAssetImage>       RentalAssetImages        => Set<RentalAssetImage>();
    public DbSet<RentalBooking>          RentalBookings           => Set<RentalBooking>();
    public DbSet<RentalBookingItem>      RentalBookingItems       => Set<RentalBookingItem>();
    public DbSet<RentalPayment>          RentalPayments           => Set<RentalPayment>();
    public DbSet<DamageReport>           DamageReports            => Set<DamageReport>();
    public DbSet<MaintenanceLog>         MaintenanceLogs          => Set<MaintenanceLog>();
    public DbSet<MarketplaceListing>     MarketplaceListings      => Set<MarketplaceListing>();

    // ─── Sync (من MesterX Pro) ─────────────────────────────────────────────
    public DbSet<SyncQueue>              SyncQueue                => Set<SyncQueue>();
    public DbSet<SyncSession>            SyncSessions             => Set<SyncSession>();
    public DbSet<SyncLog>                SyncLogs                 => Set<SyncLog>();

    // ─── Mall B2C (من MallX) ───────────────────────────────────────────────
    public DbSet<Mall>                   Malls                    => Set<Mall>();
    public DbSet<MallStore>              MallStores               => Set<MallStore>();
    public DbSet<MallProduct>            MallProducts             => Set<MallProduct>();
    public DbSet<MallCustomer>           MallCustomers            => Set<MallCustomer>();
    public DbSet<CustomerAddress>        CustomerAddresses        => Set<CustomerAddress>();
    public DbSet<CustomerRefreshToken>   CustomerRefreshTokens    => Set<CustomerRefreshToken>();
    public DbSet<Cart>                   Carts                    => Set<Cart>();
    public DbSet<CartItem>               CartItems                => Set<CartItem>();
    public DbSet<MallOrder>              MallOrders               => Set<MallOrder>();
    public DbSet<StoreOrderItem>         StoreOrderItems          => Set<StoreOrderItem>();
    public DbSet<PaymentTransaction>     PaymentTransactions      => Set<PaymentTransaction>();
    public DbSet<Driver>                 Drivers                  => Set<Driver>();
    public DbSet<LoyaltyAccount>         LoyaltyAccounts          => Set<LoyaltyAccount>();
    public DbSet<Wallet>                 Wallets                  => Set<Wallet>();
    public DbSet<WalletTransaction>      WalletTransactions       => Set<WalletTransaction>();
    public DbSet<CommissionSettlement>   CommissionSettlements    => Set<CommissionSettlement>();

    // ════════════════════════════════════════════════════════════════════════
    protected override void OnModelCreating(ModelBuilder m)
    {
        base.OnModelCreating(m);

        // ── Global Query Filters (Soft Delete + Tenant isolation) ────────
        // B2B entities — فلتر بـ IsDeleted فقط (TenantRLS يتولى الـ TenantId)
        foreach (var entityType in m.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (clrType.IsSubclassOf(typeof(BaseEntity)))
            {
                m.Entity(clrType).HasQueryFilter(
                    System.Linq.Expressions.Expression.Lambda(
                        System.Linq.Expressions.Expression.Not(
                            System.Linq.Expressions.Expression.Property(
                                System.Linq.Expressions.Expression.Parameter(clrType, "e"),
                                "IsDeleted")),
                        System.Linq.Expressions.Expression.Parameter(clrType, "e")));
            }
        }

        // ── Tenant ────────────────────────────────────────────────────────
        m.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.VatRate).HasColumnType("decimal(5,4)").HasDefaultValue(0.14m);
            e.Property(x => x.Currency).HasMaxLength(10).HasDefaultValue("EGP");
        });

        // ── Branch ────────────────────────────────────────────────────────
        m.Entity<Branch>(e =>
        {
            e.ToTable("branches");
            e.HasOne(x => x.Tenant).WithMany(t => t.Branches).HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).IsRequired(false);
        });

        // ── ApplicationUser ───────────────────────────────────────────────
        m.Entity<ApplicationUser>(e =>
        {
            e.ToTable("users");
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Username).IsUnique();
            e.HasOne(x => x.Tenant).WithMany(t => t.Users).HasForeignKey(x => x.TenantId);
            e.HasOne(x => x.Branch).WithMany(b => b.Users).HasForeignKey(x => x.BranchId).IsRequired(false);
            e.HasOne(x => x.CustomRole).WithMany().HasForeignKey(x => x.RoleId).IsRequired(false);
        });

        // ── RefreshToken ──────────────────────────────────────────────────
        m.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId);
        });

        // ── Product ───────────────────────────────────────────────────────
        m.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasIndex(x => x.Barcode);
            e.HasIndex(x => x.SKU);
            e.Property(x => x.SalePrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.CostPrice).HasColumnType("decimal(18,2)");
        });

        // ── SaleOrder ─────────────────────────────────────────────────────
        m.Entity<SaleOrder>(e =>
        {
            e.ToTable("sale_orders");
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.Property(x => x.Total).HasColumnType("decimal(18,2)");
        });

        // ── Sync ──────────────────────────────────────────────────────────
        m.Entity<SyncQueue>(e =>
        {
            e.ToTable("sync_queue");
            e.HasIndex(x => new { x.TenantId, x.BranchId, x.Status });
        });
        m.Entity<SyncSession>(e => { e.ToTable("sync_sessions"); });
        m.Entity<SyncLog>(e => { e.ToTable("sync_logs"); });

        // ── Restaurant ────────────────────────────────────────────────────
        m.Entity<RestaurantCategory>(e => { e.ToTable("restaurant_categories"); });
        m.Entity<RestaurantMenuItem>(e =>
        {
            e.ToTable("restaurant_menu_items");
            e.Property(x => x.BasePrice).HasColumnType("decimal(18,2)");
        });
        m.Entity<FloorPlan>(e => { e.ToTable("floor_plans"); });
        m.Entity<RestaurantTable>(e =>
        {
            e.ToTable("restaurant_tables");
            e.HasIndex(x => x.QrToken).IsUnique();
        });
        m.Entity<KitchenStation>(e => { e.ToTable("kitchen_stations"); });
        m.Entity<DineInOrder>(e =>
        {
            e.ToTable("dine_in_orders");
            e.HasIndex(x => x.OrderNumber).IsUnique();
        });
        m.Entity<TableSession>(e => { e.ToTable("table_sessions"); });

        // ── Rental ────────────────────────────────────────────────────────
        m.Entity<RentalAsset>(e =>
        {
            e.ToTable("rental_assets");
            e.HasIndex(x => x.AssetCode).IsUnique();
            e.Property(x => x.RentalPricePerDay).HasColumnType("decimal(18,2)");
        });
        m.Entity<RentalBooking>(e =>
        {
            e.ToTable("rental_bookings");
            e.HasIndex(x => x.BookingNumber).IsUnique();
        });

        // ── Licensing ─────────────────────────────────────────────────────
        m.Entity<License>(e =>
        {
            e.ToTable("licenses");
            e.HasIndex(x => x.LicenseKeyHash).IsUnique();
        });
        m.Entity<Plan>(e => { e.ToTable("plans"); });
        m.Entity<Subscription>(e => { e.ToTable("subscriptions"); });
        m.Entity<DeviceRegistration>(e =>
        {
            e.ToTable("device_registrations");
            e.HasIndex(x => new { x.TenantId, x.DeviceId }).IsUnique();
        });

        // ── Mall B2C (بدون BaseEntity — لا يحتاج IsDeleted global filter) ──
        m.Entity<Mall>(e =>
        {
            e.ToTable("malls");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
        });
        m.Entity<MallStore>(e =>
        {
            e.ToTable("mall_stores");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
        });
        m.Entity<MallCustomer>(e =>
        {
            e.ToTable("mall_customers");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });
        m.Entity<Cart>(e => { e.ToTable("carts"); e.HasKey(x => x.Id); });
        m.Entity<CartItem>(e => { e.ToTable("cart_items"); e.HasKey(x => x.Id); });
        m.Entity<MallOrder>(e =>
        {
            e.ToTable("mall_orders");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.Property(x => x.Total).HasColumnType("decimal(18,2)");
        });
        m.Entity<Driver>(e => { e.ToTable("drivers"); e.HasKey(x => x.Id); });
        m.Entity<LoyaltyAccount>(e => { e.ToTable("loyalty_accounts"); e.HasKey(x => x.Id); });
        m.Entity<Wallet>(e => { e.ToTable("wallets"); e.HasKey(x => x.Id); });
        m.Entity<WalletTransaction>(e => { e.ToTable("wallet_transactions"); e.HasKey(x => x.Id); });
        m.Entity<PaymentTransaction>(e =>
        {
            e.ToTable("payment_transactions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        });
        m.Entity<CommissionSettlement>(e => { e.ToTable("commission_settlements"); e.HasKey(x => x.Id); });
    }
}

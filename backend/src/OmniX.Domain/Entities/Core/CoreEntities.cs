using OmniX.Domain.Entities.Auth;
using OmniX.Domain.Enums;

namespace OmniX.Domain.Entities.Core;

// ════════════════════════════════════════════════════════════════════════════
//  OmniX Core Entities — من Ultra v6 (الأقوى والأشمل)
// ════════════════════════════════════════════════════════════════════════════

public class Tenant : BaseEntity
{
    public required string Name         { get; set; }
    public required string Slug         { get; set; }
    public string?   LogoUrl            { get; set; }
    public string?   CoverUrl           { get; set; }
    public BusinessType BusinessType    { get; set; } = BusinessType.RetailStore;
    public string?   Description        { get; set; }
    public string?   ContactEmail       { get; set; }
    public string?   ContactPhone       { get; set; }
    public string?   WhatsApp           { get; set; }
    public string?   Website            { get; set; }
    public string?   Address            { get; set; }
    public string?   City               { get; set; }
    public string?   Country            { get; set; } = "EG";
    public string?   TaxNumber          { get; set; }
    public string    Currency           { get; set; } = "EGP";
    public decimal   VatRate            { get; set; } = 0.14m;
    public string    Language           { get; set; } = "ar";
    public string    Timezone           { get; set; } = "Africa/Cairo";
    public TenantStatus Status          { get; set; } = TenantStatus.Trial;
    public bool      IsActive           { get; set; } = true;
    public bool      IsVerified         { get; set; }
    public DateTime? VerifiedAt         { get; set; }
    public DateTime? SuspendedAt        { get; set; }
    public string?   SuspendReason      { get; set; }
    public PlanType  Plan               { get; set; } = PlanType.Trial;
    public TenantModules ActiveModules  { get; set; } = TenantModules.POS | TenantModules.Inventory;
    public DateTime? TrialEndsAt        { get; set; }
    public bool      ReceiptPrintEnabled{ get; set; } = true;
    public string?   ReceiptFooter      { get; set; }
    public bool      LoyaltyEnabled     { get; set; }
    public decimal   LoyaltyPointsRate  { get; set; } = 1m;
    public int       LoyaltyRedeemMin   { get; set; } = 100;

    public virtual ICollection<Branch>              Branches      { get; set; } = [];
    public virtual ICollection<ApplicationUser>     Users         { get; set; } = [];
    public virtual ICollection<Product>             Products      { get; set; } = [];
    public virtual ICollection<Customer>            Customers     { get; set; } = [];
    public virtual ICollection<Category>            Categories    { get; set; } = [];
    public virtual ICollection<Supplier>            Suppliers     { get; set; } = [];
    public virtual ICollection<Expense>             Expenses      { get; set; } = [];
    public virtual ICollection<FeatureFlag>         FeatureFlags  { get; set; } = [];
}

public class Branch : BaseEntity
{
    public required string Name         { get; set; }
    public string?   NameAr             { get; set; }
    public string?   Code               { get; set; }
    public Guid?     ManagerId          { get; set; }
    public string?   Address            { get; set; }
    public string?   City               { get; set; }
    public string?   Country            { get; set; } = "EG";
    public string?   PhoneNumber        { get; set; }
    public string?   Email              { get; set; }
    public double?   Latitude           { get; set; }
    public double?   Longitude          { get; set; }
    public bool      IsMain             { get; set; }
    public bool      IsActive           { get; set; } = true;
    public bool      AcceptsOnlineOrders{ get; set; } = true;
    public TimeOnly? OpenTime           { get; set; }
    public TimeOnly? CloseTime         { get; set; }

    public virtual Tenant                   Tenant     { get; set; } = null!;
    public virtual ApplicationUser?         Manager    { get; set; }
    public virtual ICollection<ApplicationUser> Users      { get; set; } = [];
    public virtual ICollection<StockItem>       StockItems { get; set; } = [];
    public virtual ICollection<SaleOrder>       SaleOrders { get; set; } = [];
}

public class Product : BaseEntity
{
    public Guid?     CategoryId         { get; set; }
    public Guid?     BranchId           { get; set; }
    public required string Name         { get; set; }
    public string?   NameAr             { get; set; }
    public string?   Description        { get; set; }
    public string?   Barcode            { get; set; }
    public string?   SKU                { get; set; }
    public ProductType Type             { get; set; } = ProductType.Physical;
    public ProductUnit Unit             { get; set; } = ProductUnit.Piece;
    public decimal   CostPrice          { get; set; }
    public decimal   SalePrice          { get; set; }
    public decimal?  WholesalePrice     { get; set; }
    public decimal?  MinSalePrice       { get; set; }
    public bool      HasVat             { get; set; } = true;
    public decimal   VatRate            { get; set; } = 0.14m;
    public string?   ImageUrl           { get; set; }
    public string?   GalleryUrls        { get; set; }
    public bool      IsActive           { get; set; } = true;
    public bool      IsAvailableOnline  { get; set; } = true;
    public bool      IsFeatured         { get; set; }
    public bool      TrackInventory     { get; set; } = true;
    public int       LowStockThreshold  { get; set; } = 5;
    public decimal?  ReorderPoint       { get; set; }
    public decimal?  ReorderQuantity    { get; set; }

    public virtual Tenant                    Tenant         { get; set; } = null!;
    public virtual Category?                 Category       { get; set; }
    public virtual Branch?                   Branch         { get; set; }
    public virtual ICollection<StockItem>    StockItems     { get; set; } = [];
    public virtual ICollection<StockMovement> StockMovements { get; set; } = [];
    public virtual ICollection<SaleOrderItem> SaleOrderItems { get; set; } = [];
}

public class Customer : BaseEntity
{
    public required string Name         { get; set; }
    public string?   Phone              { get; set; }
    public string?   Email              { get; set; }
    public string?   Address            { get; set; }
    public string?   City               { get; set; }
    public Gender    Gender             { get; set; } = Gender.Unknown;
    public DateOnly? DateOfBirth        { get; set; }
    public CustomerSegment Segment      { get; set; } = CustomerSegment.New;
    public decimal   LoyaltyPoints      { get; set; }
    public decimal   TotalPurchases     { get; set; }
    public int       TotalOrders        { get; set; }
    public DateTime? LastPurchaseAt     { get; set; }
    public DateTime? FirstPurchaseAt    { get; set; }
    public decimal   CreditLimit        { get; set; }
    public decimal   OutstandingBalance { get; set; }
    public bool      AllowCredit        { get; set; }
    public bool      IsActive           { get; set; } = true;
    public bool      IsBlacklisted      { get; set; }
    public string?   BlacklistReason    { get; set; }
    public string?   Notes              { get; set; }

    public virtual Tenant                Tenant     { get; set; } = null!;
    public virtual ICollection<SaleOrder> SaleOrders { get; set; } = [];
}

public class Category : BaseEntity
{
    public required string Name    { get; set; }
    public string?   NameAr        { get; set; }
    public string?   Description   { get; set; }
    public string?   ImageUrl      { get; set; }
    public Guid?     ParentId      { get; set; }
    public int       SortOrder     { get; set; }
    public bool      IsActive      { get; set; } = true;

    public virtual Tenant                 Tenant        { get; set; } = null!;
    public virtual Category?              Parent        { get; set; }
    public virtual ICollection<Category>  SubCategories { get; set; } = [];
    public virtual ICollection<Product>   Products      { get; set; } = [];
}

public class Supplier : BaseEntity
{
    public required string Name     { get; set; }
    public string?   ContactPerson  { get; set; }
    public string?   Phone          { get; set; }
    public string?   Email          { get; set; }
    public string?   Address        { get; set; }
    public string?   City           { get; set; }
    public string?   TaxNumber      { get; set; }
    public SupplierStatus Status    { get; set; } = SupplierStatus.Active;
    public decimal   CreditLimit    { get; set; }
    public decimal   OutstandingBalance { get; set; }
    public int       PaymentTermsDays   { get; set; } = 30;
    public string?   Notes          { get; set; }

    public virtual Tenant                      Tenant         { get; set; } = null!;
    public virtual ICollection<PurchaseOrder>  PurchaseOrders { get; set; } = [];
}

public class Expense : BaseEntity
{
    public required string Description  { get; set; }
    public Guid?     BranchId           { get; set; }
    public Guid?     ApprovedById       { get; set; }
    public ExpenseCategory Category     { get; set; }
    public decimal   Amount             { get; set; }
    public DateTime  ExpenseDate        { get; set; }
    public PaymentMethod PaymentMethod  { get; set; } = PaymentMethod.Cash;
    public string?   Reference          { get; set; }
    public string?   AttachmentUrl      { get; set; }
    public string?   Notes              { get; set; }
    public bool      IsRecurring        { get; set; }

    public virtual Tenant            Tenant     { get; set; } = null!;
    public virtual Branch?           Branch     { get; set; }
    public virtual ApplicationUser?  ApprovedBy { get; set; }
}

// ── POS Entities ─────────────────────────────────────────────────────────────
public class StockItem : BaseEntity
{
    public Guid      ProductId   { get; set; }
    public Guid      BranchId    { get; set; }
    public decimal   Quantity    { get; set; }
    public decimal   Reserved    { get; set; }
    public decimal   Available   => Quantity - Reserved;
    public decimal?  AvgCost     { get; set; }
    public string?   Location    { get; set; }

    public virtual Tenant   Tenant  { get; set; } = null!;
    public virtual Product  Product { get; set; } = null!;
    public virtual Branch   Branch  { get; set; } = null!;
}

public class StockMovement : BaseEntity
{
    public Guid              ProductId      { get; set; }
    public Guid              BranchId       { get; set; }
    public Guid?             ReferenceId    { get; set; }
    public StockMovementType MovementType   { get; set; }
    public decimal           Quantity       { get; set; }
    public decimal           UnitCost       { get; set; }
    public decimal           BalanceBefore  { get; set; }
    public decimal           BalanceAfter   { get; set; }
    public string?           Notes          { get; set; }

    public virtual Tenant  Tenant  { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
    public virtual Branch  Branch  { get; set; } = null!;
}

public class SaleOrder : BaseEntity
{
    public Guid?         CustomerId     { get; set; }
    public Guid          BranchId       { get; set; }
    public Guid?         CashierId      { get; set; }
    public required string OrderNumber  { get; set; }
    public SaleStatus    Status         { get; set; } = SaleStatus.Pending;
    public decimal       SubTotal       { get; set; }
    public decimal       DiscountAmount { get; set; }
    public decimal       VatAmount      { get; set; }
    public decimal       Total          { get; set; }
    public decimal       AmountPaid     { get; set; }
    public decimal       Change         { get; set; }
    public PaymentMethod PaymentMethod  { get; set; } = PaymentMethod.Cash;
    public string?       Notes          { get; set; }
    public bool          IsSynced       { get; set; } = true;
    public string?       DeviceId       { get; set; }
    public string?       LocalId        { get; set; }

    public virtual Tenant                    Tenant    { get; set; } = null!;
    public virtual Customer?                 Customer  { get; set; }
    public virtual Branch                    Branch    { get; set; } = null!;
    public virtual ApplicationUser?          Cashier   { get; set; }
    public virtual ICollection<SaleOrderItem> Items    { get; set; } = [];
    public virtual ICollection<Payment>       Payments { get; set; } = [];
}

public class SaleOrderItem : BaseEntity
{
    public Guid    OrderId      { get; set; }
    public Guid    ProductId    { get; set; }
    public decimal Quantity     { get; set; }
    public decimal UnitPrice    { get; set; }
    public decimal CostPrice    { get; set; }
    public decimal Discount     { get; set; }
    public decimal VatRate      { get; set; } = 0.14m;
    public decimal VatAmount    { get; set; }
    public decimal Total        { get; set; }

    public virtual Tenant    Tenant  { get; set; } = null!;
    public virtual SaleOrder Order   { get; set; } = null!;
    public virtual Product   Product { get; set; } = null!;
}

public class Payment : BaseEntity
{
    public Guid          OrderId       { get; set; }
    public PaymentMethod Method        { get; set; }
    public decimal       Amount        { get; set; }
    public string?       Reference     { get; set; }
    public PaymentStatus Status        { get; set; } = PaymentStatus.Pending;
    public string?       GatewayRef    { get; set; }
    public DateTime?     PaidAt        { get; set; }

    public virtual Tenant    Tenant { get; set; } = null!;
    public virtual SaleOrder Order  { get; set; } = null!;
}

public class PurchaseOrder : BaseEntity
{
    public Guid                SupplierId    { get; set; }
    public Guid                BranchId      { get; set; }
    public required string     OrderNumber   { get; set; }
    public PurchaseOrderStatus Status        { get; set; } = PurchaseOrderStatus.Draft;
    public decimal             Total         { get; set; }
    public DateTime            OrderDate     { get; set; }
    public DateTime?           ExpectedDate  { get; set; }
    public DateTime?           ReceivedDate  { get; set; }
    public string?             Notes         { get; set; }

    public virtual Tenant                       Tenant   { get; set; } = null!;
    public virtual Supplier                     Supplier { get; set; } = null!;
    public virtual Branch                       Branch   { get; set; } = null!;
    public virtual ICollection<PurchaseOrderItem> Items { get; set; } = [];
}

public class PurchaseOrderItem : BaseEntity
{
    public Guid    OrderId        { get; set; }
    public Guid    ProductId      { get; set; }
    public decimal OrderedQty     { get; set; }
    public decimal ReceivedQty    { get; set; }
    public decimal UnitCost       { get; set; }
    public decimal Total          { get; set; }

    public virtual Tenant        Tenant  { get; set; } = null!;
    public virtual PurchaseOrder Order   { get; set; } = null!;
    public virtual Product       Product { get; set; } = null!;
}

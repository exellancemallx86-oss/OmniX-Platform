using OmniX.Domain.Enums;

namespace OmniX.Domain.Entities.Mall;

// ════════════════════════════════════════════════════════════════════════════
//  OmniX Mall Entities — من MallX SAAS (B2C layer)
//  مستقلة تماماً عن B2B entities — MallCustomer != ApplicationUser
// ════════════════════════════════════════════════════════════════════════════

// ─── MALL ─────────────────────────────────────────────────────────────────
public class Mall
{
    public Guid    Id          { get; set; } = Guid.NewGuid();
    public required string Name  { get; set; }
    public string? NameAr      { get; set; }
    public required string Slug { get; set; }
    public string? Address     { get; set; }
    public decimal? GeoLat     { get; set; }
    public decimal? GeoLng     { get; set; }
    public int GeoRadiusM      { get; set; } = 200;
    public string? LogoUrl     { get; set; }
    public string? CoverUrl    { get; set; }
    public string? Phone       { get; set; }
    public string? Email       { get; set; }
    public bool IsActive       { get; set; } = true;
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt  { get; set; } = DateTime.UtcNow;

    public virtual ICollection<MallStore>    Stores    { get; set; } = [];
    public virtual ICollection<MallCustomer> Customers { get; set; } = [];
    public virtual ICollection<MallOrder>    Orders    { get; set; } = [];
}

// ─── MALL STORE (متجر داخل المول) ────────────────────────────────────────
public class MallStore
{
    public Guid    Id             { get; set; } = Guid.NewGuid();
    public Guid    MallId         { get; set; }
    public Guid?   TenantId       { get; set; }   // ربط اختياري بـ B2B Tenant
    public required string Name   { get; set; }
    public string? NameAr         { get; set; }
    public required string Slug   { get; set; }
    public string? Description    { get; set; }
    public string? LogoUrl        { get; set; }
    public string? CoverUrl       { get; set; }
    public string? Phone          { get; set; }
    public string? Email          { get; set; }
    public decimal CommissionRate { get; set; } = 0.05m;
    public bool IsActive          { get; set; } = true;
    public bool AcceptsDelivery   { get; set; } = true;
    public bool AcceptsPickup     { get; set; } = true;
    public TimeOnly? OpenTime     { get; set; }
    public TimeOnly? CloseTime    { get; set; }
    public int PrepTimeMinutes    { get; set; } = 30;
    public decimal MinOrderAmount { get; set; } = 0;
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt     { get; set; } = DateTime.UtcNow;

    public virtual Mall                       Mall     { get; set; } = null!;
    public virtual ICollection<MallProduct>   Products { get; set; } = [];
    public virtual ICollection<StoreOrderItem> OrderItems { get; set; } = [];
}

// ─── MALL PRODUCT ─────────────────────────────────────────────────────────
public class MallProduct
{
    public Guid    Id            { get; set; } = Guid.NewGuid();
    public Guid    StoreId       { get; set; }
    public required string Name  { get; set; }
    public string? NameAr        { get; set; }
    public string? Description   { get; set; }
    public string? ImageUrl      { get; set; }
    public string? GalleryUrls   { get; set; }
    public decimal Price         { get; set; }
    public decimal? DiscountedPrice { get; set; }
    public bool IsActive         { get; set; } = true;
    public bool IsFeatured       { get; set; }
    public int  SortOrder        { get; set; }
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt    { get; set; } = DateTime.UtcNow;

    public virtual MallStore Store { get; set; } = null!;
}

// ─── MALL CUSTOMER (B2C — مستقل) ─────────────────────────────────────────
public class MallCustomer
{
    public Guid    Id            { get; set; } = Guid.NewGuid();
    public Guid    MallId        { get; set; }
    public required string FirstName { get; set; }
    public required string LastName  { get; set; }
    public required string Email     { get; set; }
    public string? Phone         { get; set; }
    public required string PasswordHash { get; set; }
    public string? AvatarUrl     { get; set; }
    public int LoyaltyPoints     { get; set; }
    public string Tier           { get; set; } = "Bronze";   // Bronze/Silver/Gold
    public bool IsActive         { get; set; } = true;
    public bool IsDeleted        { get; set; }
    public int FailedAttempts    { get; set; }
    public DateTime? LockoutEnd  { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt    { get; set; } = DateTime.UtcNow;

    public string FullName       => $"{FirstName} {LastName}";
    public bool IsLocked         => LockoutEnd.HasValue && LockoutEnd > DateTime.UtcNow;

    public virtual Mall                          Mall          { get; set; } = null!;
    public virtual ICollection<CustomerAddress>  Addresses     { get; set; } = [];
    public virtual ICollection<CustomerRefreshToken> RefreshTokens { get; set; } = [];
    public virtual ICollection<MallOrder>        Orders        { get; set; } = [];
    public virtual Cart?                         Cart          { get; set; }
    public virtual LoyaltyAccount?               LoyaltyAccount { get; set; }
    public virtual Wallet?                       Wallet        { get; set; }
}

public class CustomerAddress
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public Guid   CustomerId  { get; set; }
    public required string Label { get; set; }
    public required string Street { get; set; }
    public string? City       { get; set; }
    public decimal? Lat       { get; set; }
    public decimal? Lng       { get; set; }
    public bool IsDefault     { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual MallCustomer Customer { get; set; } = null!;
}

public class CustomerRefreshToken
{
    public Guid   Id         { get; set; } = Guid.NewGuid();
    public Guid   CustomerId { get; set; }
    public required string TokenHash { get; set; }
    public required string Salt      { get; set; }
    public string? DeviceInfo { get; set; }
    public bool IsRevoked     { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual MallCustomer Customer { get; set; } = null!;
}

// ─── CART ─────────────────────────────────────────────────────────────────
public class Cart
{
    public Guid   Id         { get; set; } = Guid.NewGuid();
    public Guid   CustomerId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual MallCustomer          Customer { get; set; } = null!;
    public virtual ICollection<CartItem> Items    { get; set; } = [];
}

public class CartItem
{
    public Guid    Id        { get; set; } = Guid.NewGuid();
    public Guid    CartId    { get; set; }
    public Guid    ProductId { get; set; }
    public Guid    StoreId   { get; set; }
    public int     Quantity  { get; set; } = 1;
    public string? Notes     { get; set; }
    public DateTime AddedAt  { get; set; } = DateTime.UtcNow;

    public virtual Cart        Cart    { get; set; } = null!;
    public virtual MallProduct Product { get; set; } = null!;
}

// ─── MALL ORDER ───────────────────────────────────────────────────────────
public class MallOrder
{
    public Guid   Id           { get; set; } = Guid.NewGuid();
    public Guid   MallId       { get; set; }
    public Guid   CustomerId   { get; set; }
    public Guid?  DriverId     { get; set; }
    public required string OrderNumber { get; set; }
    public MallOrderStatus Status { get; set; } = MallOrderStatus.Pending;
    public string FulfillmentType { get; set; } = "Delivery";
    public decimal SubTotal    { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Discount    { get; set; }
    public decimal Total       { get; set; }
    public int PointsEarned    { get; set; }
    public int PointsRedeemed  { get; set; }
    public string? Notes       { get; set; }
    public string? DeliveryAddress { get; set; }
    public decimal? DeliveryLat { get; set; }
    public decimal? DeliveryLng { get; set; }
    public DateTime? EstimatedDelivery { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt  { get; set; } = DateTime.UtcNow;

    public virtual Mall                       Mall     { get; set; } = null!;
    public virtual MallCustomer               Customer { get; set; } = null!;
    public virtual Driver?                    Driver   { get; set; }
    public virtual ICollection<StoreOrderItem> Items   { get; set; } = [];
    public virtual PaymentTransaction?        Payment  { get; set; }
}

public class StoreOrderItem
{
    public Guid    Id        { get; set; } = Guid.NewGuid();
    public Guid    OrderId   { get; set; }
    public Guid    StoreId   { get; set; }
    public Guid    ProductId { get; set; }
    public int     Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total     { get; set; }
    public string? Notes     { get; set; }

    public virtual MallOrder   Order   { get; set; } = null!;
    public virtual MallStore   Store   { get; set; } = null!;
    public virtual MallProduct Product { get; set; } = null!;
}

// ─── PAYMENT TRANSACTION (Paymob) ────────────────────────────────────────
public class PaymentTransaction
{
    public Guid   Id           { get; set; } = Guid.NewGuid();
    public Guid   MallOrderId  { get; set; }
    public Guid   CustomerId   { get; set; }
    public decimal Amount      { get; set; }
    public string Currency     { get; set; } = "EGP";
    public string Gateway      { get; set; } = "Paymob";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? GatewayOrderId  { get; set; }
    public string? GatewayTxnId    { get; set; }
    public string? GatewayResponse { get; set; }
    public string? FailureReason   { get; set; }
    public DateTime? PaidAt        { get; set; }
    public decimal RefundAmount    { get; set; }
    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;

    public virtual MallOrder Order { get; set; } = null!;
}

// ─── DRIVER (GPS Tracking) ────────────────────────────────────────────────
public class Driver
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public Guid   MallId      { get; set; }
    public required string Name  { get; set; }
    public required string Phone { get; set; }
    public string VehicleType { get; set; } = "Motorcycle";
    public string? VehiclePlate { get; set; }
    public decimal? CurrentLat  { get; set; }
    public decimal? CurrentLng  { get; set; }
    public DriverStatus Status  { get; set; } = DriverStatus.Offline;
    public DateTime? LastLocationAt { get; set; }
    public bool IsActive        { get; set; } = true;
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;

    public virtual Mall                  Mall   { get; set; } = null!;
    public virtual ICollection<MallOrder> Orders { get; set; } = [];
}

// ─── LOYALTY ──────────────────────────────────────────────────────────────
public class LoyaltyAccount
{
    public Guid   Id             { get; set; } = Guid.NewGuid();
    public Guid   CustomerId     { get; set; }
    public Guid   MallId         { get; set; }
    public int    LifetimePoints { get; set; }
    public int    RedeemedPoints { get; set; }
    public int    AvailablePoints => LifetimePoints - RedeemedPoints;
    public string Tier           { get; set; } = "Bronze";
    public DateTime? PointsExpireAt { get; set; }
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt    { get; set; } = DateTime.UtcNow;

    public virtual MallCustomer Customer { get; set; } = null!;
}

// ─── WALLET (من MallX Phase 9) ────────────────────────────────────────────
public class Wallet
{
    public Guid    Id         { get; set; } = Guid.NewGuid();
    public Guid    CustomerId { get; set; }
    public Guid    MallId     { get; set; }
    public decimal Balance    { get; set; }
    public string  Currency   { get; set; } = "EGP";
    public bool    IsActive   { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual MallCustomer Customer { get; set; } = null!;
    public virtual ICollection<WalletTransaction> Transactions { get; set; } = [];
}

public class WalletTransaction
{
    public Guid    Id          { get; set; } = Guid.NewGuid();
    public Guid    WalletId    { get; set; }
    public WalletTransactionType Type { get; set; }
    public decimal Amount      { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Reference   { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    public virtual Wallet Wallet { get; set; } = null!;
}

// ─── COMMISSION SETTLEMENT ────────────────────────────────────────────────
public class CommissionSettlement
{
    public Guid    Id            { get; set; } = Guid.NewGuid();
    public Guid    MallId        { get; set; }
    public Guid    StoreId       { get; set; }
    public DateTime PeriodStart  { get; set; }
    public DateTime PeriodEnd    { get; set; }
    public int     TotalOrders   { get; set; }
    public decimal GrossRevenue  { get; set; }
    public decimal CommissionRate { get; set; } = 0.05m;
    public decimal CommissionAmt  { get; set; }
    public decimal NetPayable    { get; set; }
    public string  Status        { get; set; } = "Pending";
    public DateTime? SettledAt   { get; set; }
    public string? Notes         { get; set; }
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
}

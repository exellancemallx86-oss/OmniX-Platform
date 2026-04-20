using OmniX.Domain.Entities.Core;
using OmniX.Domain.Entities.Auth;

namespace OmniX.Domain.Entities.Rental;

// ════════════════════════════════════════════════════════════════════════════
//  OmniX Rental Entities — من Ultra Enterprise v6
//  إدارة أصول الإيجار: ملابس أفراح، معدات، أثاث، إلكترونيات
// ════════════════════════════════════════════════════════════════════════════

public enum AssetCategory    { WeddingDress, Suit, Accessory, Decoration, Electronics, Furniture, Other }
public enum AssetStatus      { Available, Rented, Reserved, Maintenance, Damaged, Retired }
public enum AssetCondition   { Excellent, Good, Fair, Poor }
public enum RentalPricingModel { PerDay, PerHour, PerEvent, ForSale }
public enum RentalBookingType  { Rental, Sale }
public enum RentalBookingStatus { Pending, Confirmed, PickedUp, Returned, Cancelled, Overdue }
public enum RentalPaymentPurpose { Deposit, RentalFee, LatePenalty, DamageComp, Refund, SalePayment }
public enum RentalPaymentMethod  { Cash, Card, BankTransfer, MobileWallet, Credit }
public enum DamageLevel { Minor, Moderate, Severe, Totaled }

// ── Asset ─────────────────────────────────────────────────────────────────
public class RentalAsset : BaseEntity
{
    public string           AssetCode           { get; set; } = string.Empty;
    public string           Name                { get; set; } = string.Empty;
    public AssetCategory    Category            { get; set; }
    public string?          SubCategory         { get; set; }
    public string?          Brand               { get; set; }
    public string?          Size                { get; set; }
    public string?          Color               { get; set; }
    public string?          Description         { get; set; }
    public RentalPricingModel PricingModel      { get; set; } = RentalPricingModel.PerDay;
    public decimal          RentalPricePerDay   { get; set; }
    public decimal          RentalPricePerHour  { get; set; }
    public decimal          SalePrice           { get; set; }
    public decimal          DepositAmount       { get; set; }
    public decimal          PurchaseCost        { get; set; }
    public decimal          LatePenaltyPerDay   { get; set; }
    public int              MaxRentalDays       { get; set; } = 7;
    public AssetStatus      Status              { get; set; } = AssetStatus.Available;
    public AssetCondition   Condition           { get; set; } = AssetCondition.Excellent;
    public string?          Barcode             { get; set; }
    public string?          QrCodeUrl           { get; set; }
    public string?          PrimaryImageUrl     { get; set; }
    public bool             IsListedOnMarketplace { get; set; }
    public int              TotalRentalCount    { get; set; }
    public decimal          TotalRevenue        { get; set; }
    public DateTime?        LastRentedAt        { get; set; }

    public virtual ICollection<RentalAssetImage>  Images          { get; set; } = [];
    public virtual ICollection<RentalBookingItem> BookingItems    { get; set; } = [];
    public virtual ICollection<MaintenanceLog>    MaintenanceLogs { get; set; } = [];
    public virtual ICollection<DamageReport>      DamageReports   { get; set; } = [];
}

public class RentalAssetImage : BaseEntity
{
    public Guid    AssetId  { get; set; }
    public string  ImageUrl { get; set; } = string.Empty;
    public bool    IsMain   { get; set; }
    public int     SortOrder { get; set; }

    public virtual RentalAsset Asset { get; set; } = null!;
}

// ── Booking ───────────────────────────────────────────────────────────────
public class RentalBooking : BaseEntity
{
    public string              BookingNumber    { get; set; } = string.Empty;
    public Guid                CustomerId       { get; set; }
    public RentalBookingType   BookingType      { get; set; } = RentalBookingType.Rental;
    public RentalBookingStatus Status           { get; set; } = RentalBookingStatus.Pending;
    public DateTime            BookingDate      { get; set; } = DateTime.UtcNow;
    public DateTime            PickupDate       { get; set; }
    public DateTime            ReturnDueDate    { get; set; }
    public DateTime?           ActualReturnDate { get; set; }
    public DateTime?           EventDate        { get; set; }
    public string?             EventLocation    { get; set; }
    public decimal             SubTotal         { get; set; }
    public decimal             DiscountAmount   { get; set; }
    public decimal             VatAmount        { get; set; }
    public decimal             TotalAmount      { get; set; }
    public decimal             DepositRequired  { get; set; }
    public decimal             DepositPaid      { get; set; }
    public decimal             AmountPaid       { get; set; }
    public int                 LateDays         { get; set; }
    public decimal             LatePenaltyTotal { get; set; }
    public bool                PenaltyWaived    { get; set; }
    public Guid?               PenaltyWaivedBy  { get; set; }
    public bool                ContractSigned   { get; set; }
    public string?             ContractPdfUrl   { get; set; }
    public Guid?               HandledByUserId  { get; set; }
    public string?             Notes            { get; set; }
    public bool                IsOnlineBooking  { get; set; }

    public decimal AmountDue      => TotalAmount + LatePenaltyTotal - AmountPaid;
    public decimal DepositBalance => DepositPaid - DepositRequired;
    public bool    IsOverdue      => Status == RentalBookingStatus.PickedUp
                                     && DateTime.UtcNow.Date > ReturnDueDate.Date;

    public virtual Customer                        Customer      { get; set; } = null!;
    public virtual ApplicationUser?                HandledBy     { get; set; }
    public virtual ICollection<RentalBookingItem>  Items         { get; set; } = [];
    public virtual ICollection<RentalPayment>      Payments      { get; set; } = [];
    public virtual ICollection<DamageReport>       DamageReports { get; set; } = [];
}

public class RentalBookingItem : BaseEntity
{
    public Guid    BookingId    { get; set; }
    public Guid    AssetId      { get; set; }
    public decimal UnitPrice    { get; set; }
    public int     Quantity     { get; set; } = 1;
    public int     RentalDays   { get; set; }
    public decimal SubTotal     { get; set; }
    public bool    IsReturned   { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnNotes  { get; set; }

    public virtual RentalBooking Booking { get; set; } = null!;
    public virtual RentalAsset   Asset   { get; set; } = null!;
}

public class RentalPayment : BaseEntity
{
    public Guid                  BookingId { get; set; }
    public RentalPaymentPurpose  Purpose   { get; set; }
    public RentalPaymentMethod   Method    { get; set; }
    public decimal               Amount    { get; set; }
    public string?               Reference { get; set; }
    public DateTime              PaidAt    { get; set; } = DateTime.UtcNow;
    public Guid?                 ReceivedBy { get; set; }
    public string?               Notes     { get; set; }

    public virtual RentalBooking Booking { get; set; } = null!;
}

// ── Maintenance & Damage ─────────────────────────────────────────────────
public class DamageReport : BaseEntity
{
    public Guid        BookingId           { get; set; }
    public Guid        AssetId             { get; set; }
    public string      Title               { get; set; } = string.Empty;
    public string?     Description         { get; set; }
    public DamageLevel DamageLevel         { get; set; }
    public decimal     CompensationAmount  { get; set; }
    public bool        CompensationPaid    { get; set; }
    public DateTime?   CompensationPaidAt  { get; set; }
    public string      DamageImagesJson    { get; set; } = "[]";
    public bool        RequiresMaintenance { get; set; }
    public Guid?       ReportedBy          { get; set; }

    public virtual RentalBooking Booking { get; set; } = null!;
    public virtual RentalAsset   Asset   { get; set; } = null!;
}

public class MaintenanceLog : BaseEntity
{
    public Guid      AssetId         { get; set; }
    public string    Title           { get; set; } = string.Empty;
    public string?   Description     { get; set; }
    public string?   TechnicianName  { get; set; }
    public decimal   Cost            { get; set; }
    public DateTime  ScheduledDate   { get; set; }
    public DateTime? CompletedDate   { get; set; }
    public bool      IsCompleted     { get; set; }
    public Guid?     DamageReportId  { get; set; }

    public virtual RentalAsset    Asset        { get; set; } = null!;
    public virtual DamageReport?  DamageReport { get; set; }
}

// ── Marketplace Listing ───────────────────────────────────────────────────
public class MarketplaceListing : BaseEntity
{
    public Guid     AssetId       { get; set; }
    public string   Title         { get; set; } = string.Empty;
    public string?  Description   { get; set; }
    public decimal  ListedPrice   { get; set; }
    public bool     IsActive      { get; set; } = true;
    public int      ViewCount     { get; set; }
    public DateTime? ExpiresAt    { get; set; }

    public virtual RentalAsset Asset { get; set; } = null!;
}

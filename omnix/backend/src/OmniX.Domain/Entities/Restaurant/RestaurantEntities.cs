using OmniX.Domain.Entities.Core;
using OmniX.Domain.Entities.Auth;
using OmniX.Domain.Enums;

namespace OmniX.Domain.Entities.Restaurant;

// ════════════════════════════════════════════════════════════════════════════
//  OmniX Restaurant Entities — من Ultra Enterprise v6
//  يشمل: Menu + FloorPlan + Tables + Kitchen Stations + QR Ordering
// ════════════════════════════════════════════════════════════════════════════

// ── Enums ────────────────────────────────────────────────────────────────
public enum TableShape         { Square, Round, Long }
public enum TableStatus        { Available, Occupied, Reserved, Cleaning, Blocked }
public enum SessionStatus      { Active, BillSent, Closed }
public enum DineInOrderStatus  { QRDraft, QRPendingConfirm, Draft, SentToKitchen, Preparing, Ready, Delivered, Cancelled }
public enum DineInOrderSource  { Waiter, QRSelf, Cashier }
public enum OrderItemStatus    { Pending, Preparing, Ready, Delivered, Cancelled }
public enum KitchenTicketStatus { New, Seen, Preparing, Done }
public enum MenuItemAvailability { Available, SoldOut, Hidden }
public enum ReservationStatus  { Pending, Confirmed, Seated, NoShow, Cancelled }

// ── Menu ─────────────────────────────────────────────────────────────────
public class RestaurantCategory : BaseEntity
{
    public string  NameAr    { get; set; } = string.Empty;
    public string  NameEn    { get; set; } = string.Empty;
    public string? ImageUrl  { get; set; }
    public string? Color     { get; set; }
    public int     SortOrder { get; set; }
    public bool    IsActive  { get; set; } = true;

    public virtual ICollection<RestaurantMenuItem> Items { get; set; } = [];
}

public class RestaurantMenuItem : BaseEntity
{
    public Guid    CategoryId         { get; set; }
    public string  NameAr             { get; set; } = string.Empty;
    public string  NameEn             { get; set; } = string.Empty;
    public string? DescriptionAr      { get; set; }
    public string? ImageUrl           { get; set; }
    public decimal BasePrice          { get; set; }
    public int     PrepTimeMinutes    { get; set; } = 10;
    public bool    IsFeatured         { get; set; }
    public bool    IsActive           { get; set; } = true;
    public int     SortOrder          { get; set; }
    public Guid?   DefaultKitchenStationId { get; set; }

    public virtual RestaurantCategory               Category       { get; set; } = null!;
    public virtual ICollection<MenuItemVariant>     Variants       { get; set; } = [];
    public virtual ICollection<MenuModifierGroup>   ModifierGroups { get; set; } = [];
    public virtual ICollection<BranchMenuOverride>  BranchOverrides { get; set; } = [];
    public virtual ICollection<BranchItemAvailability> Availabilities { get; set; } = [];
}

public class MenuItemVariant : BaseEntity
{
    public Guid   MenuItemId  { get; set; }
    public string NameAr      { get; set; } = string.Empty;
    public string NameEn      { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
    public bool   IsDefault   { get; set; }
    public int    SortOrder   { get; set; }
    public bool   IsActive    { get; set; } = true;

    public virtual RestaurantMenuItem MenuItem { get; set; } = null!;
}

public class MenuModifierGroup : BaseEntity
{
    public Guid   MenuItemId    { get; set; }
    public string NameAr        { get; set; } = string.Empty;
    public string NameEn        { get; set; } = string.Empty;
    public bool   IsRequired    { get; set; }
    public bool   AllowMultiple { get; set; } = true;
    public int    MaxSelections { get; set; } = 10;
    public int    SortOrder     { get; set; }
    public bool   IsActive      { get; set; } = true;

    public virtual RestaurantMenuItem         MenuItem  { get; set; } = null!;
    public virtual ICollection<MenuModifier>  Modifiers { get; set; } = [];
}

public class MenuModifier : BaseEntity
{
    public Guid    ModifierGroupId { get; set; }
    public string  NameAr          { get; set; } = string.Empty;
    public string  NameEn          { get; set; } = string.Empty;
    public decimal ExtraPrice      { get; set; }
    public bool    IsDefault       { get; set; }
    public int     SortOrder       { get; set; }
    public bool    IsActive        { get; set; } = true;

    public virtual MenuModifierGroup Group { get; set; } = null!;
}

public class BranchMenuOverride : BaseEntity
{
    public Guid   BranchId         { get; set; }
    public Guid   MenuItemId       { get; set; }
    public decimal? OverridePrice  { get; set; }
    public bool   IsHiddenInBranch { get; set; }

    public virtual Branch             Branch   { get; set; } = null!;
    public virtual RestaurantMenuItem MenuItem { get; set; } = null!;
}

public class BranchItemAvailability : BaseEntity
{
    public Guid   BranchId          { get; set; }
    public Guid   MenuItemId        { get; set; }
    public MenuItemAvailability Status { get; set; } = MenuItemAvailability.Available;
    public DateTime? AvailableAgainAt { get; set; }
    public string? Reason           { get; set; }
    public Guid?  MarkedBy          { get; set; }

    public virtual Branch             Branch   { get; set; } = null!;
    public virtual RestaurantMenuItem MenuItem { get; set; } = null!;
}

// ── Floor Plan ───────────────────────────────────────────────────────────
public class FloorPlan : BaseEntity
{
    public Guid    BranchId           { get; set; }
    public string  Name               { get; set; } = string.Empty;
    public int     SortOrder          { get; set; }
    public bool    IsActive           { get; set; } = true;
    public int     CanvasWidth        { get; set; } = 1200;
    public int     CanvasHeight       { get; set; } = 800;
    public string? BackgroundImageUrl { get; set; }

    public virtual Branch                       Branch { get; set; } = null!;
    public virtual ICollection<RestaurantTable> Tables { get; set; } = [];
}

public class RestaurantTable : BaseEntity
{
    public Guid        BranchId    { get; set; }
    public Guid        FloorPlanId { get; set; }
    public string      TableNumber { get; set; } = string.Empty;
    public int         Capacity    { get; set; } = 4;
    public TableShape  Shape       { get; set; } = TableShape.Square;
    public TableStatus Status      { get; set; } = TableStatus.Available;
    public int  PosX               { get; set; }
    public int  PosY               { get; set; }
    public int  Width              { get; set; } = 80;
    public int  Height             { get; set; } = 80;
    public int  Rotation           { get; set; }
    public bool IsActive           { get; set; } = true;
    public string? QrToken         { get; set; }
    public string? QrCodeUrl       { get; set; }

    public virtual Branch                        Branch       { get; set; } = null!;
    public virtual FloorPlan                     FloorPlan    { get; set; } = null!;
    public virtual ICollection<TableSession>     Sessions     { get; set; } = [];
    public virtual ICollection<TableReservation> Reservations { get; set; } = [];
}

// ── Table Session ────────────────────────────────────────────────────────
public class TableSession : BaseEntity
{
    public Guid          BranchId         { get; set; }
    public Guid          TableId          { get; set; }
    public SessionStatus Status           { get; set; } = SessionStatus.Active;
    public int           GuestCount       { get; set; } = 1;
    public DateTime      OpenedAt         { get; set; } = DateTime.UtcNow;
    public DateTime?     ClosedAt         { get; set; }
    public Guid?         OpenedByUserId   { get; set; }
    public Guid?         CustomerId       { get; set; }
    public decimal       SubTotal         { get; set; }
    public decimal       ServiceCharge    { get; set; }
    public decimal       VatAmount        { get; set; }
    public decimal       TotalAmount      { get; set; }
    public decimal       AmountPaid       { get; set; }
    public decimal       Remaining        => Math.Max(0, TotalAmount - AmountPaid);
    public DateTime?     BillRequestedAt  { get; set; }
    public Guid?         BillRequestedBy  { get; set; }

    public virtual Branch                      Branch   { get; set; } = null!;
    public virtual RestaurantTable             Table    { get; set; } = null!;
    public virtual ApplicationUser?            OpenedBy { get; set; }
    public virtual Customer?                   Customer { get; set; }
    public virtual ICollection<DineInOrder>    Orders   { get; set; } = [];
    public virtual ICollection<SessionPayment> Payments { get; set; } = [];
}

public class TableReservation : BaseEntity
{
    public Guid               BranchId       { get; set; }
    public Guid               TableId        { get; set; }
    public string             GuestName      { get; set; } = string.Empty;
    public string?            GuestPhone     { get; set; }
    public int                GuestCount     { get; set; } = 2;
    public DateTime           ReservationAt  { get; set; }
    public int                DurationMins   { get; set; } = 90;
    public ReservationStatus  Status         { get; set; } = ReservationStatus.Pending;
    public string?            Notes          { get; set; }
    public DateTime?          ConfirmedAt    { get; set; }
    public DateTime?          SeatedAt       { get; set; }
    public DateTime?          CancelledAt    { get; set; }
    public Guid?              HandledBy      { get; set; }

    public virtual Branch          Branch { get; set; } = null!;
    public virtual RestaurantTable Table  { get; set; } = null!;
}

public class SessionPayment : BaseEntity
{
    public Guid          BranchId       { get; set; }
    public Guid          TableSessionId { get; set; }
    public decimal       Amount         { get; set; }
    public PaymentMethod PaymentMethod  { get; set; }
    public DateTime      PaidAt         { get; set; } = DateTime.UtcNow;
    public Guid?         ReceivedBy     { get; set; }
    public string?       Reference      { get; set; }

    public virtual TableSession Session { get; set; } = null!;
}

// ── Kitchen ──────────────────────────────────────────────────────────────
public class KitchenStation : BaseEntity
{
    public Guid    BranchId     { get; set; }
    public string  Name         { get; set; } = string.Empty;
    public string? Color        { get; set; }
    public bool    HasKdsScreen { get; set; }
    public bool    HasPrinter   { get; set; }
    public string? PrinterName  { get; set; }
    public int     SortOrder    { get; set; }
    public bool    IsActive     { get; set; } = true;

    // SignalR group key — unique per tenant+branch+station
    public string SignalRGroup => $"kitchen-{TenantId}-{BranchId}-{Id}";

    public virtual Branch                      Branch  { get; set; } = null!;
    public virtual ICollection<KitchenTicket>  Tickets { get; set; } = [];
}

// ── Dine-In Orders ───────────────────────────────────────────────────────
public class DineInOrder : BaseEntity
{
    public string           OrderNumber      { get; set; } = string.Empty;
    public Guid             BranchId         { get; set; }
    public Guid             TableSessionId   { get; set; }
    public DineInOrderSource OrderSource     { get; set; }
    public DineInOrderStatus Status          { get; set; }
    public Guid?            WaiterId         { get; set; }
    public Guid?            ConfirmedByUserId { get; set; }
    public DateTime?        ConfirmedAt      { get; set; }
    public string?          GuestName        { get; set; }
    public decimal          SubTotal         { get; set; }
    public decimal          DiscountAmount   { get; set; }
    public decimal          TotalAmount      { get; set; }
    public DateTime         OrderedAt        { get; set; } = DateTime.UtcNow;
    public DateTime?        KitchenSentAt    { get; set; }
    public DateTime?        ReadyAt          { get; set; }
    public DateTime?        DeliveredAt      { get; set; }
    public DateTime?        CancelledAt      { get; set; }
    public string?          Notes            { get; set; }
    public string?          CancelReason     { get; set; }
    public int              EstimatedPrepMins { get; set; }

    public bool NeedsWaiterConfirm => Status == DineInOrderStatus.QRPendingConfirm;
    public bool IsQROrder          => OrderSource == DineInOrderSource.QRSelf;

    public virtual Branch                      Branch  { get; set; } = null!;
    public virtual TableSession                Session { get; set; } = null!;
    public virtual ApplicationUser?            Waiter  { get; set; }
    public virtual ICollection<DineInOrderItem> Items   { get; set; } = [];
    public virtual ICollection<KitchenTicket>   Tickets { get; set; } = [];
}

public class DineInOrderItem : BaseEntity
{
    public Guid           DineInOrderId        { get; set; }
    public Guid           MenuItemId           { get; set; }
    public string         ItemNameAr           { get; set; } = string.Empty;
    public string         ItemNameEn           { get; set; } = string.Empty;
    public Guid?          VariantId            { get; set; }
    public string?        VariantName          { get; set; }
    public int            Quantity             { get; set; }
    public decimal        UnitPrice            { get; set; }
    public decimal        LineTotal            { get; set; }
    public OrderItemStatus Status              { get; set; }
    public Guid?          KitchenStationId     { get; set; }
    public string?        SpecialInstructions  { get; set; }

    public virtual DineInOrder                      Order          { get; set; } = null!;
    public virtual RestaurantMenuItem               MenuItem       { get; set; } = null!;
    public virtual MenuItemVariant?                 Variant        { get; set; }
    public virtual KitchenStation?                  KitchenStation { get; set; }
    public virtual ICollection<OrderItemModifier>   Modifiers      { get; set; } = [];
}

public class OrderItemModifier : BaseEntity
{
    public Guid    OrderItemId { get; set; }
    public Guid    ModifierId  { get; set; }
    public string  NameAr      { get; set; } = string.Empty;
    public string  NameEn      { get; set; } = string.Empty;
    public decimal ExtraPrice  { get; set; }

    public virtual DineInOrderItem OrderItem { get; set; } = null!;
    public virtual MenuModifier    Modifier  { get; set; } = null!;
}

public class KitchenTicket : BaseEntity
{
    public Guid               BranchId         { get; set; }
    public Guid               DineInOrderId    { get; set; }
    public Guid               KitchenStationId { get; set; }
    public string             TicketNumber     { get; set; } = string.Empty;
    public string             TableNumber      { get; set; } = string.Empty;
    public KitchenTicketStatus Status          { get; set; } = KitchenTicketStatus.New;
    public DateTime           CreatedAtKitchen { get; set; } = DateTime.UtcNow;
    public DateTime?          SeenAt           { get; set; }
    public DateTime?          PreparingAt      { get; set; }
    public DateTime?          DoneAt           { get; set; }
    public bool               IsPrinted        { get; set; }
    public DateTime?          PrintedAt        { get; set; }

    public int? PrepSeconds => DoneAt.HasValue
        ? (int)(DoneAt.Value - CreatedAtKitchen).TotalSeconds : null;

    public virtual DineInOrder                     Order   { get; set; } = null!;
    public virtual KitchenStation                  Station { get; set; } = null!;
    public virtual ICollection<KitchenTicketItem>  Items   { get; set; } = [];
}

public class KitchenTicketItem : BaseEntity
{
    public Guid           KitchenTicketId    { get; set; }
    public Guid           OrderItemId        { get; set; }
    public string         ItemName           { get; set; } = string.Empty;
    public int            Quantity           { get; set; } = 1;
    public string?        Modifiers          { get; set; }
    public string?        SpecialInstructions { get; set; }
    public OrderItemStatus Status            { get; set; } = OrderItemStatus.Pending;
    public DateTime?      DoneAt             { get; set; }

    public virtual KitchenTicket   Ticket    { get; set; } = null!;
    public virtual DineInOrderItem OrderItem { get; set; } = null!;
}

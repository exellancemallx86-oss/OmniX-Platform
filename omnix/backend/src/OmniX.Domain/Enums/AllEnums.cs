namespace OmniX.Domain.Enums;

// ════════════════════════════════════════════════════════════════════════════
//  OmniX Platform — Unified Enums
//  مدمجة من: Ultra v6 + MallX + MesterX Pro
// ════════════════════════════════════════════════════════════════════════════

// ── Users & Auth ─────────────────────────────────────────────────────────────
public enum UserRole : short
{
    PlatformOwner = 0,
    SuperAdmin    = 1,
    CompanyOwner  = 2,
    TenantAdmin   = 3,
    Manager       = 4,
    Cashier       = 5,
    Viewer        = 6,
}

// ── Tenant & Business ────────────────────────────────────────────────────────
public enum TenantStatus : short { Trial = 0, Active = 1, Suspended = 2, Cancelled = 3 }

public enum BusinessType : short
{
    Restaurant   = 0, Supermarket = 1, RetailStore = 2, Pharmacy   = 3,
    Services     = 4, RentalEvents = 5, Café       = 6, Bakery     = 7,
    Electronics  = 8, Clothing    = 9, Other       = 99,
}

[Flags]
public enum TenantModules : int
{
    None        = 0,
    POS         = 1 << 0,
    Inventory   = 1 << 1,
    Restaurant  = 1 << 2,
    Delivery    = 1 << 3,
    Rental      = 1 << 4,
    Marketplace = 1 << 5,
    CRM         = 1 << 6,
    Accounting  = 1 << 7,
    WhatsApp    = 1 << 8,
    AI          = 1 << 9,
    Mall        = 1 << 10,   // من MallX
    Loyalty     = 1 << 11,   // من MallX
    Wallet      = 1 << 12,   // من MallX Phase 9
}

// ── Plans & Billing ──────────────────────────────────────────────────────────
public enum PlanType : short { Trial = 0, Basic = 1, ProMonthly = 2, Lifetime = 3, Enterprise = 4 }
public enum BillingCycle : short { Monthly = 0, Annual = 1, Once = 2 }
public enum SubscriptionStatus : short { Trial = 0, Active = 1, PastDue = 2, Cancelled = 3, Paused = 4 }
public enum LicenseStatus : short { Active = 0, Expired = 1, Suspended = 2, Downgraded = 3, Revoked = 4 }

// ── Products & Inventory ─────────────────────────────────────────────────────
public enum ProductType : short { Physical = 0, Service = 1, Digital = 2, Composite = 3 }
public enum ProductUnit : short { Piece = 0, Kg = 1, Gram = 2, Liter = 3, Ml = 4, Meter = 5, Box = 6, Dozen = 7 }
public enum StockMovementType : short
{
    Purchase = 0, Sale = 1, Adjustment = 2, Return = 3,
    Transfer = 4, PhysicalCount = 5, Waste = 6, Opening = 7,
}

// ── Sales & POS ──────────────────────────────────────────────────────────────
public enum SaleStatus : short
{
    Pending = 0, Completed = 1, PartialRefund = 2,
    Refunded = 3, Voided = 4, OnHold = 5,
}
public enum PaymentMethod : short
{
    Cash = 0, Card = 1, Wallet = 2, BankTransfer = 3,
    Mixed = 4, Credit = 5, Fawry = 6, Paymob = 7,   // Paymob من MallX
}
public enum DiscountType : short { Amount = 0, Percentage = 1 }

// ── Orders ───────────────────────────────────────────────────────────────────
public enum OrderStatus : short
{
    Pending = 0, Confirmed = 1, Preparing = 2, ReadyForPickup = 3,
    OutForDelivery = 4, Delivered = 5, Cancelled = 6, Refunded = 7,
}
public enum DeliveryStatus : short
{
    Pending = 0, AssignedToDriver = 1, PickedUp = 2,
    InTransit = 3, Delivered = 4, FailedDelivery = 5, Returned = 6,
}
public enum DriverStatus : short { Offline = 0, Online = 1, Busy = 2, Suspended = 3 }

// ── Customers & Mall ─────────────────────────────────────────────────────────
public enum CustomerSegment : short { New = 0, Regular = 1, VIP = 2, Inactive = 3, Blocked = 4 }
public enum Gender : short { Unknown = 0, Male = 1, Female = 2 }

// MallX customer types
public enum MallCustomerStatus : short { Active = 0, Suspended = 1, Banned = 2 }
public enum MallOrderStatus : short
{
    Pending = 0, Confirmed = 1, Processing = 2, ReadyForPickup = 3,
    OutForDelivery = 4, Delivered = 5, Cancelled = 6, Refunded = 7,
}

// ── Suppliers & Purchasing ────────────────────────────────────────────────────
public enum SupplierStatus : short { Active = 0, Inactive = 1, Blacklisted = 2 }
public enum PurchaseOrderStatus : short
{
    Draft = 0, Sent = 1, Received = 2,
    PartiallyReceived = 3, Cancelled = 4, Overdue = 5,
}
public enum ExpenseCategory : short
{
    Rent = 0, Salaries = 1, Utilities = 2, Marketing = 3, Supplies = 4,
    Maintenance = 5, Delivery = 6, Insurance = 7, Taxes = 8, Other = 99,
}

// ── Sync (من Pro) ────────────────────────────────────────────────────────────
public enum SyncEntityType : short
{
    SaleOrder = 0, Customer = 1, Product = 2,
    StockAdjustment = 3, StockMovement = 4, Payment = 5, Expense = 6,
}
public enum SyncStatus : short { Pending = 0, Processing = 1, Completed = 2, Conflict = 3, Failed = 4 }
public enum SyncOperation : short { Insert = 0, Update = 1, Delete = 2 }
public enum ConflictStrategy : short { ServerWins = 0, ClientWins = 1, LatestWins = 2, Manual = 3 }

// ── Audit ─────────────────────────────────────────────────────────────────────
public enum AuditAction : short
{
    Create = 0, Update = 1, Delete = 2, Read = 3,
    Login = 4, Logout = 5, LoginFailed = 6, PasswordChange = 7,
    MfaEnabled = 8, MfaDisabled = 9, LicenseActivate = 10, LicenseDowngrade = 11,
    LicenseSuspend = 12, SyncPush = 13, SyncPull = 14, Export = 15,
    BackupCreated = 16, TenantSuspend = 17, TenantActivate = 18,
}

// ── Vendors & Marketplace ─────────────────────────────────────────────────────
public enum VendorType : short
{
    Photographer = 0, Videographer = 1, Hall = 2, Catering = 3, Decoration = 4,
    Car = 5, DressRental = 6, MakeUp = 7, Flowers = 8, Music = 9, Other = 10,
}

// VendorBooking — تم rename من Booking لحل التعارض مع StoreBooking
public enum VendorBookingStatus : short
{
    Pending = 0, Confirmed = 1, InProgress = 2, Completed = 3, Cancelled = 4, Refunded = 5,
}

// StoreBooking — من MallX Phase 3
public enum StoreBookingStatus : short
{
    Requested = 0, Confirmed = 1, Completed = 2, Cancelled = 3, NoShow = 4,
}

// ── Rental ────────────────────────────────────────────────────────────────────
public enum RentalAssetStatus : short { Available = 0, Rented = 1, Maintenance = 2, Damaged = 3, Retired = 4 }
public enum RentalBookingStatus : short
{
    Reserved = 0, Active = 1, Returned = 2, Cancelled = 3, Overdue = 4,
}
public enum DamageLevel : short { None = 0, Minor = 1, Moderate = 2, Severe = 3, TotalLoss = 4 }

// ── Loyalty & Wallet (من MallX) ───────────────────────────────────────────────
public enum WalletTransactionType : short
{
    TopUp = 0, Purchase = 1, Refund = 2, Cashback = 3, Commission = 4, Withdrawal = 5,
}
public enum LoyaltyTransactionType : short { Earned = 0, Redeemed = 1, Expired = 2, Adjusted = 3 }
public enum CouponType : short { Percentage = 0, FixedAmount = 1, FreeDelivery = 2 }

// ── Notifications ─────────────────────────────────────────────────────────────
public enum NotificationType : short { Info = 0, Success = 1, Warning = 2, Error = 3 }
public enum NotificationChannel : short { InApp = 0, Email = 1, SMS = 2, WhatsApp = 3, Push = 4 }

// ── Payment Status ────────────────────────────────────────────────────────────
public enum PaymentStatus : short { Pending = 0, Processing = 1, Completed = 2, Failed = 3, Refunded = 4 }
public enum PaymentType : short { Deposit = 0, FullPayment = 1, Refund = 2, VendorCommission = 3 }

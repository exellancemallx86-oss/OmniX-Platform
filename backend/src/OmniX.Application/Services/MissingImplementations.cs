// ═══════════════════════════════════════════════════════════════════════════
//  OmniX — Missing Service Implementations
//  يُصلح: 10 classes غير موجودة (DI مسجّلة لكن implementations ناقصة)
//
//  الحالة:
//  - بعضها stub مؤقت (AI, Paymob, Delivery) — يكتمل في Phase 2
//  - بعضها functional (Inventory, Loyalty, Wallet, Customer, Dashboard)
//  - بعضها معيد التوجيه لـ services موجودة (Mall, Rental)
// ═══════════════════════════════════════════════════════════════════════════
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniX.Application.Services;
using OmniX.Domain.Entities.Auth;
using OmniX.Domain.Entities.Core;
using OmniX.Domain.Entities.Mall;
using OmniX.Domain.Entities.Restaurant;
using OmniX.Domain.Enums;
using OmniX.Infrastructure.Data;

// ══════════════════════════════════════════════════════════════════════════
//  SHARED DTOs
// ══════════════════════════════════════════════════════════════════════════
namespace OmniX.Application.Services;

public record InventoryItemDto(Guid ProductId, string Name, decimal Quantity,
    decimal MinQty, string Unit, string BranchName, bool IsLow);

public record LoyaltyBalanceDto(int Points, string Tier,
    int PointsToNext, decimal MaxRedeemValue);

public record LoyaltyTransactionDto(Guid Id, string Type, int Points,
    string Description, DateTime CreatedAt, int BalanceAfter);

public record WalletBalanceDto(decimal Balance, string Currency);

public record WalletTxDto(Guid Id, string Type, decimal Amount,
    decimal BalanceAfter, string? Note, DateTime CreatedAt);

public record CustomerProfileDto(Guid Id, string Name, string? Phone,
    string? Email, decimal Balance, int LoyaltyPoints, DateTime CreatedAt);

public record DashboardKpiDto(decimal TodaySales, int TodayOrders,
    decimal WeekSales, int WeekOrders, decimal MonthSales,
    int MonthOrders, int LowStockCount, int ActiveTables);

public record SalesTrendDto(IReadOnlyList<SalesPointDto> Points,
    DateTime From, DateTime To);

public record SalesPointDto(DateTime Date, decimal Revenue, int Orders);

public record DeliveryDto(Guid Id, string OrderNumber, string Status,
    string CustomerName, string? DriverName, DateTime CreatedAt);

public record AIChatResponseDto(string Reply,
    IReadOnlyList<AIRecommendationDto>? Recommendations);

public record AIRecommendationDto(string Name, decimal Price, string Reason);

public record PaymobPaymentDto(string IframeUrl, string Token,
    long OrderId, bool Success);

public record AddCartItemRequest(Guid ProductId, int Quantity, string? Notes);

public record UpdateCartItemRequest(Guid CartItemId, int Quantity);

public record CartDto(Guid Id, IReadOnlyList<CartItemDto> Items, decimal Total);

public record CartItemDto(Guid Id, Guid ProductId, string ProductName,
    string StoreName, int Quantity, decimal UnitPrice, decimal Subtotal);

public record InitiatePaymentRequest(decimal Amount, string OrderId,
    string CustomerEmail, string CustomerPhone, string CustomerName);

// ══════════════════════════════════════════════════════════════════════════
//  1. INVENTORY SERVICE
// ══════════════════════════════════════════════════════════════════════════
public class InventoryService : IInventoryService
{
    private readonly OmniXDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ILogger<InventoryService> _log;

    public InventoryService(OmniXDbContext db, ICurrentUserService user,
        ILogger<InventoryService> log) => (_db, _user, _log) = (db, user, log);

    public async Task<ApiResponse<IReadOnlyList<InventoryItemDto>>> GetStockAsync(
        Guid branchId, CancellationToken ct)
    {
        var items = await _db.StockItems.AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.Branch)
            .Where(s => s.TenantId == _user.TenantId && s.BranchId == branchId)
            .OrderBy(s => s.Product.Name)
            .Select(s => new InventoryItemDto(
                s.ProductId, s.Product.Name, s.Quantity,
                (decimal)s.Product.LowStockAlert,
                s.Product.Unit.ToString(),
                s.Branch.Name,
                s.Quantity <= s.Product.LowStockAlert))
            .ToListAsync(ct);

        return ApiResponse<IReadOnlyList<InventoryItemDto>>.Ok(items);
    }

    public async Task<ApiResponse<bool>> AdjustStockAsync(
        Guid branchId, Guid productId, decimal qty, string reason,
        CancellationToken ct)
    {
        var stock = await _db.StockItems
            .FirstOrDefaultAsync(s =>
                s.TenantId == _user.TenantId &&
                s.BranchId == branchId &&
                s.ProductId == productId, ct);

        if (stock is null)
        {
            stock = new StockItem
            {
                TenantId  = _user.TenantId ?? Guid.Empty,
                BranchId  = branchId,
                ProductId = productId,
                Quantity  = 0
            };
            _db.StockItems.Add(stock);
        }

        var before = stock.Quantity;
        stock.Quantity  += qty;
        stock.UpdatedAt  = DateTime.UtcNow;

        _db.StockMovements.Add(new StockMovement
        {
            TenantId      = _user.TenantId ?? Guid.Empty,
            BranchId      = branchId,
            ProductId     = productId,
            MovementType  = qty > 0 ? MovementType.Adjustment : MovementType.Adjustment,
            Quantity      = qty,
            BalanceBefore = before,
            BalanceAfter  = stock.Quantity,
            Notes         = reason
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<IReadOnlyList<InventoryItemDto>>> GetLowStockAsync(
        CancellationToken ct)
    {
        var items = await _db.StockItems.AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.Branch)
            .Where(s => s.TenantId == _user.TenantId &&
                        s.Quantity <= s.Product.LowStockAlert)
            .Select(s => new InventoryItemDto(
                s.ProductId, s.Product.Name, s.Quantity,
                (decimal)s.Product.LowStockAlert,
                s.Product.Unit.ToString(), s.Branch.Name, true))
            .ToListAsync(ct);

        return ApiResponse<IReadOnlyList<InventoryItemDto>>.Ok(items);
    }
}

// ══════════════════════════════════════════════════════════════════════════
//  2. LOYALTY SERVICE
// ══════════════════════════════════════════════════════════════════════════
public class LoyaltyService : ILoyaltyService
{
    private readonly OmniXDbContext _db;
    private readonly ICurrentUserService _user;

    public LoyaltyService(OmniXDbContext db, ICurrentUserService user)
        => (_db, _user) = (db, user);

    public async Task<ApiResponse<LoyaltyBalanceDto>> GetBalanceAsync(
        Guid customerId, CancellationToken ct)
    {
        var customer = await _db.MallCustomers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);

        if (customer is null)
            return ApiResponse<LoyaltyBalanceDto>.Fail("Customer not found");

        var tier = customer.LoyaltyPoints switch
        {
            >= 2000 => "ذهبي",
            >= 500  => "فضي",
            _       => "برونزي"
        };

        var toNext = customer.LoyaltyPoints switch
        {
            < 500  => 500  - customer.LoyaltyPoints,
            < 2000 => 2000 - customer.LoyaltyPoints,
            _      => 0
        };

        return ApiResponse<LoyaltyBalanceDto>.Ok(new LoyaltyBalanceDto(
            customer.LoyaltyPoints, tier, toNext,
            customer.LoyaltyPoints * 0.5m));
    }

    public async Task<ApiResponse<bool>> AwardPointsAsync(
        Guid customerId, int points, string reason, CancellationToken ct)
    {
        var customer = await _db.MallCustomers
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);

        if (customer is null) return ApiResponse<bool>.Fail("Customer not found");

        customer.LoyaltyPoints += points;
        customer.UpdatedAt      = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> RedeemPointsAsync(
        Guid customerId, int points, Guid orderId, CancellationToken ct)
    {
        var customer = await _db.MallCustomers
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);

        if (customer is null) return ApiResponse<bool>.Fail("Customer not found");
        if (customer.LoyaltyPoints < points)
            return ApiResponse<bool>.Fail("Insufficient points");

        customer.LoyaltyPoints -= points;
        customer.UpdatedAt      = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }
}

// ══════════════════════════════════════════════════════════════════════════
//  3. WALLET SERVICE
// ══════════════════════════════════════════════════════════════════════════
public class WalletService : IWalletService
{
    private readonly OmniXDbContext _db;

    public WalletService(OmniXDbContext db) => _db = db;

    public async Task<ApiResponse<WalletBalanceDto>> GetBalanceAsync(
        Guid customerId, CancellationToken ct)
    {
        var customer = await _db.MallCustomers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);

        return customer is null
            ? ApiResponse<WalletBalanceDto>.Fail("Customer not found")
            : ApiResponse<WalletBalanceDto>.Ok(
                new WalletBalanceDto(customer.WalletBalance, "EGP"));
    }

    public async Task<ApiResponse<bool>> TopUpAsync(
        Guid customerId, decimal amount, string? reference, CancellationToken ct)
    {
        if (amount <= 0) return ApiResponse<bool>.Fail("Amount must be positive");

        var customer = await _db.MallCustomers
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);

        if (customer is null) return ApiResponse<bool>.Fail("Customer not found");

        customer.WalletBalance += amount;
        customer.UpdatedAt      = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> DeductAsync(
        Guid customerId, decimal amount, string orderId, CancellationToken ct)
    {
        var customer = await _db.MallCustomers
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);

        if (customer is null) return ApiResponse<bool>.Fail("Customer not found");
        if (customer.WalletBalance < amount)
            return ApiResponse<bool>.Fail("Insufficient wallet balance");

        customer.WalletBalance -= amount;
        customer.UpdatedAt      = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }
}

// ══════════════════════════════════════════════════════════════════════════
//  4. CUSTOMER SERVICE
// ══════════════════════════════════════════════════════════════════════════
public class CustomerService : ICustomerService
{
    private readonly OmniXDbContext _db;
    private readonly ICurrentUserService _user;

    public CustomerService(OmniXDbContext db, ICurrentUserService user)
        => (_db, _user) = (db, user);

    public async Task<ApiResponse<IReadOnlyList<CustomerProfileDto>>> GetCustomersAsync(
        string? search, int page, CancellationToken ct)
    {
        var query = _db.Customers.AsNoTracking()
            .Where(c => c.TenantId == _user.TenantId && c.IsActive);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(c =>
                c.Name.Contains(search) ||
                (c.Phone != null && c.Phone.Contains(search)));

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * 20).Take(20)
            .Select(c => new CustomerProfileDto(
                c.Id, c.Name, c.Phone, c.Email,
                c.CurrentBalance, c.LoyaltyPoints, c.CreatedAt))
            .ToListAsync(ct);

        return ApiResponse<IReadOnlyList<CustomerProfileDto>>.Ok(items);
    }

    public async Task<ApiResponse<CustomerProfileDto?>> GetByIdAsync(
        Guid id, CancellationToken ct)
    {
        var c = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct);

        return c is null
            ? ApiResponse<CustomerProfileDto?>.Fail("Customer not found")
            : ApiResponse<CustomerProfileDto?>.Ok(
                new CustomerProfileDto(c.Id, c.Name, c.Phone, c.Email,
                    c.CurrentBalance, c.LoyaltyPoints, c.CreatedAt));
    }
}

// ══════════════════════════════════════════════════════════════════════════
//  5. DASHBOARD SERVICE
// ══════════════════════════════════════════════════════════════════════════
public class DashboardService : IDashboardService
{
    private readonly OmniXDbContext _db;
    private readonly ICurrentUserService _user;

    public DashboardService(OmniXDbContext db, ICurrentUserService user)
        => (_db, _user) = (db, user);

    public async Task<ApiResponse<DashboardKpiDto>> GetKpisAsync(
        Guid? branchId, CancellationToken ct)
    {
        var tid   = _user.TenantId ?? Guid.Empty;
        var today = DateTime.UtcNow.Date;
        var week  = today.AddDays(-7);
        var month = today.AddDays(-30);

        var salesQ = _db.SaleOrders.AsNoTracking()
            .Where(o => o.TenantId == tid && o.Status == SaleStatus.Completed);

        if (branchId.HasValue)
            salesQ = salesQ.Where(o => o.BranchId == branchId.Value);

        var todaySales  = await salesQ.Where(o => o.CreatedAt >= today).SumAsync(o => (decimal?)o.Total, ct) ?? 0;
        var todayOrders = await salesQ.CountAsync(o => o.CreatedAt >= today, ct);
        var weekSales   = await salesQ.Where(o => o.CreatedAt >= week).SumAsync(o => (decimal?)o.Total, ct) ?? 0;
        var weekOrders  = await salesQ.CountAsync(o => o.CreatedAt >= week, ct);
        var monthSales  = await salesQ.Where(o => o.CreatedAt >= month).SumAsync(o => (decimal?)o.Total, ct) ?? 0;
        var monthOrders = await salesQ.CountAsync(o => o.CreatedAt >= month, ct);

        var lowStock = await _db.StockItems.AsNoTracking()
            .Include(s => s.Product)
            .CountAsync(s => s.TenantId == tid && s.Quantity <= s.Product.LowStockAlert, ct);

        var activeTables = await _db.TableSessions.AsNoTracking()
            .CountAsync(s => s.TenantId == tid && s.Status == SessionStatus.Active, ct);

        return ApiResponse<DashboardKpiDto>.Ok(new DashboardKpiDto(
            todaySales, todayOrders,
            weekSales,  weekOrders,
            monthSales, monthOrders,
            lowStock,   activeTables));
    }

    public async Task<ApiResponse<SalesTrendDto>> GetSalesTrendAsync(
        Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var tid = _user.TenantId ?? Guid.Empty;

        var raw = await _db.SaleOrders.AsNoTracking()
            .Where(o => o.TenantId == tid &&
                        o.Status == SaleStatus.Completed &&
                        o.CreatedAt >= from && o.CreatedAt <= to)
            .Select(o => new { o.CreatedAt, o.Total })
            .ToListAsync(ct);

        if (branchId.HasValue)
        {
            var bid = branchId.Value;
            raw = await _db.SaleOrders.AsNoTracking()
                .Where(o => o.TenantId == tid && o.BranchId == bid &&
                            o.Status == SaleStatus.Completed &&
                            o.CreatedAt >= from && o.CreatedAt <= to)
                .Select(o => new { o.CreatedAt, o.Total })
                .ToListAsync(ct);
        }

        var points = raw
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new SalesPointDto(g.Key, g.Sum(o => o.Total), g.Count()))
            .OrderBy(p => p.Date)
            .ToList();

        return ApiResponse<SalesTrendDto>.Ok(new SalesTrendDto(points, from, to));
    }
}

// ══════════════════════════════════════════════════════════════════════════
//  6. DELIVERY SERVICE
// ══════════════════════════════════════════════════════════════════════════
public class DeliveryService : IDeliveryService
{
    private readonly OmniXDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ILogger<DeliveryService> _log;

    public DeliveryService(OmniXDbContext db, ICurrentUserService user,
        ILogger<DeliveryService> log) => (_db, _user, _log) = (db, user, log);

    public async Task<ApiResponse<IReadOnlyList<DeliveryDto>>> GetActiveDeliveriesAsync(
        CancellationToken ct)
    {
        // MallOrders مع status = OutForDelivery
        var orders = await _db.MallOrders.AsNoTracking()
            .Include(o => o.Customer)
            .Where(o => o.TenantId == _user.TenantId &&
                        o.Status == "OutForDelivery")
            .OrderByDescending(o => o.CreatedAt)
            .Take(50)
            .Select(o => new DeliveryDto(
                o.Id, o.OrderNumber, o.Status,
                o.Customer.FirstName + " " + o.Customer.LastName,
                null, o.CreatedAt))
            .ToListAsync(ct);

        return ApiResponse<IReadOnlyList<DeliveryDto>>.Ok(orders);
    }

    public async Task<ApiResponse<bool>> AssignDriverAsync(
        Guid orderId, Guid driverId, CancellationToken ct)
    {
        var order = await _db.MallOrders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == _user.TenantId, ct);

        if (order is null) return ApiResponse<bool>.Fail("Order not found");

        order.Status    = "OutForDelivery";
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Driver {DriverId} assigned to order {OrderId}", driverId, orderId);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> UpdateDriverLocationAsync(
        Guid driverId, decimal lat, decimal lng, CancellationToken ct)
    {
        var driver = await _db.Drivers
            .FirstOrDefaultAsync(d => d.Id == driverId, ct);

        if (driver is null) return ApiResponse<bool>.Fail("Driver not found");

        driver.CurrentLat = lat;
        driver.CurrentLng = lng;
        driver.UpdatedAt  = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }
}

// ══════════════════════════════════════════════════════════════════════════
//  7. MALL SERVICE (Platform-level mall management)
// ══════════════════════════════════════════════════════════════════════════
public class MallService : IMallService
{
    private readonly OmniXDbContext _db;
    private readonly ICurrentUserService _user;

    public MallService(OmniXDbContext db, ICurrentUserService user)
        => (_db, _user) = (db, user);

    public async Task<ApiResponse<IReadOnlyList<Mall>>> GetMallsAsync(
        CancellationToken ct)
    {
        var malls = await _db.Malls.AsNoTracking()
            .Where(m => m.TenantId == _user.TenantId && m.IsActive)
            .OrderBy(m => m.Name)
            .ToListAsync(ct);

        return ApiResponse<IReadOnlyList<Mall>>.Ok(malls);
    }

    public async Task<ApiResponse<Mall>> CreateMallAsync(
        string name, string slug, CancellationToken ct)
    {
        var exists = await _db.Malls.AnyAsync(m => m.Slug == slug, ct);
        if (exists) return ApiResponse<Mall>.Fail("Slug already taken");

        var mall = new Mall
        {
            TenantId = _user.TenantId ?? Guid.Empty,
            Name     = name,
            Slug     = slug,
            IsActive = true
        };

        _db.Malls.Add(mall);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Mall>.Ok(mall);
    }
}

// ══════════════════════════════════════════════════════════════════════════
//  8. RENTAL SERVICE (delegates to RentalService in RentalControllers)
// ══════════════════════════════════════════════════════════════════════════
public class RentalService : IRentalService
{
    private readonly OmniXDbContext _db;
    private readonly ICurrentUserService _user;

    public RentalService(OmniXDbContext db, ICurrentUserService user)
        => (_db, _user) = (db, user);

    public async Task<ApiResponse<IReadOnlyList<RentalAsset>>> GetAssetsAsync(
        CancellationToken ct)
    {
        var assets = await _db.RentalAssets.AsNoTracking()
            .Where(a => a.TenantId == _user.TenantId && a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        return ApiResponse<IReadOnlyList<RentalAsset>>.Ok(assets);
    }

    public async Task<ApiResponse<RentalBooking>> CreateBookingAsync(
        Guid assetId, string customerName, string customerPhone,
        DateTime startsAt, DateTime endsAt, CancellationToken ct)
    {
        var asset = await _db.RentalAssets
            .FirstOrDefaultAsync(a => a.Id == assetId && a.TenantId == _user.TenantId, ct);

        if (asset is null) return ApiResponse<RentalBooking>.Fail("Asset not found");
        if (!asset.IsAvailable) return ApiResponse<RentalBooking>.Fail("Asset not available");

        var days    = (int)(endsAt - startsAt).TotalDays;
        var total   = days * asset.DailyRate;

        var booking = new RentalBooking
        {
            TenantId      = _user.TenantId ?? Guid.Empty,
            BookingNumber = $"RNT-{DateTime.UtcNow:yyyyMMddHHmm}",
            CustomerName  = customerName,
            CustomerPhone = customerPhone,
            StartsAt      = startsAt,
            EndsAt        = endsAt,
            Total         = total,
            Status        = RentalStatus.Confirmed
        };

        _db.RentalBookings.Add(booking);
        _db.RentalBookingItems.Add(new RentalBookingItem
        {
            TenantId  = _user.TenantId ?? Guid.Empty,
            BookingId = booking.Id,
            AssetId   = assetId,
            Days      = days,
            DailyRate = asset.DailyRate,
            Total     = total
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse<RentalBooking>.Ok(booking);
    }
}

// ══════════════════════════════════════════════════════════════════════════
//  9. AI SERVICE — Anthropic Claude integration
// ══════════════════════════════════════════════════════════════════════════
public class AnthropicAIService : IAIService
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration     _config;
    private readonly OmniXDbContext     _db;
    private readonly ICurrentUserService _user;
    private readonly ILogger<AnthropicAIService> _log;

    private string ApiKey => _config["Anthropic:ApiKey"] ?? "";
    private string Model  => _config["Anthropic:Model"]  ?? "claude-sonnet-4-5";
    private bool   IsConfigured => !string.IsNullOrEmpty(ApiKey);

    public AnthropicAIService(IHttpClientFactory http, IConfiguration config,
        OmniXDbContext db, ICurrentUserService user,
        ILogger<AnthropicAIService> log)
        => (_http, _config, _db, _user, _log) = (http, config, db, user, log);

    public async Task<ApiResponse<AIChatResponseDto>> ChatAsync(
        string message, IReadOnlyList<object>? history, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            _log.LogWarning("Anthropic AI not configured");
            return ApiResponse<AIChatResponseDto>.Ok(
                new AIChatResponseDto(
                    "مرحباً! خدمة الذكاء الاصطناعي غير مفعّلة حالياً. تواصل مع المدير.",
                    null));
        }

        try
        {
            var client = _http.CreateClient("Anthropic");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-api-key", ApiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var tenantName = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == _user.TenantId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct) ?? "المنشأة";

            var systemPrompt = $"""
                أنت مساعد ذكي لنظام OmniX لإدارة {tenantName}.
                تساعد الموظفين والمدراء على:
                - فهم التقارير والأرقام
                - الإجابة على أسئلة حول المنتجات والعمليات
                - تقديم اقتراحات لتحسين الأداء
                الرد دائماً بالعربية ما لم يُطلب غير ذلك.
                كن موجزاً ومفيداً.
                """;

            var messages = new List<object>();
            if (history != null) messages.AddRange(history);
            messages.Add(new { role = "user", content = message });

            var payload = new
            {
                model      = Model,
                max_tokens = 1024,
                system     = systemPrompt,
                messages
            };

            var response = await client.PostAsJsonAsync(
                "https://api.anthropic.com/v1/messages", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogError("Anthropic error: {Status}", response.StatusCode);
                return ApiResponse<AIChatResponseDto>.Fail("AI service temporarily unavailable");
            }

            using var doc = System.Text.Json.JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(ct));

            var reply = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text").GetString() ?? "";

            return ApiResponse<AIChatResponseDto>.Ok(
                new AIChatResponseDto(reply, null));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "AI chat error");
            return ApiResponse<AIChatResponseDto>.Fail("AI error: " + ex.Message);
        }
    }

    public async Task<ApiResponse<IReadOnlyList<AIRecommendationDto>>> GetRecommendationsAsync(
        Guid branchId, CancellationToken ct)
    {
        // بناءً على المبيعات — TOP items مع منتجات قليلة المخزون
        var topItems = await _db.SaleOrderItems.AsNoTracking()
            .Include(i => i.Order)
            .Where(i => i.TenantId == _user.TenantId &&
                        i.Order.BranchId == branchId &&
                        i.Order.CreatedAt >= DateTime.UtcNow.AddDays(-7))
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new AIRecommendationDto(
                g.Key.ProductName,
                g.Sum(i => i.UnitPrice * i.Quantity),
                $"الأكثر مبيعاً هذا الأسبوع ({g.Sum(i => i.Quantity)} وحدة)"))
            .OrderByDescending(x => x.Price)
            .Take(5)
            .ToListAsync(ct);

        return ApiResponse<IReadOnlyList<AIRecommendationDto>>.Ok(topItems);
    }
}

// ══════════════════════════════════════════════════════════════════════════
//  10. PAYMOB SERVICE
// ══════════════════════════════════════════════════════════════════════════
public class PaymobService : IPaymobService
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration     _config;
    private readonly ILogger<PaymobService> _log;

    private string ApiKey        => _config["Paymob:ApiKey"]        ?? "";
    private string IntegrationId => _config["Paymob:IntegrationId"] ?? "";
    private string IframeId      => _config["Paymob:IframeId"]      ?? "";
    private bool   IsConfigured  => !string.IsNullOrEmpty(ApiKey);

    public PaymobService(IHttpClientFactory http, IConfiguration config,
        ILogger<PaymobService> log) => (_http, _config, _log) = (http, config, log);

    public async Task<ApiResponse<PaymobPaymentDto>> InitiatePaymentAsync(
        InitiatePaymentRequest req, CancellationToken ct)
    {
        if (!IsConfigured)
            return ApiResponse<PaymobPaymentDto>.Fail(
                "Payment gateway not configured. Contact administrator.");

        try
        {
            var client = _http.CreateClient();

            // Step 1: Auth token
            var authResp = await client.PostAsJsonAsync(
                "https://accept.paymob.com/api/auth/tokens",
                new { api_key = ApiKey }, ct);

            if (!authResp.IsSuccessStatusCode)
                return ApiResponse<PaymobPaymentDto>.Fail("Payment auth failed");

            using var authDoc  = JsonDocument.Parse(await authResp.Content.ReadAsStringAsync(ct));
            var authToken      = authDoc.RootElement.GetProperty("token").GetString();

            // Step 2: Order registration
            var amountCents = (long)(req.Amount * 100);
            var orderResp   = await client.PostAsJsonAsync(
                "https://accept.paymob.com/api/ecommerce/orders",
                new
                {
                    auth_token     = authToken,
                    delivery_needed= false,
                    amount_cents   = amountCents,
                    currency       = "EGP",
                    merchant_order_id = req.OrderId
                }, ct);

            if (!orderResp.IsSuccessStatusCode)
                return ApiResponse<PaymobPaymentDto>.Fail("Order registration failed");

            using var orderDoc = JsonDocument.Parse(await orderResp.Content.ReadAsStringAsync(ct));
            var paymobOrderId  = orderDoc.RootElement.GetProperty("id").GetInt64();

            // Step 3: Payment key
            var keyResp = await client.PostAsJsonAsync(
                "https://accept.paymob.com/api/acceptance/payment_keys",
                new
                {
                    auth_token      = authToken,
                    amount_cents    = amountCents,
                    expiration      = 3600,
                    order_id        = paymobOrderId,
                    billing_data    = new
                    {
                        email         = req.CustomerEmail,
                        first_name    = req.CustomerName.Split(' ')[0],
                        last_name     = req.CustomerName.Contains(' ')
                                          ? req.CustomerName.Split(' ')[1] : ".",
                        phone_number  = req.CustomerPhone,
                        country       = "EG",
                        city          = "Cairo",
                        street        = "NA",
                        floor         = "NA",
                        building      = "NA",
                        apartment     = "NA",
                        shipping_method = "PKG",
                        postal_code   = "NA"
                    },
                    currency        = "EGP",
                    integration_id  = IntegrationId
                }, ct);

            if (!keyResp.IsSuccessStatusCode)
                return ApiResponse<PaymobPaymentDto>.Fail("Payment key failed");

            using var keyDoc = JsonDocument.Parse(await keyResp.Content.ReadAsStringAsync(ct));
            var paymentKey   = keyDoc.RootElement.GetProperty("token").GetString();
            var iframeUrl    = $"https://accept.paymob.com/api/acceptance/iframes/{IframeId}?payment_token={paymentKey}";

            return ApiResponse<PaymobPaymentDto>.Ok(new PaymobPaymentDto(
                iframeUrl, paymentKey ?? "", paymobOrderId, true));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Paymob payment error");
            return ApiResponse<PaymobPaymentDto>.Fail("Payment error: " + ex.Message);
        }
    }

    public Task<ApiResponse<bool>> VerifyHmacAsync(
        string hmac, string payload, CancellationToken ct)
    {
        // HMAC verification for Paymob webhooks
        var secret    = _config["Paymob:HmacSecret"] ?? "";
        using var hmacSha = new System.Security.Cryptography.HMACSHA512(
            System.Text.Encoding.UTF8.GetBytes(secret));
        var hash      = Convert.ToHexString(
            hmacSha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload)));
        var valid     = string.Equals(hash, hmac, StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(ApiResponse<bool>.Ok(valid));
    }
}

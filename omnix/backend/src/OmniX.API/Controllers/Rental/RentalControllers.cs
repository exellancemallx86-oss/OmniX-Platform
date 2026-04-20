using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OmniX.Domain.Entities.Rental;
using OmniX.Infrastructure.Data;

namespace OmniX.API.Controllers.Rental;

// ════════════════════════════════════════════════════════════════════════════
//  RENTAL ASSETS CONTROLLER
//  Route: /api/v1/rental/assets
// ════════════════════════════════════════════════════════════════════════════
[Authorize]
[ApiController]
[Route("api/v1/rental/assets")]
[EnableRateLimiting("api")]
public class RentalAssetController : OmniXBaseController
{
    private readonly OmniXDbContext _db;
    private readonly ITenantProvider _tenant;
    public RentalAssetController(OmniXDbContext db, ITenantProvider tenant)
    { _db = db; _tenant = tenant; }

    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();
    private bool IsManager => User.IsInRole("Manager") || User.IsInRole("CompanyOwner")
                           || User.IsInRole("TenantAdmin") || User.IsInRole("PlatformOwner");

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] AssetCategory? category = null,
        [FromQuery] AssetStatus? status = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var q = _db.RentalAssets.Where(a => a.TenantId == TenantId && !a.IsDeleted);
        if (category.HasValue) q = q.Where(a => a.Category == category.Value);
        if (status.HasValue)   q = q.Where(a => a.Status == status.Value);
        if (!string.IsNullOrEmpty(search))
            q = q.Where(a => a.Name.Contains(search) || a.AssetCode.Contains(search));

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(a => a.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new {
                a.Id, a.AssetCode, a.Name, a.Category, a.SubCategory, a.Brand,
                a.Size, a.Color, a.PricingModel, a.RentalPricePerDay, a.SalePrice,
                a.DepositAmount, a.Status, a.Condition, a.PrimaryImageUrl,
                a.IsListedOnMarketplace, a.TotalRentalCount, a.TotalRevenue
            }).ToListAsync(ct);

        return Ok(new { success = true, total, page, pageSize, items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var asset = await _db.RentalAssets
            .Include(a => a.Images)
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == id && !a.IsDeleted, ct);
        return asset is null ? NotFound() : Ok(new { success = true, data = asset });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRentalAssetReq req, CancellationToken ct)
    {
        if (!IsManager) return Forbid();

        var code = $"AST-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var asset = new RentalAsset {
            TenantId = TenantId, AssetCode = code, Name = req.Name,
            Category = req.Category, SubCategory = req.SubCategory,
            Brand = req.Brand, Size = req.Size, Color = req.Color,
            Description = req.Description, PricingModel = req.PricingModel,
            RentalPricePerDay = req.RentalPricePerDay, RentalPricePerHour = req.RentalPricePerHour,
            SalePrice = req.SalePrice, DepositAmount = req.DepositAmount,
            PurchaseCost = req.PurchaseCost, LatePenaltyPerDay = req.LatePenaltyPerDay,
            MaxRentalDays = req.MaxRentalDays, Barcode = req.Barcode,
            PrimaryImageUrl = req.PrimaryImageUrl, IsListedOnMarketplace = req.IsListedOnMarketplace,
            Status = AssetStatus.Available, Condition = AssetCondition.Excellent,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _db.RentalAssets.Add(asset);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = asset.Id }, new { success = true, data = new { asset.Id, asset.AssetCode } });
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] AssetStatus newStatus, CancellationToken ct)
    {
        if (!IsManager) return Forbid();
        var asset = await _db.RentalAssets.FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == id, ct);
        if (asset is null) return NotFound();
        asset.Status = newStatus; asset.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpGet("{id:guid}/availability")]
    public async Task<IActionResult> Availability(
        Guid id, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var bookings = await _db.RentalBookings
            .Include(b => b.Items)
            .Where(b => b.TenantId == TenantId
                && b.Items.Any(i => i.AssetId == id)
                && b.Status != RentalBookingStatus.Cancelled
                && b.ReturnDueDate >= from && b.PickupDate <= to)
            .Select(b => new { b.PickupDate, b.ReturnDueDate, b.BookingNumber, b.Status })
            .ToListAsync(ct);

        return Ok(new { success = true, assetId = id, from, to, isAvailable = !bookings.Any(), bookings });
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  RENTAL BOOKINGS CONTROLLER
//  Route: /api/v1/rental/bookings
// ════════════════════════════════════════════════════════════════════════════
[Authorize]
[ApiController]
[Route("api/v1/rental/bookings")]
[EnableRateLimiting("api")]
public class RentalBookingController : OmniXBaseController
{
    private readonly OmniXDbContext _db;
    private readonly ITenantProvider _tenant;
    public RentalBookingController(OmniXDbContext db, ITenantProvider tenant)
    { _db = db; _tenant = tenant; }

    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();
    private Guid UserId   => Guid.TryParse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private bool IsManager => User.IsInRole("Manager") || User.IsInRole("CompanyOwner") || User.IsInRole("TenantAdmin") || User.IsInRole("PlatformOwner");
    private bool IsOwner   => User.IsInRole("CompanyOwner") || User.IsInRole("PlatformOwner");

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] RentalBookingStatus? status = null,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var q = _db.RentalBookings
            .Include(b => b.Customer)
            .Where(b => b.TenantId == TenantId && !b.IsDeleted);
        if (status.HasValue) q = q.Where(b => b.Status == status.Value);
        if (from.HasValue)   q = q.Where(b => b.PickupDate >= from.Value);
        if (to.HasValue)     q = q.Where(b => b.ReturnDueDate <= to.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(b => b.BookingDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new {
                b.Id, b.BookingNumber, b.Status, b.BookingType,
                CustomerName = b.Customer.Name, b.Customer.Phone,
                b.PickupDate, b.ReturnDueDate, b.TotalAmount, b.AmountPaid, b.IsOverdue
            }).ToListAsync(ct);

        return Ok(new { success = true, total, page, pageSize, items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var booking = await _db.RentalBookings
            .Include(b => b.Customer)
            .Include(b => b.Items).ThenInclude(i => i.Asset)
            .Include(b => b.Payments)
            .Include(b => b.DamageReports)
            .FirstOrDefaultAsync(b => b.TenantId == TenantId && b.Id == id && !b.IsDeleted, ct);
        return booking is null ? NotFound() : Ok(new { success = true, data = booking });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRentalBookingReq req, CancellationToken ct)
    {
        var cnt = await _db.RentalBookings.CountAsync(b => b.TenantId == TenantId, ct);
        var bookingNum = $"RNT-{DateTime.UtcNow:yyyyMMdd}-{cnt + 1:D4}";

        decimal subTotal = 0, deposit = 0;
        var items = new List<RentalBookingItem>();

        foreach (var ir in req.Items)
        {
            var asset = await _db.RentalAssets
                .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == ir.AssetId, ct);
            if (asset is null) return BadRequest(new { success = false, message = $"الأصل {ir.AssetId} غير موجود" });
            if (asset.Status != AssetStatus.Available)
                return BadRequest(new { success = false, message = $"الأصل {asset.Name} غير متاح" });

            var days       = (req.ReturnDueDate - req.PickupDate).Days;
            var unitPrice  = asset.RentalPricePerDay * days;
            var lineTotal  = unitPrice * ir.Quantity;
            subTotal      += lineTotal;
            deposit       += asset.DepositAmount * ir.Quantity;

            items.Add(new RentalBookingItem {
                AssetId = ir.AssetId, Quantity = ir.Quantity, UnitPrice = unitPrice,
                RentalDays = days, SubTotal = lineTotal,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });

            asset.Status = AssetStatus.Reserved; asset.UpdatedAt = DateTime.UtcNow;
        }

        var vatAmount = subTotal * 0.14m;
        var total     = subTotal + vatAmount;

        var booking = new RentalBooking {
            TenantId = TenantId, CustomerId = req.CustomerId,
            BookingNumber = bookingNum, Status = RentalBookingStatus.Pending,
            BookingType = req.BookingType ?? RentalBookingType.Rental,
            PickupDate = req.PickupDate, ReturnDueDate = req.ReturnDueDate,
            EventDate = req.EventDate, EventLocation = req.EventLocation,
            SubTotal = subTotal, VatAmount = vatAmount, TotalAmount = total,
            DepositRequired = deposit, HandledByUserId = UserId,
            Notes = req.Notes, IsOnlineBooking = false,
            BookingDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _db.RentalBookings.Add(booking);
        await _db.SaveChangesAsync(ct);

        foreach (var item in items)
        {
            item.BookingId = booking.Id;
            item.TenantId  = TenantId;
            _db.RentalBookingItems.Add(item);
        }
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = booking.Id },
            new { success = true, data = new { booking.Id, booking.BookingNumber, total, deposit } });
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        if (!IsManager) return Forbid();
        var b = await _db.RentalBookings.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (b is null) return NotFound();
        b.Status = RentalBookingStatus.Confirmed; b.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("{id:guid}/pickup")]
    public async Task<IActionResult> MarkPickedUp(Guid id, CancellationToken ct)
    {
        var b = await _db.RentalBookings
            .Include(x => x.Items).ThenInclude(i => i.Asset)
            .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (b is null) return NotFound();
        b.Status = RentalBookingStatus.PickedUp; b.UpdatedAt = DateTime.UtcNow;
        foreach (var item in b.Items)
        {
            if (item.Asset is not null) { item.Asset.Status = AssetStatus.Rented; item.Asset.UpdatedAt = DateTime.UtcNow; }
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("{id:guid}/return")]
    public async Task<IActionResult> MarkReturned(Guid id, CancellationToken ct)
    {
        var b = await _db.RentalBookings
            .Include(x => x.Items).ThenInclude(i => i.Asset)
            .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (b is null) return NotFound();

        var lateDays    = Math.Max(0, (DateTime.UtcNow.Date - b.ReturnDueDate.Date).Days);
        var penalty     = lateDays > 0
            ? b.Items.Sum(i => (i.Asset?.LatePenaltyPerDay ?? 0) * lateDays) : 0;

        b.Status = RentalBookingStatus.Returned;
        b.ActualReturnDate = DateTime.UtcNow;
        b.LateDays = lateDays; b.LatePenaltyTotal = penalty;
        b.UpdatedAt = DateTime.UtcNow;

        foreach (var item in b.Items)
        {
            item.IsReturned = true; item.ReturnedAt = DateTime.UtcNow;
            if (item.Asset is not null) { item.Asset.Status = AssetStatus.Available; item.Asset.TotalRentalCount++; item.Asset.UpdatedAt = DateTime.UtcNow; }
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true, lateDays, penalty });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (!IsManager) return Forbid();
        var b = await _db.RentalBookings
            .Include(x => x.Items).ThenInclude(i => i.Asset)
            .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (b is null) return NotFound();
        b.Status = RentalBookingStatus.Cancelled; b.UpdatedAt = DateTime.UtcNow;
        foreach (var item in b.Items.Where(i => i.Asset is not null))
        { item.Asset!.Status = AssetStatus.Available; item.Asset.UpdatedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("payment")]
    public async Task<IActionResult> AddPayment([FromBody] AddRentalPaymentReq req, CancellationToken ct)
    {
        var booking = await _db.RentalBookings
            .FirstOrDefaultAsync(b => b.TenantId == TenantId && b.Id == req.BookingId, ct);
        if (booking is null) return NotFound();

        _db.RentalPayments.Add(new RentalPayment {
            TenantId = TenantId, BookingId = req.BookingId,
            Purpose = req.Purpose, Method = req.Method,
            Amount = req.Amount, Reference = req.Reference,
            ReceivedBy = UserId, Notes = req.Notes,
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        booking.AmountPaid += req.Purpose == RentalPaymentPurpose.Deposit
            ? 0 : req.Amount;
        if (req.Purpose == RentalPaymentPurpose.Deposit)
            booking.DepositPaid += req.Amount;
        booking.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────
public record CreateRentalAssetReq(string Name, AssetCategory Category, string? SubCategory,
    string? Brand, string? Size, string? Color, string? Description,
    RentalPricingModel PricingModel, decimal RentalPricePerDay, decimal RentalPricePerHour,
    decimal SalePrice, decimal DepositAmount, decimal PurchaseCost,
    decimal LatePenaltyPerDay, int MaxRentalDays, string? Barcode,
    string? PrimaryImageUrl, bool IsListedOnMarketplace);

public record CreateRentalBookingReq(Guid CustomerId, RentalBookingType? BookingType,
    DateTime PickupDate, DateTime ReturnDueDate, DateTime? EventDate, string? EventLocation,
    List<RentalBookingItemReq> Items, string? Notes);

public record RentalBookingItemReq(Guid AssetId, int Quantity);

public record AddRentalPaymentReq(Guid BookingId, RentalPaymentPurpose Purpose,
    RentalPaymentMethod Method, decimal Amount, string? Reference, string? Notes);

public interface ITenantProvider
{
    Guid? TenantId { get; }
    void  SetTenantId(Guid tenantId);
}

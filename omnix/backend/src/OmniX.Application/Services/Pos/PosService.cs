using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniX.Domain.Entities.Core;
using OmniX.Domain.Enums;
using OmniX.Infrastructure.Caching;
using OmniX.Infrastructure.Data;
using OmniX.Application.Services.Auth;

namespace OmniX.Application.Services.Pos;

// ════════════════════════════════════════════════════════════════════════════
//  DTOs
// ════════════════════════════════════════════════════════════════════════════
public record ProductWithStockDto(Guid Id, string Name, string? NameAr, string? Barcode, string? SKU,
    decimal SalePrice, decimal CostPrice, bool HasVat, decimal VatRate, string? ImageUrl,
    ProductType Type, ProductUnit Unit, int LowStockThreshold, decimal StockQty,
    string? CategoryName, Guid? CategoryId);

public record SaleOrderDto(Guid Id, string OrderNumber, SaleStatus Status, DateTime CreatedAt,
    decimal SubTotal, decimal DiscountAmount, decimal VatAmount, decimal Total,
    decimal AmountPaid, decimal Change, PaymentMethod PaymentMethod,
    string? CustomerName, string? CashierName, string? BranchName,
    List<SaleOrderItemDto> Items, List<PaymentDto> Payments);

public record SaleOrderItemDto(Guid ProductId, string ProductName, decimal Quantity,
    decimal UnitPrice, decimal Discount, decimal VatAmount, decimal Total);

public record PaymentDto(PaymentMethod Method, decimal Amount, string? Reference, PaymentStatus Status);

public record CreateSaleRequest(Guid BranchId, Guid? CustomerId, PaymentMethod PaymentMethod,
    decimal AmountPaid, List<SaleItemRequest> Items, decimal? DiscountAmount,
    int? RedeemLoyaltyPoints, string? Notes, string? LocalId);

public record SaleItemRequest(Guid ProductId, decimal Quantity, decimal? UnitPrice, decimal? DiscountAmount);

public record RefundRequest(List<Guid> ItemIds, string? Reason);

public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize, int TotalPages);

public interface IPosService
{
    Task<ApiResponse<List<ProductWithStockDto>>>  GetProductsAsync(Guid branchId, string? search, CancellationToken ct = default);
    Task<ApiResponse<ProductWithStockDto>>        GetByBarcodeAsync(string barcode, Guid branchId, CancellationToken ct = default);
    Task<ApiResponse<SaleOrderDto>>               CreateSaleAsync(CreateSaleRequest req, CancellationToken ct = default);
    Task<ApiResponse<PagedResult<SaleOrderDto>>>  GetSalesAsync(Guid branchId, DateTime? from, DateTime? to, int page, int size, CancellationToken ct = default);
    Task<ApiResponse<SaleOrderDto>>               GetSaleByIdAsync(Guid saleId, CancellationToken ct = default);
    Task<ApiResponse<SaleOrderDto>>               RefundSaleAsync(Guid saleId, RefundRequest req, CancellationToken ct = default);
}

// ════════════════════════════════════════════════════════════════════════════
//  PosService — VAT 14% + Cache + Loyalty + Stock + Audit
// ════════════════════════════════════════════════════════════════════════════
public class PosService : IPosService
{
    private readonly OmniXDbContext      _db;
    private readonly IAuditService       _audit;
    private readonly ITenantProvider     _tenant;
    private readonly ICacheService       _cache;
    private readonly ICurrentUserService _me;
    private readonly ILogger<PosService> _log;
    private const decimal VAT_RATE = 0.14m;

    public PosService(OmniXDbContext db, IAuditService audit, ITenantProvider tenant,
        ICacheService cache, ICurrentUserService me, ILogger<PosService> log)
    { _db=db; _audit=audit; _tenant=tenant; _cache=cache; _me=me; _log=log; }

    // ── GET PRODUCTS (with Cache) ─────────────────────────────────────────
    public async Task<ApiResponse<List<ProductWithStockDto>>> GetProductsAsync(
        Guid branchId, string? search, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new UnauthorizedAccessException();

        if (search is null)
        {
            var cached = await _cache.GetAsync<List<ProductWithStockDto>>($"pos:products:{branchId}");
            if (cached is not null) return ApiResponse<List<ProductWithStockDto>>.Ok(cached);
        }

        var products = await _db.Products
            .Include(p => p.Category)
            .Where(p => p.TenantId == tenantId && p.IsActive && !p.IsDeleted &&
                (search == null || p.Name.Contains(search) || p.Barcode == search || p.SKU == search))
            .OrderBy(p => p.Name).ToListAsync(ct);

        var stocks = await _db.StockItems
            .Where(s => s.BranchId == branchId && !s.IsDeleted)
            .ToDictionaryAsync(s => s.ProductId, ct);

        var dtos = products.Select(p => {
            stocks.TryGetValue(p.Id, out var stock);
            return new ProductWithStockDto(p.Id, p.Name, p.NameAr, p.Barcode, p.SKU,
                p.SalePrice, p.CostPrice, p.HasVat, p.VatRate, p.ImageUrl,
                p.Type, p.Unit, p.LowStockThreshold, stock?.Quantity ?? 0,
                p.Category?.Name, p.CategoryId);
        }).ToList();

        if (search is null)
            await _cache.SetAsync($"pos:products:{branchId}", dtos, TimeSpan.FromMinutes(10));

        return ApiResponse<List<ProductWithStockDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<ProductWithStockDto>> GetByBarcodeAsync(
        string barcode, Guid branchId, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new UnauthorizedAccessException();
        var product  = await _db.Products.Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId
                && (p.Barcode == barcode || p.SKU == barcode)
                && p.IsActive && !p.IsDeleted, ct);

        if (product is null) return ApiResponse<ProductWithStockDto>.Fail("المنتج غير موجود");

        var stock = await _db.StockItems
            .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == branchId, ct);

        return ApiResponse<ProductWithStockDto>.Ok(new ProductWithStockDto(
            product.Id, product.Name, product.NameAr, product.Barcode, product.SKU,
            product.SalePrice, product.CostPrice, product.HasVat, product.VatRate,
            product.ImageUrl, product.Type, product.Unit, product.LowStockThreshold,
            stock?.Quantity ?? 0, product.Category?.Name, product.CategoryId));
    }

    // ── CREATE SALE ────────────────────────────────────────────────────────
    public async Task<ApiResponse<SaleOrderDto>> CreateSaleAsync(
        CreateSaleRequest req, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new UnauthorizedAccessException();
        var userId   = _me.UserId ?? throw new UnauthorizedAccessException();

        // Idempotency check
        if (req.LocalId is not null)
        {
            var existing = await _db.SaleOrders
                .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.LocalId == req.LocalId, ct);
            if (existing is not null) return ApiResponse<SaleOrderDto>.Ok(await ToSaleDto(existing, ct));
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            decimal subTotal = 0m, vatAmount = 0m;
            var items = new List<SaleOrderItem>();

            foreach (var ir in req.Items)
            {
                var product = await _db.Products
                    .FirstOrDefaultAsync(p => p.TenantId == tenantId
                        && p.Id == ir.ProductId && p.IsActive && !p.IsDeleted, ct);
                if (product is null) return ApiResponse<SaleOrderDto>.Fail($"المنتج {ir.ProductId} غير موجود");

                // Stock check
                if (product.TrackInventory)
                {
                    var stock = await _db.StockItems
                        .FirstOrDefaultAsync(s => s.ProductId == product.Id && s.BranchId == req.BranchId, ct);
                    if (stock is null || stock.Available < ir.Quantity)
                        return ApiResponse<SaleOrderDto>.Fail($"المخزون غير كافٍ: {product.Name}");
                }

                var unitPrice    = ir.UnitPrice ?? product.SalePrice;
                var itemDiscount = ir.DiscountAmount ?? 0m;
                var netPrice     = (unitPrice * ir.Quantity) - itemDiscount;
                var itemVat      = product.HasVat ? netPrice * product.VatRate : 0m;

                subTotal  += netPrice;
                vatAmount += itemVat;

                items.Add(new SaleOrderItem {
                    TenantId = tenantId, ProductId = product.Id,
                    Quantity = ir.Quantity, UnitPrice = unitPrice,
                    CostPrice = product.CostPrice, Discount = itemDiscount,
                    VatRate = product.VatRate, VatAmount = itemVat, Total = netPrice + itemVat,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                });
            }

            var discount = req.DiscountAmount ?? 0m;
            var total    = subTotal + vatAmount - discount;
            var change   = req.AmountPaid - total;

            // Order number
            var todayCnt = await _db.SaleOrders
                .CountAsync(o => o.TenantId == tenantId && o.BranchId == req.BranchId
                    && o.CreatedAt.Date == DateTime.UtcNow.Date, ct);

            var order = new SaleOrder {
                TenantId = tenantId, BranchId = req.BranchId, CustomerId = req.CustomerId,
                CashierId = userId,
                OrderNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{todayCnt + 1:D4}",
                Status = SaleStatus.Completed, SubTotal = subTotal,
                DiscountAmount = discount, VatAmount = vatAmount,
                Total = total, AmountPaid = req.AmountPaid, Change = Math.Max(0, change),
                PaymentMethod = req.PaymentMethod, Notes = req.Notes,
                LocalId = req.LocalId, IsSynced = req.LocalId is null,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _db.SaleOrders.Add(order);
            await _db.SaveChangesAsync(ct);

            // Items
            foreach (var item in items)
            {
                item.OrderId = order.Id;
                _db.SaleOrderItems.Add(item);
            }

            // Payment record
            _db.Payments.Add(new Payment {
                TenantId = tenantId, OrderId = order.Id,
                Method = req.PaymentMethod, Amount = req.AmountPaid,
                Status = PaymentStatus.Completed, PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync(ct);

            // Stock deduction
            foreach (var item in items)
            {
                var product = await _db.Products.FindAsync(new object[] { item.ProductId }, ct);
                if (product?.TrackInventory != true) continue;

                var stock = await _db.StockItems
                    .FirstOrDefaultAsync(s => s.ProductId == item.ProductId
                        && s.BranchId == req.BranchId, ct);
                if (stock is null) continue;

                var before = stock.Quantity;
                stock.Quantity -= item.Quantity;
                stock.UpdatedAt = DateTime.UtcNow;

                _db.StockMovements.Add(new StockMovement {
                    TenantId = tenantId, ProductId = item.ProductId, BranchId = req.BranchId,
                    ReferenceId = order.Id, MovementType = StockMovementType.Sale,
                    Quantity = -item.Quantity, UnitCost = item.CostPrice,
                    BalanceBefore = before, BalanceAfter = stock.Quantity,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                });
            }

            // Loyalty points
            if (req.CustomerId.HasValue)
            {
                var loyaltyPts = (int)Math.Floor(total);
                var customer = await _db.Customers.FindAsync(new object[] { req.CustomerId.Value }, ct);
                if (customer is not null)
                {
                    customer.LoyaltyPoints  += loyaltyPts;
                    customer.TotalPurchases += total;
                    customer.TotalOrders++;
                    customer.LastPurchaseAt = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // Invalidate product cache
            await _cache.RemoveAsync($"pos:products:{req.BranchId}");

            await _audit.LogAsync(tenantId, userId, AuditAction.Create, "SaleOrder",
                order.Id, ct: ct);

            _log.LogInformation("[POS] Sale {OrderNum} — Total: {Total} — Tenant: {TenantId}",
                order.OrderNumber, total, tenantId);

            return ApiResponse<SaleOrderDto>.Ok(await ToSaleDto(order, ct));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.LogError(ex, "[POS] Sale failed — Tenant: {TenantId}", tenantId);
            return ApiResponse<SaleOrderDto>.Fail("فشل إتمام عملية البيع. حاول مرة أخرى.");
        }
    }

    public async Task<ApiResponse<PagedResult<SaleOrderDto>>> GetSalesAsync(
        Guid branchId, DateTime? from, DateTime? to, int page, int size, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new UnauthorizedAccessException();
        var q = _db.SaleOrders.Where(o => o.TenantId == tenantId
            && o.BranchId == branchId && !o.IsDeleted);
        if (from.HasValue) q = q.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue)   q = q.Where(o => o.CreatedAt <= to.Value);

        var total  = await q.CountAsync(ct);
        var orders = await q.Include(o => o.Items).Include(o => o.Payments)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);

        var dtos = new List<SaleOrderDto>();
        foreach (var o in orders) dtos.Add(await ToSaleDto(o, ct));

        return ApiResponse<PagedResult<SaleOrderDto>>.Ok(
            new PagedResult<SaleOrderDto>(dtos, total, page, size, (int)Math.Ceiling((double)total / size)));
    }

    public async Task<ApiResponse<SaleOrderDto>> GetSaleByIdAsync(Guid saleId, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new UnauthorizedAccessException();
        var order = await _db.SaleOrders.Include(o => o.Items).Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == saleId, ct);
        return order is null
            ? ApiResponse<SaleOrderDto>.Fail("الفاتورة غير موجودة")
            : ApiResponse<SaleOrderDto>.Ok(await ToSaleDto(order, ct));
    }

    public async Task<ApiResponse<SaleOrderDto>> RefundSaleAsync(
        Guid saleId, RefundRequest req, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new UnauthorizedAccessException();
        var userId   = _me.UserId ?? throw new UnauthorizedAccessException();

        var order = await _db.SaleOrders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == saleId, ct);
        if (order is null) return ApiResponse<SaleOrderDto>.Fail("الفاتورة غير موجودة");
        if (order.Status == SaleStatus.Refunded) return ApiResponse<SaleOrderDto>.Fail("مُسترجعة بالفعل");

        var itemsToRefund = req.ItemIds?.Any() == true
            ? order.Items.Where(i => req.ItemIds.Contains(i.Id)).ToList()
            : order.Items.ToList();

        // Restore stock
        foreach (var item in itemsToRefund)
        {
            var stock = await _db.StockItems
                .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.BranchId == order.BranchId, ct);
            if (stock is null) continue;
            var before = stock.Quantity;
            stock.Quantity += item.Quantity;
            _db.StockMovements.Add(new StockMovement {
                TenantId = tenantId, ProductId = item.ProductId, BranchId = order.BranchId,
                ReferenceId = order.Id, MovementType = StockMovementType.Return,
                Quantity = item.Quantity, UnitCost = item.CostPrice,
                BalanceBefore = before, BalanceAfter = stock.Quantity,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
        }

        var isFullRefund = req.ItemIds?.Any() != true;
        order.Status = isFullRefund ? SaleStatus.Refunded : SaleStatus.PartialRefund;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(tenantId, userId, AuditAction.Update, "SaleOrder", saleId, ct: ct);

        return ApiResponse<SaleOrderDto>.Ok(await ToSaleDto(order, ct));
    }

    private async Task<SaleOrderDto> ToSaleDto(SaleOrder o, CancellationToken ct)
    {
        var cashier  = o.CashierId.HasValue ? await _db.Users.FindAsync(new object[] { o.CashierId.Value }, ct) : null;
        var customer = o.CustomerId.HasValue ? await _db.Customers.FindAsync(new object[] { o.CustomerId.Value }, ct) : null;
        var branch   = await _db.Branches.FindAsync(new object[] { o.BranchId }, ct);

        return new SaleOrderDto(o.Id, o.OrderNumber, o.Status, o.CreatedAt,
            o.SubTotal, o.DiscountAmount, o.VatAmount, o.Total,
            o.AmountPaid, o.Change, o.PaymentMethod,
            customer?.Name, cashier?.FullName, branch?.Name,
            o.Items.Select(i => new SaleOrderItemDto(
                i.ProductId, "", i.Quantity, i.UnitPrice, i.Discount, i.VatAmount, i.Total)).ToList(),
            o.Payments.Select(p => new PaymentDto(p.Method, p.Amount, p.Reference, p.Status)).ToList());
    }
}

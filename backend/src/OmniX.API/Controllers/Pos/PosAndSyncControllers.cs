using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OmniX.Application.Services.Pos;
using OmniX.Application.Services.Sync;

namespace OmniX.API.Controllers.Pos;

// ════════════════════════════════════════════════════════════════════════════
//  POS CONTROLLER
//  Route: /api/v1/pos
// ════════════════════════════════════════════════════════════════════════════
[Authorize]
[ApiController]
[Route("api/v1/pos")]
[EnableRateLimiting("api")]
public class PosController : OmniXBaseController
{
    private readonly IPosService _pos;
    public PosController(IPosService pos) => _pos = pos;

    /// <summary>قائمة المنتجات مع المخزون — مُخزَّنة Cache 10 دقائق</summary>
    [HttpGet("products")]
    public async Task<IActionResult> Products(
        [FromQuery] Guid branchId,
        [FromQuery] string? search,
        CancellationToken ct)
        => Ok(await _pos.GetProductsAsync(branchId, search, ct));

    /// <summary>بحث بالباركود</summary>
    [HttpGet("barcode/{barcode}")]
    public async Task<IActionResult> ByBarcode(
        string barcode,
        [FromQuery] Guid branchId,
        CancellationToken ct)
    {
        var res = await _pos.GetByBarcodeAsync(barcode, branchId, ct);
        return res.Success ? Ok(res) : NotFound(res);
    }

    /// <summary>إتمام عملية بيع — VAT 14% + Stock + Loyalty + Audit</summary>
    [HttpPost("sale")]
    [EnableRateLimiting("api")]
    public async Task<IActionResult> Sale(
        [FromBody] CreateSaleRequest req,
        CancellationToken ct)
    {
        var res = await _pos.CreateSaleAsync(req, ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }

    /// <summary>قائمة الفواتير مع Pagination</summary>
    [HttpGet("sales")]
    public async Task<IActionResult> Sales(
        [FromQuery] Guid branchId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken ct = default)
        => Ok(await _pos.GetSalesAsync(branchId, from, to, page, size, ct));

    /// <summary>تفاصيل فاتورة محددة</summary>
    [HttpGet("sales/{id:guid}")]
    public async Task<IActionResult> SaleById(Guid id, CancellationToken ct)
    {
        var res = await _pos.GetSaleByIdAsync(id, ct);
        return res.Success ? Ok(res) : NotFound(res);
    }

    /// <summary>استرجاع كامل أو جزئي</summary>
    [HttpPost("sales/{id:guid}/refund")]
    [Authorize(Policy = "Manager+")]
    public async Task<IActionResult> Refund(
        Guid id,
        [FromBody] RefundRequest req,
        CancellationToken ct)
    {
        var res = await _pos.RefundSaleAsync(id, req, ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  SYNC CONTROLLER (Offline-First — من Pro)
//  Route: /api/v1/sync
// ════════════════════════════════════════════════════════════════════════════
[Authorize]
[ApiController]
[Route("api/v1/sync")]
public class SyncController : OmniXBaseController
{
    private readonly ISyncService _sync;
    public SyncController(ISyncService sync) => _sync = sync;

    /// <summary>رفع البيانات المحلية إلى السيرفر (Offline → Cloud)</summary>
    [HttpPost("push")]
    public async Task<IActionResult> Push(
        [FromBody] SyncPushRequest req,
        CancellationToken ct)
    {
        var res = await _sync.PushAsync(req, ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }

    /// <summary>جلب التغييرات من السيرفر (Cloud → Client)</summary>
    [HttpGet("pull")]
    public async Task<IActionResult> Pull(
        [FromQuery] Guid branchId,
        [FromQuery] DateTime? since,
        CancellationToken ct)
    {
        var res = await _sync.PullAsync(branchId, since ?? DateTime.UtcNow.AddDays(-7), ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }
}

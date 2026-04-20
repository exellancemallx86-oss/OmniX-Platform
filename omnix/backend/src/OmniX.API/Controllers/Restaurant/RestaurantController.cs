using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniX.Domain.Entities.Restaurant;

namespace OmniX.API.Controllers.Restaurant;

public interface IRestaurantService
{
    Task<bool>   IsModuleEnabledAsync(Guid tenantId, CancellationToken ct);
    Task<object> GetCategoriesAsync(Guid tenantId, CancellationToken ct);
    Task<object> CreateCategoryAsync(Guid tenantId, CreateCategoryReq req, CancellationToken ct);
    Task<object> GetBranchMenuAsync(Guid tenantId, Guid branchId, Guid? categoryId, CancellationToken ct);
    Task<object> CreateMenuItemAsync(Guid tenantId, CreateMenuItemReq req, CancellationToken ct);
    Task<object> Set86Async(Guid tenantId, Guid branchId, Guid itemId, MenuItemAvailability status, string? reason, Guid userId, CancellationToken ct);
    Task<object> GetFloorPlansAsync(Guid tenantId, Guid branchId, CancellationToken ct);
    Task<object> GetTablesAsync(Guid tenantId, Guid branchId, Guid? floorPlanId, CancellationToken ct);
    Task<object> GenerateQRAsync(Guid tenantId, Guid tableId, CancellationToken ct);
    Task<object> GetStationsAsync(Guid tenantId, Guid branchId, CancellationToken ct);
    Task<object> GetActiveTicketsAsync(Guid tenantId, Guid stationId, CancellationToken ct);
    Task<object> UpdateTicketStatusAsync(Guid tenantId, Guid ticketId, KitchenTicketStatus status, CancellationToken ct);
    Task<object> OpenSessionAsync(Guid tenantId, Guid tableId, Guid branchId, int guestCount, Guid userId, CancellationToken ct);
    Task<object> GetActiveSessionAsync(Guid tenantId, Guid tableId, CancellationToken ct);
    Task<object> RequestBillAsync(Guid tenantId, Guid sessionId, Guid userId, CancellationToken ct);
    Task<object> CloseSessionAsync(Guid tenantId, Guid sessionId, Guid userId, CancellationToken ct);
    Task<object> GetActiveOrdersAsync(Guid tenantId, Guid branchId, CancellationToken ct);
    Task<object> CreateOrderAsync(Guid tenantId, CreateOrderReq req, Guid waiterId, CancellationToken ct);
    Task<object> CreateQROrderAsync(QROrderReq req, CancellationToken ct);
    Task<object> ConfirmQROrderAsync(Guid tenantId, Guid orderId, Guid userId, CancellationToken ct);
    Task<object> SendToKitchenAsync(Guid tenantId, Guid orderId, Guid userId, CancellationToken ct);
    Task<object> DeliverOrderAsync(Guid tenantId, Guid orderId, Guid userId, CancellationToken ct);
    Task<object> CancelOrderAsync(Guid tenantId, Guid orderId, Guid userId, string reason, CancellationToken ct);
    Task<object> GetReservationsAsync(Guid tenantId, Guid branchId, DateTime? date, CancellationToken ct);
    Task<object> CreateReservationAsync(Guid tenantId, CreateReservationReq req, CancellationToken ct);
    Task<object> ConfirmReservationAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct);
    Task<object> SeatReservationAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct);
}

// ── DTOs ──────────────────────────────────────────────────────────────────
public record CreateCategoryReq(string NameAr, string NameEn, string? ImageUrl, string? Color, int SortOrder);
public record CreateMenuItemReq(Guid CategoryId, string NameAr, string NameEn, decimal BasePrice, int PrepTimeMinutes, Guid? DefaultKitchenStationId);
public record Set86Req(MenuItemAvailability Status, string? Reason);
public record OpenSessionReq(Guid TableId, Guid BranchId, int GuestCount);
public record UpdateTicketReq(KitchenTicketStatus Status);
public record CreateOrderReq(Guid TableSessionId, Guid BranchId, string OrderSource, List<OrderItemReq> Items, string? Notes);
public record OrderItemReq(Guid MenuItemId, Guid? VariantId, int Quantity, List<Guid>? ModifierIds, string? SpecialInstructions);
public record QROrderReq(string TableToken, List<OrderItemReq> Items, string? GuestName, string? Notes);
public record CancelOrderReq(string Reason);
public record CreateReservationReq(Guid BranchId, Guid TableId, string GuestName, string? GuestPhone, int GuestCount, DateTime ReservationAt, int DurationMins, string? Notes);

// ════════════════════════════════════════════════════════════════════════════
//  Restaurant Controller — من Ultra Enterprise v6
// ════════════════════════════════════════════════════════════════════════════
[Authorize]
[ApiController]
[Route("api/v1/restaurant")]
public class RestaurantController : OmniXBaseController
{
    private readonly IRestaurantService _svc;
    public RestaurantController(IRestaurantService svc) => _svc = svc;

    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenantId")!);
    private Guid UserId   => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private Guid BranchId => Guid.TryParse(User.FindFirstValue("branchId"), out var b) ? b : Guid.Empty;

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
        => Ok(new { enabled = await _svc.IsModuleEnabledAsync(TenantId, ct) });

    // ── Menu ────────────────────────────────────────────────────────────
    [HttpGet("menu/categories")]
    public async Task<IActionResult> Categories(CancellationToken ct)
        => Ok(await _svc.GetCategoriesAsync(TenantId, ct));

    [HttpPost("menu/categories")]
    [Authorize(Policy = "Manager+")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryReq req, CancellationToken ct)
        => Ok(await _svc.CreateCategoryAsync(TenantId, req, ct));

    [HttpGet("menu/items")]
    public async Task<IActionResult> Menu([FromQuery] Guid? branchId, [FromQuery] Guid? categoryId, CancellationToken ct)
        => Ok(await _svc.GetBranchMenuAsync(TenantId, branchId ?? BranchId, categoryId, ct));

    [HttpPost("menu/items")]
    [Authorize(Policy = "Manager+")]
    public async Task<IActionResult> CreateItem([FromBody] CreateMenuItemReq req, CancellationToken ct)
        => Ok(await _svc.CreateMenuItemAsync(TenantId, req, ct));

    [HttpPost("menu/items/{id:guid}/86")]
    public async Task<IActionResult> Set86(Guid id, [FromBody] Set86Req req, [FromQuery] Guid? branchId, CancellationToken ct)
        => Ok(await _svc.Set86Async(TenantId, branchId ?? BranchId, id, req.Status, req.Reason, UserId, ct));

    // ── Floor Plans & Tables ────────────────────────────────────────────
    [HttpGet("floor-plans")]
    public async Task<IActionResult> FloorPlans([FromQuery] Guid? branchId, CancellationToken ct)
        => Ok(await _svc.GetFloorPlansAsync(TenantId, branchId ?? BranchId, ct));

    [HttpGet("tables")]
    public async Task<IActionResult> Tables([FromQuery] Guid? branchId, [FromQuery] Guid? floorPlanId, CancellationToken ct)
        => Ok(await _svc.GetTablesAsync(TenantId, branchId ?? BranchId, floorPlanId, ct));

    [HttpPost("tables/{id:guid}/generate-qr")]
    [Authorize(Policy = "Manager+")]
    public async Task<IActionResult> GenerateQR(Guid id, CancellationToken ct)
        => Ok(await _svc.GenerateQRAsync(TenantId, id, ct));

    // ── Kitchen ─────────────────────────────────────────────────────────
    [HttpGet("kitchen/stations")]
    public async Task<IActionResult> Stations([FromQuery] Guid? branchId, CancellationToken ct)
        => Ok(await _svc.GetStationsAsync(TenantId, branchId ?? BranchId, ct));

    [HttpGet("kitchen/stations/{stationId:guid}/tickets")]
    public async Task<IActionResult> Tickets(Guid stationId, CancellationToken ct)
        => Ok(await _svc.GetActiveTicketsAsync(TenantId, stationId, ct));

    [HttpPatch("kitchen/tickets/{ticketId:guid}/status")]
    public async Task<IActionResult> UpdateTicket(Guid ticketId, [FromBody] UpdateTicketReq req, CancellationToken ct)
        => Ok(await _svc.UpdateTicketStatusAsync(TenantId, ticketId, req.Status, ct));

    // ── Sessions ─────────────────────────────────────────────────────────
    [HttpPost("sessions")]
    public async Task<IActionResult> OpenSession([FromBody] OpenSessionReq req, CancellationToken ct)
        => Ok(await _svc.OpenSessionAsync(TenantId, req.TableId, req.BranchId, req.GuestCount, UserId, ct));

    [HttpGet("tables/{tableId:guid}/session")]
    public async Task<IActionResult> ActiveSession(Guid tableId, CancellationToken ct)
        => Ok(await _svc.GetActiveSessionAsync(TenantId, tableId, ct));

    [HttpPost("sessions/{id:guid}/bill")]
    public async Task<IActionResult> RequestBill(Guid id, CancellationToken ct)
        => Ok(await _svc.RequestBillAsync(TenantId, id, UserId, ct));

    [HttpPost("sessions/{id:guid}/close")]
    public async Task<IActionResult> CloseSession(Guid id, CancellationToken ct)
        => Ok(await _svc.CloseSessionAsync(TenantId, id, UserId, ct));

    // ── Orders ───────────────────────────────────────────────────────────
    [HttpGet("orders/active")]
    public async Task<IActionResult> ActiveOrders([FromQuery] Guid? branchId, CancellationToken ct)
        => Ok(await _svc.GetActiveOrdersAsync(TenantId, branchId ?? BranchId, ct));

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderReq req, CancellationToken ct)
        => Ok(await _svc.CreateOrderAsync(TenantId, req, UserId, ct));

    [HttpPost("orders/qr")]
    [AllowAnonymous]
    public async Task<IActionResult> QROrder([FromBody] QROrderReq req, CancellationToken ct)
        => Ok(await _svc.CreateQROrderAsync(req, ct));

    [HttpPost("orders/{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
        => Ok(await _svc.ConfirmQROrderAsync(TenantId, id, UserId, ct));

    [HttpPost("orders/{id:guid}/send-to-kitchen")]
    public async Task<IActionResult> SendToKitchen(Guid id, CancellationToken ct)
        => Ok(await _svc.SendToKitchenAsync(TenantId, id, UserId, ct));

    [HttpPost("orders/{id:guid}/deliver")]
    public async Task<IActionResult> Deliver(Guid id, CancellationToken ct)
        => Ok(await _svc.DeliverOrderAsync(TenantId, id, UserId, ct));

    [HttpPost("orders/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelOrderReq req, CancellationToken ct)
        => Ok(await _svc.CancelOrderAsync(TenantId, id, UserId, req.Reason, ct));

    // ── Reservations ─────────────────────────────────────────────────────
    [HttpGet("reservations")]
    public async Task<IActionResult> Reservations([FromQuery] Guid? branchId, [FromQuery] DateTime? date, CancellationToken ct)
        => Ok(await _svc.GetReservationsAsync(TenantId, branchId ?? BranchId, date, ct));

    [HttpPost("reservations")]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationReq req, CancellationToken ct)
        => Ok(await _svc.CreateReservationAsync(TenantId, req, ct));

    [HttpPatch("reservations/{id:guid}/confirm")]
    public async Task<IActionResult> ConfirmReservation(Guid id, CancellationToken ct)
        => Ok(await _svc.ConfirmReservationAsync(TenantId, id, UserId, ct));

    [HttpPatch("reservations/{id:guid}/seat")]
    public async Task<IActionResult> SeatReservation(Guid id, CancellationToken ct)
        => Ok(await _svc.SeatReservationAsync(TenantId, id, UserId, ct));
}

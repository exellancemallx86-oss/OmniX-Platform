using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OmniX.API.Hubs;

// ════════════════════════════════════════════════════════════════════════════
//  RestaurantHub — من Ultra Enterprise v6
//  Kitchen Stations + Floor Plan + Table Sessions + QR Orders
// ════════════════════════════════════════════════════════════════════════════
[Authorize]
public class RestaurantHub : Hub
{
    private readonly ILogger<RestaurantHub> _logger;
    public RestaurantHub(ILogger<RestaurantHub> logger) => _logger = logger;

    public async Task JoinKitchenStation(string tenantId, string branchId, string stationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"kitchen-{tenantId}-{branchId}-{stationId}");
    }

    public async Task LeaveKitchenStation(string tenantId, string branchId, string stationId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"kitchen-{tenantId}-{branchId}-{stationId}");

    public async Task JoinFloorPlan(string tenantId, string branchId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"floor-{tenantId}-{branchId}");

    public async Task JoinTableSession(string sessionId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"session-{sessionId}");

    public async Task LeaveTableSession(string sessionId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session-{sessionId}");

    public async Task JoinTableQR(string tableToken)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"qr-{tableToken}");

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Restaurant Hub: {User} connected", Context.UserIdentifier);
        await base.OnConnectedAsync();
    }
}

// ─── IRestaurantNotifier ───────────────────────────────────────────────────
public interface IRestaurantNotifier
{
    Task NotifyNewTicketAsync(string tenantId, string branchId, string stationId, object ticket);
    Task NotifyTicketStatusAsync(string tenantId, string branchId, string stationId, object update);
    Task NotifyTableStatusAsync(string tenantId, string branchId, object tableStatus);
    Task NotifyOrderToQRTableAsync(string tableToken, object order);
    Task NotifySessionBillAsync(string sessionId, object billSummary);
}

public class SignalRRestaurantNotifier : IRestaurantNotifier
{
    private readonly IHubContext<RestaurantHub> _hub;
    public SignalRRestaurantNotifier(IHubContext<RestaurantHub> hub) => _hub = hub;

    public Task NotifyNewTicketAsync(string t, string b, string s, object ticket)
        => _hub.Clients.Group($"kitchen-{t}-{b}-{s}").SendAsync("NewKitchenTicket", ticket);

    public Task NotifyTicketStatusAsync(string t, string b, string s, object update)
        => _hub.Clients.Group($"kitchen-{t}-{b}-{s}").SendAsync("TicketStatusChanged", update);

    public Task NotifyTableStatusAsync(string t, string b, object status)
        => _hub.Clients.Group($"floor-{t}-{b}").SendAsync("TableStatusChanged", status);

    public Task NotifyOrderToQRTableAsync(string token, object order)
        => _hub.Clients.Group($"qr-{token}").SendAsync("OrderStatusUpdated", order);

    public Task NotifySessionBillAsync(string sessionId, object bill)
        => _hub.Clients.Group($"session-{sessionId}").SendAsync("BillReady", bill);
}

// ════════════════════════════════════════════════════════════════════════════
//  MallOrderHub — من MallX SAAS
//  B2C Order Tracking + Driver GPS
// ════════════════════════════════════════════════════════════════════════════
public class MallOrderHub : Hub
{
    private readonly ILogger<MallOrderHub> _logger;
    public MallOrderHub(ILogger<MallOrderHub> logger) => _logger = logger;

    public async Task TrackOrder(string orderId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"order-{orderId}");

    public async Task StopTrackingOrder(string orderId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order-{orderId}");

    public async Task UpdateDriverLocation(string driverId, double lat, double lng)
        => await Clients.Group($"driver-{driverId}").SendAsync("DriverLocationUpdated",
            new { driverId, lat, lng, timestamp = DateTime.UtcNow });

    public async Task JoinStore(string storeId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"store-{storeId}");

    public async Task JoinMallAdmin(string mallId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"mall-admin-{mallId}");
}

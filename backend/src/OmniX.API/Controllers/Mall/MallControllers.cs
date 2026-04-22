using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OmniX.Application.Services.Auth;
using OmniX.Domain.Entities.Mall;
using OmniX.Domain.Enums;
using OmniX.Infrastructure.Data;

namespace OmniX.API.Controllers.Mall;

// ════════════════════════════════════════════════════════════════════════════
//  MALL CUSTOMER AUTH
//  Route: /api/v1/mall/auth
//  مستقل عن B2B Auth — يستخدم MallCustomer وليس ApplicationUser
// ════════════════════════════════════════════════════════════════════════════
[ApiController]
[Route("api/v1/mall/auth")]
public class MallCustomerAuthController : OmniXBaseController
{
    private readonly IMallAuthService _auth;
    public MallCustomerAuthController(IMallAuthService auth) => _auth = auth;

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] CustomerRegisterReq req, CancellationToken ct)
    {
        var res = await _auth.RegisterAsync(req, ClientIp, ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] CustomerLoginReq req, CancellationToken ct)
    {
        var res = await _auth.LoginAsync(req, ClientIp, ClientAgent, ct);
        return res.Success ? Ok(res) : Unauthorized(res);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshCustomerReq req, CancellationToken ct)
    {
        var res = await _auth.RefreshAsync(req.Token, ClientIp, ct);
        return res.Success ? Ok(res) : Unauthorized(res);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshCustomerReq req, CancellationToken ct)
    {
        await _auth.LogoutAsync(req.Token, ct);
        return Ok(new { success = true, message = "تم تسجيل الخروج" });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var customerId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
        var res = await _auth.GetProfileAsync(customerId, ct);
        return res.Success ? Ok(res) : NotFound(res);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  CART CONTROLLER
//  Route: /api/v1/mall/cart
// ════════════════════════════════════════════════════════════════════════════
[Authorize]
[ApiController]
[Route("api/v1/mall/cart")]
public class CartController : OmniXBaseController
{
    private readonly ICartService _cart;
    public CartController(ICartService cart) => _cart = cart;

    private Guid CustomerId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private Guid MallId     => Guid.TryParse(User.FindFirstValue("mallId"), out var id) ? id : Guid.Empty;

    [HttpGet]
    public async Task<IActionResult> GetCart(CancellationToken ct)
        => Ok(await _cart.GetCartAsync(CustomerId, ct));

    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddToCartReq req, CancellationToken ct)
    {
        var res = await _cart.AddItemAsync(CustomerId, MallId, req, ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }

    [HttpPut("items")]
    public async Task<IActionResult> UpdateItem([FromBody] UpdateCartItemReq req, CancellationToken ct)
    {
        var res = await _cart.UpdateItemAsync(CustomerId, req, ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }

    [HttpDelete("items/{productId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid productId, CancellationToken ct)
        => Ok(await _cart.RemoveItemAsync(CustomerId, productId, ct));

    [HttpDelete]
    public async Task<IActionResult> ClearCart(CancellationToken ct)
    {
        await _cart.ClearCartAsync(CustomerId, ct);
        return Ok(new { success = true });
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  MALL ORDER CONTROLLER (Customer)
//  Route: /api/v1/mall/orders
// ════════════════════════════════════════════════════════════════════════════
[Authorize]
[ApiController]
[Route("api/v1/mall/orders")]
public class MallOrderController : OmniXBaseController
{
    private readonly IMallOrderService _orders;
    public MallOrderController(IMallOrderService orders) => _orders = orders;

    private Guid CustomerId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    [HttpPost("checkout")]
    [EnableRateLimiting("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutReq req, CancellationToken ct)
    {
        var res = await _orders.CheckoutAsync(CustomerId, req, ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }

    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetOrder(Guid orderId, CancellationToken ct)
    {
        var res = await _orders.GetOrderAsync(CustomerId, orderId, ct);
        return res.Success ? Ok(res) : NotFound(res);
    }

    [HttpGet]
    public async Task<IActionResult> History(
        [FromQuery] int page = 1, [FromQuery] int size = 10, CancellationToken ct = default)
        => Ok(await _orders.GetHistoryAsync(CustomerId, page, size, ct));
}

// ════════════════════════════════════════════════════════════════════════════
//  MALL STORE CONTROLLER (Store Owner Dashboard)
//  Route: /api/v1/mall/store
// ════════════════════════════════════════════════════════════════════════════
[Authorize]
[ApiController]
[Route("api/v1/mall/store")]
public class StoreOwnerController : OmniXBaseController
{
    private readonly IMallOrderService _orders;
    public StoreOwnerController(IMallOrderService orders) => _orders = orders;

    private Guid TenantId => Guid.TryParse(User.FindFirstValue("tenantId"), out var id) ? id : Guid.Empty;

    [HttpGet("orders/incoming")]
    public async Task<IActionResult> Incoming(CancellationToken ct)
        => Ok(await _orders.GetIncomingAsync(TenantId, ct));

    [HttpPatch("orders/{itemId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid itemId, [FromBody] UpdateStoreOrderReq req, CancellationToken ct)
    {
        var res = await _orders.UpdateStoreOrderStatusAsync(TenantId, itemId, req, ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  MALL ADMIN CONTROLLER
//  Route: /api/v1/mall/admin
// ════════════════════════════════════════════════════════════════════════════
[Authorize(Policy = "Admin+")]
[ApiController]
[Route("api/v1/mall/admin")]
public class MallAdminController : OmniXBaseController
{
    private readonly IMallOrderService _orders;
    public MallAdminController(IMallOrderService orders) => _orders = orders;

    private Guid MallId => Guid.TryParse(User.FindFirstValue("mallId"), out var id) ? id : Guid.Empty;

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] Guid? mallId, CancellationToken ct)
        => Ok(await _orders.GetAdminDashboardAsync(mallId ?? MallId, ct));

    [HttpGet("orders")]
    public async Task<IActionResult> Orders(
        [FromQuery] Guid? mallId, [FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
        => Ok(await _orders.GetAllOrdersAsync(mallId ?? MallId, page, size, ct));
}

// ════════════════════════════════════════════════════════════════════════════
//  MALL BROWSE (Public — لا يحتاج Auth)
//  Route: /api/v1/mall
// ════════════════════════════════════════════════════════════════════════════
[ApiController]
[Route("api/v1/mall")]
public class MallBrowseController : OmniXBaseController
{
    private readonly OmniXDbContext _db;
    public MallBrowseController(OmniXDbContext db) => _db = db;

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetMall(string slug, CancellationToken ct)
    {
        var mall = await _db.Malls
            .Where(m => m.Slug == slug && m.IsActive)
            .Select(m => new { m.Id, m.Name, m.NameAr, m.Slug, m.LogoUrl, m.CoverUrl, m.Address, m.Phone })
            .FirstOrDefaultAsync(ct);
        return mall is null ? NotFound() : Ok(mall);
    }

    [HttpGet("{slug}/stores")]
    public async Task<IActionResult> GetStores(string slug, CancellationToken ct)
    {
        var mall = await _db.Malls.FirstOrDefaultAsync(m => m.Slug == slug && m.IsActive, ct);
        if (mall is null) return NotFound();

        var stores = await _db.MallStores
            .Where(s => s.MallId == mall.Id && s.IsActive)
            .Select(s => new { s.Id, s.Name, s.NameAr, s.Slug, s.LogoUrl, s.CoverUrl, s.AcceptsDelivery })
            .ToListAsync(ct);
        return Ok(stores);
    }

    [HttpGet("stores/{storeSlug}/products")]
    public async Task<IActionResult> GetProducts(
        string storeSlug, [FromQuery] string? search, CancellationToken ct)
    {
        var store = await _db.MallStores.FirstOrDefaultAsync(s => s.Slug == storeSlug && s.IsActive, ct);
        if (store is null) return NotFound();

        var q = _db.MallProducts.Where(p => p.StoreId == store.Id && p.IsActive);
        if (!string.IsNullOrEmpty(search)) q = q.Where(p => p.Name.Contains(search));

        var products = await q.OrderBy(p => p.SortOrder)
            .Select(p => new { p.Id, p.Name, p.NameAr, p.Price, p.DiscountedPrice, p.ImageUrl, p.IsFeatured })
            .ToListAsync(ct);
        return Ok(products);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  DRIVER LOCATION — Real-time GPS
//  Route: /api/v1/mall/drivers
// ════════════════════════════════════════════════════════════════════════════
[Authorize]
[ApiController]
[Route("api/v1/mall/drivers")]
public class DriverController : OmniXBaseController
{
    private readonly OmniXDbContext _db;
    public DriverController(OmniXDbContext db) => _db = db;

    [HttpPatch("{driverId:guid}/location")]
    public async Task<IActionResult> UpdateLocation(
        Guid driverId, [FromBody] UpdateLocationReq req, CancellationToken ct)
    {
        var driver = await _db.Drivers.FindAsync(new object[] { driverId }, ct);
        if (driver is null) return NotFound();
        driver.CurrentLat = req.Lat; driver.CurrentLng = req.Lng;
        driver.LastLocationAt = DateTime.UtcNow; driver.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPatch("{driverId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid driverId, [FromBody] UpdateDriverStatusReq req, CancellationToken ct)
    {
        var driver = await _db.Drivers.FindAsync(new object[] { driverId }, ct);
        if (driver is null) return NotFound();
        driver.Status = req.Status; driver.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────
public record CustomerRegisterReq(Guid MallId, string FirstName, string LastName, string Email, string Password, string? Phone);
public record CustomerLoginReq(string Email, string Password);
public record RefreshCustomerReq(string Token);
public record AddToCartReq(Guid ProductId, Guid StoreId, int Quantity, string? Notes);
public record UpdateCartItemReq(Guid ProductId, int Quantity);
public record CheckoutReq(Guid MallId, string FulfillmentType, Guid? AddressId, string? Notes, bool UseWallet, int? LoyaltyPointsToRedeem, string? PaymentMethod);
public record UpdateStoreOrderReq(string Status, string? Notes);
public record UpdateLocationReq(decimal Lat, decimal Lng);
public record UpdateDriverStatusReq(DriverStatus Status);

// ── Service Interfaces ────────────────────────────────────────────────────
public interface IMallAuthService
{
    Task<ApiResponse<object>> RegisterAsync(CustomerRegisterReq req, string ip, CancellationToken ct);
    Task<ApiResponse<object>> LoginAsync(CustomerLoginReq req, string ip, string ua, CancellationToken ct);
    Task<ApiResponse<object>> RefreshAsync(string token, string ip, CancellationToken ct);
    Task LogoutAsync(string token, CancellationToken ct);
    Task<ApiResponse<object>> GetProfileAsync(Guid customerId, CancellationToken ct);
}

public interface ICartService
{
    Task<object> GetCartAsync(Guid customerId, CancellationToken ct);
    Task<ApiResponse<object>> AddItemAsync(Guid customerId, Guid mallId, AddToCartReq req, CancellationToken ct);
    Task<ApiResponse<object>> UpdateItemAsync(Guid customerId, UpdateCartItemReq req, CancellationToken ct);
    Task<object> RemoveItemAsync(Guid customerId, Guid productId, CancellationToken ct);
    Task ClearCartAsync(Guid customerId, CancellationToken ct);
}

public interface IMallOrderService
{
    Task<ApiResponse<object>> CheckoutAsync(Guid customerId, CheckoutReq req, CancellationToken ct);
    Task<ApiResponse<object>> GetOrderAsync(Guid customerId, Guid orderId, CancellationToken ct);
    Task<object> GetHistoryAsync(Guid customerId, int page, int size, CancellationToken ct);
    Task<object> GetIncomingAsync(Guid tenantId, CancellationToken ct);
    Task<ApiResponse<object>> UpdateStoreOrderStatusAsync(Guid tenantId, Guid itemId, UpdateStoreOrderReq req, CancellationToken ct);
    Task<object> GetAdminDashboardAsync(Guid mallId, CancellationToken ct);
    Task<object> GetAllOrdersAsync(Guid mallId, int page, int size, CancellationToken ct);
}

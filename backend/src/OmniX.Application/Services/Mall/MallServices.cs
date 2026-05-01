using OmniX.Application.Services.WhatsApp;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OmniX.API.Controllers.Mall;
using OmniX.Application.Services.Auth;
using OmniX.Domain.Entities.Mall;
using OmniX.Infrastructure.Data;

namespace OmniX.Application.Services.Mall;

// ════════════════════════════════════════════════════════════════════════════
//  MallCustomerAuthService — B2C Auth مستقل (من MallX)
//  MallCustomer != ApplicationUser — namespace فصل كامل
// ════════════════════════════════════════════════════════════════════════════
public class MallCustomerAuthService : IMallAuthService
{
    private readonly OmniXDbContext  _db;
    private readonly IConfiguration  _cfg;
    private readonly ILogger<MallCustomerAuthService> _log;
    private const int MAX_FAIL = 5, LOCK_MIN = 15, JWT_MIN = 60, REF_DAYS = 30;

    public MallCustomerAuthService(OmniXDbContext db, IConfiguration cfg,
        ILogger<MallCustomerAuthService> log)
    { _db = db; _cfg = cfg; _log = log; }

    public async Task<ApiResponse<object>> RegisterAsync(
        CustomerRegisterReq req, string ip, CancellationToken ct)
    {
        if (await _db.MallCustomers.AnyAsync(c => c.Email == req.Email && !c.IsDeleted, ct))
            return ApiResponse<object>.Fail("البريد الإلكتروني مستخدم بالفعل");

        var (ok, err) = SecurityHelper.ValidatePasswordPolicy(req.Password);
        if (!ok) return ApiResponse<object>.Fail(err!);

        var customer = new MallCustomer {
            MallId = req.MallId, FirstName = req.FirstName, LastName = req.LastName,
            Email = req.Email, Phone = req.Phone,
            PasswordHash = SecurityHelper.HashPassword(req.Password),
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _db.MallCustomers.Add(customer);

        // Create empty cart
        _db.Carts.Add(new Cart {
            CustomerId = customer.Id, UpdatedAt = DateTime.UtcNow,
        });

        // Create loyalty account
        _db.LoyaltyAccounts.Add(new LoyaltyAccount {
            CustomerId = customer.Id, MallId = req.MallId,
            LifetimePoints = 50, // signup bonus
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });

        // Create wallet
        _db.Wallets.Add(new Wallet {
            CustomerId = customer.Id, MallId = req.MallId,
            Balance = 0, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);

        var jwt = BuildCustomerJwt(customer);
        var (raw, hash, salt) = SecurityHelper.GenerateRefreshToken();
        _db.CustomerRefreshTokens.Add(new CustomerRefreshToken {
            CustomerId = customer.Id, TokenHash = hash, Salt = salt,
            ExpiresAt = DateTime.UtcNow.AddDays(REF_DAYS), CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        return ApiResponse<object>.Ok(new {
            accessToken = jwt, refreshToken = raw, expiresIn = JWT_MIN * 60,
            customer = ToCustomerDto(customer)
        });
    }

    public async Task<ApiResponse<object>> LoginAsync(
        CustomerLoginReq req, string ip, string ua, CancellationToken ct)
    {
        const string BAD = "بيانات الدخول غير صحيحة";
        var customer = await _db.MallCustomers
            .FirstOrDefaultAsync(c => c.Email == req.Email && !c.IsDeleted, ct);

        if (customer is null) return ApiResponse<object>.Fail(BAD);
        if (customer.IsLocked)
        {
            var min = (int)Math.Ceiling((customer.LockoutEnd!.Value - DateTime.UtcNow).TotalMinutes);
            return ApiResponse<object>.Fail($"الحساب مقفول. حاول بعد {min} دقيقة");
        }
        if (!SecurityHelper.VerifyPassword(req.Password, customer.PasswordHash))
        {
            customer.FailedAttempts++;
            if (customer.FailedAttempts >= MAX_FAIL)
                customer.LockoutEnd = DateTime.UtcNow.AddMinutes(LOCK_MIN);
            await _db.SaveChangesAsync(ct);
            return ApiResponse<object>.Fail(BAD);
        }
        if (!customer.IsActive) return ApiResponse<object>.Fail("الحساب معطّل");

        customer.FailedAttempts = 0; customer.LockoutEnd = null;
        customer.LastActivityAt = DateTime.UtcNow; customer.UpdatedAt = DateTime.UtcNow;

        var jwt = BuildCustomerJwt(customer);
        var (raw, hash, salt) = SecurityHelper.GenerateRefreshToken();
        _db.CustomerRefreshTokens.Add(new CustomerRefreshToken {
            CustomerId = customer.Id, TokenHash = hash, Salt = salt,
            DeviceInfo = ua?[..Math.Min(ua?.Length ?? 0, 250)],
            ExpiresAt = DateTime.UtcNow.AddDays(REF_DAYS), CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        return ApiResponse<object>.Ok(new {
            accessToken = jwt, refreshToken = raw, expiresIn = JWT_MIN * 60,
            customer = ToCustomerDto(customer)
        });
    }

    public async Task<ApiResponse<object>> RefreshAsync(string token, string ip, CancellationToken ct)
    {
        var all = await _db.CustomerRefreshTokens
            .Include(r => r.Customer)
            .Where(r => !r.IsRevoked && r.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

        var rt = all.FirstOrDefault(r => SecurityHelper.VerifyRefreshToken(token, r.TokenHash, r.Salt));
        if (rt is null) return ApiResponse<object>.Fail("Refresh token غير صالح");

        rt.IsRevoked = true;
        var (raw, hash, salt) = SecurityHelper.GenerateRefreshToken();
        _db.CustomerRefreshTokens.Add(new CustomerRefreshToken {
            CustomerId = rt.CustomerId, TokenHash = hash, Salt = salt,
            ExpiresAt = DateTime.UtcNow.AddDays(REF_DAYS), CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        return ApiResponse<object>.Ok(new {
            accessToken  = BuildCustomerJwt(rt.Customer),
            refreshToken = raw, expiresIn = JWT_MIN * 60
        });
    }

    public async Task LogoutAsync(string token, CancellationToken ct)
    {
        var all = await _db.CustomerRefreshTokens.Where(r => !r.IsRevoked).ToListAsync(ct);
        var rt  = all.FirstOrDefault(r => SecurityHelper.VerifyRefreshToken(token, r.TokenHash, r.Salt));
        if (rt is not null) { rt.IsRevoked = true; await _db.SaveChangesAsync(ct); }
    }

    public async Task<ApiResponse<object>> GetProfileAsync(Guid customerId, CancellationToken ct)
    {
        var customer = await _db.MallCustomers
            .FirstOrDefaultAsync(c => c.Id == customerId && !c.IsDeleted, ct);
        if (customer is null) return ApiResponse<object>.Fail("العميل غير موجود");

        var loyalty = await _db.LoyaltyAccounts
            .FirstOrDefaultAsync(l => l.CustomerId == customerId, ct);
        var wallet  = await _db.Wallets
            .FirstOrDefaultAsync(w => w.CustomerId == customerId, ct);

        return ApiResponse<object>.Ok(new {
            customer = ToCustomerDto(customer),
            loyaltyPoints = loyalty?.AvailablePoints ?? 0,
            walletBalance = wallet?.Balance ?? 0m,
            tier = loyalty?.Tier ?? "Bronze"
        });
    }

    private string BuildCustomerJwt(MallCustomer c)
    {
        var secret = _cfg["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret مفقود");
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[] {
            new Claim(JwtRegisteredClaimNames.Sub,   c.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, c.Email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim("mallId",     c.MallId.ToString()),
            new Claim("token_type", "customer"),
            new Claim("firstName",  c.FirstName),
        };
        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(_cfg["Jwt:Issuer"] ?? "OmniX", _cfg["Jwt:Audience"] ?? "OmniXClient",
                claims, expires: DateTime.UtcNow.AddMinutes(JWT_MIN), signingCredentials: creds));
    }

    private static object ToCustomerDto(MallCustomer c) => new {
        c.Id, c.FirstName, c.LastName, c.FullName,
        c.Email, c.Phone, c.AvatarUrl, c.Tier, c.LoyaltyPoints, c.LastActivityAt,
    };
}

// ════════════════════════════════════════════════════════════════════════════
//  CartService — من MallX
// ════════════════════════════════════════════════════════════════════════════
public class CartService : ICartService
{
    private readonly OmniXDbContext _db;
    public CartService(OmniXDbContext db) => _db = db;

    public async Task<object> GetCartAsync(Guid customerId, CancellationToken ct)
    {
        var cart = await _db.Carts
            .Include(c => c.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Store)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);

        if (cart is null) return new { items = Array.Empty<object>(), total = 0m, itemCount = 0 };

        var items = cart.Items.Select(i => new {
            i.Id, i.ProductId,
            productName   = i.Product.Name,
            storeId       = i.Product.StoreId,
            storeName     = i.Product.Store.Name,
            i.Quantity,
            unitPrice     = i.Product.DiscountedPrice ?? i.Product.Price,
            lineTotal     = i.Quantity * (i.Product.DiscountedPrice ?? i.Product.Price),
            imageUrl      = i.Product.ImageUrl,
            i.Notes
        }).ToList();

        var total = items.Sum(i => (decimal)i.lineTotal);
        return new { success = true, data = new { items, total, itemCount = items.Count } };
    }

    public async Task<ApiResponse<object>> AddItemAsync(
        Guid customerId, Guid mallId, AddToCartReq req, CancellationToken ct)
    {
        var product = await _db.MallProducts
            .Include(p => p.Store)
            .FirstOrDefaultAsync(p => p.Id == req.ProductId && p.IsActive, ct);
        if (product is null) return ApiResponse<object>.Fail("المنتج غير موجود");
        if (!product.Store.IsActive) return ApiResponse<object>.Fail("المتجر غير متاح");

        var cart = await _db.Carts.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);

        if (cart is null)
        {
            cart = new Cart { CustomerId = customerId, UpdatedAt = DateTime.UtcNow };
            _db.Carts.Add(cart);
            await _db.SaveChangesAsync(ct);
        }

        var existing = cart.Items.FirstOrDefault(i => i.ProductId == req.ProductId);
        if (existing is not null)
        {
            existing.Quantity += req.Quantity;
            existing.Notes     = req.Notes ?? existing.Notes;
        }
        else
        {
            _db.CartItems.Add(new CartItem {
                CartId = cart.Id, ProductId = req.ProductId, StoreId = req.StoreId,
                Quantity = req.Quantity, Notes = req.Notes, AddedAt = DateTime.UtcNow,
            });
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { success = true });
    }

    public async Task<ApiResponse<object>> UpdateItemAsync(
        Guid customerId, UpdateCartItemReq req, CancellationToken ct)
    {
        var cart = await _db.Carts.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
        if (cart is null) return ApiResponse<object>.Fail("السلة فارغة");

        var item = cart.Items.FirstOrDefault(i => i.ProductId == req.ProductId);
        if (item is null) return ApiResponse<object>.Fail("المنتج غير موجود في السلة");

        if (req.Quantity <= 0)
            _db.CartItems.Remove(item);
        else
            item.Quantity = req.Quantity;

        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { success = true });
    }

    public async Task<object> RemoveItemAsync(Guid customerId, Guid productId, CancellationToken ct)
    {
        var item = await _db.CartItems
            .Where(i => i.Cart.CustomerId == customerId && i.ProductId == productId)
            .FirstOrDefaultAsync(ct);
        if (item is not null) { _db.CartItems.Remove(item); await _db.SaveChangesAsync(ct); }
        return new { success = true };
    }

    public async Task ClearCartAsync(Guid customerId, CancellationToken ct)
    {
        var items = await _db.CartItems
            .Where(i => i.Cart.CustomerId == customerId).ToListAsync(ct);
        _db.CartItems.RemoveRange(items);
        await _db.SaveChangesAsync(ct);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  MallOrderService — Checkout + Order Management (من MallX)
// ════════════════════════════════════════════════════════════════════════════
public class MallOrderService : IMallOrderService
{
    private readonly OmniXDbContext _db;
    private readonly ILogger<MallOrderService> _log;

    public MallOrderService(OmniXDbContext db, ILogger<MallOrderService> log)
    { _db = db; _log = log; }

    public async Task<ApiResponse<object>> CheckoutAsync(
        Guid customerId, CheckoutReq req, CancellationToken ct)
    {
        var cart = await _db.Carts
            .Include(c => c.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Store)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);

        if (cart is null || !cart.Items.Any())
            return ApiResponse<object>.Fail("السلة فارغة");

        decimal subTotal = cart.Items.Sum(i =>
            i.Quantity * (i.Product.DiscountedPrice ?? i.Product.Price));
        decimal deliveryFee = req.FulfillmentType == "Delivery" ? 15m : 0m;
        decimal total       = subTotal + deliveryFee;

        var cnt = await _db.MallOrders.CountAsync(o => o.MallId == req.MallId, ct);
        var orderNum = $"MLX-{DateTime.UtcNow:yyyyMMdd}-{cnt + 1:D5}";

        var order = new MallOrder {
            MallId = req.MallId, CustomerId = customerId,
            OrderNumber = orderNum, Status = MallOrderStatus.Pending,
            FulfillmentType = req.FulfillmentType,
            SubTotal = subTotal, DeliveryFee = deliveryFee, Total = total,
            Notes = req.Notes,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _db.MallOrders.Add(order);
        await _db.SaveChangesAsync(ct);

        foreach (var item in cart.Items)
        {
            _db.StoreOrderItems.Add(new StoreOrderItem {
                OrderId = order.Id, StoreId = item.StoreId,
                ProductId = item.ProductId, Quantity = item.Quantity,
                UnitPrice = item.Product.DiscountedPrice ?? item.Product.Price,
                Total = item.Quantity * (item.Product.DiscountedPrice ?? item.Product.Price),
                Notes = item.Notes,
            });
        }

        // Clear cart
        _db.CartItems.RemoveRange(cart.Items);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("[Mall] Order {OrderNum} — Total: {Total} — Customer: {CustomerId}",
            orderNum, total, customerId);

        return ApiResponse<object>.Ok(new { orderId = order.Id, orderNum, total, status = "Pending" });
    }

    public async Task<ApiResponse<object>> GetOrderAsync(
        Guid customerId, Guid orderId, CancellationToken ct)
    {
        var order = await _db.MallOrders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Items).ThenInclude(i => i.Store)
            .Include(o => o.Driver)
            .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.Id == orderId, ct);

        return order is null
            ? ApiResponse<object>.Fail("الطلب غير موجود")
            : ApiResponse<object>.Ok(new {
                order.Id, order.OrderNumber, order.Status, order.FulfillmentType,
                order.SubTotal, order.DeliveryFee, order.Total,
                order.EstimatedDelivery, order.DeliveredAt, order.Notes,
                driver = order.Driver is null ? null : new {
                    order.Driver.Name, order.Driver.Phone,
                    lat = order.Driver.CurrentLat, lng = order.Driver.CurrentLng,
                    status = order.Driver.Status.ToString(),
                },
                items = order.Items.Select(i => new {
                    i.StoreId, storeName = i.Store.Name, i.ProductId,
                    productName = i.Product.Name, i.Quantity, i.UnitPrice, i.Total
                })
            });
    }

    public async Task<object> GetHistoryAsync(
        Guid customerId, int page, int size, CancellationToken ct)
    {
        var total  = await _db.MallOrders.CountAsync(o => o.CustomerId == customerId, ct);
        var orders = await _db.MallOrders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(o => new {
                o.Id, o.OrderNumber, o.Status, o.Total,
                o.CreatedAt, itemCount = o.Items.Count
            }).ToListAsync(ct);

        return new { success = true, total, page, size, orders };
    }

    public async Task<object> GetIncomingAsync(Guid tenantId, CancellationToken ct)
    {
        var store = await _db.MallStores.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.IsActive, ct);
        if (store is null) return new { success = false, message = "المتجر غير موجود" };

        var items = await _db.StoreOrderItems
            .Include(i => i.Order).Include(i => i.Product)
            .Where(i => i.StoreId == store.Id &&
                i.Order.Status < MallOrderStatus.Delivered &&
                i.Order.Status != MallOrderStatus.Cancelled)
            .OrderBy(i => i.Order.CreatedAt)
            .Select(i => new {
                i.Id, orderId = i.Order.Id, orderNum = i.Order.OrderNumber,
                orderStatus = i.Order.Status.ToString(),
                productName = i.Product.Name, i.Quantity, i.UnitPrice, i.Total,
                createdAt = i.Order.CreatedAt
            }).ToListAsync(ct);

        return new { success = true, data = items };
    }

    public async Task<ApiResponse<object>> UpdateStoreOrderStatusAsync(
        Guid tenantId, Guid itemId, UpdateStoreOrderReq req, CancellationToken ct)
    {
        var item = await _db.StoreOrderItems
            .Include(i => i.Order)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.Store.TenantId == tenantId, ct);
        if (item is null) return ApiResponse<object>.Fail("العنصر غير موجود");

        if (Enum.TryParse<MallOrderStatus>(req.Status, out var newStatus))
        {
            item.Order.Status = newStatus;

            // ── WhatsApp — إشعار تغيير حالة الطلب ────────────────────────
            // يُضاف لاحقاً بعد ربط customer phone بالطلب
            // TODO: _ = _whatsApp.SendOrderStatusAsync(phone, ...)
            item.Order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return ApiResponse<object>.Ok(new { success = true });
    }

    public async Task<object> GetAdminDashboardAsync(Guid mallId, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var orders = await _db.MallOrders
            .Where(o => o.MallId == mallId).ToListAsync(ct);

        return new {
            success = true,
            data = new {
                totalOrders      = orders.Count,
                todayOrders      = orders.Count(o => o.CreatedAt.Date == today),
                totalRevenue     = orders.Where(o => o.Status == MallOrderStatus.Delivered).Sum(o => o.Total),
                todayRevenue     = orders.Where(o => o.CreatedAt.Date == today && o.Status == MallOrderStatus.Delivered).Sum(o => o.Total),
                pendingOrders    = orders.Count(o => o.Status == MallOrderStatus.Pending),
                deliveredOrders  = orders.Count(o => o.Status == MallOrderStatus.Delivered),
                cancelledOrders  = orders.Count(o => o.Status == MallOrderStatus.Cancelled),
            }
        };
    }

    public async Task<object> GetAllOrdersAsync(Guid mallId, int page, int size, CancellationToken ct)
    {
        var total  = await _db.MallOrders.CountAsync(o => o.MallId == mallId, ct);
        var orders = await _db.MallOrders
            .Include(o => o.Customer)
            .Where(o => o.MallId == mallId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(o => new {
                o.Id, o.OrderNumber, o.Status, o.Total,
                customerName = o.Customer.FullName, o.CreatedAt
            }).ToListAsync(ct);

        return new { success = true, total, page, size, orders };
    }
}

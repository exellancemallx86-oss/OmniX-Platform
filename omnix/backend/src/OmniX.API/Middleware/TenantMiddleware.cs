using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using OmniX.Infrastructure.Data;

namespace OmniX.API.Middleware;

// ════════════════════════════════════════════════════════════════════════════
//  TenantMiddleware — يستخرج TenantId من الـ JWT ويمرره للـ RLS Interceptor
// ════════════════════════════════════════════════════════════════════════════
public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, ITenantProvider tenantProvider)
    {
        var claim = ctx.User.FindFirst("tenantId")
                 ?? ctx.User.FindFirst("TenantId")
                 ?? ctx.User.FindFirst("tid");

        if (claim != null && Guid.TryParse(claim.Value, out var tenantId))
            tenantProvider.SetTenantId(tenantId);

        // دعم MallX public endpoints التي لا تحتاج TenantId
        await _next(ctx);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  TenantProvider — scoped implementation
// ════════════════════════════════════════════════════════════════════════════
public class TenantProvider : ITenantProvider
{
    private Guid? _tenantId;
    public Guid? TenantId => _tenantId;
    public void SetTenantId(Guid tenantId) => _tenantId = tenantId;
}

// ════════════════════════════════════════════════════════════════════════════
//  ICurrentUserService + Implementation
// ════════════════════════════════════════════════════════════════════════════
public interface ICurrentUserService
{
    Guid?   UserId   { get; }
    Guid?   TenantId { get; }
    string? Username { get; }
    string? Role     { get; }
    bool    IsAuthenticated { get; }
}

public class CurrentUserService : ICurrentUserService
{
    public Guid?   UserId   { get; }
    public Guid?   TenantId { get; }
    public string? Username { get; }
    public string? Role     { get; }
    public bool    IsAuthenticated { get; }

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        var user = accessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return;

        IsAuthenticated = true;
        var uid = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (Guid.TryParse(uid, out var userId)) UserId = userId;

        var tid = user.FindFirst("tenantId")?.Value ?? user.FindFirst("tid")?.Value;
        if (Guid.TryParse(tid, out var tenantId)) TenantId = tenantId;

        Username = user.FindFirst(ClaimTypes.Name)?.Value
                ?? user.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value;
        Role = user.FindFirst(ClaimTypes.Role)?.Value;
    }
}

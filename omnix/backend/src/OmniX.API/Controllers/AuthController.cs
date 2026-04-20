using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OmniX.Application.Services.Auth;

namespace OmniX.API.Controllers;

// ════════════════════════════════════════════════════════════════════════════
//  BaseController
// ════════════════════════════════════════════════════════════════════════════
[ApiController]
[Produces("application/json")]
public abstract class OmniXBaseController : ControllerBase
{
    protected string ClientIp =>
        HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "0.0.0.0";

    protected string ClientAgent =>
        Request.Headers.UserAgent.ToString() is { Length: > 0 } ua
            ? ua[..Math.Min(250, ua.Length)] : "unknown";
}

// ════════════════════════════════════════════════════════════════════════════
//  AUTH CONTROLLER — B2B Staff
//  POST /api/v1/auth/register
//  POST /api/v1/auth/login
//  POST /api/v1/auth/refresh
//  POST /api/v1/auth/logout
//  GET  /api/v1/auth/me
//  POST /api/v1/auth/change-password
//  POST /api/v1/auth/mfa/setup | verify | disable
// ════════════════════════════════════════════════════════════════════════════
[Route("api/v1/auth")]
public class AuthController : OmniXBaseController
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var res = await _auth.RegisterAsync(req, ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var res = await _auth.LoginAsync(req, ClientIp, ClientAgent, ct);
        return res.Success ? Ok(res) : Unauthorized(res);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var res = await _auth.RefreshTokenAsync(req.Token, ClientIp, ct);
        return res.Success ? Ok(res) : Unauthorized(res);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req, CancellationToken ct)
        => Ok(await _auth.LogoutAsync(req.Token, ct));

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var res = await _auth.GetCurrentUserAsync(ct);
        return res.Success ? Ok(res) : NotFound(res);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        var res = await _auth.ChangePasswordAsync(req, ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }

    // ── TOTP 2FA ─────────────────────────────────────────────────────────
    [HttpPost("mfa/setup")]
    [Authorize]
    public async Task<IActionResult> MfaSetup(CancellationToken ct)
        => Ok(await _auth.SetupMfaAsync(ct));

    [HttpPost("mfa/verify")]
    [Authorize]
    public async Task<IActionResult> MfaVerify([FromBody] MfaVerifyRequest req, CancellationToken ct)
    {
        var res = await _auth.VerifyMfaAsync(req, ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }

    [HttpPost("mfa/disable")]
    [Authorize]
    public async Task<IActionResult> MfaDisable([FromBody] MfaVerifyRequest req, CancellationToken ct)
    {
        var res = await _auth.DisableMfaAsync(req, ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  HEALTH CONTROLLER — for Render and Kubernetes
// ════════════════════════════════════════════════════════════════════════════
[Route("")]
[ApiExplorerSettings(IgnoreApi = true)]
public class HealthController : OmniXBaseController
{
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new
    {
        status    = "Healthy",
        timestamp = DateTime.UtcNow,
        version   = "1.0.0",
        platform  = "OmniX Business Platform",
    });

    [HttpGet("")]
    [AllowAnonymous]
    public IActionResult Root() => Ok(new
    {
        name    = "OmniX Business Platform API",
        version = "1.0.0",
        docs    = "/swagger",
        health  = "/health",
    });
}

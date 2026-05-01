// ═══════════════════════════════════════════════════════════════════════════
//  OmniX — WhatsApp Controller
//  POST /api/whatsapp/webhook  ← يستقبل رسائل Meta
//  GET  /api/whatsapp/webhook  ← Meta verification challenge
//  POST /api/whatsapp/test     ← اختبار الإرسال
// ═══════════════════════════════════════════════════════════════════════════
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniX.Application.Services.WhatsApp;

namespace OmniX.API.Controllers.WhatsApp;

[ApiController]
[Route("api/whatsapp")]
public class WhatsAppController : ControllerBase
{
    private readonly IWhatsAppService _whatsApp;
    private readonly ILogger<WhatsAppController> _log;

    public WhatsAppController(IWhatsAppService whatsApp, ILogger<WhatsAppController> log)
    {
        _whatsApp = whatsApp;
        _log      = log;
    }

    // ─── Webhook Verification (GET) — Meta يستدعيه عند الإعداد ──────────
    [HttpGet("webhook")]
    [AllowAnonymous]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")]       string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")]  string? challenge)
    {
        if (_whatsApp.VerifyWebhook(mode ?? "", token ?? "", challenge ?? "",
            out var verifyChallenge))
        {
            _log.LogInformation("WhatsApp webhook verified successfully");
            return Content(verifyChallenge ?? "");
        }

        _log.LogWarning("WhatsApp webhook verification failed — invalid token");
        return Forbid();
    }

    // ─── Webhook Events (POST) — Meta يرسل إليه الرسائل والأحداث ────────
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> ReceiveWebhook(
        [FromBody] WhatsAppWebhookPayload payload,
        CancellationToken ct)
    {
        try
        {
            await _whatsApp.HandleWebhookAsync(payload, ct);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "WhatsApp webhook processing error");
            return Ok(new { success = true }); // نرجع 200 دائماً لـ Meta
        }
    }

    // ─── Test Send (للمطورين فقط) ────────────────────────────────────────
    [HttpPost("test")]
    [Authorize(Policy = "Admin+")]
    public async Task<IActionResult> TestSend(
        [FromBody] TestSendRequest req, CancellationToken ct)
    {
        var result = await _whatsApp.SendTextAsync(req.Phone, req.Message, ct);
        return result.Success
            ? Ok(new { success = true, messageId = result.MessageId })
            : BadRequest(new { success = false, error = result.Error });
    }
}

public record TestSendRequest(string Phone, string Message);

// ═══════════════════════════════════════════════════════════════════════════
//  OmniX — WhatsApp Business API Service (Meta Cloud API)
//  يرسل إشعارات الطلبات + الحجوزات + التوصيل + التنبيهات
//
//  المتطلبات:
//    - WhatsApp Business Account على Meta Business Manager
//    - Phone Number ID + Access Token من Meta Developer Console
//    - الـ templates تحتاج موافقة Meta قبل الاستخدام
//
//  الـ endpoints:
//    POST /api/whatsapp/webhook  ← يستقبل رسائل العملاء
//    GET  /api/whatsapp/webhook  ← Meta verification
// ═══════════════════════════════════════════════════════════════════════════
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniX.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace OmniX.Application.Services.WhatsApp;

// ══════════════════════════════════════════════════════════════════════════
//  DTOs
// ══════════════════════════════════════════════════════════════════════════
public record WhatsAppSendResult(bool Success, string? MessageId, string? Error);

public record WhatsAppTemplateParam(string Type, string Text);

public enum WhatsAppNotificationType
{
    OrderConfirmed,
    OrderPreparing,
    OrderReady,
    OrderPickedUp,
    OrderDelivered,
    OrderCancelled,
    ReservationConfirmed,
    ReservationReminder,
    ReservationCancelled,
    LowStockAlert,
    DailyReport,
    WelcomeMessage,
    OtpCode,
}

// ══════════════════════════════════════════════════════════════════════════
//  INTERFACE
// ══════════════════════════════════════════════════════════════════════════
public interface IWhatsAppService
{
    // ── إشعارات الطلبات ──────────────────────────────────────────────────
    Task<WhatsAppSendResult> SendOrderConfirmedAsync(string phone, string customerName,
        string orderNumber, decimal total, int prepMinutes, CancellationToken ct = default);

    Task<WhatsAppSendResult> SendOrderPreparingAsync(string phone, string customerName,
        string orderNumber, int prepMinutes, CancellationToken ct = default);

    Task<WhatsAppSendResult> SendOrderReadyAsync(string phone, string customerName,
        string orderNumber, string fulfillmentType, CancellationToken ct = default);

    Task<WhatsAppSendResult> SendOrderPickedUpAsync(string phone, string customerName,
        string orderNumber, string driverName, string driverPhone, CancellationToken ct = default);

    Task<WhatsAppSendResult> SendOrderDeliveredAsync(string phone, string customerName,
        string orderNumber, CancellationToken ct = default);

    Task<WhatsAppSendResult> SendOrderCancelledAsync(string phone, string customerName,
        string orderNumber, string? reason, CancellationToken ct = default);

    // ── إشعارات الحجوزات ────────────────────────────────────────────────
    Task<WhatsAppSendResult> SendReservationConfirmedAsync(string phone, string customerName,
        string restaurantName, DateTime reservedAt, int guestCount, string? notes,
        CancellationToken ct = default);

    Task<WhatsAppSendResult> SendReservationReminderAsync(string phone, string customerName,
        string restaurantName, DateTime reservedAt, CancellationToken ct = default);

    Task<WhatsAppSendResult> SendReservationCancelledAsync(string phone, string customerName,
        string restaurantName, CancellationToken ct = default);

    // ── إشعارات المخزن (للمدير) ──────────────────────────────────────────
    Task<WhatsAppSendResult> SendLowStockAlertAsync(string managerPhone,
        string branchName, string productName, decimal currentQty, decimal minQty,
        CancellationToken ct = default);

    // ── تقرير يومي (للمدير) ──────────────────────────────────────────────
    Task<WhatsAppSendResult> SendDailyReportAsync(string managerPhone,
        string branchName, decimal totalSales, int ordersCount, int tablesServed,
        CancellationToken ct = default);

    // ── رسالة ترحيب + OTP ────────────────────────────────────────────────
    Task<WhatsAppSendResult> SendWelcomeAsync(string phone, string customerName,
        string businessName, CancellationToken ct = default);

    Task<WhatsAppSendResult> SendOtpAsync(string phone, string otp,
        CancellationToken ct = default);

    // ── رسالة حرة (للمدير فقط) ───────────────────────────────────────────
    Task<WhatsAppSendResult> SendTextAsync(string phone, string message,
        CancellationToken ct = default);

    // ── Webhook Handler ───────────────────────────────────────────────────
    Task HandleWebhookAsync(WhatsAppWebhookPayload payload, CancellationToken ct = default);
    bool VerifyWebhook(string mode, string token, string challenge, out string? verifyChallenge);
}

// ══════════════════════════════════════════════════════════════════════════
//  WEBHOOK MODELS
// ══════════════════════════════════════════════════════════════════════════
public class WhatsAppWebhookPayload
{
    [JsonPropertyName("object")]  public string? Object  { get; set; }
    [JsonPropertyName("entry")]   public List<WhatsAppEntry>? Entry { get; set; }
}

public class WhatsAppEntry
{
    [JsonPropertyName("id")]      public string? Id      { get; set; }
    [JsonPropertyName("changes")] public List<WhatsAppChange>? Changes { get; set; }
}

public class WhatsAppChange
{
    [JsonPropertyName("value")]   public WhatsAppChangeValue? Value { get; set; }
    [JsonPropertyName("field")]   public string? Field { get; set; }
}

public class WhatsAppChangeValue
{
    [JsonPropertyName("messaging_product")] public string? MessagingProduct { get; set; }
    [JsonPropertyName("metadata")]          public WhatsAppMetadata? Metadata { get; set; }
    [JsonPropertyName("messages")]          public List<WhatsAppMessage>? Messages { get; set; }
    [JsonPropertyName("statuses")]          public List<WhatsAppStatus>? Statuses { get; set; }
}

public class WhatsAppMetadata
{
    [JsonPropertyName("display_phone_number")] public string? DisplayPhoneNumber { get; set; }
    [JsonPropertyName("phone_number_id")]      public string? PhoneNumberId { get; set; }
}

public class WhatsAppMessage
{
    [JsonPropertyName("id")]        public string? Id        { get; set; }
    [JsonPropertyName("from")]      public string? From      { get; set; }
    [JsonPropertyName("timestamp")] public string? Timestamp { get; set; }
    [JsonPropertyName("type")]      public string? Type      { get; set; }
    [JsonPropertyName("text")]      public WhatsAppText? Text { get; set; }
}

public class WhatsAppText
{
    [JsonPropertyName("body")] public string? Body { get; set; }
}

public class WhatsAppStatus
{
    [JsonPropertyName("id")]           public string? Id          { get; set; }
    [JsonPropertyName("status")]       public string? Status      { get; set; }
    [JsonPropertyName("timestamp")]    public string? Timestamp   { get; set; }
    [JsonPropertyName("recipient_id")] public string? RecipientId { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════
//  IMPLEMENTATION
// ══════════════════════════════════════════════════════════════════════════
public class WhatsAppService : IWhatsAppService
{
    private readonly IHttpClientFactory    _httpFactory;
    private readonly IConfiguration        _config;
    private readonly OmniXDbContext        _db;
    private readonly ILogger<WhatsAppService> _log;

    // Meta Cloud API settings
    private string PhoneNumberId  => _config["WhatsApp:PhoneNumberId"]  ?? "";
    private string AccessToken    => _config["WhatsApp:AccessToken"]     ?? "";
    private string VerifyToken    => _config["WhatsApp:VerifyToken"]     ?? "omnix_webhook_verify";
    private string BaseUrl        => _config["WhatsApp:BaseUrl"]         ?? "https://graph.facebook.com/v18.0";

    private bool IsConfigured     => !string.IsNullOrEmpty(PhoneNumberId) &&
                                     !string.IsNullOrEmpty(AccessToken);

    public WhatsAppService(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        OmniXDbContext db,
        ILogger<WhatsAppService> log)
    {
        _httpFactory = httpFactory;
        _config      = config;
        _db          = db;
        _log         = log;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ORDER NOTIFICATIONS
    // ══════════════════════════════════════════════════════════════════════

    public Task<WhatsAppSendResult> SendOrderConfirmedAsync(
        string phone, string customerName, string orderNumber,
        decimal total, int prepMinutes, CancellationToken ct)
    {
        var message = $"""
            ✅ *تأكيد الطلب — {orderNumber}*

            أهلاً {customerName}! 🎉
            تم استلام طلبك بنجاح.

            💰 *الإجمالي:* {total:F2} جنيه
            ⏱ *وقت التحضير المتوقع:* {prepMinutes} دقيقة

            سنُعلمك فور جاهزية طلبك 📱
            شكراً لاختيارك! 🙏
            """;

        return SendTextAsync(phone, message, ct);
    }

    public Task<WhatsAppSendResult> SendOrderPreparingAsync(
        string phone, string customerName, string orderNumber,
        int prepMinutes, CancellationToken ct)
    {
        var message = $"""
            🍳 *طلبك الآن قيد التحضير — {orderNumber}*

            {customerName}، فريقنا يعمل على طلبك الآن! 👨‍🍳

            ⏱ *الوقت المتبقي تقريباً:* {prepMinutes} دقيقة

            سنُبلغك فور الانتهاء 🔔
            """;

        return SendTextAsync(phone, message, ct);
    }

    public Task<WhatsAppSendResult> SendOrderReadyAsync(
        string phone, string customerName, string orderNumber,
        string fulfillmentType, CancellationToken ct)
    {
        var fulfillmentMsg = fulfillmentType.ToLower() == "delivery"
            ? "🚗 السائق في طريقه إليك الآن!"
            : "📍 طلبك جاهز للاستلام من الفرع!";

        var message = $"""
            🎉 *طلبك جاهز! — {orderNumber}*

            {customerName}، طلبك اكتمل تحضيره! 🌟

            {fulfillmentMsg}

            نتمنى لك وجبة شهية! 😊
            """;

        return SendTextAsync(phone, message, ct);
    }

    public Task<WhatsAppSendResult> SendOrderPickedUpAsync(
        string phone, string customerName, string orderNumber,
        string driverName, string driverPhone, CancellationToken ct)
    {
        var message = $"""
            🚗 *طلبك في الطريق — {orderNumber}*

            {customerName}، السائق اصطحب طلبك! 🛍

            👨‍✈️ *السائق:* {driverName}
            📞 *رقمه:* {driverPhone}

            يمكنك التواصل معه مباشرة إن احتجت.
            نتمنى لك استلاماً سريعاً! 🙏
            """;

        return SendTextAsync(phone, message, ct);
    }

    public Task<WhatsAppSendResult> SendOrderDeliveredAsync(
        string phone, string customerName, string orderNumber, CancellationToken ct)
    {
        var message = $"""
            ✅ *تم التسليم — {orderNumber}*

            {customerName}، وصل طلبك بنجاح! 🎊

            نتمنى أن ينال إعجابك 😊
            لا تنسَ تقييم تجربتك!

            شكراً لثقتك بنا 🙏
            """;

        return SendTextAsync(phone, message, ct);
    }

    public Task<WhatsAppSendResult> SendOrderCancelledAsync(
        string phone, string customerName, string orderNumber,
        string? reason, CancellationToken ct)
    {
        var reasonLine = !string.IsNullOrEmpty(reason)
            ? $"\n📋 *السبب:* {reason}"
            : "";

        var message = $"""
            ❌ *تم إلغاء الطلب — {orderNumber}*

            {customerName}، نأسف لإبلاغك بإلغاء طلبك.
            {reasonLine}

            للاستفسار تواصل معنا مباشرة.
            نعتذر عن الإزعاج 🙏
            """;

        return SendTextAsync(phone, message, ct);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  RESERVATION NOTIFICATIONS
    // ══════════════════════════════════════════════════════════════════════

    public Task<WhatsAppSendResult> SendReservationConfirmedAsync(
        string phone, string customerName, string restaurantName,
        DateTime reservedAt, int guestCount, string? notes, CancellationToken ct)
    {
        var notesLine = !string.IsNullOrEmpty(notes)
            ? $"\n📝 *ملاحظات:* {notes}"
            : "";

        var message = $"""
            🍽 *تأكيد الحجز — {restaurantName}*

            أهلاً {customerName}! 🌟
            تم تأكيد حجزك بنجاح.

            📅 *التاريخ والوقت:* {reservedAt:dd/MM/yyyy hh:mm tt}
            👥 *عدد الأشخاص:* {guestCount}
            {notesLine}

            في حال الرغبة في التعديل أو الإلغاء، يرجى الإبلاغ قبل ساعتين.
            نتطلع لاستقبالكم! 🙏
            """;

        return SendTextAsync(phone, message, ct);
    }

    public Task<WhatsAppSendResult> SendReservationReminderAsync(
        string phone, string customerName, string restaurantName,
        DateTime reservedAt, CancellationToken ct)
    {
        var message = $"""
            ⏰ *تذكير بحجزك — {restaurantName}*

            {customerName}، نذكّرك بحجزك اليوم! 📅

            🕐 *الموعد:* {reservedAt:hh:mm tt}
            📍 *المطعم:* {restaurantName}

            نراك قريباً! 🌟
            """;

        return SendTextAsync(phone, message, ct);
    }

    public Task<WhatsAppSendResult> SendReservationCancelledAsync(
        string phone, string customerName, string restaurantName, CancellationToken ct)
    {
        var message = $"""
            ❌ *تم إلغاء الحجز — {restaurantName}*

            {customerName}، تم إلغاء حجزك في {restaurantName}.

            نأمل أن نستقبلك مرة أخرى قريباً 🙏
            """;

        return SendTextAsync(phone, message, ct);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  MANAGEMENT NOTIFICATIONS
    // ══════════════════════════════════════════════════════════════════════

    public Task<WhatsAppSendResult> SendLowStockAlertAsync(
        string managerPhone, string branchName, string productName,
        decimal currentQty, decimal minQty, CancellationToken ct)
    {
        var message = $"""
            ⚠️ *تنبيه: مخزون منخفض — {branchName}*

            📦 *الصنف:* {productName}
            📉 *الكمية الحالية:* {currentQty:F1}
            🔴 *الحد الأدنى:* {minQty:F1}

            يرجى إعادة الطلب في أقرب وقت!
            """;

        return SendTextAsync(managerPhone, message, ct);
    }

    public Task<WhatsAppSendResult> SendDailyReportAsync(
        string managerPhone, string branchName,
        decimal totalSales, int ordersCount, int tablesServed, CancellationToken ct)
    {
        var message = $"""
            📊 *التقرير اليومي — {branchName}*
            📅 {DateTime.Now:dd/MM/yyyy}

            💰 *إجمالي المبيعات:* {totalSales:F2} جنيه
            🛒 *عدد الطلبات:* {ordersCount}
            🍽 *الطاولات المخدومة:* {tablesServed}
            📈 *متوسط الفاتورة:* {(ordersCount > 0 ? totalSales / ordersCount : 0):F2} جنيه

            تصبح على خير! 🌙
            """;

        return SendTextAsync(managerPhone, message, ct);
    }

    public Task<WhatsAppSendResult> SendWelcomeAsync(
        string phone, string customerName, string businessName, CancellationToken ct)
    {
        var message = $"""
            👋 *أهلاً وسهلاً في {businessName}!*

            مرحباً {customerName}! 🌟

            يسعدنا تسجيلك معنا.
            يمكنك الآن تتبع طلباتك وحجوزاتك مباشرة على واتساب.

            لأي استفسار نحن دائماً هنا! 🙏
            """;

        return SendTextAsync(phone, message, ct);
    }

    public Task<WhatsAppSendResult> SendOtpAsync(
        string phone, string otp, CancellationToken ct)
    {
        var message = $"""
            🔐 *رمز التحقق*

            رمزك هو: *{otp}*

            صالح لمدة 5 دقائق.
            لا تشاركه مع أحد 🔒
            """;

        return SendTextAsync(phone, message, ct);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  CORE SEND METHOD
    // ══════════════════════════════════════════════════════════════════════

    public async Task<WhatsAppSendResult> SendTextAsync(
        string phone, string message, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _log.LogWarning("WhatsApp not configured — skipping message to {Phone}", phone);
            return new WhatsAppSendResult(false, null, "WhatsApp not configured");
        }

        // تنظيف رقم الهاتف — يجب أن يبدأ بكود الدولة بدون +
        var cleanPhone = CleanPhoneNumber(phone);
        if (string.IsNullOrEmpty(cleanPhone))
        {
            _log.LogWarning("Invalid phone number: {Phone}", phone);
            return new WhatsAppSendResult(false, null, "Invalid phone number");
        }

        try
        {
            var client = _httpFactory.CreateClient("WhatsApp");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {AccessToken}");

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type    = "individual",
                to                = cleanPhone,
                type              = "text",
                text              = new { preview_url = false, body = message }
            };

            var url = $"{BaseUrl}/{PhoneNumberId}/messages";
            var response = await client.PostAsJsonAsync(url, payload, ct);
            var content  = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var msgId = doc.RootElement
                    .GetProperty("messages")[0]
                    .GetProperty("id").GetString();

                _log.LogInformation("WhatsApp sent to {Phone}: {MsgId}", cleanPhone, msgId);

                // سجّل في DB
                await LogWhatsAppMessageAsync(cleanPhone, message, msgId, true, null, ct);

                return new WhatsAppSendResult(true, msgId, null);
            }
            else
            {
                _log.LogError("WhatsApp failed {Status}: {Content}", response.StatusCode, content);
                await LogWhatsAppMessageAsync(cleanPhone, message, null, false, content, ct);
                return new WhatsAppSendResult(false, null, content);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "WhatsApp exception sending to {Phone}", cleanPhone);
            return new WhatsAppSendResult(false, null, ex.Message);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  WEBHOOK
    // ══════════════════════════════════════════════════════════════════════

    public bool VerifyWebhook(string mode, string token, string challenge,
        out string? verifyChallenge)
    {
        verifyChallenge = null;
        if (mode == "subscribe" && token == VerifyToken)
        {
            verifyChallenge = challenge;
            return true;
        }
        return false;
    }

    public async Task HandleWebhookAsync(
        WhatsAppWebhookPayload payload, CancellationToken ct)
    {
        if (payload.Entry is null) return;

        foreach (var entry in payload.Entry)
        foreach (var change in entry.Changes ?? [])
        {
            // معالجة الرسائل الواردة من العملاء
            foreach (var msg in change.Value?.Messages ?? [])
            {
                _log.LogInformation("WhatsApp incoming from {From}: {Body}",
                    msg.From, msg.Text?.Body);

                // Auto-reply بسيط
                if (msg.Text?.Body?.Contains("طلب", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await SendTextAsync(msg.From ?? "", """
                        🙏 شكراً للتواصل معنا!
                        لمتابعة طلبك، يمكنك التحقق من التطبيق أو الاتصال بالفرع مباشرة.
                        """, ct);
                }
            }

            // تتبع حالة الرسائل المرسلة
            foreach (var status in change.Value?.Statuses ?? [])
            {
                _log.LogDebug("WhatsApp status {Status} for msg {Id}",
                    status.Status, status.Id);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════════════

    private static string CleanPhoneNumber(string phone)
    {
        // إزالة كل الرموز غير الرقمية
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // مصر: 01xxxxxxxxx → 201xxxxxxxxx
        if (digits.StartsWith("01") && digits.Length == 11)
            return "2" + digits;

        // مع كود الدولة
        if (digits.StartsWith("20") && digits.Length == 12)
            return digits;

        // رقم دولي عام
        if (digits.Length >= 10)
            return digits;

        return "";
    }

    private async Task LogWhatsAppMessageAsync(
        string phone, string body, string? msgId,
        bool success, string? error, CancellationToken ct)
    {
        try
        {
            // نسجّل في audit_logs
            _db.AuditLogs.Add(new OmniX.Domain.Entities.Auth.AuditLog
            {
                Id        = Guid.NewGuid(),
                TenantId  = Guid.Empty,  // platform-level log
                Action    = "WhatsApp.Send",
                Entity    = "WhatsAppMessage",
                EntityId  = msgId,
                NewValues = JsonSerializer.Serialize(new
                {
                    phone, body = body[..Math.Min(100, body.Length)],
                    success, error, msgId
                }),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to log WhatsApp message");
        }
    }
}

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OmniX.API.Hubs;
using OmniX.API.Middleware;
using OmniX.Infrastructure.Caching;
using OmniX.Infrastructure.Data;
using Serilog;
using Serilog.Events;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

// ── Serilog Bootstrap ─────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft",                     LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System",                        LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/omnix-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    var cfg = builder.Configuration;

    // ── PostgreSQL + RLS ─────────────────────────────────────────────────
    builder.Services.AddScoped<ITenantProvider, TenantProvider>();
    builder.Services.AddScoped<TenantRLSInterceptor>();

    builder.Services.AddDbContext<OmniXDbContext>((sp, opt) =>
    {
        var connStr = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection not set.");

        opt.UseNpgsql(connStr, pg =>
        {
            pg.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            pg.CommandTimeout(30);
            pg.MigrationsAssembly("OmniX.Infrastructure");
        });

        // RLS Interceptor — يضبط tenant_id على مستوى PostgreSQL connection
        opt.AddInterceptors(sp.GetRequiredService<TenantRLSInterceptor>());

        if (builder.Environment.IsDevelopment())
            opt.EnableSensitiveDataLogging().EnableDetailedErrors();
    });

    // ── Redis (من MallX) ─────────────────────────────────────────────────
    var redisConn = cfg.GetConnectionString("Redis");
    if (!string.IsNullOrEmpty(redisConn))
    {
        builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConn);
        builder.Services.AddSingleton<ICacheService, RedisCacheService>();
        Log.Information("✅ Redis connected: {Conn}", redisConn[..Math.Min(30, redisConn.Length)] + "...");
    }
    else
    {
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
        Log.Warning("⚠️ Redis not configured — using MemoryCache (single-instance only)");
    }

    // ── HTTP Context ────────────────────────────────────────────────────
    builder.Services.AddHttpContextAccessor();

    // ── Application Services ─────────────────────────────────────────────
    // Auth
    builder.Services.AddScoped<IAuthService,         AuthService>();
    builder.Services.AddScoped<IMallAuthService,     MallCustomerAuthService>();
    builder.Services.AddSingleton<ITotpService,      TotpService>();

    // Core
    builder.Services.AddScoped<IPosService,          PosService>();
    builder.Services.AddScoped<IInventoryService,    InventoryService>();
    builder.Services.AddScoped<ICustomerService,     CustomerService>();
    builder.Services.AddScoped<IDashboardService,    DashboardService>();

    // Sync (من Pro)
    builder.Services.AddScoped<ISyncService,         SyncService>();

    // Restaurant (من Ultra)
    builder.Services.AddScoped<IRestaurantService,   RestaurantService>();
    builder.Services.AddScoped<IRestaurantNotifier,  SignalRRestaurantNotifier>();

    // Rental (من Ultra)
    builder.Services.AddScoped<IRentalService,       RentalService>();

    // Mall (من MallX)
    builder.Services.AddScoped<IMallService,         MallService>();
    builder.Services.AddScoped<IMallOrderService,    MallOrderService>();
    builder.Services.AddScoped<ILoyaltyService,      LoyaltyService>();
    builder.Services.AddScoped<IWalletService,       WalletService>();
    builder.Services.AddScoped<IPaymobService,       PaymobService>();
    builder.Services.AddScoped<IDeliveryService,     DeliveryService>();

    // Licensing (من Ultra)
    builder.Services.AddScoped<ILicenseService,      LicenseService>();
    builder.Services.AddSingleton<IFeatureFlagService, FeatureFlagService>();

    // AI (من MallX + Pro)
    builder.Services.AddScoped<IAIService,           AnthropicAIService>();

    // Audit (من Ultra)
    builder.Services.AddScoped<IAuditService,        AuditService>();

    // Infrastructure
    builder.Services.AddScoped<ICurrentUserService,  CurrentUserService>();

    // ── HTTP Clients ─────────────────────────────────────────────────────
    builder.Services.AddHttpClient("Paymob",    c => c.BaseAddress = new Uri(cfg["Paymob:BaseUrl"] ?? "https://accept.paymob.com/api"));
    builder.Services.AddHttpClient("Firebase",  c => c.BaseAddress = new Uri("https://fcm.googleapis.com"));
    builder.Services.AddHttpClient("Anthropic", c => { c.BaseAddress = new Uri("https://api.anthropic.com"); c.Timeout = TimeSpan.FromSeconds(60); });

    // ── Background Jobs ───────────────────────────────────────────────────
    builder.Services.AddHostedService<LicenseEnforcementJob>();   // من Ultra
    builder.Services.AddHostedService<SyncCleanupJob>();          // من Pro
    builder.Services.AddHostedService<LoyaltyExpiryJob>();        // من MallX

    // ── SignalR (Restaurant + Mall) ───────────────────────────────────────
    var signalRBuilder = builder.Services.AddSignalR(o =>
    {
        o.EnableDetailedErrors      = builder.Environment.IsDevelopment();
        o.MaximumReceiveMessageSize = 64 * 1024;
        o.ClientTimeoutInterval     = TimeSpan.FromSeconds(60);
        o.KeepAliveInterval         = TimeSpan.FromSeconds(15);
    });

    // إضافة Redis backplane للـ SignalR (من MallX)
    if (!string.IsNullOrEmpty(redisConn))
        signalRBuilder.AddStackExchangeRedis(redisConn);

    // ── JWT Authentication ────────────────────────────────────────────────
    var jwtSecret = cfg["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

    if (jwtSecret.Length < 64)
        throw new InvalidOperationException("Jwt:Secret must be ≥ 64 characters.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.SaveToken = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = cfg["Jwt:Issuer"]   ?? "OmniX",
                ValidAudience            = cfg["Jwt:Audience"] ?? "OmniXClient",
                IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew                = TimeSpan.FromSeconds(30),
            };
            o.Events = new JwtBearerEvents
            {
                OnChallenge = ctx =>
                {
                    ctx.HandleResponse();
                    ctx.Response.StatusCode  = 401;
                    ctx.Response.ContentType = "application/json";
                    return ctx.Response.WriteAsJsonAsync(new { success = false, message = "غير مصرح. يرجى تسجيل الدخول." });
                },
                OnForbidden = ctx =>
                {
                    ctx.Response.StatusCode  = 403;
                    ctx.Response.ContentType = "application/json";
                    return ctx.Response.WriteAsJsonAsync(new { success = false, message = "ليس لديك صلاحية للوصول." });
                },
                // دعم SignalR WebSocket authentication
                OnMessageReceived = ctx =>
                {
                    var token = ctx.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(token) &&
                        (ctx.HttpContext.Request.Path.StartsWithSegments("/hubs")))
                        ctx.Token = token;
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization(o =>
    {
        o.AddPolicy("PlatformOwner", p => p.RequireRole("PlatformOwner"));
        o.AddPolicy("Manager+",      p => p.RequireRole("PlatformOwner","SuperAdmin","CompanyOwner","TenantAdmin","Manager"));
        o.AddPolicy("Admin+",        p => p.RequireRole("PlatformOwner","SuperAdmin","CompanyOwner","TenantAdmin"));
    });

    // ── Rate Limiting (من Ultra) ─────────────────────────────────────────
    builder.Services.AddRateLimiter(o =>
    {
        o.AddFixedWindowLimiter("auth", l =>     { l.PermitLimit = 10;  l.Window = TimeSpan.FromMinutes(1); l.QueueLimit = 0; });
        o.AddFixedWindowLimiter("api",  l =>     { l.PermitLimit = 300; l.Window = TimeSpan.FromMinutes(1); l.QueueLimit = 5; });
        o.AddFixedWindowLimiter("checkout", l => { l.PermitLimit = 20;  l.Window = TimeSpan.FromMinutes(1); l.QueueLimit = 0; });
        o.RejectionStatusCode = 429;
        o.OnRejected = async (ctx, ct) =>
        {
            ctx.HttpContext.Response.StatusCode  = 429;
            ctx.HttpContext.Response.ContentType = "application/json";
            await ctx.HttpContext.Response.WriteAsJsonAsync(
                new { success = false, message = "تجاوزت الحد الأقصى. حاول بعد دقيقة." }, ct);
        };
    });

    // ── CORS ─────────────────────────────────────────────────────────────
    var origins = cfg.GetSection("AllowedOrigins").Get<string[]>()
                  ?? ["http://localhost:3000", "http://localhost:3001", "http://localhost:19006"];

    builder.Services.AddCors(o => o.AddPolicy("OmniXPolicy", p =>
        p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

    // ── Controllers + JSON ────────────────────────────────────────────────
    builder.Services.AddControllers().AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy   = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        o.JsonSerializerOptions.WriteIndented          = builder.Environment.IsDevelopment();
    });

    // ── Swagger ───────────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title       = "OmniX Business Platform API",
            Version     = "v1.0",
            Description = "🇪🇬 POS · Restaurant · Rental · Mall · Offline Sync · Multi-Tenant | مدمج من MesterX Pro + MallX + Ultra Enterprise",
            Contact     = new OpenApiContact { Name = "OmniX Support", Email = "support@omnix.app" },
        });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
            In = ParameterLocation.Header, Description = "أدخل الـ JWT token",
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement {{
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }});
        c.EnableAnnotations();
        c.TagActionsBy(api => [api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] ?? "Default"]);
    });

    // ── Health Checks ─────────────────────────────────────────────────────
    var hcBuilder = builder.Services.AddHealthChecks()
        .AddNpgSql(cfg.GetConnectionString("DefaultConnection")!, name: "database", tags: ["db","ready"])
        .AddCheck("self", () => HealthCheckResult.Healthy("API is running"), ["live"]);

    if (!string.IsNullOrEmpty(redisConn))
        hcBuilder.AddRedis(redisConn, name: "redis", tags: ["cache","ready"]);

    // ── Compression ───────────────────────────────────────────────────────
    builder.Services.AddResponseCompression(o =>
    {
        o.EnableForHttps = true;
        o.MimeTypes = ["application/json", "text/plain", "application/javascript"];
    });

    // ══════════════════════════════════════════════════════════════════════
    var app = builder.Build();

    // ── Auto Migrate + Seed ───────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OmniXDbContext>();
        try
        {
            await db.Database.MigrateAsync();
            Log.Information("✅ DB migrations applied");

            if (!await db.Tenants.AnyAsync())
            {
                await OmniXSeeder.SeedAsync(db);
                Log.Information("✅ Seed data applied");
            }
        }
        catch (Exception ex) { Log.Error(ex, "❌ DB startup failed"); }
    }

    // ── Middleware Pipeline ───────────────────────────────────────────────
    app.UseResponseCompression();
    app.UseSerilogRequestLogging(o =>
    {
        o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} ({Elapsed:0.0}ms)";
        o.GetLevel = (ctx, _, ex) =>
            ex != null || ctx.Response.StatusCode >= 500 ? LogEventLevel.Error :
            ctx.Response.StatusCode >= 400               ? LogEventLevel.Warning :
                                                           LogEventLevel.Information;
    });

    // Security Headers (من Ultra)
    app.Use(async (ctx, next) =>
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"]        = "DENY";
        h["X-XSS-Protection"]       = "1; mode=block";
        h["Referrer-Policy"]        = "strict-origin-when-cross-origin";
        h["Permissions-Policy"]     = "geolocation=(), microphone=(), camera=()";
        if (!app.Environment.IsDevelopment())
            h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        await next();
    });

    // Tenant Middleware
    app.UseMiddleware<TenantMiddleware>();

    // Global Exception Handler
    app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
    {
        ctx.Response.StatusCode  = 500;
        ctx.Response.ContentType = "application/json";
        var err = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        Log.Error(err?.Error, "Unhandled exception on {Path}", ctx.Request.Path);
        await ctx.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "خطأ داخلي. يرجى المحاولة مرة أخرى أو التواصل مع الدعم الفني.",
            traceId = System.Diagnostics.Activity.Current?.Id ?? ctx.TraceIdentifier,
        });
    }));

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "OmniX Platform API v1");
            c.RoutePrefix   = "swagger";
            c.DocumentTitle = "OmniX Business Platform";
        });
    }
    else { app.UseHsts(); }

    app.UseHttpsRedirection();
    app.UseCors("OmniXPolicy");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // ── Routes ───────────────────────────────────────────────────────────
    app.MapControllers().RequireRateLimiting("api");

    // SignalR Hubs
    app.MapHub<RestaurantHub>("/hubs/restaurant");   // من Ultra
    app.MapHub<MallOrderHub>("/hubs/mall-orders");   // من MallX

    // Health Checks
    app.MapHealthChecks("/health",       new HealthCheckOptions { Predicate = _ => true });
    app.MapHealthChecks("/health/live",  new HealthCheckOptions { Predicate = c => c.Tags.Contains("live") });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

    Log.Information("🚀 OmniX Platform — {Env} — POS+Restaurant+Rental+Mall+Sync", app.Environment.EnvironmentName);
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "💀 OmniX failed to start");
    Environment.Exit(1);
}
finally { Log.CloseAndFlush(); }

public partial class Program { }   // Required for WebApplicationFactory in tests

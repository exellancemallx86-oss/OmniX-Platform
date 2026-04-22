using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OmniX.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace OmniX.Tests.Integration;

// ════════════════════════════════════════════════════════════════════════════
//  OmniX WebApplicationFactory — يبني API حقيقي في الذاكرة للاختبار
// ════════════════════════════════════════════════════════════════════════════
public class OmniXWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // استبدال PostgreSQL بـ InMemory DB للاختبار
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<OmniXDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<OmniXDbContext>(opts =>
                opts.UseInMemoryDatabase($"omnix_integration_{Guid.NewGuid()}"));

            // Seed test data
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OmniXDbContext>();
            db.Database.EnsureCreated();
            OmniXSeeder.SeedAsync(db).Wait();
        });
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  API Integration Tests
// ════════════════════════════════════════════════════════════════════════════
public class ApiIntegrationTests : IClassFixture<OmniXWebFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(OmniXWebFactory factory)
        => _client = factory.CreateClient();

    // ── Health Check ─────────────────────────────────────────────────────
    [Fact]
    public async Task HealthCheck_Returns200()
    {
        var res = await _client.GetAsync("/health");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthLive_Returns200()
    {
        var res = await _client.GetAsync("/health/live");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Swagger ──────────────────────────────────────────────────────────
    [Fact]
    public async Task SwaggerJson_Returns200()
    {
        var res = await _client.GetAsync("/swagger/v1/swagger.json");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Auth ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task Login_WithSeededAdmin_ReturnsToken()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = "admin@omnix.app",
            password = "Admin@123456"
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<dynamic>();
        ((bool)body!.success).Should().BeTrue();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = "admin@omnix.app",
            password = "WrongPassword"
        });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MeEndpoint_WithoutToken_Returns401()
    {
        var res = await _client.GetAsync("/api/auth/me");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── License Plans (Public) ────────────────────────────────────────────
    [Fact]
    public async Task GetPlans_NoAuth_Returns200()
    {
        var res = await _client.GetAsync("/api/license/plans");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Rate Limiting ─────────────────────────────────────────────────────
    [Fact]
    public async Task Login_After11Requests_Returns429()
    {
        var tasks = Enumerable.Range(0, 11).Select(_ =>
            _client.PostAsJsonAsync("/api/auth/login", new
            {
                email = $"spam{Guid.NewGuid()}@test.com",
                password = "spam"
            }));

        var results = await Task.WhenAll(tasks);
        results.Should().Contain(r => r.StatusCode == HttpStatusCode.TooManyRequests);
    }
}

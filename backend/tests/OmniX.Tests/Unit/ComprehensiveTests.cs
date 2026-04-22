using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OmniX.Application.Services.Auth;
using OmniX.Application.Services.Pos;
using OmniX.Domain.Entities.Core;
using OmniX.Domain.Enums;
using OmniX.Infrastructure.Data;
using Xunit;

namespace OmniX.Tests.Unit;

// ════════════════════════════════════════════════════════════════════════════
//  Shared InMemory DB Factory
// ════════════════════════════════════════════════════════════════════════════
public class InMemoryDbFactory : IDisposable
{
    public OmniXDbContext Db { get; }

    public InMemoryDbFactory(string dbName = "OmniXTest")
    {
        var opts = new DbContextOptionsBuilder<OmniXDbContext>()
            .UseInMemoryDatabase(dbName + "_" + Guid.NewGuid())
            .Options;
        Db = new OmniXDbContext(opts);
        Db.Database.EnsureCreated();
        SeedBasicData();
    }

    private void SeedBasicData()
    {
        var tenant = new Tenant {
            Id = TestIds.TenantId, Name = "Test Tenant", Slug = "test",
            Currency = "EGP", VatRate = 0.14m, IsActive = true,
            ActiveModules = TenantModules.POS | TenantModules.Inventory | TenantModules.Restaurant,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        var branch = new Branch {
            Id = TestIds.BranchId, TenantId = TestIds.TenantId,
            Name = "Main Branch", IsMain = true, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        var category = new Category {
            Id = TestIds.CategoryId, TenantId = TestIds.TenantId,
            Name = "Food", NameAr = "طعام", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        var product = new Product {
            Id = TestIds.ProductId, TenantId = TestIds.TenantId,
            CategoryId = TestIds.CategoryId, Name = "Burger", NameAr = "برجر",
            Barcode = "1234567890", SalePrice = 100m, CostPrice = 60m,
            HasVat = true, VatRate = 0.14m, IsActive = true, TrackInventory = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        var stock = new StockItem {
            Id = Guid.NewGuid(), TenantId = TestIds.TenantId,
            ProductId = TestIds.ProductId, BranchId = TestIds.BranchId,
            Quantity = 100, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        var customer = new Customer {
            Id = TestIds.CustomerId, TenantId = TestIds.TenantId,
            Name = "Ahmed Ali", Phone = "01012345678",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };

        Db.Tenants.Add(tenant);
        Db.Branches.Add(branch);
        Db.Categories.Add(category);
        Db.Products.Add(product);
        Db.StockItems.Add(stock);
        Db.Customers.Add(customer);
        Db.SaveChanges();
    }

    public void Dispose() => Db.Dispose();
}

public static class TestIds
{
    public static readonly Guid TenantId   = Guid.Parse("11111111-0000-0000-0000-000000000001");
    public static readonly Guid BranchId   = Guid.Parse("22222222-0000-0000-0000-000000000002");
    public static readonly Guid CategoryId = Guid.Parse("33333333-0000-0000-0000-000000000003");
    public static readonly Guid ProductId  = Guid.Parse("44444444-0000-0000-0000-000000000004");
    public static readonly Guid CustomerId = Guid.Parse("55555555-0000-0000-0000-000000000005");
    public static readonly Guid UserId     = Guid.Parse("66666666-0000-0000-0000-000000000006");
}

// ════════════════════════════════════════════════════════════════════════════
//  Security Helper Tests
// ════════════════════════════════════════════════════════════════════════════
public class SecurityHelperTests
{
    [Fact]
    public void HashPassword_ShouldProduceDifferentHashEachTime()
    {
        var hash1 = SecurityHelper.HashPassword("Test@123");
        var hash2 = SecurityHelper.HashPassword("Test@123");
        hash1.Should().NotBe(hash2);  // BCrypt uses random salt
    }

    [Fact]
    public void VerifyPassword_ShouldReturnTrue_WhenCorrect()
    {
        var hash = SecurityHelper.HashPassword("Test@123");
        SecurityHelper.VerifyPassword("Test@123", hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalse_WhenWrong()
    {
        var hash = SecurityHelper.HashPassword("Test@123");
        SecurityHelper.VerifyPassword("Wrong@123", hash).Should().BeFalse();
    }

    [Theory]
    [InlineData("short", false)]
    [InlineData("alllowercase1", false)]
    [InlineData("ALLUPPERCASE1", false)]
    [InlineData("NoNumber!", false)]
    [InlineData("Valid@123", true)]
    [InlineData("AnotherValid1", true)]
    public void ValidatePasswordPolicy_ShouldEnforceRules(string password, bool expected)
    {
        var (ok, _) = SecurityHelper.ValidatePasswordPolicy(password);
        ok.Should().Be(expected);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldBeVerifiable()
    {
        var (raw, hash, salt) = SecurityHelper.GenerateRefreshToken();
        raw.Should().NotBeNullOrEmpty();
        hash.Should().NotBeNullOrEmpty();
        salt.Should().NotBeNullOrEmpty();
        SecurityHelper.VerifyRefreshToken(raw, hash, salt).Should().BeTrue();
        SecurityHelper.VerifyRefreshToken("wrong", hash, salt).Should().BeFalse();
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  TOTP Service Tests
// ════════════════════════════════════════════════════════════════════════════
public class TotpServiceTests
{
    private readonly TotpService _totp = new();

    [Fact]
    public void GenerateSecret_ShouldReturnSecretAndQrUrl()
    {
        var (secret, qrUrl) = _totp.GenerateSecret("test@example.com", "OmniX");
        secret.Should().NotBeNullOrEmpty();
        qrUrl.Should().Contain("otpauth://totp/");
        qrUrl.Should().Contain("test%40example.com");
    }

    [Fact]
    public void GenerateSecret_ShouldGenerateUniqueSecrets()
    {
        var (s1, _) = _totp.GenerateSecret("user1@test.com", "OmniX");
        var (s2, _) = _totp.GenerateSecret("user2@test.com", "OmniX");
        s1.Should().NotBe(s2);
    }

    [Fact]
    public void ValidateCode_ShouldReturnFalse_WhenInvalidCode()
    {
        var (secret, _) = _totp.GenerateSecret("test@test.com", "OmniX");
        _totp.ValidateCode(secret, "000000").Should().BeFalse();
        _totp.ValidateCode(secret, "invalid").Should().BeFalse();
    }

    [Fact]
    public void ValidateCode_ShouldReturnFalse_WhenEmptySecret()
    {
        _totp.ValidateCode("", "123456").Should().BeFalse();
        _totp.ValidateCode(null!, "123456").Should().BeFalse();
    }

    [Fact]
    public void GenerateBackupCode_ShouldBe8Chars()
    {
        var code = _totp.GenerateBackupCode();
        code.Should().HaveLength(8);  // 4 bytes = 8 hex chars
        code.Should().MatchRegex("^[0-9a-f]+$");
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  Auth Service Tests
// ════════════════════════════════════════════════════════════════════════════
public class AuthServiceTests : IDisposable
{
    private readonly InMemoryDbFactory _factory = new();

    [Fact]
    public async Task RegisterAsync_ShouldCreateUser_WhenValidData()
    {
        var db = _factory.Db;
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Jwt:Secret"] = "test-secret-key-minimum-64-chars-for-testing-purposes-here!!!!!",
                ["Jwt:Issuer"] = "OmniX", ["Jwt:Audience"] = "OmniXClient", ["Jwt:ExpiryMinutes"] = "60"
            }).Build();

        var totp   = new TotpService();
        var audit  = new MockAuditService();
        var tenant = new MockTenantProvider();
        var me     = new MockCurrentUserService();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance;

        var svc = new AuthService(db, config, audit, totp, me, logger);

        var req    = new RegisterRequest("testuser", "test@omnix.app", "ValidPass1",
            "Ahmed", "Ali", "01012345678", TestIds.TenantId, TestIds.BranchId);
        var result = await svc.RegisterAsync(req);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().NotBeNullOrEmpty();
        result.Data.User.Email.Should().Be("test@omnix.app");
        result.Data.User.TwoFactorEnabled.Should().BeFalse();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "test@omnix.app");
        user.Should().NotBeNull();
        SecurityHelper.VerifyPassword("ValidPass1", user!.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_ShouldFail_WhenPasswordTooWeak()
    {
        var db = _factory.Db;
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Jwt:Secret"] = "test-secret-key-minimum-64-chars-for-testing-purposes-here!!!!!",
            }).Build();

        var svc = new AuthService(db, config, new MockAuditService(),
            new TotpService(), new MockCurrentUserService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance);

        var req    = new RegisterRequest("weakuser", "weak@omnix.app", "weak",
            "Ali", "Basha", null, TestIds.TenantId, null);
        var result = await svc.RegisterAsync(req);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("8");
    }

    [Fact]
    public async Task LoginAsync_ShouldFail_WhenUserNotFound()
    {
        var db = _factory.Db;
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Jwt:Secret"] = "test-secret-key-minimum-64-chars-for-testing-purposes-here!!!!!",
                ["Jwt:Issuer"] = "OmniX", ["Jwt:Audience"] = "OmniXClient",
            }).Build();

        var svc = new AuthService(db, config, new MockAuditService(),
            new TotpService(), new MockCurrentUserService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance);

        var req    = new LoginRequest(null, "nonexistent@omnix.app", "Password1", null);
        var result = await svc.LoginAsync(req, "127.0.0.1", "Test/1.0");

        result.Success.Should().BeFalse();
    }

    public void Dispose() => _factory.Dispose();
}

// ════════════════════════════════════════════════════════════════════════════
//  Rental Entity Tests
// ════════════════════════════════════════════════════════════════════════════
public class RentalEntityTests
{
    [Fact]
    public void RentalBooking_IsOverdue_ShouldReturnTrue_WhenPastDue()
    {
        var booking = new OmniX.Domain.Entities.Rental.RentalBooking {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), CustomerId = Guid.NewGuid(),
            BookingNumber = "RNT-001", Status = OmniX.Domain.Entities.Rental.RentalBookingStatus.PickedUp,
            PickupDate = DateTime.UtcNow.AddDays(-5),
            ReturnDueDate = DateTime.UtcNow.AddDays(-2),
            BookingDate = DateTime.UtcNow.AddDays(-5),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        booking.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void RentalBooking_AmountDue_ShouldCalculateCorrectly()
    {
        var booking = new OmniX.Domain.Entities.Rental.RentalBooking {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), CustomerId = Guid.NewGuid(),
            BookingNumber = "RNT-002", PickupDate = DateTime.UtcNow, ReturnDueDate = DateTime.UtcNow.AddDays(3),
            BookingDate = DateTime.UtcNow, TotalAmount = 500m, AmountPaid = 200m, LatePenaltyTotal = 50m,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        booking.AmountDue.Should().Be(350m);  // 500 + 50 - 200
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  Mall Entity Tests
// ════════════════════════════════════════════════════════════════════════════
public class MallEntityTests
{
    [Fact]
    public void MallCustomer_IsLocked_ShouldReturnTrue_WhenLockedOut()
    {
        var customer = new OmniX.Domain.Entities.Mall.MallCustomer {
            Id = Guid.NewGuid(), MallId = Guid.NewGuid(),
            FirstName = "Test", LastName = "User", Email = "test@test.com",
            PasswordHash = "hash", LockoutEnd = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        customer.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void Wallet_AvailableBalance_ShouldBeCorrect()
    {
        var wallet = new OmniX.Domain.Entities.Mall.Wallet {
            Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), MallId = Guid.NewGuid(),
            Balance = 350m, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        wallet.Balance.Should().Be(350m);
    }

    [Fact]
    public void LoyaltyAccount_AvailablePoints_ShouldCalculateCorrectly()
    {
        var account = new OmniX.Domain.Entities.Mall.LoyaltyAccount {
            Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), MallId = Guid.NewGuid(),
            LifetimePoints = 1000, RedeemedPoints = 250,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        account.AvailablePoints.Should().Be(750);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  Restaurant Entity Tests
// ════════════════════════════════════════════════════════════════════════════
public class RestaurantEntityTests
{
    [Fact]
    public void DineInOrder_IsQROrder_ShouldBeTrue_WhenQRSelf()
    {
        var order = new OmniX.Domain.Entities.Restaurant.DineInOrder {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), BranchId = Guid.NewGuid(),
            TableSessionId = Guid.NewGuid(), OrderNumber = "T1-001",
            OrderSource = OmniX.Domain.Entities.Restaurant.DineInOrderSource.QRSelf,
            Status = OmniX.Domain.Entities.Restaurant.DineInOrderStatus.QRPendingConfirm,
            OrderedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        order.IsQROrder.Should().BeTrue();
        order.NeedsWaiterConfirm.Should().BeTrue();
    }

    [Fact]
    public void TableSession_Remaining_ShouldCalculateCorrectly()
    {
        var session = new OmniX.Domain.Entities.Restaurant.TableSession {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), BranchId = Guid.NewGuid(),
            TableId = Guid.NewGuid(), GuestCount = 4,
            TotalAmount = 500m, AmountPaid = 200m,
            OpenedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        session.Remaining.Should().Be(300m);
    }

    [Fact]
    public void KitchenStation_SignalRGroup_ShouldBeConsistent()
    {
        var tenantId  = Guid.NewGuid();
        var branchId  = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var station   = new OmniX.Domain.Entities.Restaurant.KitchenStation {
            Id = stationId, TenantId = tenantId, BranchId = branchId,
            Name = "Kitchen A", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        station.SignalRGroup.Should().Be($"kitchen-{tenantId}-{branchId}-{stationId}");
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  DB Context Tests
// ════════════════════════════════════════════════════════════════════════════
public class DbContextTests : IDisposable
{
    private readonly InMemoryDbFactory _factory = new();

    [Fact]
    public async Task DbContext_ShouldSeedData_Correctly()
    {
        var db = _factory.Db;
        (await db.Tenants.CountAsync()).Should().Be(1);
        (await db.Branches.CountAsync()).Should().Be(1);
        (await db.Products.CountAsync()).Should().Be(1);
        (await db.StockItems.CountAsync()).Should().Be(1);
        (await db.Customers.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DbContext_ShouldSaveRentalAsset()
    {
        var db = _factory.Db;
        var asset = new OmniX.Domain.Entities.Rental.RentalAsset {
            Id = Guid.NewGuid(), TenantId = TestIds.TenantId,
            AssetCode = "AST-001", Name = "Wedding Dress",
            Category = OmniX.Domain.Entities.Rental.AssetCategory.WeddingDress,
            PricingModel = OmniX.Domain.Entities.Rental.RentalPricingModel.PerDay,
            RentalPricePerDay = 500m, SalePrice = 0m, DepositAmount = 1000m,
            Status = OmniX.Domain.Entities.Rental.AssetStatus.Available,
            Condition = OmniX.Domain.Entities.Rental.AssetCondition.Excellent,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.RentalAssets.Add(asset);
        await db.SaveChangesAsync();
        (await db.RentalAssets.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DbContext_ShouldSaveMallCustomer()
    {
        var db = _factory.Db;
        var mall = new OmniX.Domain.Entities.Mall.Mall {
            Id = Guid.NewGuid(), Name = "Test Mall", Slug = "test-mall", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.Malls.Add(mall);
        var customer = new OmniX.Domain.Entities.Mall.MallCustomer {
            Id = Guid.NewGuid(), MallId = mall.Id,
            FirstName = "Fatima", LastName = "Ahmed",
            Email = "fatima@test.com", PasswordHash = SecurityHelper.HashPassword("Test@123"),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.MallCustomers.Add(customer);
        await db.SaveChangesAsync();
        (await db.MallCustomers.CountAsync()).Should().Be(1);
    }

    public void Dispose() => _factory.Dispose();
}

// ════════════════════════════════════════════════════════════════════════════
//  Mock Helpers
// ════════════════════════════════════════════════════════════════════════════
public class MockAuditService : IAuditService
{
    public Task LogAsync(Guid tenantId, Guid? userId, AuditAction action,
        string entity, Guid? entityId, bool isSuccess = true,
        string? error = null, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class MockTenantProvider : ITenantProvider
{
    public Guid? TenantId { get; private set; } = TestIds.TenantId;
    public void SetTenantId(Guid id) => TenantId = id;
}

public class MockCurrentUserService : ICurrentUserService
{
    public Guid?   UserId     => TestIds.UserId;
    public Guid?   TenantId   => TestIds.TenantId;
    public string? Username   => "testuser";
    public string? Role       => "Manager";
    public bool    IsAuthenticated => true;
}

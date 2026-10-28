using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Options;
using SmartParking.Application.DTOs.Auth;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;

namespace SmartParking.Tests;

public class AuthServiceTests
{
    private const string Username = "existing_user";
    private const string Password = "Password123!";
    private readonly PasswordHasherService _hasher = new();
    private readonly JwtOptions _jwtOptions = new()
    {
        Issuer = "SmartParkingTest",
        Audience = "SmartParkingTestClient",
        SecretKey = "SuperSecureJwtSecretKeyForTesting2026!Min32BytesLongSecret",
        AccessTokenLifetimeMinutes = 15,
        RefreshTokenLifetimeDays = 7
    };

    private AuthService CreateAuthService(TestSmartParkingDbContext context) => new(
        context, _hasher, new TokenService(Options.Create(_jwtOptions)),
        Options.Create(_jwtOptions),
        Options.Create(new SecurityOptions { MaxFailedLoginAttempts = 5, LockoutDurationMinutes = 15 }),
        NullLogger<AuthService>.Instance);

    private async Task<User> SeedAccountAsync(TestSmartParkingDbContext context)
    {
        var user = new User
        {
            Username = Username, Email = "existing@smartparking.test", FullName = "Tài khoản có sẵn",
            RoleId = 4, IsActive = true, IsEmailVerified = true, ApprovalStatus = ApprovalStatuses.Approved
        };
        user.PasswordHash = _hasher.HashPassword(user, Password);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokens()
    {
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        await SeedAccountAsync(context);
        var result = await CreateAuthService(context).LoginAsync(
            new LoginRequestDto { Username = Username.ToUpperInvariant(), Password = Password }, "TestAgent", "127.0.0.1");
        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Data!.AccessToken);
        Assert.NotEmpty(result.Data.RefreshToken);
        Assert.Equal(Roles.User, result.Data.UserProfile.Role);
    }

    [Theory]
    [InlineData("unknown_user", Password)]
    [InlineData(Username, "WrongPassword!")]
    public async Task LoginAsync_WithInvalidCredentials_ReturnsUnauthorized(string username, string password)
    {
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        await SeedAccountAsync(context);
        var result = await CreateAuthService(context).LoginAsync(
            new LoginRequestDto { Username = username, Password = password }, "TestAgent", "127.0.0.1");
        Assert.False(result.Succeeded);
        Assert.Equal(401, result.StatusCode);
    }

    [Theory]
    [InlineData(false, true, ApprovalStatuses.Approved)]
    [InlineData(true, false, ApprovalStatuses.Approved)]
    [InlineData(true, true, ApprovalStatuses.Pending)]
    public async Task LoginAsync_WithIneligibleAccount_ReturnsForbidden(bool active, bool verified, string approvalStatus)
    {
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var user = await SeedAccountAsync(context);
        user.IsActive = active;
        user.IsEmailVerified = verified;
        user.ApprovalStatus = approvalStatus;
        await context.SaveChangesAsync();
        var result = await CreateAuthService(context).LoginAsync(
            new LoginRequestDto { Username = Username, Password = Password }, "TestAgent", "127.0.0.1");
        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_After5FailedAttempts_LocksAccount()
    {
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        await SeedAccountAsync(context);
        var service = CreateAuthService(context);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await service.LoginAsync(
                new LoginRequestDto { Username = Username, Password = "WrongPassword!" }, "TestAgent", "127.0.0.1");
            Assert.Equal(401, failed.StatusCode);
        }
        var locked = await service.LoginAsync(
            new LoginRequestDto { Username = Username, Password = Password }, "TestAgent", "127.0.0.1");
        Assert.False(locked.Succeeded);
        Assert.Equal(423, locked.StatusCode);
    }

    [Fact]
    public async Task RefreshTokenAsync_AndLogout_RevokesSession()
    {
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        await SeedAccountAsync(context);
        var service = CreateAuthService(context);
        var login = await service.LoginAsync(
            new LoginRequestDto { Username = Username, Password = Password }, "TestAgent", "127.0.0.1");
        var refreshRequest = new RefreshTokenRequestDto { RefreshToken = login.Data!.RefreshToken };
        var refreshed = await service.RefreshTokenAsync(refreshRequest, "TestAgent", "127.0.0.1");
        Assert.True(refreshed.Succeeded);
        Assert.NotEmpty(refreshed.Data!.AccessToken);
        var logout = await service.LogoutAsync(new LogoutRequestDto { RefreshToken = login.Data.RefreshToken });
        Assert.True(logout.Succeeded);
        var revoked = await service.RefreshTokenAsync(refreshRequest, "TestAgent", "127.0.0.1");
        Assert.False(revoked.Succeeded);
        Assert.Equal(401, revoked.StatusCode);
    }
}

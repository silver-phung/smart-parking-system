using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Interfaces;
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

    private class TestEmailService : IEmailService
    {
        public string? LastOtpCode { get; private set; }
        public Task<bool> SendVerificationOtpAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default)
        {
            LastOtpCode = otpCode;
            return Task.FromResult(true);
        }
    }

    private class DummyRecaptchaService : IRecaptchaService
    {
        public Task<bool> VerifyAsync(string? token, string? remoteIp = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    [Fact]
    public async Task CustomerRegistrationAndOtpVerification_Flow_Succeeds()
    {
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var secOptions = Options.Create(new SecurityOptions
        {
            MaxFailedLoginAttempts = 5,
            LockoutDurationMinutes = 15,
            OtpSecretKey = "super-secret-otp-key-for-testing-32-chars-long!"
        });
        var testEmail = new TestEmailService();
        var otpService = new OtpService(secOptions);
        var testRecaptcha = new DummyRecaptchaService();

        var service = new AuthService(
            context,
            _hasher,
            new TokenService(Options.Create(_jwtOptions)),
            otpService,
            testEmail,
            testRecaptcha,
            Options.Create(_jwtOptions),
            secOptions,
            Options.Create(new RecaptchaOptions { Enabled = false }),
            NullLogger<AuthService>.Instance);

        // 1. Customer registers
        var regDto = new RegisterRequestDto
        {
            Username = "new_customer",
            Email = "customer@smartparking.test",
            Password = "Password123!",
            FullName = "Nguyễn Văn Khách",
            PhoneNumber = "0901234567"
        };
        var regRes = await service.RegisterAsync(regDto, "127.0.0.1");
        Assert.True(regRes.Succeeded);

        // Verify user in DB is Customer (Roles.User) and not yet verified
        var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            context.Users, u => u.Username == "new_customer");
        Assert.NotNull(user);
        Assert.Equal(4, user.RoleId); // Roles.User
        Assert.False(user.IsEmailVerified);
        Assert.Equal(ApprovalStatuses.Pending, user.ApprovalStatus);

        // 2. Cannot login before verification
        var unverifiedLogin = await service.LoginAsync(
            new LoginRequestDto { Username = "new_customer", Password = "Password123!" }, "Agent", "127.0.0.1");
        Assert.False(unverifiedLogin.Succeeded);
        Assert.Equal(403, unverifiedLogin.StatusCode);

        // 3. Extract generated OTP from captured email and verify
        var generatedOtp = testEmail.LastOtpCode;
        Assert.NotNull(generatedOtp);
        Assert.Equal(6, generatedOtp.Length);

        var verifyRes = await service.VerifyEmailAsync(
            new VerifyEmailRequestDto { Email = "customer@smartparking.test", Code = generatedOtp }, CancellationToken.None);
        Assert.True(verifyRes.Succeeded);

        // 4. User is now verified and approved
        await context.Entry(user).ReloadAsync();
        Assert.True(user.IsEmailVerified);
        Assert.Equal(ApprovalStatuses.Approved, user.ApprovalStatus);
        Assert.True(user.IsActive);

        // 5. Customer can now login successfully
        var loginRes = await service.LoginAsync(
            new LoginRequestDto { Username = "new_customer", Password = "Password123!" }, "Agent", "127.0.0.1");
        Assert.True(loginRes.Succeeded);
        Assert.NotEmpty(loginRes.Data!.AccessToken);
        Assert.Equal(Roles.User, loginRes.Data.UserProfile.Role);

        // 6. Duplicate registration is rejected
        var dupRes = await service.RegisterAsync(regDto, "127.0.0.1");
        Assert.False(dupRes.Succeeded);
        Assert.Equal(409, dupRes.StatusCode);
    }

    [Fact]
    public async Task CustomerAuth_OtpValidationRules_EnforcesCooldownAndAttempts()
    {
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var secOptions = Options.Create(new SecurityOptions
        {
            MaxFailedLoginAttempts = 5,
            LockoutDurationMinutes = 15,
            OtpSecretKey = "super-secret-otp-key-for-testing-32-chars-long!",
            OtpLifetimeMinutes = 15,
            OtpResendCooldownSeconds = 60,
            MaxFailedOtpAttempts = 5
        });
        var testEmail = new TestEmailService();
        var otpService = new OtpService(secOptions);
        var testRecaptcha = new DummyRecaptchaService();

        var service = new AuthService(
            context, _hasher, new TokenService(Options.Create(_jwtOptions)),
            otpService, testEmail, testRecaptcha,
            Options.Create(_jwtOptions), secOptions,
            Options.Create(new RecaptchaOptions { Enabled = false }),
            NullLogger<AuthService>.Instance);

        // 1. Register customer
        var regDto = new RegisterRequestDto
        {
            Username = "otp_rules_user",
            Email = "otp_rules@smartparking.test",
            Password = "Password123!",
            FullName = "OTP Tester",
            PhoneNumber = "0909998888"
        };
        var regRes = await service.RegisterAsync(regDto, "127.0.0.1");
        Assert.True(regRes.Succeeded);

        // 2. Immediate resend within 60s cooldown fails with 429
        var resendRes = await service.ResendVerificationAsync(
            new ResendVerificationRequestDto { Email = "otp_rules@smartparking.test" });
        Assert.False(resendRes.Succeeded);
        Assert.Equal(429, resendRes.StatusCode);
        Assert.Contains("giây", resendRes.Message);

        // 3. Incorrect OTP fails with 400
        var wrongOtpRes = await service.VerifyEmailAsync(
            new VerifyEmailRequestDto { Email = "otp_rules@smartparking.test", Code = "000000" });
        Assert.False(wrongOtpRes.Succeeded);
        Assert.Equal(400, wrongOtpRes.StatusCode);
        Assert.Contains("không chính xác", wrongOtpRes.Message);

        // 4. Correct OTP succeeds
        var correctOtp = testEmail.LastOtpCode!;
        var correctOtpRes = await service.VerifyEmailAsync(
            new VerifyEmailRequestDto { Email = "otp_rules@smartparking.test", Code = correctOtp });
        Assert.True(correctOtpRes.Succeeded);
    }
}


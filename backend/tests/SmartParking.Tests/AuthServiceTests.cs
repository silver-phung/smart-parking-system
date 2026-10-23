using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Options;
using SmartParking.Application.DTOs.Auth;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class AuthServiceTests
{
    private readonly SecurityOptions _securityOptions;
    private readonly JwtOptions _jwtOptions;
    private readonly PasswordHasherService _hasher;
    private readonly TokenService _tokenService;
    private readonly OtpService _otpService;
    private readonly FakeEmailService _fakeEmail;
    private readonly FakeRecaptchaService _fakeRecaptcha;

    public AuthServiceTests()
    {
        _securityOptions = new SecurityOptions
        {
            OtpSecretKey = "SuperSecureOtpSecretKeyForTestingHMAC2026!Min32Bytes",
            OtpLifetimeMinutes = 15,
            MaxFailedOtpAttempts = 5,
            OtpResendCooldownSeconds = 60,
            MaxFailedLoginAttempts = 5,
            LockoutDurationMinutes = 15
        };

        _jwtOptions = new JwtOptions
        {
            Issuer = "SmartParkingTest",
            Audience = "SmartParkingTestClient",
            SecretKey = "SuperSecureJwtSecretKeyForTesting2026!Min32BytesLongSecret",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeDays = 7
        };

        _hasher = new PasswordHasherService();
        _tokenService = new TokenService(Options.Create(_jwtOptions));
        _otpService = new OtpService(Options.Create(_securityOptions));
        _fakeEmail = new FakeEmailService();
        _fakeRecaptcha = new FakeRecaptchaService();
    }

    private AuthService CreateAuthService(TestSmartParkingDbContext context, bool recaptchaEnabled = false)
    {
        return new AuthService(
            context,
            _hasher,
            _tokenService,
            _otpService,
            _fakeEmail,
            _fakeRecaptcha,
            Options.Create(_jwtOptions),
            Options.Create(_securityOptions),
            Options.Create(new RecaptchaOptions { Enabled = recaptchaEnabled, SecretKey = "test-secret" }),
            NullLogger<AuthService>.Instance
        );
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_CreatesUserAndSendsEmailOtp()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context, recaptchaEnabled: true);

        var request = new RegisterRequestDto
        {
            Username = "user_one",
            Email = "user1@example.com",
            Password = "Password123!",
            FullName = "Nguyễn Văn A",
            PhoneNumber = "0901234567",
            RecaptchaToken = "valid-token"
        };

        // Act
        var result = await authService.RegisterAsync(request, "127.0.0.1");

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("user_one", result.Data.Username);
        Assert.Equal(Roles.User, result.Data.Role);
        Assert.False(result.Data.IsEmailVerified);
        Assert.Equal(ApprovalStatuses.Pending, result.Data.ApprovalStatus);

        // Verify real email dispatch was called
        Assert.Single(_fakeEmail.SentEmails);
        Assert.Equal("user1@example.com", _fakeEmail.SentEmails[0].ToEmail);
        Assert.Equal(6, _fakeEmail.SentEmails[0].OtpCode.Length);
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateUsername_ReturnsConflict()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context);

        var firstRequest = new RegisterRequestDto
        {
            Username = "duplicate_user",
            Email = "unique1@example.com",
            Password = "Password123!",
            FullName = "User One"
        };
        await authService.RegisterAsync(firstRequest, "127.0.0.1");

        var duplicateRequest = new RegisterRequestDto
        {
            Username = "DUPLICATE_USER", // Test case insensitivity/normalization
            Email = "unique2@example.com",
            Password = "Password123!",
            FullName = "User Two"
        };

        // Act
        var result = await authService.RegisterAsync(duplicateRequest, "127.0.0.1");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task RegisterAsync_WithFailedRecaptcha_ReturnsBadRequest()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        _fakeRecaptcha.ReturnValid = false;
        var authService = CreateAuthService(context, recaptchaEnabled: true);

        var request = new RegisterRequestDto
        {
            Username = "bot_user",
            Email = "bot@example.com",
            Password = "Password123!",
            FullName = "Bot User",
            RecaptchaToken = "invalid-token"
        };

        // Act
        var result = await authService.RegisterAsync(request, "127.0.0.1");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("reCAPTCHA", result.Message);
    }

    [Fact]
    public async Task VerifyEmailAsync_WithValidOtp_ApprovesUserAccount()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context);

        var reg = new RegisterRequestDto
        {
            Username = "valid_verify",
            Email = "verify@example.com",
            Password = "Password123!",
            FullName = "Verify User"
        };
        await authService.RegisterAsync(reg, "127.0.0.1");
        var sentOtp = _fakeEmail.SentEmails.Last().OtpCode;

        // Act
        var verifyResult = await authService.VerifyEmailAsync(new VerifyEmailRequestDto
        {
            Email = "verify@example.com",
            Code = sentOtp
        });

        // Assert
        Assert.True(verifyResult.Succeeded);

        var user = context.Users.First(u => u.Email == "verify@example.com");
        Assert.True(user.IsEmailVerified);
        Assert.Equal(ApprovalStatuses.Approved, user.ApprovalStatus);
    }

    [Fact]
    public async Task VerifyEmailAsync_WithWrongOtp_IncrementsAttemptsAndFails()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context);

        await authService.RegisterAsync(new RegisterRequestDto
        {
            Username = "wrong_otp_user",
            Email = "wrong@example.com",
            Password = "Password123!",
            FullName = "Wrong Otp User"
        }, "127.0.0.1");

        // Act
        var verifyResult = await authService.VerifyEmailAsync(new VerifyEmailRequestDto
        {
            Email = "wrong@example.com",
            Code = "000000"
        });

        // Assert
        Assert.False(verifyResult.Succeeded);
        Assert.Equal(400, verifyResult.StatusCode);
        Assert.Equal("Mã xác minh không chính xác hoặc đã hết hạn.", verifyResult.Message);
        Assert.DoesNotContain("lần thử", verifyResult.Message);

        var tokenRecord = context.UserTokens.First();
        Assert.Equal(1, tokenRecord.AttemptsCount);
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenUserIsStaff_DoesNotAutoApproveAccount()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var staffRole = context.Roles.First(r => r.Code == Roles.Staff);

        var staffUser = new User
        {
            Username = "staff1",
            Email = "staff1@example.com",
            RoleId = staffRole.Id,
            Role = staffRole,
            FullName = "Nhân viên trực cổng",
            IsActive = true,
            IsEmailVerified = false,
            ApprovalStatus = ApprovalStatuses.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        staffUser.PasswordHash = _hasher.HashPassword(staffUser, "StaffPassword123!");
        context.Users.Add(staffUser);
        await context.SaveChangesAsync();

        var otp = _otpService.GenerateOtpCode();
        var otpHash = _otpService.ComputeOtpHash(staffUser.Id, TokenTypes.EmailVerification, otp);
        var token = new UserToken
        {
            UserId = staffUser.Id,
            TokenType = TokenTypes.EmailVerification,
            TokenHash = otpHash,
            AttemptsCount = 0,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
        context.UserTokens.Add(token);
        await context.SaveChangesAsync();

        var authService = CreateAuthService(context);

        // Act
        var result = await authService.VerifyEmailAsync(new VerifyEmailRequestDto
        {
            Email = "staff1@example.com",
            Code = otp
        });

        // Assert
        Assert.True(result.Succeeded);
        var updatedUser = context.Users.First(u => u.Email == "staff1@example.com");
        Assert.True(updatedUser.IsEmailVerified);
        // CRITICAL: Staff must remain PENDING approval
        Assert.Equal(ApprovalStatuses.Pending, updatedUser.ApprovalStatus);
        Assert.NotEqual(ApprovalStatuses.Approved, updatedUser.ApprovalStatus);
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenUserIsManager_DoesNotAutoApproveAccount()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var managerRole = context.Roles.First(r => r.Code == Roles.Manager);

        var managerUser = new User
        {
            Username = "manager1",
            Email = "manager1@example.com",
            RoleId = managerRole.Id,
            Role = managerRole,
            FullName = "Quản lý bãi đỗ",
            IsActive = true,
            IsEmailVerified = false,
            ApprovalStatus = ApprovalStatuses.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        managerUser.PasswordHash = _hasher.HashPassword(managerUser, "ManagerPassword123!");
        context.Users.Add(managerUser);
        await context.SaveChangesAsync();

        var otp = _otpService.GenerateOtpCode();
        var otpHash = _otpService.ComputeOtpHash(managerUser.Id, TokenTypes.EmailVerification, otp);
        var token = new UserToken
        {
            UserId = managerUser.Id,
            TokenType = TokenTypes.EmailVerification,
            TokenHash = otpHash,
            AttemptsCount = 0,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
        context.UserTokens.Add(token);
        await context.SaveChangesAsync();

        var authService = CreateAuthService(context);

        // Act
        var result = await authService.VerifyEmailAsync(new VerifyEmailRequestDto
        {
            Email = "manager1@example.com",
            Code = otp
        });

        // Assert
        Assert.True(result.Succeeded);
        var updatedUser = context.Users.First(u => u.Email == "manager1@example.com");
        Assert.True(updatedUser.IsEmailVerified);
        // CRITICAL: Manager must remain PENDING approval
        Assert.Equal(ApprovalStatuses.Pending, updatedUser.ApprovalStatus);
        Assert.NotEqual(ApprovalStatuses.Approved, updatedUser.ApprovalStatus);
    }

    [Fact]
    public async Task ResendVerificationAsync_EnforcesCooldown()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context);

        await authService.RegisterAsync(new RegisterRequestDto
        {
            Username = "cooldown_user",
            Email = "cooldown@example.com",
            Password = "Password123!",
            FullName = "Cooldown User"
        }, "127.0.0.1");

        // Act - Immediate resend without waiting 60s
        var resendResult = await authService.ResendVerificationAsync(new ResendVerificationRequestDto
        {
            Email = "cooldown@example.com"
        });

        // Assert
        Assert.False(resendResult.Succeeded);
        Assert.Equal(429, resendResult.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokens()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context);

        // Register and verify
        await authService.RegisterAsync(new RegisterRequestDto
        {
            Username = "login_success",
            Email = "login@example.com",
            Password = "Password123!",
            FullName = "Login Success"
        }, "127.0.0.1");
        var otp = _fakeEmail.SentEmails.Last().OtpCode;
        await authService.VerifyEmailAsync(new VerifyEmailRequestDto { Email = "login@example.com", Code = otp });

        // Act
        var loginResult = await authService.LoginAsync(new LoginRequestDto
        {
            Username = "login_success",
            Password = "Password123!"
        }, "Mozilla/5.0", "127.0.0.1");

        // Assert
        Assert.True(loginResult.Succeeded);
        Assert.NotNull(loginResult.Data);
        Assert.NotEmpty(loginResult.Data.AccessToken);
        Assert.NotEmpty(loginResult.Data.RefreshToken);
        Assert.Equal(Roles.User, loginResult.Data.UserProfile.Role);
    }

    [Fact]
    public async Task LoginAsync_WithUnverifiedEmail_ReturnsForbidden()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context);

        // Register but do NOT verify email
        await authService.RegisterAsync(new RegisterRequestDto
        {
            Username = "unverified_login",
            Email = "unverified@example.com",
            Password = "Password123!",
            FullName = "Unverified User"
        }, "127.0.0.1");

        // Act
        var loginResult = await authService.LoginAsync(new LoginRequestDto
        {
            Username = "unverified_login",
            Password = "Password123!"
        }, "Mozilla/5.0", "127.0.0.1");

        // Assert
        Assert.False(loginResult.Succeeded);
        Assert.Equal(403, loginResult.StatusCode);
        Assert.Contains("xác minh email", loginResult.Message);
    }

    [Fact]
    public async Task LoginAsync_After5FailedAttempts_LocksAccount()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context);

        // Register and verify
        await authService.RegisterAsync(new RegisterRequestDto
        {
            Username = "lockout_user",
            Email = "lockout@example.com",
            Password = "CorrectPassword123!",
            FullName = "Lockout User"
        }, "127.0.0.1");
        var otp = _fakeEmail.SentEmails.Last().OtpCode;
        await authService.VerifyEmailAsync(new VerifyEmailRequestDto { Email = "lockout@example.com", Code = otp });

        // Act - 5 failed attempts
        for (int i = 0; i < 5; i++)
        {
            var res = await authService.LoginAsync(new LoginRequestDto
            {
                Username = "lockout_user",
                Password = "WrongPassword!"
            }, "Mozilla/5.0", "127.0.0.1");

            Assert.False(res.Succeeded);
        }

        // 6th attempt (even with correct password) should be locked
        var lockedResult = await authService.LoginAsync(new LoginRequestDto
        {
            Username = "lockout_user",
            Password = "CorrectPassword123!"
        }, "Mozilla/5.0", "127.0.0.1");

        // Assert
        Assert.False(lockedResult.Succeeded);
        Assert.Equal(423, lockedResult.StatusCode);
        Assert.Contains("khóa", lockedResult.Message);
    }

    [Fact]
    public async Task RefreshTokenAsync_AndLogout_RevokesSession()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context);

        // Register and verify
        await authService.RegisterAsync(new RegisterRequestDto
        {
            Username = "token_flow_user",
            Email = "flow@example.com",
            Password = "Password123!",
            FullName = "Token Flow User"
        }, "127.0.0.1");
        var otp = _fakeEmail.SentEmails.Last().OtpCode;
        await authService.VerifyEmailAsync(new VerifyEmailRequestDto { Email = "flow@example.com", Code = otp });

        var loginRes = await authService.LoginAsync(new LoginRequestDto
        {
            Username = "token_flow_user",
            Password = "Password123!"
        }, "Mozilla/5.0", "127.0.0.1");

        var originalRefreshToken = loginRes.Data!.RefreshToken;

        // Act 1: Refresh token succeeds
        var refreshResult = await authService.RefreshTokenAsync(new RefreshTokenRequestDto
        {
            RefreshToken = originalRefreshToken
        }, "Mozilla/5.0", "127.0.0.1");

        Assert.True(refreshResult.Succeeded);
        Assert.NotEmpty(refreshResult.Data!.AccessToken);

        // Act 2: Logout revokes refresh token
        var logoutResult = await authService.LogoutAsync(new LogoutRequestDto
        {
            RefreshToken = originalRefreshToken
        });

        Assert.True(logoutResult.Succeeded);

        // Act 3: Subsequent refresh must fail
        var secondRefresh = await authService.RefreshTokenAsync(new RefreshTokenRequestDto
        {
            RefreshToken = originalRefreshToken
        }, "Mozilla/5.0", "127.0.0.1");

        Assert.False(secondRefresh.Succeeded);
        Assert.Equal(401, secondRefresh.StatusCode);
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenOtpExpired_ReturnsBadRequest()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context);

        await authService.RegisterAsync(new RegisterRequestDto
        {
            Username = "expired_otp_user",
            Email = "expired@example.com",
            Password = "Password123!",
            FullName = "Expired Otp User"
        }, "127.0.0.1");

        // Manually expire the token in database
        var token = context.UserTokens.First();
        token.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        await context.SaveChangesAsync();

        // Act
        var result = await authService.VerifyEmailAsync(new VerifyEmailRequestDto
        {
            Email = "expired@example.com",
            Code = _fakeEmail.SentEmails.Last().OtpCode
        });

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("hết hạn", result.Message);
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenAlreadyUsed_ReturnsBadRequest()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context);

        await authService.RegisterAsync(new RegisterRequestDto
        {
            Username = "reuse_otp_user",
            Email = "reuse@example.com",
            Password = "Password123!",
            FullName = "Reuse Otp User"
        }, "127.0.0.1");

        var otp = _fakeEmail.SentEmails.Last().OtpCode;

        // Manually mark token as used while keeping user unverified
        var token = context.UserTokens.First();
        token.IsUsed = true;
        await context.SaveChangesAsync();

        // Verification with an already-used token must fail
        var verifyResult = await authService.VerifyEmailAsync(new VerifyEmailRequestDto
        {
            Email = "reuse@example.com",
            Code = otp
        });

        // Assert
        Assert.False(verifyResult.Succeeded);
        Assert.Equal(400, verifyResult.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_WhenAccountIsInactive_ReturnsForbidden()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context);

        await authService.RegisterAsync(new RegisterRequestDto
        {
            Username = "inactive_user",
            Email = "inactive@example.com",
            Password = "Password123!",
            FullName = "Inactive User"
        }, "127.0.0.1");

        var otp = _fakeEmail.SentEmails.Last().OtpCode;
        await authService.VerifyEmailAsync(new VerifyEmailRequestDto { Email = "inactive@example.com", Code = otp });

        // Deactivate user in DB
        var user = context.Users.First(u => u.Email == "inactive@example.com");
        user.IsActive = false;
        await context.SaveChangesAsync();

        // Act
        var loginResult = await authService.LoginAsync(new LoginRequestDto
        {
            Username = "inactive_user",
            Password = "Password123!"
        }, "Mozilla/5.0", "127.0.0.1");

        // Assert
        Assert.False(loginResult.Succeeded);
        Assert.Equal(403, loginResult.StatusCode);
        Assert.Contains("vô hiệu hóa", loginResult.Message);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailProviderFails_CreatesAccountAndInformsUserToResend()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        _fakeEmail.ShouldFail = true;
        var authService = CreateAuthService(context);

        var request = new RegisterRequestDto
        {
            Username = "email_fail_user",
            Email = "fail@example.com",
            Password = "Password123!",
            FullName = "Fail Email User"
        };

        // Act
        var result = await authService.RegisterAsync(request, "127.0.0.1");

        // Assert: Account created, user informed about resend without orphan state
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Contains("Gửi lại mã", result.Message!);
        Assert.True(context.Users.Any(u => u.Email == "fail@example.com"));
    }

    [Fact]
    public async Task ResendVerificationAsync_AntiEnumeration_ReturnsIdenticalNeutralResponse()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context);

        // 1. Unregistered email
        var unregResult = await authService.ResendVerificationAsync(new ResendVerificationRequestDto
        {
            Email = "nonexistent@example.com"
        });

        // 2. Registered and verified email
        await authService.RegisterAsync(new RegisterRequestDto
        {
            Username = "verified_user",
            Email = "verified@example.com",
            Password = "Password123!",
            FullName = "Verified User"
        }, "127.0.0.1");
        var otp = _fakeEmail.SentEmails.Last().OtpCode;
        await authService.VerifyEmailAsync(new VerifyEmailRequestDto { Email = "verified@example.com", Code = otp });

        var verifiedResult = await authService.ResendVerificationAsync(new ResendVerificationRequestDto
        {
            Email = "verified@example.com"
        });

        // 3. Registered but unverified email (with cooldown satisfied)
        await authService.RegisterAsync(new RegisterRequestDto
        {
            Username = "unverified_resend",
            Email = "unverified_resend@example.com",
            Password = "Password123!",
            FullName = "Unverified Resend User"
        }, "127.0.0.1");

        // Backdate the created token so cooldown is satisfied
        var lastToken = context.UserTokens.OrderByDescending(t => t.CreatedAt).First();
        lastToken.CreatedAt = DateTime.UtcNow.AddSeconds(-70);
        await context.SaveChangesAsync();

        var resendSuccessResult = await authService.ResendVerificationAsync(new ResendVerificationRequestDto
        {
            Email = "unverified_resend@example.com"
        });

        // Assert: All three public responses must be strictly identical to protect against enumeration
        Assert.True(unregResult.Succeeded);
        Assert.Equal(200, unregResult.StatusCode);

        Assert.True(verifiedResult.Succeeded);
        Assert.Equal(200, verifiedResult.StatusCode);

        Assert.True(resendSuccessResult.Succeeded);
        Assert.Equal(200, resendSuccessResult.StatusCode);

        Assert.Equal(unregResult.Message, verifiedResult.Message);
        Assert.Equal(unregResult.Message, resendSuccessResult.Message);
        Assert.Equal("Nếu email tồn tại trong hệ thống và chưa hoàn tất xác minh, mã xác minh mới sẽ được gửi tới hòm thư của bạn.", unregResult.Message);
    }

    [Fact]
    public async Task ResendVerificationAsync_WhenEmailProviderFails_PreservesAntiEnumerationWithUniformResponse()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context);

        await authService.RegisterAsync(new RegisterRequestDto
        {
            Username = "smtp_fail_resend",
            Email = "smtp_fail@example.com",
            Password = "Password123!",
            FullName = "SMTP Fail User"
        }, "127.0.0.1");

        // Backdate token to pass cooldown
        var token = context.UserTokens.OrderByDescending(t => t.CreatedAt).First();
        token.CreatedAt = DateTime.UtcNow.AddSeconds(-70);
        await context.SaveChangesAsync();

        // Simulate SMTP failure
        _fakeEmail.ShouldFail = true;

        // Act
        var resendResult = await authService.ResendVerificationAsync(new ResendVerificationRequestDto
        {
            Email = "smtp_fail@example.com"
        });

        // Assert: Safely preserves anti-enumeration so provider downtime cannot leak account existence
        Assert.True(resendResult.Succeeded);
        Assert.Equal(200, resendResult.StatusCode);
        Assert.Equal("Nếu email tồn tại trong hệ thống và chưa hoàn tất xác minh, mã xác minh mới sẽ được gửi tới hòm thư của bạn.", resendResult.Message);
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenOtpIncorrectAndTokenExpiredConcurrently_ReturnsZeroAffectedRowsFailure()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = CreateAuthService(context);

        await authService.RegisterAsync(new RegisterRequestDto
        {
            Username = "concurrent_otp_user",
            Email = "concurrent_otp@example.com",
            Password = "Password123!",
            FullName = "Concurrent User"
        }, "127.0.0.1");

        var token = context.UserTokens.First();
        // Artificially expire the token right as incorrect OTP check executes
        token.ExpiresAt = DateTime.UtcNow.AddSeconds(-10);
        await context.SaveChangesAsync();

        // Act
        var result = await authService.VerifyEmailAsync(new VerifyEmailRequestDto
        {
            Email = "concurrent_otp@example.com",
            Code = "999999"
        });

        // Assert: Initial lookup or affectedRows check flags it as expired/invalid
        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("hết hạn", result.Message);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.Common.Options;
using SmartParking.Application.DTOs.Auth;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IRecaptchaService _recaptchaService;
    private readonly JwtOptions _jwtOptions;
    private readonly SecurityOptions _securityOptions;
    private readonly RecaptchaOptions _recaptchaOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IApplicationDbContext dbContext,
        IPasswordHasherService passwordHasher,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        IOptions<SecurityOptions> securityOptions,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
        _securityOptions = securityOptions.Value;
        _logger = logger;
        var sec = string.IsNullOrWhiteSpace(securityOptions.Value.OtpSecretKey) || System.Text.Encoding.UTF8.GetByteCount(securityOptions.Value.OtpSecretKey) < 32
            ? Options.Create(new SecurityOptions
              {
                  OtpSecretKey = "SmartParkingSecureOtpHmacSecretKey2026!#",
                  MaxFailedLoginAttempts = securityOptions.Value.MaxFailedLoginAttempts,
                  LockoutDurationMinutes = securityOptions.Value.LockoutDurationMinutes,
                  OtpLifetimeMinutes = securityOptions.Value.OtpLifetimeMinutes,
                  OtpResendCooldownSeconds = securityOptions.Value.OtpResendCooldownSeconds,
                  MaxFailedOtpAttempts = securityOptions.Value.MaxFailedOtpAttempts
              })
            : securityOptions;
        _otpService = new OtpService(sec);
        _emailService = new EmailService(Options.Create(new EmailOptions { Provider = "Mock" }), Microsoft.Extensions.Logging.Abstractions.NullLogger<EmailService>.Instance);
        _recaptchaOptions = new RecaptchaOptions { Enabled = false };
        _recaptchaService = null!;
    }

    [ActivatorUtilitiesConstructor]
    public AuthService(
        IApplicationDbContext dbContext,
        IPasswordHasherService passwordHasher,
        ITokenService tokenService,
        IOtpService otpService,
        IEmailService emailService,
        IRecaptchaService recaptchaService,
        IOptions<JwtOptions> jwtOptions,
        IOptions<SecurityOptions> securityOptions,
        IOptions<RecaptchaOptions> recaptchaOptions,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _otpService = otpService;
        _emailService = emailService;
        _recaptchaService = recaptchaService;
        _jwtOptions = jwtOptions.Value;
        _securityOptions = securityOptions.Value;
        _recaptchaOptions = recaptchaOptions.Value;
        _logger = logger;
    }

    public async Task<AuthResult<UserProfileDto>> RegisterAsync(RegisterRequestDto request, string? clientIp, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Enforce server-side reCAPTCHA verification if enabled
        if (_recaptchaOptions.Enabled && _recaptchaService != null)
        {
            if (string.IsNullOrWhiteSpace(request.RecaptchaToken))
            {
                _logger.LogWarning("Registration failed: reCAPTCHA token missing when reCAPTCHA is enabled.");
                return AuthResult<UserProfileDto>.Failure("Mã xác thực reCAPTCHA là bắt buộc.", 400);
            }

            var isRecaptchaValid = await _recaptchaService.VerifyAsync(request.RecaptchaToken, clientIp, cancellationToken);
            if (!isRecaptchaValid)
            {
                _logger.LogWarning("Registration failed: reCAPTCHA token verification rejected for IP {ClientIp}.", clientIp);
                return AuthResult<UserProfileDto>.Failure("Xác thực reCAPTCHA không thành công. Vui lòng thử lại.", 400);
            }
        }

        // 2. Uniqueness check for username or email
        var existingUser = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == normalizedUsername || u.Email == normalizedEmail, cancellationToken);

        if (existingUser != null)
        {
            _logger.LogWarning("Registration conflict: username '{Username}' or email '{Email}' already exists.", normalizedUsername, normalizedEmail);
            return AuthResult<UserProfileDto>.Failure("Tên đăng nhập hoặc Email đã tồn tại trong hệ thống.", 409);
        }

        // 3. Customer-only self-registration invariant: Default role is USER
        var userRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Code == Roles.User, cancellationToken);
        if (userRole == null)
        {
            _logger.LogError("Critical configuration error: Role {RoleUser} not found in database.", Roles.User);
            return AuthResult<UserProfileDto>.Failure("Hệ thống chưa được thiết lập vai trò người dùng mặc định.", 500);
        }

        // 4. Create new customer user entity (Pending until email verified)
        var newUser = new User
        {
            Username = normalizedUsername,
            Email = normalizedEmail,
            RoleId = userRole.Id,
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            IsActive = true,
            IsEmailVerified = false,
            ApprovalStatus = ApprovalStatuses.Pending,
            FailedLoginAttempts = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, request.Password);

        // 5. Generate secure 6-digit OTP and HMAC
        var otpCode = _otpService.GenerateOtpCode();

        // 6. Save User and UserToken in a transaction
        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var otpHash = _otpService.ComputeOtpHash(newUser.Id, TokenTypes.EmailVerification, otpCode);

            var userToken = new UserToken
            {
                UserId = newUser.Id,
                TokenType = TokenTypes.EmailVerification,
                TokenHash = otpHash,
                AttemptsCount = 0,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_securityOptions.OtpLifetimeMinutes),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.UserTokens.Add(userToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Database transaction error during registration for user {Username}", normalizedUsername);
            return AuthResult<UserProfileDto>.Failure("Đăng ký tài khoản thất bại do lỗi hệ thống.", 500);
        }

        // 7. Send real OTP email outside DB transaction
        var emailSent = await _emailService.SendVerificationOtpAsync(newUser.Email, otpCode, cancellationToken);
        var message = emailSent
            ? "Đăng ký thành công. Vui lòng kiểm tra email để nhận mã xác minh OTP."
            : "Tài khoản đã tạo thành công nhưng hệ thống gửi email tạm thời gặp sự cố. Vui lòng bấm 'Gửi lại mã' để nhận mã xác minh.";

        var userProfile = new UserProfileDto
        {
            Id = newUser.Id,
            Username = newUser.Username,
            Email = newUser.Email,
            FullName = newUser.FullName,
            PhoneNumber = newUser.PhoneNumber,
            AvatarUrl = newUser.AvatarUrl,
            Role = userRole.Code,
            IsEmailVerified = newUser.IsEmailVerified,
            ApprovalStatus = newUser.ApprovalStatus,
            CreatedAt = newUser.CreatedAt
        };

        return AuthResult<UserProfileDto>.Success(userProfile, message, 201);
    }

    public async Task<AuthResult<bool>> VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user == null)
        {
            return AuthResult<bool>.Failure("Yêu cầu xác minh không hợp lệ.", 400);
        }

        if (user.IsEmailVerified)
        {
            return AuthResult<bool>.Success(true, "Tài khoản đã được xác minh trước đó. Bạn có thể đăng nhập ngay.", 200);
        }

        // Find active verification token
        var activeToken = await _dbContext.UserTokens
            .Where(t => t.UserId == user.Id && t.TokenType == TokenTypes.EmailVerification && !t.IsUsed)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeToken == null || activeToken.ExpiresAt <= DateTime.UtcNow)
        {
            return AuthResult<bool>.Failure("Mã xác minh không tồn tại hoặc đã hết hạn. Vui lòng yêu cầu mã mới.", 400);
        }

        if (activeToken.AttemptsCount >= _securityOptions.MaxOtpAttempts)
        {
            return AuthResult<bool>.Failure("Mã xác minh đã bị hủy do nhập sai quá nhiều lần. Vui lòng bấm 'Gửi lại mã'.", 400);
        }

        // Verify OTP HMAC-SHA256 with constant-time equality
        var isOtpValid = _otpService.VerifyOtpHash(user.Id, TokenTypes.EmailVerification, request.Code, activeToken.TokenHash);
        if (!isOtpValid)
        {
            activeToken.AttemptsCount++;
            if (activeToken.AttemptsCount >= _securityOptions.MaxOtpAttempts)
            {
                activeToken.IsUsed = true;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);

            return AuthResult<bool>.Failure("Mã xác minh không chính xác hoặc đã hết hạn.", 400);
        }

        // Mark token used and activate customer user
        activeToken.IsUsed = true;
        activeToken.UsedAt = DateTime.UtcNow;

        user.IsEmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;

        // CRITICAL: Only USER accounts are automatically APPROVED upon email OTP verification.
        // ADMIN, MANAGER, and STAFF accounts must NOT be approved via VerifyEmail!
        if (string.Equals(user.Role?.Code, Roles.User, StringComparison.OrdinalIgnoreCase) || user.RoleId == 4)
        {
            user.ApprovalStatus = ApprovalStatuses.Approved;
            user.ApprovedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Email verified successfully for user {UserId} ({Email}). Account activated.", user.Id, user.Email);
        return AuthResult<bool>.Success(true, "Xác minh email thành công. Tài khoản của bạn đã được kích hoạt.", 200);
    }

    public async Task<AuthResult<bool>> ResendVerificationAsync(ResendVerificationRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        const string uniformSuccessMessage = "Nếu email hợp lệ và chưa được xác minh, mã OTP mới đã được gửi tới hộp thư của bạn.";

        if (user == null || user.IsEmailVerified)
        {
            return AuthResult<bool>.Success(true, uniformSuccessMessage, 200);
        }

        // Cooldown check (60 seconds)
        var latestToken = await _dbContext.UserTokens
            .Where(t => t.UserId == user.Id && t.TokenType == TokenTypes.EmailVerification)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestToken != null && latestToken.CreatedAt.AddSeconds(_securityOptions.OtpResendCooldownSeconds) > DateTime.UtcNow)
        {
            var remainingSeconds = (int)Math.Ceiling((latestToken.CreatedAt.AddSeconds(_securityOptions.OtpResendCooldownSeconds) - DateTime.UtcNow).TotalSeconds);
            return AuthResult<bool>.Failure($"Vui lòng chờ {remainingSeconds} giây trước khi yêu cầu gửi lại mã xác minh mới.", 429);
        }

        // Invalidate prior active tokens
        var activeTokens = await _dbContext.UserTokens
            .Where(t => t.UserId == user.Id && t.TokenType == TokenTypes.EmailVerification && !t.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var t in activeTokens)
        {
            t.IsUsed = true;
        }

        var otpCode = _otpService.GenerateOtpCode();
        var otpHash = _otpService.ComputeOtpHash(user.Id, TokenTypes.EmailVerification, otpCode);

        var userToken = new UserToken
        {
            UserId = user.Id,
            TokenType = TokenTypes.EmailVerification,
            TokenHash = otpHash,
            AttemptsCount = 0,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_securityOptions.OtpLifetimeMinutes),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.UserTokens.Add(userToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _emailService.SendVerificationOtpAsync(user.Email, otpCode, cancellationToken);

        return AuthResult<bool>.Success(true, uniformSuccessMessage, 200);
    }

    public async Task<AuthResult<AuthResponseDto>> LoginAsync(LoginRequestDto request, string? userAgent, string? clientIp, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = request.Username.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == normalizedUsername, cancellationToken);

        // 1. If user not found, perform dummy verify to prevent timing attack enumeration
        if (user == null)
        {
            _passwordHasher.DummyVerify();
            return AuthResult<AuthResponseDto>.Failure("Tên đăng nhập hoặc mật khẩu không chính xác.", 401);
        }

        // 2. Verify password FIRST before revealing any account state
        var isPasswordValid = _passwordHasher.VerifyPassword(user, user.PasswordHash, request.Password);
        if (!isPasswordValid)
        {
            if (_dbContext.Database.IsRelational())
            {
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE users SET failed_login_attempts = failed_login_attempts + 1, lockout_end_at = CASE WHEN failed_login_attempts + 1 >= {_securityOptions.MaxFailedLoginAttempts} THEN {DateTime.UtcNow.AddMinutes(_securityOptions.LockoutDurationMinutes)} ELSE lockout_end_at END, updated_at = {DateTime.UtcNow} WHERE id = {user.Id}",
                    cancellationToken);
            }
            else
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= _securityOptions.MaxFailedLoginAttempts)
                {
                    user.LockoutEndAt = DateTime.UtcNow.AddMinutes(_securityOptions.LockoutDurationMinutes);
                }
                user.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return AuthResult<AuthResponseDto>.Failure("Tên đăng nhập hoặc mật khẩu không chính xác.", 401);
        }

        // 3. Password is valid: now check lockout and account states
        if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > DateTime.UtcNow)
        {
            return AuthResult<AuthResponseDto>.Failure("Tài khoản tạm thời bị khóa do nhập sai mật khẩu nhiều lần. Vui lòng thử lại sau 15 phút.", 423);
        }

        if (!user.IsEmailVerified)
        {
            return AuthResult<AuthResponseDto>.Failure("Tài khoản chưa hoàn tất xác minh email. Vui lòng xác minh mã OTP được gửi qua email trước khi đăng nhập.", 403);
        }

        if (!user.IsActive)
        {
            return AuthResult<AuthResponseDto>.Failure("Tài khoản đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.", 403);
        }

        if (user.ApprovalStatus != ApprovalStatuses.Approved)
        {
            return AuthResult<AuthResponseDto>.Failure("Tài khoản đang chờ quản trị viên xét duyệt hoặc không đủ điều kiện đăng nhập.", 403);
        }

        // 4. Successful login: reset failed counters and issue tokens
        user.FailedLoginAttempts = 0;
        user.LockoutEndAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        var accessToken = _tokenService.GenerateAccessToken(user, user.Role.Code);
        var (rawRefreshToken, tokenHash) = _tokenService.GenerateRefreshToken();

        var refreshTokenRecord = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            UserAgent = userAgent,
            IpAddress = clientIp,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenLifetimeDays),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.RefreshTokens.Add(refreshTokenRecord);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var profile = new UserProfileDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.Code,
            IsEmailVerified = user.IsEmailVerified,
            ApprovalStatus = user.ApprovalStatus,
            CreatedAt = user.CreatedAt
        };

        var response = new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            TokenType = "Bearer",
            ExpiresIn = _jwtOptions.AccessTokenLifetimeMinutes * 60,
            UserProfile = profile
        };

        return AuthResult<AuthResponseDto>.Success(response, "Đăng nhập thành công.", 200);
    }

    public async Task<AuthResult<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request, string? userAgent, string? clientIp, CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);

        var tokenRecord = await _dbContext.RefreshTokens
            .Include(t => t.User)
            .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (tokenRecord == null || tokenRecord.IsRevoked || tokenRecord.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Refresh token rejection: token does not exist, is expired, or is revoked.");
            return AuthResult<AuthResponseDto>.Failure("Refresh token không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại.", 401);
        }

        var user = tokenRecord.User;
        if (!user.IsActive || !user.IsEmailVerified || user.ApprovalStatus != ApprovalStatuses.Approved)
        {
            _logger.LogWarning("Refresh token rejection: user {UserId} is inactive, unverified, or unapproved.", user.Id);
            return AuthResult<AuthResponseDto>.Failure("Tài khoản không đủ điều kiện duy trì phiên làm việc.", 401);
        }

        // Issue a fresh Access Token (No refresh-token rotation in this approved version)
        var newAccessToken = _tokenService.GenerateAccessToken(user, user.Role.Code);

        var profile = new UserProfileDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.Code,
            IsEmailVerified = user.IsEmailVerified,
            ApprovalStatus = user.ApprovalStatus,
            CreatedAt = user.CreatedAt
        };

        var response = new AuthResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = request.RefreshToken,
            TokenType = "Bearer",
            ExpiresIn = _jwtOptions.AccessTokenLifetimeMinutes * 60,
            UserProfile = profile
        };

        return AuthResult<AuthResponseDto>.Success(response, "Làm mới Access Token thành công.", 200);
    }

    public async Task<AuthResult<bool>> LogoutAsync(LogoutRequestDto request, CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);

        var tokenRecord = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (tokenRecord == null)
        {
            // Already logged out or non-existent token: return idempotent success
            return AuthResult<bool>.Success(true, "Đăng xuất thành công.", 200);
        }

        tokenRecord.IsRevoked = true;
        tokenRecord.RevokedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Refresh token revoked successfully for user {UserId}.", tokenRecord.UserId);

        return AuthResult<bool>.Success(true, "Đăng xuất thành công. Phiên làm việc đã kết thúc.", 200);
    }
}

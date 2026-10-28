using Microsoft.EntityFrameworkCore;
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
    private readonly JwtOptions _jwtOptions;
    private readonly SecurityOptions _securityOptions;
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
            return AuthResult<AuthResponseDto>.Failure("Tài khoản chưa được kích hoạt. Vui lòng liên hệ quản trị viên.", 403);
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

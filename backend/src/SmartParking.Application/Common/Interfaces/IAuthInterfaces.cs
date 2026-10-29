using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Auth;
using SmartParking.Domain.Entities;

namespace SmartParking.Application.Common.Interfaces;

public interface IPasswordHasherService
{
    string HashPassword(User user, string password);
    bool VerifyPassword(User user, string hashedPassword, string providedPassword);
    void DummyVerify();
}

public interface ITokenService
{
    string GenerateAccessToken(User user, string roleCode);
    (string RawToken, string TokenHash) GenerateRefreshToken();
    string HashToken(string rawToken);
}

public interface IAuthService
{
    Task<AuthResult<UserProfileDto>> RegisterAsync(RegisterRequestDto request, string? clientIp, CancellationToken cancellationToken = default);
    Task<AuthResult<bool>> VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResult<bool>> ResendVerificationAsync(ResendVerificationRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResult<AuthResponseDto>> LoginAsync(LoginRequestDto request, string? userAgent, string? clientIp, CancellationToken cancellationToken = default);
    Task<AuthResult<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request, string? userAgent, string? clientIp, CancellationToken cancellationToken = default);
    Task<AuthResult<bool>> LogoutAsync(LogoutRequestDto request, CancellationToken cancellationToken = default);
}

public interface IOtpService
{
    string GenerateOtpCode();
    string ComputeOtpHash(long userId, string tokenType, string otpCode);
    bool VerifyOtpHash(long userId, string tokenType, string otpCode, string storedHash);
}

public interface IRecaptchaService
{
    Task<bool> VerifyAsync(string? token, string? remoteIp = null, CancellationToken cancellationToken = default);
}

public interface IEmailService
{
    Task<bool> SendVerificationOtpAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default);
}


public interface IUserService
{
    Task<UserProfileDto?> GetUserProfileAsync(long userId, CancellationToken cancellationToken = default);
    Task<UserProfileDto?> UpdateUserProfileAsync(long userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default);
}

public interface IAdminBootstrapService
{
    Task BootstrapAdminAsync(CancellationToken cancellationToken = default);
}

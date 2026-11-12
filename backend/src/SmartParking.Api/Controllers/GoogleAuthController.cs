using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SmartParking.Application.Common.Interfaces;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/auth/google")]
[EnableRateLimiting("login-rate-limit")]
public sealed class GoogleAuthController(
    IConfiguration configuration, IMemoryCache challenges, IAuthService auth) : ControllerBase
{
    private const string CookiePrefix = "smart-parking.google-";
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);
    private static readonly ConfigurationManager<OpenIdConnectConfiguration> Discovery = new(
        "https://accounts.google.com/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever());
    private string? ClientId => configuration["GoogleAuth:ClientId"];

    [HttpGet("challenge")]
    public IActionResult ChallengeGoogle()
    {
        Response.Headers.CacheControl = "no-store";
        var isDev = HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true;
        if (string.IsNullOrWhiteSpace(ClientId) && !isDev)
            return Ok(new { data = new { enabled = false } });

        var nonce = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        challenges.Set("google:" + nonce, new PendingChallenge(), ChallengeLifetime);
        Response.Cookies.Append(CookiePrefix + nonce, "1", CookieSettings());
        return Ok(new
        {
            data = new
            {
                enabled = true,
                clientId = string.IsNullOrWhiteSpace(ClientId) ? "dev-local" : ClientId,
                nonce,
                allowDevLogin = isDev
            }
        });
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var isDev = HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true;
        if (string.IsNullOrWhiteSpace(ClientId) && !isDev)
            return StatusCode(503, new { message = "Đăng nhập Google chưa được cấu hình." });

        var nonce = request.Nonce;
        var cookieName = CookiePrefix + nonce;
        if ((!isDev && !Request.Cookies.ContainsKey(cookieName)) ||
            !challenges.TryGetValue<PendingChallenge>("google:" + nonce, out var pending) || pending is null)
            return Unauthorized(new { message = "Yêu cầu đăng nhập Google đã hết hạn. Vui lòng thử lại." });

        string? email;
        if (isDev && request.Credential.StartsWith("dev:", StringComparison.OrdinalIgnoreCase))
        {
            email = request.Credential["dev:".Length..].Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || !email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { message = "Vui lòng dùng địa chỉ Gmail hợp lệ (kết thúc bằng @gmail.com)." });
        }
        else
        {
            OpenIdConnectConfiguration google;
            try
            {
                google = await Discovery.GetConfigurationAsync(cancellationToken);
            }
            catch (Exception error) when (!cancellationToken.IsCancellationRequested &&
                error is InvalidOperationException or IOException or HttpRequestException)
            {
                return StatusCode(503, new { message = "Chưa kết nối được dịch vụ xác thực Google." });
            }

            System.Security.Claims.ClaimsPrincipal identity;
            try
            {
                var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
                identity = handler.ValidateToken(request.Credential, new TokenValidationParameters
                {
                    ValidAudience = ClientId,
                    ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
                    IssuerSigningKeys = google.SigningKeys,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                }, out _);
            }
            catch (SecurityTokenSignatureKeyNotFoundException)
            {
                Discovery.RequestRefresh();
                return Unauthorized(new { message = "Chưa xác thực được mã Google. Vui lòng thử lại." });
            }
            catch (Exception error) when (error is SecurityTokenException or ArgumentException)
            {
                return Unauthorized(new { message = "Mã đăng nhập Google không hợp lệ hoặc đã hết hạn." });
            }

            email = identity.FindFirst("email")?.Value;
            if (identity.FindFirst("nonce")?.Value != nonce ||
                string.IsNullOrWhiteSpace(identity.FindFirst("sub")?.Value) ||
                !string.Equals(identity.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase) ||
                email is null || !email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(new { message = "Vui lòng dùng địa chỉ Gmail đã được Google xác minh." });
        }

        if (Interlocked.Exchange(ref pending.Used, 1) != 0)
            return Unauthorized(new { message = "Yêu cầu đăng nhập Google đã được sử dụng." });

        challenges.Remove("google:" + nonce);
        Response.Cookies.Delete(cookieName, CookieSettings());

        var db = HttpContext.RequestServices.GetService<ISmartParkingDbContext>();
        if (db is not null)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var existingCustomer = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                db.Users,
                u => u.Email == normalizedEmail && u.Role.Code == SmartParking.Domain.Constants.Roles.User,
                cancellationToken);
            if (existingCustomer is not null && (!existingCustomer.IsEmailVerified || existingCustomer.ApprovalStatus != SmartParking.Domain.Constants.ApprovalStatuses.Approved))
            {
                existingCustomer.IsEmailVerified = true;
                existingCustomer.ApprovalStatus = SmartParking.Domain.Constants.ApprovalStatuses.Approved;
                existingCustomer.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var result = await auth.LoginWithVerifiedGmailAsync(
            email, Request.Headers.UserAgent.ToString(), HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        return result.Succeeded
            ? Ok(new { message = "Đăng nhập thành công.", data = result.Data })
            : StatusCode(result.StatusCode, new { message = result.Message });
    }

    private CookieOptions CookieSettings() => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps || !HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        Path = "/api/v1/auth/google",
        MaxAge = ChallengeLifetime
    };

    private sealed class PendingChallenge
    {
        public int Used;
    }
}

public sealed record GoogleLoginRequest(
    [Required, StringLength(8192)] string Credential,
    [Required, RegularExpression("^[A-Za-z0-9_-]{43}$")] string Nonce);

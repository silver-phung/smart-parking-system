using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.DTOs.Auth;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Đăng ký tài khoản người dùng mới (vai trò mặc định: CUSTOMER).
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting("register-rate-limit")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RegisterAsync(request, clientIp, cancellationToken);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new
            {
                message = result.Message
            });
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = result.Message,
            data = result.Data
        });
    }

    /// <summary>
    /// Xác minh địa chỉ email bằng mã OTP 6 số.
    /// </summary>
    [HttpPost("verify-email")]
    [EnableRateLimiting("verify-rate-limit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.VerifyEmailAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new
            {
                message = result.Message
            });
        }

        return Ok(new
        {
            message = "Xác minh email thành công. Tài khoản của bạn đã được kích hoạt."
        });
    }

    /// <summary>
    /// Gửi lại mã OTP xác minh qua email (có áp dụng cooldown 60 giây và rate limiting).
    /// </summary>
    [HttpPost("resend-verification")]
    [EnableRateLimiting("resend-rate-limit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.ResendVerificationAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new
            {
                message = result.Message
            });
        }

        return Ok(new
        {
            message = result.Message
        });
    }

    /// <summary>
    /// Đăng nhập bằng Username và Mật khẩu.
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login-rate-limit")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userAgent = Request.Headers.UserAgent.ToString();
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _authService.LoginAsync(request, userAgent, clientIp, cancellationToken);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new
            {
                message = result.Message
            });
        }

        return Ok(new
        {
            message = "Đăng nhập thành công.",
            data = result.Data
        });
    }

    /// <summary>
    /// Làm mới JWT Access Token bằng Refresh Token hợp lệ.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userAgent = Request.Headers.UserAgent.ToString();
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _authService.RefreshTokenAsync(request, userAgent, clientIp, cancellationToken);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new
            {
                message = result.Message
            });
        }

        return Ok(new
        {
            message = "Làm mới Access Token thành công.",
            data = result.Data
        });
    }

    /// <summary>
    /// Đăng xuất và thu hồi Refresh Token (hoạt động ngay cả khi Access Token đã hết hạn).
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.LogoutAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new
            {
                message = result.Message
            });
        }

        return Ok(new
        {
            message = "Đăng xuất thành công. Phiên làm việc đã kết thúc."
        });
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.DTOs.Auth;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy thông tin hồ sơ của người dùng hiện tại đang đăng nhập.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Không thể xác định danh tính người dùng từ token xác thực." });
        }

        var profile = await _userService.GetUserProfileAsync(userId.Value, cancellationToken);
        if (profile == null)
        {
            return NotFound(new { message = "Không tìm thấy hồ sơ người dùng." });
        }

        return Ok(new
        {
            message = "Lấy thông tin người dùng thành công.",
            data = profile
        });
    }

    /// <summary>
    /// Cập nhật thông tin hồ sơ của người dùng hiện tại (chỉ cho phép cập nhật FullName và PhoneNumber).
    /// </summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Không thể xác định danh tính người dùng từ token xác thực." });
        }

        var profile = await _userService.UpdateUserProfileAsync(userId.Value, request, cancellationToken);
        if (profile == null)
        {
            return NotFound(new { message = "Không tìm thấy hồ sơ người dùng để cập nhật." });
        }

        return Ok(new
        {
            message = "Cập nhật thông tin hồ sơ thành công.",
            data = profile
        });
    }

    private long? GetCurrentUserId()
    {
        var claimValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (long.TryParse(claimValue, out var userId))
        {
            return userId;
        }

        return null;
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.AdminUsers;
using SmartParking.Domain.Constants;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Produces("application/json")]
[Authorize(Roles = Roles.Admin)]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(IAdminUserService adminUserService, ILogger<AdminUsersController> logger)
    {
        _adminUserService = adminUserService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách người dùng với phân trang, tìm kiếm và bộ lọc (Dành cho ADMIN).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminUserSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers([FromQuery] AdminUserFilterDto filter, CancellationToken ct)
    {
        var result = await _adminUserService.GetUsersAsync(filter, ct);
        return Ok(new
        {
            message = "Lấy danh sách người dùng thành công.",
            data = result
        });
    }

    /// <summary>
    /// Lấy thông tin chi tiết người dùng theo ID (Dành cho ADMIN).
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(AdminUserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById([FromRoute] long id, CancellationToken ct)
    {
        var user = await _adminUserService.GetUserByIdAsync(id, ct);
        if (user == null)
        {
            return NotFound(new { message = $"Không tìm thấy người dùng với ID: {id}." });
        }

        return Ok(new
        {
            message = "Lấy thông tin người dùng thành công.",
            data = user
        });
    }

    /// <summary>
    /// Tạo tài khoản người dùng mới trực tiếp bởi ADMIN (Hỗ trợ STAFF, MANAGER, USER, ADMIN).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdminUserDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        if (actorId == null)
        {
            return Unauthorized(new { message = "Không thể xác định danh tính quản trị viên từ token xác thực." });
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _adminUserService.CreateUserAsync(request, actorId.Value, clientIp, ct);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = result.Message,
            data = result.Data
        });
    }

    /// <summary>
    /// Cập nhật trạng thái hoạt động (Enable / Disable) tài khoản người dùng (Dành cho ADMIN).
    /// </summary>
    [HttpPatch("{id:long}/status")]
    [ProducesResponseType(typeof(AdminUserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserStatus(
        [FromRoute] long id,
        [FromBody] AdminUpdateStatusRequestDto request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        if (actorId == null)
        {
            return Unauthorized(new { message = "Không thể xác định danh tính quản trị viên từ token xác thực." });
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _adminUserService.UpdateUserStatusAsync(id, request, actorId.Value, clientIp, ct);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        return Ok(new
        {
            message = result.Message,
            data = result.Data
        });
    }

    /// <summary>
    /// Thay đổi vai trò (Role) của người dùng (Dành cho ADMIN).
    /// </summary>
    [HttpPatch("{id:long}/role")]
    [ProducesResponseType(typeof(AdminUserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserRole(
        [FromRoute] long id,
        [FromBody] AdminUpdateRoleRequestDto request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        if (actorId == null)
        {
            return Unauthorized(new { message = "Không thể xác định danh tính quản trị viên từ token xác thực." });
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _adminUserService.UpdateUserRoleAsync(id, request, actorId.Value, clientIp, ct);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        return Ok(new
        {
            message = result.Message,
            data = result.Data
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

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Sessions;
using SmartParking.Domain.Constants;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/parking-sessions")]
[Produces("application/json")]
public class ParkingSessionsController : ControllerBase
{
    private readonly IParkingSessionService _sessionService;
    private readonly ILogger<ParkingSessionsController> _logger;

    public ParkingSessionsController(IParkingSessionService sessionService, ILogger<ParkingSessionsController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Check-in xe vào bãi: Tạo phiên đỗ mới, gán ô đỗ (nếu có), tăng occupancy và kích hoạt chống quay vòng (Anti-Passback).
    /// Dành cho ADMIN hoặc MANAGER/STAFF được phân công tại bãi đỗ xe này.
    /// </summary>
    [HttpPost("check-in")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff}")]
    [ProducesResponseType(typeof(ParkingSessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.Admin;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, message, session) = await _sessionService.CheckInAsync(request, actorId, actorRole, clientIp, ct);
        if (!success)
        {
            if (message.Contains("không có quyền"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message });
            }
            if (message.Contains("Không tìm thấy"))
            {
                return NotFound(new { message });
            }
            if (message.Contains("chưa kết thúc (ACTIVE/PAID)") || message.Contains("hiện đang được phân công"))
            {
                return Conflict(new { message });
            }
            return BadRequest(new { message });
        }

        return CreatedAtAction(nameof(GetSessionById), new { id = session!.Id }, new
        {
            message,
            data = session
        });
    }

    /// <summary>
    /// Check-out xe rời bãi: Xác nhận thủ công từ nhân viên/quản trị viên khi xe rời bãi (chưa tích hợp cảm biến IoT cổng ra).
    /// Chuyển trạng thái phiên sang COMPLETED và giải phóng ô đỗ về EMPTY nếu trước đó có xe đỗ.
    /// Dành cho ADMIN hoặc MANAGER/STAFF được phân công tại bãi đỗ xe này.
    /// </summary>
    [HttpPost("{id:long}/check-out")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff}")]
    [ProducesResponseType(typeof(ParkingSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckOut(long id, [FromBody] CheckOutRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.Admin;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, message, session) = await _sessionService.CheckOutAsync(id, request, actorId, actorRole, clientIp, ct);
        if (!success)
        {
            if (message.Contains("không có quyền"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message });
            }
            if (message.Contains("Không tìm thấy"))
            {
                return NotFound(new { message });
            }
            return BadRequest(new { message });
        }

        return Ok(new
        {
            message,
            data = session
        });
    }

    /// <summary>
    /// Lấy danh sách các phiên đỗ xe đang hoạt động (ACTIVE/PAID).
    /// ADMIN có thể xem toàn bộ; MANAGER/STAFF tự động lọc theo các bãi được phân công.
    /// </summary>
    [HttpGet("active")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff}")]
    [ProducesResponseType(typeof(PagedResult<ParkingSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetActiveSessions([FromQuery] ParkingSessionFilterDto filter, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var result = await _sessionService.GetActiveSessionsAsync(filter, currentUserId, actorRole, ct);
        return Ok(new
        {
            message = "Lấy danh sách phiên đỗ đang hoạt động thành công.",
            data = result
        });
    }

    /// <summary>
    /// Lấy lịch sử phiên đỗ xe của chính người dùng đang đăng nhập (USER).
    /// </summary>
    [HttpGet("my-sessions")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.User}")]
    [ProducesResponseType(typeof(PagedResult<ParkingSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMySessions([FromQuery] ParkingSessionFilterDto filter, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
        }

        var result = await _sessionService.GetUserSessionsAsync(currentUserId.Value, filter, ct);
        return Ok(new
        {
            message = "Lấy lịch sử phiên đỗ xe thành công.",
            data = result
        });
    }

    /// <summary>
    /// Xem chi tiết một phiên đỗ xe theo ID.
    /// ADMIN: xem toàn bộ; MANAGER/STAFF: xem nếu phiên thuộc bãi được phân công; USER: xem nếu phiên thuộc xe của mình.
    /// </summary>
    [HttpGet("{id:long}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff},{Roles.User}")]
    [ProducesResponseType(typeof(ParkingSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionById(long id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var session = await _sessionService.GetSessionByIdAsync(id, currentUserId, actorRole, ct);
        if (session == null)
        {
            return NotFound(new { message = $"Không tìm thấy phiên đỗ xe với ID: {id} hoặc bạn không có quyền truy cập." });
        }

        return Ok(new
        {
            message = "Lấy chi tiết phiên đỗ xe thành công.",
            data = session
        });
    }

    /// <summary>
    /// Tra cứu phiên đỗ đang hoạt động theo biển số xe.
    /// ADMIN: xem toàn bộ; MANAGER/STAFF: xem nếu phiên thuộc bãi được phân công; USER: xem nếu phiên thuộc xe của mình.
    /// </summary>
    [HttpGet("by-plate/{plateNumber}/active")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff},{Roles.User}")]
    [ProducesResponseType(typeof(ParkingSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveSessionByPlate(string plateNumber, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var session = await _sessionService.GetActiveSessionByPlateAsync(plateNumber, currentUserId, actorRole, ct);
        if (session == null)
        {
            return NotFound(new { message = $"Không tìm thấy phiên đang đỗ cho biển số '{plateNumber}' hoặc bạn không có quyền truy cập." });
        }

        return Ok(new
        {
            message = "Tra cứu phiên đang đỗ thành công.",
            data = session
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

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Dispatches;
using SmartParking.Domain.Constants;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/parking-dispatches")]
[Produces("application/json")]
public class ParkingDispatchesController : ControllerBase
{
    private readonly IParkingDispatchService _dispatchService;
    private readonly ILogger<ParkingDispatchesController> _logger;

    public ParkingDispatchesController(IParkingDispatchService dispatchService, ILogger<ParkingDispatchesController> logger)
    {
        _dispatchService = dispatchService;
        _logger = logger;
    }

    /// <summary>
    /// Tạo yêu cầu điều phối đỗ xe (Dispatch) cho phiên gửi xe ACTIVE.
    /// Tự động tìm ô đỗ trống khả dụng hoặc gán theo ô chỉ định.
    /// ADMIN có quyền toàn hệ thống; MANAGER/STAFF chỉ tạo trong bãi được phân công.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff}")]
    [ProducesResponseType(typeof(ParkingDispatchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDispatch([FromBody] CreateDispatchRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.Admin;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, message, dispatch) = await _dispatchService.CreateDispatchAsync(request, actorId, actorRole, clientIp, ct);
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
            if (message.Contains("Xung đột") || message.Contains("hiện đang có một lệnh điều phối") || message.Contains("hiện đã có một lệnh điều phối") || message.Contains("hiện đang được phân công"))
            {
                return Conflict(new { message });
            }
            return BadRequest(new { message });
        }

        return CreatedAtAction(nameof(GetDispatchById), new { id = dispatch!.Id }, new
        {
            message,
            data = dispatch
        });
    }

    /// <summary>
    /// Tra cứu danh sách lệnh điều phối theo bộ lọc phân trang.
    /// Phục vụ điều hành bãi đỗ và hiển thị mô hình 3D.
    /// ADMIN có thể xem toàn bộ; MANAGER/STAFF xem trong bãi được phân công; USER chỉ xem xe của mình.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff},{Roles.User}")]
    [ProducesResponseType(typeof(PagedResult<ParkingDispatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDispatches([FromQuery] ParkingDispatchFilterDto filter, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.User;

        var result = await _dispatchService.GetDispatchesAsync(filter, currentUserId, currentUserRole, ct);
        return Ok(new
        {
            message = "Lấy danh sách lệnh điều phối thành công.",
            data = result
        });
    }

    /// <summary>
    /// Lấy chi tiết một lệnh điều phối theo ID, bao gồm tọa độ 3D (PositionX, PositionY, PositionZ) phục vụ hiển thị mô hình.
    /// </summary>
    [HttpGet("{id:long}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff},{Roles.User}")]
    [ProducesResponseType(typeof(ParkingDispatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDispatchById(long id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.User;

        var dispatch = await _dispatchService.GetDispatchByIdAsync(id, currentUserId, currentUserRole, ct);
        if (dispatch == null)
        {
            return NotFound(new { message = $"Không tìm thấy lệnh điều phối với ID: {id} hoặc bạn không có quyền truy cập." });
        }

        return Ok(new
        {
            message = "Lấy chi tiết lệnh điều phối thành công.",
            data = dispatch
        });
    }

    /// <summary>
    /// Tra cứu lệnh điều phối đang hoạt động (PENDING hoặc ASSIGNED) theo mã phiên đỗ xe (sessionId).
    /// </summary>
    [HttpGet("session/{sessionId:long}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff},{Roles.User}")]
    [ProducesResponseType(typeof(ParkingDispatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveDispatchBySessionId(long sessionId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.User;

        var dispatch = await _dispatchService.GetActiveDispatchBySessionIdAsync(sessionId, currentUserId, currentUserRole, ct);
        if (dispatch == null)
        {
            return NotFound(new { message = $"Không tìm thấy lệnh điều phối đang hoạt động cho phiên #{sessionId} hoặc bạn không có quyền truy cập." });
        }

        return Ok(new
        {
            message = "Tra cứu lệnh điều phối theo phiên thành công.",
            data = dispatch
        });
    }

    /// <summary>
    /// Điều phối lại ô đỗ khi ô hiện tại không khả dụng hoặc có sự cố.
    /// ADMIN có quyền toàn hệ thống; MANAGER/STAFF chỉ thao tác trong bãi được phân công.
    /// </summary>
    [HttpPost("{id:long}/assign")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff}")]
    [ProducesResponseType(typeof(ParkingDispatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReassignSlot(long id, [FromBody] AssignSlotRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.Admin;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, message, dispatch) = await _dispatchService.ReassignSlotAsync(id, request, actorId, actorRole, clientIp, ct);
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
            if (message.Contains("Xung đột") || message.Contains("hiện đang được phân công"))
            {
                return Conflict(new { message });
            }
            return BadRequest(new { message });
        }

        return Ok(new
        {
            message,
            data = dispatch
        });
    }

    /// <summary>
    /// Xác nhận xe đã đỗ đúng ô chỉ định. Chuyển trạng thái Dispatch sang OCCUPIED và ô đỗ sang OCCUPIED.
    /// ADMIN có quyền toàn hệ thống; MANAGER/STAFF chỉ thao tác trong bãi được phân công.
    /// </summary>
    [HttpPost("{id:long}/confirm-occupied")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff}")]
    [ProducesResponseType(typeof(ParkingDispatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmOccupied(long id, [FromBody] ConfirmOccupiedRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.Admin;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, message, dispatch) = await _dispatchService.ConfirmOccupiedAsync(id, request, actorId, actorRole, clientIp, ct);
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
            if (message.Contains("Xung đột"))
            {
                return Conflict(new { message });
            }
            return BadRequest(new { message });
        }

        return Ok(new
        {
            message,
            data = dispatch
        });
    }

    /// <summary>
    /// Ghi nhận xe đỗ sai ô so với ô được chỉ định (WRONG_SLOT).
    /// Cập nhật ô thực tế thành OCCUPIED, giải phóng ô được chỉ định ban đầu về EMPTY.
    /// ADMIN có quyền toàn hệ thống; MANAGER/STAFF chỉ thao tác trong bãi được phân công.
    /// </summary>
    [HttpPost("{id:long}/report-wrong-slot")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff}")]
    [ProducesResponseType(typeof(ParkingDispatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReportWrongSlot(long id, [FromBody] ReportWrongSlotRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.Admin;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, message, dispatch) = await _dispatchService.ReportWrongSlotAsync(id, request, actorId, actorRole, clientIp, ct);
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
            if (message.Contains("Xung đột") || message.Contains("hiện đã có xe khác") || message.Contains("hiện đang được phân công"))
            {
                return Conflict(new { message });
            }
            return BadRequest(new { message });
        }

        return Ok(new
        {
            message,
            data = dispatch
        });
    }

    /// <summary>
    /// Hủy lệnh điều phối khi hợp lệ (khi lệnh đang ở trạng thái PENDING hoặc ASSIGNED).
    /// Ô đỗ được phân công sẽ được giải phóng khỏi trạng thái gán.
    /// ADMIN có quyền toàn hệ thống; MANAGER/STAFF chỉ thao tác trong bãi được phân công.
    /// </summary>
    [HttpPost("{id:long}/cancel")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelDispatch(long id, [FromBody] CancelDispatchRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.Admin;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, message) = await _dispatchService.CancelDispatchAsync(id, request, actorId, actorRole, clientIp, ct);
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
            message
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

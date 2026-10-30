using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Parking;
using SmartParking.Domain.Constants;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/parking-slots")]
[Produces("application/json")]
public class ParkingSlotsController : ControllerBase
{
    private readonly IParkingManagementService _parkingService;
    private readonly IParkingAuthorizationService _authService;
    private readonly ILogger<ParkingSlotsController> _logger;

    public ParkingSlotsController(
        IParkingManagementService parkingService,
        IParkingAuthorizationService authService,
        ILogger<ParkingSlotsController> logger)
    {
        _parkingService = parkingService;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách ô đỗ của một tầng có phân trang và lọc theo trạng thái vật lý (Công khai).
    /// </summary>
    [HttpGet("floor/{floorId:int}")]
    [HttpGet("/api/v1/parking-floors/{floorId:int}/slots")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<ParkingSlotDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSlotsByFloorId(int floorId, [FromQuery] ParkingSlotFilterDto filter, CancellationToken ct)
    {
        var result = await _parkingService.GetSlotsByFloorIdAsync(floorId, filter, ct);
        return Ok(new
        {
            message = "Lấy danh sách ô đỗ thành công.",
            data = result
        });
    }

    /// <summary>
    /// Lấy chi tiết một ô đỗ xe theo ID.
    /// Khách, Người dùng, Staff và Manager chỉ xem được trạng thái vật lý công khai (chống lộ dữ liệu khi chưa có staff_lot_assignments).
    /// Chỉ ADMIN được xem thông tin nội bộ toàn hệ thống (biển số xe và ID phiên đỗ).
    /// </summary>
    [HttpGet("{id:long}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParkingSlotDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSlotById(long id, CancellationToken ct)
    {
        var isAdmin = User.IsInRole(Roles.Admin);
        var actorId = GetCurrentUserId();
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var hasAssignedAccess = false;
        if (!isAdmin && actorId.HasValue && (actorRole == Roles.Manager || actorRole == Roles.Staff))
        {
            var rawSlot = await _parkingService.GetSlotByIdAsync(id, includeInternalDetails: false, ct);
            if (rawSlot != null)
            {
                hasAssignedAccess = await _authService.HasLotAccessAsync(actorId.Value, actorRole, rawSlot.LotId, ct);
            }
        }

        var slot = await _parkingService.GetSlotByIdAsync(id, isAdmin || hasAssignedAccess, ct);
        if (slot == null)
        {
            return NotFound(new { message = $"Không tìm thấy ô đỗ với ID: {id}." });
        }

        return Ok(new
        {
            message = "Lấy thông tin chi tiết ô đỗ thành công.",
            data = slot
        });
    }

    /// <summary>
    /// Tạo mới ô đỗ xe trong tầng (Chỉ dành cho ADMIN).
    /// </summary>
    [HttpPost("floor/{floorId:int}")]
    [HttpPost("/api/v1/parking-floors/{floorId:int}/slots")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(ParkingSlotDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSlot(int floorId, [FromBody] CreateParkingSlotRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var (success, message, slot) = await _parkingService.CreateSlotAsync(floorId, request, actorId, ct);

        if (!success)
        {
            if (message.Contains("không tồn tại"))
            {
                return NotFound(new { message });
            }
            if (message.Contains("không hợp lệ"))
            {
                return BadRequest(new { message });
            }
            return Conflict(new { message });
        }

        return CreatedAtAction(nameof(GetSlotById), new { id = slot!.Id }, new
        {
            message,
            data = slot
        });
    }

    /// <summary>
    /// Cập nhật thông tin ô đỗ (Mã ô, tọa độ, IsActive) (Chỉ dành cho ADMIN).
    /// </summary>
    [HttpPut("{id:long}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(ParkingSlotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSlot(long id, [FromBody] UpdateParkingSlotRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var (success, message, slot) = await _parkingService.UpdateSlotAsync(id, request, actorId, ct);

        if (!success)
        {
            if (message.Contains("Không tìm thấy"))
            {
                return NotFound(new { message });
            }
            return Conflict(new { message });
        }

        return Ok(new
        {
            message,
            data = slot
        });
    }

    /// <summary>
    /// Cập nhật trạng thái vật lý của ô đỗ xe (Bảo trì/Không khả dụng/Trống).
    /// Dành cho ADMIN hoặc MANAGER được phân công tại bãi đỗ xe chứa ô này.
    /// Tuân thủ quy tắc nghiêm ngặt: Tuyệt đối không cho phép chuyển ô OCCUPIED sang EMPTY khi xe chưa hoàn tất rời bãi.
    /// </summary>
    [HttpPatch("{id:long}/status")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    [ProducesResponseType(typeof(ParkingSlotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSlotPhysicalStatus(long id, [FromBody] UpdateSlotPhysicalStatusRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.Admin;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var slotInfo = await _parkingService.GetSlotByIdAsync(id, includeInternalDetails: true, ct);
        if (slotInfo == null)
        {
            return NotFound(new { message = "Không tìm thấy ô đỗ xe." });
        }

        if (!await _authService.CanManageSlotStatusAsync(actorId ?? 0, actorRole, slotInfo.LotId, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn không có quyền quản lý trạng thái ô đỗ tại bãi đỗ xe này." });
        }

        var (success, message, slot) = await _parkingService.UpdateSlotPhysicalStatusAsync(id, request, actorId, actorRole, clientIp, ct);

        if (!success)
        {
            if (message.Contains("Không tìm thấy"))
            {
                return NotFound(new { message });
            }
            return BadRequest(new { message });
        }

        return Ok(new
        {
            message,
            data = slot
        });
    }

    /// <summary>
    /// Xóa ô đỗ xe (Chỉ dành cho ADMIN; Từ chối nếu ô đỗ đã có lịch sử phiên hoặc điều phối).
    /// </summary>
    [HttpDelete("{id:long}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSlot(long id, CancellationToken ct)
    {
        var actorId = GetCurrentUserId();
        var (success, message) = await _parkingService.DeleteSlotAsync(id, actorId, ct);

        if (!success)
        {
            if (message.Contains("Không tìm thấy"))
            {
                return NotFound(new { message });
            }
            return BadRequest(new { message });
        }

        return Ok(new { message });
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

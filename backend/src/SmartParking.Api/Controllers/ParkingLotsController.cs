using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Bookings;
using SmartParking.Application.DTOs.Parking;
using SmartParking.Domain.Constants;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/parking-lots")]
[Produces("application/json")]
public class ParkingLotsController : ControllerBase
{
    private readonly IParkingManagementService _parkingService;
    private readonly IParkingAuthorizationService _authService;
    private readonly IBookingService _bookingService;
    private readonly ILogger<ParkingLotsController> _logger;

    public ParkingLotsController(
        IParkingManagementService parkingService,
        IParkingAuthorizationService authService,
        IBookingService bookingService,
        ILogger<ParkingLotsController> logger)
    {
        _parkingService = parkingService;
        _authService = authService;
        _bookingService = bookingService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách bãi đỗ xe có phân trang và tìm kiếm (Công khai cho khách/người dùng xem các bãi đang hoạt động).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<ParkingLotDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLots([FromQuery] ParkingLotFilterDto filter, CancellationToken ct)
    {
        var isAdmin = User.IsInRole(Roles.Admin);
        var result = await _parkingService.GetLotsAsync(filter, isAdmin, ct);
        return Ok(new
        {
            message = "Lấy danh sách bãi đỗ xe thành công.",
            data = result
        });
    }

    /// <summary>
    /// Lấy chi tiết bãi đỗ xe theo ID bao gồm danh sách tóm tắt các tầng và số lượng ô trống (Công khai).
    /// </summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParkingLotDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLotById(int id, CancellationToken ct)
    {
        var isAdmin = User.IsInRole(Roles.Admin);
        var lot = await _parkingService.GetLotByIdAsync(id, isAdmin, ct);
        if (lot == null)
        {
            return NotFound(new { message = $"Không tìm thấy bãi đỗ xe với ID: {id}." });
        }

        return Ok(new
        {
            message = "Lấy thông tin chi tiết bãi đỗ xe thành công.",
            data = lot
        });
    }

    /// <summary>
    /// Lấy thông tin sức chứa và tình trạng khả dụng thời gian thực của bãi đỗ xe (Công khai).
    /// Phục vụ hiển thị trực quan cho người dùng đặt chỗ và xe vãng lai.
    /// </summary>
    [HttpGet("{id:int}/capacity")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LotCapacityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLotCapacity(int id, CancellationToken ct)
    {
        var capacity = await _bookingService.GetLotCapacityAsync(id, ct);
        if (capacity == null)
        {
            return NotFound(new { message = $"Không tìm thấy bãi đỗ xe với ID: {id}." });
        }

        return Ok(new
        {
            message = "Lấy thông tin sức chứa bãi đỗ xe thành công.",
            data = capacity
        });
    }

    /// <summary>
    /// Tạo mới một bãi đỗ xe (Chỉ dành cho Quản trị viên hệ thống - ADMIN).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(ParkingLotDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateLot([FromBody] CreateParkingLotRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var (success, message, lot) = await _parkingService.CreateLotAsync(request, actorId, ct);

        if (!success)
        {
            return Conflict(new { message });
        }

        return CreatedAtAction(nameof(GetLotById), new { id = lot!.Id }, new
        {
            message,
            data = lot
        });
    }

    /// <summary>
    /// Cập nhật thông tin cấu trúc bãi đỗ xe (Chỉ dành cho ADMIN).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(ParkingLotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateLot(int id, [FromBody] UpdateParkingLotRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var (success, message, lot) = await _parkingService.UpdateLotAsync(id, request, actorId, ct);

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
            data = lot
        });
    }

    /// <summary>
    /// Mở hoặc đóng tiếp nhận xe vào bãi (IsActive) theo quy tắc nghiệp vụ.
    /// Dành cho ADMIN hoặc MANAGER được phân công tại bãi đỗ xe này.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    [ProducesResponseType(typeof(ParkingLotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLotStatus(int id, [FromBody] UpdateLotStatusRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        if (!await _authService.CanManageLotStatusAsync(actorId ?? 0, actorRole, id, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn không có quyền quản lý trạng thái tiếp nhận của bãi đỗ xe này." });
        }

        var (success, message, lot) = await _parkingService.UpdateLotStatusAsync(id, request, actorId, ct);

        if (!success)
        {
            return NotFound(new { message });
        }

        return Ok(new
        {
            message,
            data = lot
        });
    }

    /// <summary>
    /// Xóa bãi đỗ xe (Chỉ dành cho ADMIN; Từ chối nếu bãi đang có tầng hoặc lịch sử gửi xe).
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLot(int id, CancellationToken ct)
    {
        var actorId = GetCurrentUserId();
        var (success, message) = await _parkingService.DeleteLotAsync(id, actorId, ct);

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

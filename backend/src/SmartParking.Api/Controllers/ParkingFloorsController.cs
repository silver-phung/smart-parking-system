using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.DTOs.Parking;
using SmartParking.Domain.Constants;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/parking-floors")]
[Produces("application/json")]
public class ParkingFloorsController : ControllerBase
{
    private readonly IParkingManagementService _parkingService;
    private readonly ILogger<ParkingFloorsController> _logger;

    public ParkingFloorsController(IParkingManagementService parkingService, ILogger<ParkingFloorsController> logger)
    {
        _parkingService = parkingService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách các tầng của một bãi xe theo lotId (Công khai).
    /// </summary>
    [HttpGet("lot/{lotId:int}")]
    [HttpGet("/api/v1/parking-lots/{lotId:int}/floors")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ParkingFloorDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFloorsByLotId(int lotId, [FromQuery] bool? isActiveOnly, CancellationToken ct)
    {
        var floors = await _parkingService.GetFloorsByLotIdAsync(lotId, isActiveOnly, ct);
        return Ok(new
        {
            message = "Lấy danh sách tầng thành công.",
            data = floors
        });
    }

    /// <summary>
    /// Lấy chi tiết thông tin một tầng theo ID (Công khai).
    /// </summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParkingFloorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFloorById(int id, CancellationToken ct)
    {
        var floor = await _parkingService.GetFloorByIdAsync(id, ct);
        if (floor == null)
        {
            return NotFound(new { message = $"Không tìm thấy tầng với ID: {id}." });
        }

        return Ok(new
        {
            message = "Lấy thông tin tầng thành công.",
            data = floor
        });
    }

    /// <summary>
    /// Tạo mới một tầng trong bãi xe (Chỉ dành cho ADMIN).
    /// </summary>
    [HttpPost("lot/{lotId:int}")]
    [HttpPost("/api/v1/parking-lots/{lotId:int}/floors")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(ParkingFloorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateFloor(int lotId, [FromBody] CreateParkingFloorRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var (success, message, floor) = await _parkingService.CreateFloorAsync(lotId, request, actorId, ct);

        if (!success)
        {
            if (message.Contains("không tồn tại"))
            {
                return NotFound(new { message });
            }
            return Conflict(new { message });
        }

        return CreatedAtAction(nameof(GetFloorById), new { id = floor!.Id }, new
        {
            message,
            data = floor
        });
    }

    /// <summary>
    /// Cập nhật thông tin tầng (Chỉ dành cho ADMIN).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(ParkingFloorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateFloor(int id, [FromBody] UpdateParkingFloorRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        var (success, message, floor) = await _parkingService.UpdateFloorAsync(id, request, actorId, ct);

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
            data = floor
        });
    }

    /// <summary>
    /// Xóa tầng (Chỉ dành cho ADMIN; Từ chối nếu tầng đang chứa ô đỗ xe).
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFloor(int id, CancellationToken ct)
    {
        var actorId = GetCurrentUserId();
        var (success, message) = await _parkingService.DeleteFloorAsync(id, actorId, ct);

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

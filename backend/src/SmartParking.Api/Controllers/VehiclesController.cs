using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Vehicles;
using SmartParking.Domain.Constants;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/vehicles")]
[Produces("application/json")]
[Authorize(Roles = $"{Roles.Admin},{Roles.User}")]
public class VehiclesController : ControllerBase
{
    private readonly IVehicleService _vehicleService;
    private readonly ILogger<VehiclesController> _logger;

    public VehiclesController(IVehicleService vehicleService, ILogger<VehiclesController> logger)
    {
        _vehicleService = vehicleService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách phương tiện. USER chỉ xem các xe thuộc tài khoản của mình. ADMIN có thể xem toàn bộ hoặc lọc theo UserId.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VehicleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetVehicles([FromQuery] VehicleFilterDto filter, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var isAdmin = User.IsInRole(Roles.Admin);

        var result = await _vehicleService.GetVehiclesAsync(filter, currentUserId, isAdmin, ct);
        return Ok(new
        {
            message = "Lấy danh sách phương tiện thành công.",
            data = result
        });
    }

    /// <summary>
    /// Xem thông tin chi tiết một phương tiện theo ID. USER chỉ xem được xe thuộc tài khoản của mình.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVehicleById(long id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var isAdmin = User.IsInRole(Roles.Admin);

        var vehicle = await _vehicleService.GetVehicleByIdAsync(id, currentUserId, isAdmin, ct);
        if (vehicle == null)
        {
            return NotFound(new { message = $"Không tìm thấy phương tiện với ID: {id} hoặc bạn không có quyền truy cập." });
        }

        return Ok(new
        {
            message = "Lấy thông tin phương tiện thành công.",
            data = vehicle
        });
    }

    /// <summary>
    /// Tra cứu phương tiện theo biển số xe. USER chỉ tra cứu được nếu xe thuộc quyền sở hữu của mình.
    /// </summary>
    [HttpGet("by-plate/{plateNumber}")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVehicleByPlate(string plateNumber, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var isAdmin = User.IsInRole(Roles.Admin);

        var vehicle = await _vehicleService.GetVehicleByPlateAsync(plateNumber, currentUserId, isAdmin, ct);
        if (vehicle == null)
        {
            return NotFound(new { message = $"Không tìm thấy phương tiện với biển số '{plateNumber}' hoặc bạn không có quyền truy cập." });
        }

        return Ok(new
        {
            message = "Tra cứu phương tiện thành công.",
            data = vehicle
        });
    }

    /// <summary>
    /// Đăng ký phương tiện mới vào hệ thống.
    /// USER tự động được gán là chủ xe. ADMIN có thể đăng ký xe cho bất kỳ người dùng nào hoặc xe vãng lai (UserId = null).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateVehicle([FromBody] CreateVehicleRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserId = GetCurrentUserId();
        var isAdmin = User.IsInRole(Roles.Admin);

        var (success, message, vehicle) = await _vehicleService.CreateVehicleAsync(request, currentUserId, isAdmin, ct);
        if (!success)
        {
            if (message.Contains("không hợp lệ"))
            {
                return BadRequest(new { message });
            }
            return Conflict(new { message });
        }

        return CreatedAtAction(nameof(GetVehicleById), new { id = vehicle!.Id }, new
        {
            message,
            data = vehicle
        });
    }

    /// <summary>
    /// Cập nhật thông tin phương tiện (Hãng xe, model, màu sắc, trạng thái hoạt động).
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateVehicle(long id, [FromBody] UpdateVehicleRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserId = GetCurrentUserId();
        var isAdmin = User.IsInRole(Roles.Admin);

        var (success, message, vehicle) = await _vehicleService.UpdateVehicleAsync(id, request, currentUserId, isAdmin, ct);
        if (!success)
        {
            if (message.Contains("Không tìm thấy"))
            {
                return NotFound(new { message });
            }
            if (message.Contains("không có quyền"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message });
            }
            return BadRequest(new { message });
        }

        return Ok(new
        {
            message,
            data = vehicle
        });
    }

    /// <summary>
    /// Xóa phương tiện khỏi tài khoản. Từ chối xóa nếu xe đã từng có lịch sử gửi xe trong hệ thống.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteVehicle(long id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var isAdmin = User.IsInRole(Roles.Admin);

        var (success, message) = await _vehicleService.DeleteVehicleAsync(id, currentUserId, isAdmin, ct);
        if (!success)
        {
            if (message.Contains("Không tìm thấy"))
            {
                return NotFound(new { message });
            }
            if (message.Contains("không có quyền"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message });
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

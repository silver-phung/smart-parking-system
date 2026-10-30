using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Bookings;
using SmartParking.Domain.Constants;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/bookings")]
[Produces("application/json")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(IBookingService bookingService, ILogger<BookingsController> logger)
    {
        _bookingService = bookingService;
        _logger = logger;
    }

    /// <summary>
    /// Đặt trước vị trí đỗ xe tại bãi (Giữ công suất, không cố định ô đỗ vật lý).
    /// Dành cho người dùng đã đăng nhập hoặc nhân viên đặt hộ.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
        }

        var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.User;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, statusCode, message, data) = await _bookingService.CreateBookingAsync(request, userId.Value, userRole, clientIp, ct);
        if (!success)
        {
            return StatusCode(statusCode, new { message });
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            message,
            data
        });
    }

    /// <summary>
    /// Lấy danh sách lượt đặt chỗ của chính người dùng hiện tại đang đăng nhập.
    /// </summary>
    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(PagedResult<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyBookings([FromQuery] BookingFilterDto filter, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
        }

        var result = await _bookingService.GetMyBookingsAsync(userId.Value, filter, ct);
        return Ok(new
        {
            message = "Lấy danh sách đặt chỗ của bạn thành công.",
            data = result
        });
    }

    /// <summary>
    /// Lấy thông tin chi tiết một lượt đặt chỗ theo ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [Authorize]
    [ProducesResponseType(typeof(BookingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBookingById(long id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
        }

        var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.User;
        var (success, statusCode, message, data) = await _bookingService.GetBookingByIdAsync(id, userId.Value, userRole, ct);
        if (!success)
        {
            return StatusCode(statusCode, new { message });
        }

        return Ok(new
        {
            message,
            data
        });
    }

    /// <summary>
    /// Hủy lượt đặt chỗ của người dùng (Giải phóng công suất đã giữ cho bãi đỗ xe).
    /// </summary>
    [HttpPost("{id:long}/cancel")]
    [Authorize]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelBooking(long id, [FromBody] CancelBookingRequestDto request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
        }

        var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.User;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, statusCode, message, data) = await _bookingService.CancelBookingAsync(id, request, userId.Value, userRole, clientIp, ct);
        if (!success)
        {
            return StatusCode(statusCode, new { message });
        }

        return Ok(new
        {
            message,
            data
        });
    }

    /// <summary>
    /// Danh sách đặt chỗ dành cho Quản trị viên / Nhân viên vận hành (có phân trang và bộ lọc).
    /// Chỉ hiển thị các bãi đỗ xe mà nhân viên/quản lý được phân công.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff}")]
    [ProducesResponseType(typeof(PagedResult<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBookings([FromQuery] BookingFilterDto filter, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
        }

        var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? Roles.Staff;
        var data = await _bookingService.GetBookingsAsync(filter, userId.Value, userRole, ct);

        return Ok(new
        {
            message = "Lấy danh sách đặt chỗ thành công.",
            data
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

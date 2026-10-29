using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Common.Exceptions;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.StaffAssignments;
using SmartParking.Domain.Constants;
using SmartParking.Infrastructure.Services;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/staff-assignments")]
[Produces("application/json")]
public class StaffAssignmentsController : ControllerBase
{
    private readonly IStaffLotAssignmentService _assignmentService;
    private readonly ILogger<StaffAssignmentsController> _logger;
    private readonly IStaffDetectionService _detectionService;

    public StaffAssignmentsController(
        IStaffLotAssignmentService assignmentService,
        ILogger<StaffAssignmentsController> logger,
        IStaffDetectionService detectionService)
    {
        _assignmentService = assignmentService;
        _logger = logger;
        _detectionService = detectionService;
    }

    [HttpPost("/api/v1/staff/detect")]
    [Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Detect(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "A non-empty image file is required in the 'file' field." });

        var actorId = GetCurrentUserId();
        if (!actorId.HasValue)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _detectionService.DetectAsync(
                stream, file.ContentType, file.Length, actorId.Value, ct);
            return Ok(new { message = "Image recognized successfully.", data = result });
        }
        catch (ImageValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (AiServiceException exception)
        {
            return StatusCode((int)exception.StatusCode, new { message = exception.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "AI service is unavailable." });
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception,
                "Failed to persist AI recognition result for staff user {StaffUserId}.", actorId.Value);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "AI recognition database is unavailable." });
        }
    }

    [HttpPost("/api/v1/staff/detections/{id:long}/confirm")]
    [Authorize(Roles = $"{Roles.Staff},{Roles.Admin}")]
    [ProducesResponseType(typeof(StaffDetectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmDetection(
        long id, [FromBody] ConfirmStaffDetectionRequestDto request, CancellationToken ct)
    {
        var plateNumber = request.PlateNumber?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(plateNumber) || plateNumber.Length > 32
            || string.IsNullOrWhiteSpace(request.Note) && !System.Text.RegularExpressions.Regex.IsMatch(
                plateNumber, "^[A-Z0-9.-]+$"))
            return BadRequest(new { message = "A plate number between 1 and 32 characters is required." });

        var actorId = GetCurrentUserId();
        if (!actorId.HasValue)
            return Unauthorized(new { message = "Cannot identify the current user." });

        try
        {
            var result = await _detectionService.ConfirmAsync(id, plateNumber,
                request.Note?.Trim(), actorId.Value, ct);
            return result is null ? NotFound(new { message = "Recognition record was not found." }) : Ok(new { data = result });
        }
        catch (ImageValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    /// <summary>
    /// Phân công nhân sự (MANAGER hoặc STAFF) vào làm việc tại một bãi đỗ xe cụ thể.
    /// Chỉ dành riêng cho ADMIN.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(StaffLotAssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAssignment([FromBody] CreateStaffAssignmentRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        if (!actorId.HasValue)
        {
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, message, assignment) = await _assignmentService.CreateAssignmentAsync(request, actorId.Value, clientIp, ct);
        if (!success)
        {
            if (message.Contains("Không tìm thấy"))
            {
                return NotFound(new { message });
            }
            if (message.Contains("Xung đột") || message.Contains("hiện đã được phân công"))
            {
                return Conflict(new { message });
            }
            return BadRequest(new { message });
        }

        return CreatedAtAction(nameof(GetAssignmentById), new { id = assignment!.Id }, new
        {
            message,
            data = assignment
        });
    }

    /// <summary>
    /// Tra cứu danh sách phân công nhân sự bãi đỗ xe theo bộ lọc phân trang.
    /// Chỉ dành riêng cho ADMIN.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(PagedResult<StaffLotAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAssignments([FromQuery] StaffAssignmentFilterDto filter, CancellationToken ct)
    {
        var actorId = GetCurrentUserId();
        if (!actorId.HasValue)
        {
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
        }

        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var result = await _assignmentService.GetAssignmentsAsync(filter, actorId.Value, actorRole, ct);
        return Ok(new
        {
            message = "Lấy danh sách phân công nhân sự thành công.",
            data = result
        });
    }

    /// <summary>
    /// Lấy danh sách các bãi đỗ xe mà tài khoản đang đăng nhập (MANAGER hoặc STAFF) được phân công làm việc.
    /// </summary>
    [HttpGet("my-lots")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff}")]
    [ProducesResponseType(typeof(List<AssignedLotSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyAssignedLots(CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
        }

        var lots = await _assignmentService.GetMyAssignedLotsAsync(currentUserId.Value, ct);
        return Ok(new
        {
            message = "Lấy danh sách bãi xe được phân công thành công.",
            data = lots
        });
    }

    /// <summary>
    /// Xem chi tiết một bản ghi phân công theo ID.
    /// Chỉ dành riêng cho ADMIN.
    /// </summary>
    [HttpGet("{id:long}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(StaffLotAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssignmentById(long id, CancellationToken ct)
    {
        var actorId = GetCurrentUserId();
        if (!actorId.HasValue)
        {
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
        }

        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var assignment = await _assignmentService.GetAssignmentByIdAsync(id, actorId.Value, actorRole, ct);
        if (assignment == null)
        {
            return NotFound(new { message = $"Không tìm thấy bản ghi phân công với ID: {id} hoặc bạn không có quyền truy cập." });
        }

        return Ok(new
        {
            message = "Lấy chi tiết phân công thành công.",
            data = assignment
        });
    }

    /// <summary>
    /// Thu hồi quyền phân công nhân sự tại bãi xe (Soft-Revoke).
    /// Phân công sẽ bị vô hiệu hóa (is_active = false) và có hiệu lực ngay lập tức ở request tiếp theo.
    /// Chỉ dành riêng cho ADMIN.
    /// </summary>
    [HttpPost("{id:long}/revoke")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeAssignment(long id, [FromBody] RevokeStaffAssignmentRequestDto request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var actorId = GetCurrentUserId();
        if (!actorId.HasValue)
        {
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, message) = await _assignmentService.RevokeAssignmentAsync(id, request, actorId.Value, clientIp, ct);
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

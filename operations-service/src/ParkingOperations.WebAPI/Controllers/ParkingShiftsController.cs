using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingOperations.Application.CashHandovers;

namespace ParkingOperations.WebAPI.Controllers;

[ApiController]
[Route("api/v1/operations/shifts")]
[Produces("application/json")]
public sealed class ParkingShiftsController(IParkingShiftService shiftService, ILogger<ParkingShiftsController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "MANAGER,STAFF")]
    public async Task<IActionResult> List([FromQuery] int lotId, [FromQuery] DateOnly? businessDate,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        if (lotId <= 0 || page <= 0 || pageSize is < 1 or > 100)
            return BadRequest(new { message = "lotId, page hoặc pageSize không hợp lệ." });
        if (!TryActor(out var actorId))
            return Unauthorized(new { message = "Không xác định được người dùng từ access token." });
        try
        {
            var (items, total) = await shiftService.ListAsync(lotId, businessDate, page, pageSize, actorId,
                User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty, AccessToken(), ct);
            return Ok(new { data = new { items, totalCount = total, pageNumber = page, pageSize } });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Core authorization check failed while listing shifts for lot {LotId}", lotId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Không xác minh được quyền với Core. Hãy thử lại sau." });
        }
    }

    [HttpPost]
    [Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> Create([FromBody] CreateShiftRequest request, CancellationToken ct)
    {
        if (!TryActor(out var managerId))
            return Unauthorized(new { message = "Không xác định được người dùng từ access token." });
        try { return ToActionResult(await shiftService.CreateAsync(request, managerId, AccessToken(), ct), created: true); }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Core authorization check failed while creating shift for lot {LotId}", request.LotId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Không xác minh được quyền với Core. Hãy thử lại sau." });
        }
    }

    [HttpPost("{id:guid}/assignments")]
    [Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignStaffToShiftRequest request, CancellationToken ct)
    {
        if (!TryActor(out var managerId))
            return Unauthorized(new { message = "Không xác định được người dùng từ access token." });
        try { return ToActionResult(await shiftService.AssignAsync(id, request, managerId, AccessToken(), ct)); }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Core authorization check failed while assigning staff to shift {ShiftId}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Không xác minh được phân công nhân viên với Core. Hãy thử lại sau." });
        }
    }

    [HttpPost("{id:guid}/start")]
    [Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        if (!TryActor(out var managerId)) return Unauthorized();
        try { return ToActionResult(await shiftService.StartAsync(id, managerId, AccessToken(), ct)); }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Core authorization check failed while starting shift {ShiftId}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Không xác minh được quyền với Core. Hãy thử lại sau." });
        }
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        if (!TryActor(out var managerId)) return Unauthorized();
        try { return ToActionResult(await shiftService.CompleteAsync(id, managerId, AccessToken(), ct)); }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Core authorization check failed while completing shift {ShiftId}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Không xác minh được quyền với Core. Hãy thử lại sau." });
        }
    }

    private IActionResult ToActionResult(ShiftOperationResult result, bool created = false) =>
        !result.Success ? StatusCode(result.StatusCode, new { message = result.Message })
        : created ? StatusCode(StatusCodes.Status201Created, new { message = result.Message, data = result.Data })
        : Ok(new { message = result.Message, data = result.Data });

    private string AccessToken()
    {
        var authorization = Request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..].Trim()
            : string.Empty;
    }

    private bool TryActor(out long actorId)
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return long.TryParse(value, out actorId) && actorId > 0;
    }
}

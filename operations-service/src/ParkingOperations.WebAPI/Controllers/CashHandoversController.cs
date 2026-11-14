using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingOperations.Application.CashHandovers;

namespace ParkingOperations.WebAPI.Controllers;

[ApiController]
[Route("api/v1/operations/handovers")]
[Produces("application/json")]
public sealed class CashHandoversController(ICashHandoverService handoverService, ILogger<CashHandoversController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "MANAGER,STAFF")]
    public async Task<IActionResult> List([FromQuery] int lotId, [FromQuery] DateOnly? businessDate,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (lotId <= 0 || page <= 0 || pageSize is < 1 or > 100)
            return BadRequest(new { message = "lotId, page hoặc pageSize không hợp lệ." });
        if (!TryActor(out var actorId))
            return Unauthorized(new { message = "Không xác định được người dùng từ access token." });

        try
        {
            var (items, total) = await handoverService.ListAsync(lotId, businessDate, page, pageSize,
                AccessToken(), actorId, User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty, ct);
            return Ok(new { data = new { items, totalCount = total, pageNumber = page, pageSize } });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Core authorization check failed while listing handovers for lot {LotId}", lotId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Không xác minh được quyền với Core. Hãy thử lại sau." });
        }
    }

    [HttpPost]
    [Authorize(Roles = "MANAGER")]
    [ProducesResponseType(typeof(CashHandoverDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCashHandoverRequest request, CancellationToken ct)
    {
        if (!TryActor(out var managerId))
            return Unauthorized(new { message = "Không xác định được người dùng từ access token." });
        try
        {
            var result = await handoverService.CreateAsync(request, managerId, AccessToken(), ct);
            if (!result.Success)
                return StatusCode(result.StatusCode, new { message = result.Message });
            return CreatedAtAction(nameof(List), new { lotId = result.Data!.LotId, businessDate = result.Data.BusinessDate },
                new { message = result.Message, data = result.Data });
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Core authorization check failed while creating a handover for lot {LotId}", request.LotId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Không xác minh được quyền hoặc phân công với Core. Hãy thử lại sau." });
        }
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = "STAFF,MANAGER")]
    public async Task<IActionResult> Confirm(Guid id, [FromBody] ConfirmCashHandoverRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actorId))
            return Unauthorized(new { message = "Không xác định được người dùng từ access token." });
        try
        {
            var result = await handoverService.ConfirmAsync(id, request.CountedAmount, actorId,
                User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty, AccessToken(), ct);
            return ToActionResult(result);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Core assignment check failed while confirming handover {HandoverId}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Không xác minh được phân công với Core. Hãy thử lại sau." });
        }
    }

    [HttpPost("{id:guid}/dispute")]
    [Authorize(Roles = "STAFF,MANAGER")]
    public async Task<IActionResult> Dispute(Guid id, [FromBody] DisputeCashHandoverRequest request, CancellationToken ct)
    {
        if (!TryActor(out var actorId))
            return Unauthorized(new { message = "Không xác định được người dùng từ access token." });
        try
        {
            var result = await handoverService.DisputeAsync(id, request.Reason, actorId,
                User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty, AccessToken(), ct);
            return ToActionResult(result);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Core assignment check failed while disputing handover {HandoverId}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Không xác minh được phân công với Core. Hãy thử lại sau." });
        }
    }

    private IActionResult ToActionResult(CashHandoverOperationResult result) => result.Success
        ? Ok(new { message = result.Message, data = result.Data })
        : StatusCode(result.StatusCode, new { message = result.Message });

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

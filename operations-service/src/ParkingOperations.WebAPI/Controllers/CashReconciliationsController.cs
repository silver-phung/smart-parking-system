using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingOperations.Application.CashHandovers;

namespace ParkingOperations.WebAPI.Controllers;

[ApiController]
[Route("api/v1/operations/reconciliations")]
[Produces("application/json")]
[Authorize(Roles = "MANAGER")]
public sealed class CashReconciliationsController(ICashReconciliationService service, ILogger<CashReconciliationsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int lotId, CancellationToken ct)
    {
        if (lotId <= 0) return BadRequest(new { message = "lotId không hợp lệ." });
        try
        {
            var (items, total) = await service.ListAsync(lotId, Token(), ct);
            return Ok(new { data = new { items, totalCount = total } });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (HttpRequestException ex) { logger.LogWarning(ex, "Cannot verify manager lot assignment for reconciliation list"); return StatusCode(503, new { message = "Không xác minh được quyền với Core. Hãy thử lại sau." }); }
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitCashReconciliationRequest request, CancellationToken ct)
    {
        if (!Actor(out var managerId)) return Unauthorized();
        try { return Result(await service.SubmitAsync(request, managerId, Token(), ct), true); }
        catch (HttpRequestException ex) { logger.LogWarning(ex, "Cannot verify manager lot assignment for reconciliation submission"); return StatusCode(503, new { message = "Không xác minh được quyền với Core. Hãy thử lại sau." }); }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveCashReconciliationRequest request, CancellationToken ct)
    {
        if (!Actor(out var managerId)) return Unauthorized();
        try { return Result(await service.ApproveAsync(id, request, managerId, Token(), ct)); }
        catch (HttpRequestException ex) { logger.LogWarning(ex, "Cannot verify manager lot assignment for reconciliation approval {ReconciliationId}", id); return StatusCode(503, new { message = "Không xác minh được quyền với Core. Hãy thử lại sau." }); }
    }

    private IActionResult Result(CashReconciliationOperationResult result, bool created = false) => !result.Success
        ? StatusCode(result.StatusCode, new { message = result.Message })
        : created ? StatusCode(201, new { message = result.Message, data = result.Data })
        : Ok(new { message = result.Message, data = result.Data });
    private string Token() { var value = Request.Headers.Authorization.ToString(); return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..].Trim() : string.Empty; }
    private bool Actor(out long id) { var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value; return long.TryParse(value, out id) && id > 0; }
}

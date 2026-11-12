using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Infrastructure.Services;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/parking-lots/{lotId:int}/scene")]
public sealed class ParkingSceneController(ParkingSceneService scene) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Read(int lotId, CancellationToken ct)
    {
        var authenticated = User.Identity?.IsAuthenticated == true;
        long? actor = authenticated && long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;
        var result = await scene.Read(lotId, actor, authenticated ? User.FindFirstValue(ClaimTypes.Role) ?? "" : "", ct);
        return result == null ? NotFound(new { message = "Không tìm thấy bãi đỗ." }) : Ok(new { data = result });
    }
}

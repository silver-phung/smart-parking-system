using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Application.DTOs.Operations;
using SmartParking.Domain.Constants;
using SmartParking.Infrastructure.Services;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/v1/parking-lots/{lotId:int}/operations")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Manager},{Roles.Staff}")]
public class ParkingOperationsController(ParkingOperationsService service) : ControllerBase
{
    private long Actor => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? "";
    private async Task<IActionResult> Run(Func<Task<object>> operation)
    {
        try { return Ok(new { data = await operation() }); }
        catch (ParkingOperationException e) { return StatusCode(e.Status, new { e.Code, message = e.Message }); }
    }
    [HttpGet("zones")]
    public Task<IActionResult> Zones(int lotId, CancellationToken ct) => Run(() => service.Zones(lotId, Actor, Role, ct));
    [HttpGet("layout")]
    public Task<IActionResult> Layout(int lotId, CancellationToken ct) => Run(() => service.Layout(lotId, Actor, Role, ct));
    [HttpGet("sessions/{id:long}/evidence")]
    public Task<IActionResult> Evidence(int lotId, long id, CancellationToken ct) => Run(() => service.Evidence(lotId, id, Actor, Role, ct));
    [HttpPost("zones")]
    public Task<IActionResult> CreateZone(int lotId, ZoneRequest r, CancellationToken ct) => Run(() => service.SaveZone(lotId, null, r, Actor, Role, ct));
    [HttpPut("zones/{id:int}")]
    public Task<IActionResult> UpdateZone(int lotId, int id, ZoneRequest r, CancellationToken ct) => Run(() => service.SaveZone(lotId, id, r, Actor, Role, ct));
    [HttpPut("floors/{id:int}/zone")]
    public Task<IActionResult> FloorZone(int lotId, int id, FloorZoneRequest r, CancellationToken ct) => Run(() => service.AssignFloor(lotId, id, r.ZoneId, Actor, Role, ct));
    [HttpPut("slots/{id:long}/fuel")]
    public Task<IActionResult> SlotFuel(int lotId, long id, SlotFuelRequest r, CancellationToken ct) => Run(() => service.ConfigureFuel(lotId, id, r, Actor, Role, ct));
    [HttpGet("policies")]
    public Task<IActionResult> Policies(int lotId, CancellationToken ct) => Run(() => service.Policies(lotId, Actor, Role, ct));
    [HttpGet("policies/effective")]
    public Task<IActionResult> Policy(int lotId, int? zoneId, CancellationToken ct) => Run(() => service.EffectivePolicy(lotId, zoneId, Actor, Role, ct));
    [HttpPost("policies")]
    public Task<IActionResult> CreatePolicy(int lotId, PolicyRequest r, CancellationToken ct) => Run(() => service.CreatePolicy(lotId, r, Actor, Role, ct));
    [HttpPost("entries")]
    public Task<IActionResult> Entry(int lotId, EntryRequest r, CancellationToken ct) => Run(async () => await service.Enter(lotId, r, Actor, Role, ct));
    [HttpGet("sessions")]
    public Task<IActionResult> Sessions(int lotId, bool waitingOnly, CancellationToken ct) => Run(() => service.Sessions(lotId, waitingOnly, Actor, Role, ct));
    [HttpGet("tickets/{code}")]
    public Task<IActionResult> Ticket(int lotId, string code, CancellationToken ct) => Run(async () => await service.Lookup(lotId, code, Actor, Role, ct));
    [HttpPost("tickets/{code}/change")]
    public Task<IActionResult> ChangeTicket(int lotId, string code, TicketChangeRequest r, CancellationToken ct) => Run(() => service.ChangeTicket(lotId, code, r, Actor, Role, ct));
    [HttpPost("sessions/{id:long}/verify")]
    public Task<IActionResult> Verify(int lotId, long id, VerifyVehicleRequest r, CancellationToken ct) => Run(async () => await service.Verify(lotId, id, r, Actor, Role, ct));
    [HttpPost("sessions/{id:long}/move")]
    public Task<IActionResult> Move(int lotId, long id, MoveVehicleRequest r, CancellationToken ct) => Run(async () => await service.Move(lotId, id, r, Actor, Role, ct));
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartParking.Application.Services;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("parking-lots")]
public class SimulatorParkingLotsController : ControllerBase
{
    private readonly ParkingLotService _parkingLotService;
    private readonly ILogger<SimulatorParkingLotsController> _logger;

    public SimulatorParkingLotsController(
        ParkingLotService parkingLotService,
        ILogger<SimulatorParkingLotsController> logger)
    {
        _parkingLotService = parkingLotService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var parkingLots = await _parkingLotService.GetAllAsync();
            return Ok(parkingLots);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Không thể tải danh sách bãi đỗ xe.");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Không thể tải danh sách bãi đỗ xe. Vui lòng thử lại sau." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi tải danh sách bãi đỗ xe.");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Không thể tải danh sách bãi đỗ xe. Vui lòng thử lại sau." });
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartParking.Application.IoT;
using SmartParking.Application.Services;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IoTController : ControllerBase
{
    private readonly IoTService _ioTService;
    private readonly ParkingLotService _parkingLotService;
    private readonly ILogger<IoTController> _logger;

    public IoTController(
        IoTService ioTService,
        ParkingLotService parkingLotService,
        ILogger<IoTController> logger)
    {
        _ioTService = ioTService;
        _parkingLotService = parkingLotService;
        _logger = logger;
    }

    /// <summary>
    /// Nhận sự kiện xe vào/ra từ cảm biến IoT hoặc Simulator (US-FR-IOT-001).
    /// Ràng buộc: Cùng 1 biển số xe (carId) không thể check-in 2 lần;
    /// check-out phải đúng bãi mà xe đang đỗ.
    /// </summary>
    [HttpPost("events")]
    public async Task<IActionResult> IngestEvent([FromBody] IoTEventDto eventDto)
    {
        _logger.LogInformation("IoT Ingestion: Car={CarId}, Lot={Lot}, Type={Type}",
            eventDto.CarId, eventDto.ParkingLotId, eventDto.EventType);

        var result = await _ioTService.ProcessEventAsync(eventDto);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách lịch sử telemetry các sự kiện IoT gần nhất (tối đa 200 bản ghi).
    /// </summary>
    [HttpGet("telemetry")]
    public async Task<IActionResult> GetTelemetryHistory()
    {
        var history = await _ioTService.GetTelemetryHistoryAsync();
        return Ok(history);
    }

    /// <summary>
    /// Lấy danh sách xe đang đỗ trong tất cả các bãi (theo bộ nhớ IoT Ingestion).
    /// </summary>
    [HttpGet("parked-cars")]
    public async Task<IActionResult> GetParkedCars()
    {
        var cars = await _ioTService.GetParkedCarsAsync();
        return Ok(cars);
    }

    /// <summary>
    /// Reset dữ liệu 3 bãi đỗ xe, telemetry và danh sách xe đang đỗ về trạng thái mặc định.
    /// </summary>
    // DEPRECATED Sprint 3: Đừng gọi endpoint này trong production. Dùng ParkingEventController thay thế.
    [Obsolete("DEPRECATED Sprint 3: Dùng ParkingEventController thay thế.")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost("reset")]
    public async Task<IActionResult> Reset()
    {
        var success = await _ioTService.ResetAllAsync();
        return Ok(new { success, message = "Đã reset bãi đỗ xe, telemetry và danh sách xe đang đỗ về mặc định." });
    }
}

using System.Collections.Concurrent;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.IoT;

namespace SmartParking.Application.Services;

public class IoTService
{
    private readonly IParkingLotRepository _repository;

    // Lịch sử sự kiện telemetry (giới hạn 200 bản ghi gần nhất)
    private static readonly ConcurrentQueue<IoTTelemetryDto> _telemetryHistory = new();
    private const int MaxHistory = 200;

    // Bộ nhớ theo dõi xe đang đỗ: Key = carId (biển số), Value = thông tin bãi + slot
    // Ngăn chặn: cùng 1 xe check-in 2 lần, hoặc check-out xe chưa từng check-in.
    private static readonly ConcurrentDictionary<string, ParkedCarInfoDto> _parkedCars = new(StringComparer.OrdinalIgnoreCase);

    public IoTService(IParkingLotRepository repository)
    {
        _repository = repository;
    }

    public async Task<IoTEventResultDto> ProcessEventAsync(IoTEventDto dto)
    {
        if (dto.ParkingLotId <= 0)
        {
            return Fail("ParkingLotId không hợp lệ.");
        }

        var carId = (dto.CarId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(carId))
        {
            return Fail("carId (biển số xe) không được để trống.");
        }

        var lot = await _repository.GetByIdAsync(dto.ParkingLotId);
        if (lot == null)
        {
            return Fail($"Không tìm thấy bãi đỗ xe ID={dto.ParkingLotId}.");
        }

        int delta = 0;
        string normalizedType = (dto.EventType ?? "CarParked").Trim();

        if (normalizedType.Equals("CarParked", StringComparison.OrdinalIgnoreCase) ||
            normalizedType.Equals("CheckIn", StringComparison.OrdinalIgnoreCase))
        {
            if (_parkedCars.TryGetValue(carId, out var existingInfo))
            {
                return new IoTEventResultDto
                {
                    Success = false,
                    Message = $"Xe '{carId}' đã đang đỗ tại bãi '{existingInfo.ParkingLotName}' (check-in lúc {existingInfo.CheckInTime:HH:mm:ss}). Không thể check-in trùng!",
                    CarId = carId,
                    ParkingLotId = lot.Id,
                    ParkingLotName = lot.Name,
                    AvailableSlots = lot.AvailableSlots,
                    EventType = normalizedType,
                    ProcessedAt = DateTimeOffset.UtcNow
                };
            }

            if (lot.AvailableSlots <= 0)
            {
                return new IoTEventResultDto
                {
                    Success = false,
                    Message = $"Bãi '{lot.Name}' đã hết chỗ trống (0 chỗ)! Xe '{carId}' không thể vào.",
                    CarId = carId,
                    ParkingLotId = lot.Id,
                    ParkingLotName = lot.Name,
                    AvailableSlots = 0,
                    EventType = normalizedType,
                    ProcessedAt = DateTimeOffset.UtcNow
                };
            }

            delta = -1;

            _parkedCars[carId] = new ParkedCarInfoDto
            {
                CarId = carId,
                ParkingLotId = lot.Id,
                ParkingLotName = lot.Name,
                SlotNumber = dto.SlotNumber,
                CheckInTime = dto.Timestamp ?? DateTimeOffset.UtcNow
            };
        }
        else if (normalizedType.Equals("CarDeparted", StringComparison.OrdinalIgnoreCase) ||
                 normalizedType.Equals("CheckOut", StringComparison.OrdinalIgnoreCase))
        {
            if (!_parkedCars.TryGetValue(carId, out var parkedInfo))
            {
                return new IoTEventResultDto
                {
                    Success = false,
                    Message = $"Xe '{carId}' không có trong hệ thống (chưa check-in). Không thể check-out!",
                    CarId = carId,
                    ParkingLotId = lot.Id,
                    ParkingLotName = lot.Name,
                    AvailableSlots = lot.AvailableSlots,
                    EventType = normalizedType,
                    ProcessedAt = DateTimeOffset.UtcNow
                };
            }

            if (parkedInfo.ParkingLotId != dto.ParkingLotId)
            {
                return new IoTEventResultDto
                {
                    Success = false,
                    Message = $"Xe '{carId}' đang đỗ tại bãi '{parkedInfo.ParkingLotName}' (ID {parkedInfo.ParkingLotId}), không phải bãi '{lot.Name}' (ID {lot.Id}). Vui lòng check-out đúng bãi!",
                    CarId = carId,
                    ParkingLotId = lot.Id,
                    ParkingLotName = lot.Name,
                    AvailableSlots = lot.AvailableSlots,
                    EventType = normalizedType,
                    ProcessedAt = DateTimeOffset.UtcNow
                };
            }

            delta = 1;
            _parkedCars.TryRemove(carId, out _);
        }
        else if (normalizedType.Equals("Fault", StringComparison.OrdinalIgnoreCase))
        {
            delta = 0;
        }

        if (delta != 0)
        {
            await _repository.UpdateAvailableSlotsAsync(dto.ParkingLotId, delta);
            lot = await _repository.GetByIdAsync(dto.ParkingLotId) ?? lot;
        }

        var telemetry = new IoTTelemetryDto
        {
            CarId = carId,
            ParkingLotId = lot.Id,
            ParkingLotName = lot.Name,
            SlotNumber = dto.SlotNumber ?? "Auto",
            EventType = normalizedType,
            Timestamp = dto.Timestamp ?? DateTimeOffset.UtcNow,
            Status = normalizedType.Equals("Fault", StringComparison.OrdinalIgnoreCase) ? "Fault" : "Normal"
        };

        _telemetryHistory.Enqueue(telemetry);
        while (_telemetryHistory.Count > MaxHistory)
        {
            _telemetryHistory.TryDequeue(out _);
        }

        return new IoTEventResultDto
        {
            Success = true,
            Message = delta < 0
                ? $"Xe '{carId}' vào bãi '{lot.Name}'. Số chỗ còn lại: {lot.AvailableSlots}."
                : delta > 0
                    ? $"Xe '{carId}' rời bãi '{lot.Name}'. Số chỗ còn lại: {lot.AvailableSlots}."
                    : $"Cảm biến ghi nhận lỗi (Fault) tại bãi '{lot.Name}'.",
            CarId = carId,
            ParkingLotId = lot.Id,
            ParkingLotName = lot.Name,
            AvailableSlots = lot.AvailableSlots,
            EventType = normalizedType,
            ProcessedAt = DateTimeOffset.UtcNow
        };
    }

    public Task<IEnumerable<IoTTelemetryDto>> GetTelemetryHistoryAsync()
    {
        return Task.FromResult<IEnumerable<IoTTelemetryDto>>(_telemetryHistory.ToArray().Reverse());
    }

    public Task<IEnumerable<ParkedCarInfoDto>> GetParkedCarsAsync()
    {
        return Task.FromResult<IEnumerable<ParkedCarInfoDto>>(_parkedCars.Values.ToArray());
    }

    public async Task<bool> ResetAllAsync()
    {
        _telemetryHistory.Clear();
        _parkedCars.Clear();
        return await _repository.ResetToDefaultAsync();
    }

    private static IoTEventResultDto Fail(string message) => new()
    {
        Success = false,
        Message = message,
        ProcessedAt = DateTimeOffset.UtcNow
    };
}

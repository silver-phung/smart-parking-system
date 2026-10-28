using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Events.Dtos;
using SmartParking.Domain.Entities;

namespace SmartParking.Api.Controllers;

/// <summary>
/// API quản lý và cung cấp danh sách ô đỗ xe cho giao diện 3D / 2D (Three.js & Web Viewer).
/// Hỗ trợ cả 2 route chuẩn:
///   - /api/ParkingSlots (theo yêu cầu Sprint 3 - Ngày 6-9)
///   - /api/v1/parking-slots (tương thích cấu trúc RESTful của Phụng)
/// </summary>
[ApiController]
[Produces("application/json")]
public class ParkingSlotsController : ControllerBase
{
    private readonly IParkingSessionRepository _sessionRepo;
    private readonly ILogger<ParkingSlotsController> _logger;

    // Cache bộ nhớ lưu trạng thái override thủ công hoặc tùy chỉnh của các ô
    private static readonly ConcurrentDictionary<string, ParkingSlotDto> _slotStore = new(StringComparer.OrdinalIgnoreCase);
    private static bool _initialized = false;
    private static readonly object _initLock = new();

    public ParkingSlotsController(
        IParkingSessionRepository sessionRepo,
        ILogger<ParkingSlotsController> logger)
    {
        _sessionRepo = sessionRepo;
        _logger = logger;
        EnsureDefaultSlotsInitialized();
    }

    /// <summary>
    /// Lấy danh sách ô đỗ xe của bãi/tầng để render 3D tự động.
    /// GET /api/ParkingSlots?lotId=1&floorId=B1
    /// </summary>
    [HttpGet("/api/ParkingSlots")]
    [HttpGet("/api/v1/parking-slots")]
    [ProducesResponseType(typeof(IReadOnlyList<ParkingSlotDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSlots(
        [FromQuery] int lotId = 1,
        [FromQuery] string? floorId = null,
        CancellationToken ct = default)
    {
        var targetFloor = string.IsNullOrWhiteSpace(floorId) ? "B1" : floorId.Trim().ToUpperInvariant();
        var slots = _slotStore.Values
            .Where(s => s.LotId == lotId && (string.IsNullOrWhiteSpace(floorId) || s.FloorId.Equals(targetFloor, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(s => s.SlotCode)
            .ToList();

        // Đồng bộ dữ liệu phiên xe thực tế từ IParkingSessionRepository
        var activeSessions = await _sessionRepo.GetActiveByLotAsync(lotId, ct);
        var sessionBySlot = activeSessions
            .Where(s => !string.IsNullOrEmpty(s.SlotCode))
            .ToDictionary(s => s.SlotCode!, s => s, StringComparer.OrdinalIgnoreCase);

        // Các phiên đang chờ gán slot (pending_slot) có thể gán tạm thời trạng thái RESERVED cho ô demo
        var pendingSessions = activeSessions
            .Where(s => s.Status == ParkingSessionStatus.PendingSlot)
            .ToList();

        foreach (var slot in slots)
        {
            if (sessionBySlot.TryGetValue(slot.SlotCode, out var session))
            {
                if (session.Status == ParkingSessionStatus.Parked)
                {
                    slot.PhysicalStatus = "OCCUPIED";
                    slot.CurrentCarPlate = session.CarPlate;
                    slot.VehicleColor = session.CarColor ?? "white";
                    slot.VehicleModel = session.CarModel ?? "Sedan";
                    slot.SessionId = session.SessionId;
                    slot.ParkedSinceUtc = session.ParkedAtUtc;
                }
            }
        }

        return Ok(new
        {
            success = true,
            lotId,
            floorId = targetFloor,
            total = slots.Count,
            availableCount = slots.Count(s => s.PhysicalStatus == "EMPTY"),
            occupiedCount = slots.Count(s => s.PhysicalStatus == "OCCUPIED"),
            reservedCount = slots.Count(s => s.PhysicalStatus == "RESERVED"),
            maintenanceCount = slots.Count(s => s.PhysicalStatus == "MAINTENANCE"),
            unavailableCount = slots.Count(s => s.PhysicalStatus == "UNAVAILABLE"),
            slots
        });
    }

    /// <summary>
    /// Lấy danh sách ô đỗ xe theo tầng cụ thể:
    /// GET /api/ParkingSlots/floor/{floorId}
    /// </summary>
    [HttpGet("/api/ParkingSlots/floor/{floorId}")]
    [HttpGet("/api/v1/parking-floors/{floorId}/slots")]
    public Task<IActionResult> GetSlotsByFloor(string floorId, [FromQuery] int lotId = 1, CancellationToken ct = default)
    {
        return GetSlots(lotId, floorId, ct);
    }

    /// <summary>
    /// Cập nhật nhanh trạng thái vật lý của một ô đỗ (cho demo tương tác trực tiếp trên 3D):
    /// PATCH /api/ParkingSlots/{slotCode}/status
    /// </summary>
    [HttpPatch("/api/ParkingSlots/{slotCode}/status")]
    [HttpPatch("/api/v1/parking-slots/{slotCode}/status")]
    public IActionResult UpdateSlotStatus(string slotCode, [FromBody] UpdateSlotStatusRequest request)
    {
        var normalizedCode = slotCode.Trim().ToUpperInvariant();
        var existing = _slotStore.Values.FirstOrDefault(s => s.SlotCode.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            return NotFound(new { success = false, message = $"Không tìm thấy ô đỗ có mã {slotCode}." });
        }

        var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EMPTY", "OCCUPIED", "RESERVED", "MAINTENANCE", "UNAVAILABLE"
        };

        var newStatus = request.PhysicalStatus?.Trim().ToUpperInvariant() ?? "EMPTY";
        if (!validStatuses.Contains(newStatus))
        {
            return BadRequest(new { success = false, message = $"Trạng thái '{request.PhysicalStatus}' không hợp lệ. Phải là: EMPTY, OCCUPIED, RESERVED, MAINTENANCE, UNAVAILABLE." });
        }

        existing.PhysicalStatus = newStatus;
        if (newStatus == "OCCUPIED")
        {
            existing.CurrentCarPlate = !string.IsNullOrWhiteSpace(request.CarPlate) ? request.CarPlate.Trim().ToUpperInvariant() : "51F-88888";
            existing.ParkedSinceUtc = DateTimeOffset.UtcNow;
        }
        else if (newStatus == "EMPTY")
        {
            existing.CurrentCarPlate = null;
            existing.SessionId = null;
            existing.ParkedSinceUtc = null;
        }
        else if (newStatus == "RESERVED")
        {
            existing.CurrentCarPlate = !string.IsNullOrWhiteSpace(request.CarPlate) ? request.CarPlate.Trim().ToUpperInvariant() : "51G-99999";
        }

        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _logger.LogInformation("Slot {SlotCode} status updated to {Status}", normalizedCode, newStatus);

        return Ok(new
        {
            success = true,
            message = $"Cập nhật trạng thái ô {slotCode} thành công.",
            slot = existing
        });
    }

    private void EnsureDefaultSlotsInitialized()
    {
        if (_initialized) return;

        lock (_initLock)
        {
            if (_initialized) return;

            // Khởi tạo các tầng và ô đỗ 3D mẫu tiêu chuẩn
            // Khu A: Ô tô xăng/dầu chuẩn (A1-001 -> A1-016)
            // Khu B: Ô tô điện EV charging (B1-001 -> B1-012)
            // Khu C: Xe máy hoặc ô tô VIP (C1-001 -> C1-010)
            var rng = new Random(20261028);

            // Bãi 1 - Tầng Hầm B1
            GenerateFloorSlots(lotId: 1, lotName: "Bãi xe FPT Software Quận 9", floorId: "B1", floorName: "Hầm B1", rng);

            // Bãi 1 - Tầng Hầm B2
            GenerateFloorSlots(lotId: 1, lotName: "Bãi xe FPT Software Quận 9", floorId: "B2", floorName: "Hầm B2", rng);

            // Bãi 2 - Tầng Trệt
            GenerateFloorSlots(lotId: 2, lotName: "Bãi xe Vincom Landmark 81", floorId: "G", floorName: "Tầng Trệt", rng);

            _initialized = true;
        }
    }

    private static void GenerateFloorSlots(int lotId, string lotName, string floorId, string floorName, Random rng)
    {
        long idCounter = lotId * 1000L + (floorId.GetHashCode() & 0xFF);

        // Hàng 1 (Dãy A - Phía Bắc): Ô tô xăng/diesel, tọa độ Z = -8m, X trải dài từ -18m đến +18m
        for (int i = 1; i <= 10; i++)
        {
            string code = $"{floorId}-A{i:D2}";
            string status = i switch
            {
                1 or 4 or 7 => "OCCUPIED",
                3 => "RESERVED", // Ô được phân nhưng xe chưa vào (nhấp nháy viền)
                8 => "MAINTENANCE",
                _ => "EMPTY"
            };

            var slot = new ParkingSlotDto
            {
                Id = ++idCounter,
                LotId = lotId,
                LotName = lotName,
                FloorId = floorId,
                FloorName = floorName,
                SlotCode = code,
                PhysicalStatus = status,
                VehicleType = "car",
                PositionX = -20 + (i * 3.6),
                PositionY = 0,
                PositionZ = -8,
                RotationY = 0,
                CurrentCarPlate = status == "OCCUPIED" ? $"51F-{rng.Next(100, 999)}.{rng.Next(10, 99)}" :
                                  status == "RESERVED" ? $"72A-{rng.Next(100, 999)}.{rng.Next(10, 99)}" : null,
                VehicleColor = status == "OCCUPIED" ? (i % 3 == 0 ? "white" : i % 3 == 1 ? "black" : "blue") : null,
                VehicleModel = status == "OCCUPIED" ? (i % 2 == 0 ? "Toyota Vios" : "Mazda 3") : null,
                ParkedSinceUtc = status == "OCCUPIED" ? DateTimeOffset.UtcNow.AddMinutes(-rng.Next(15, 180)) : null
            };
            _slotStore[code] = slot;
        }

        // Hàng 2 (Dãy B - Phía Nam): Ô tô điện EV, tọa độ Z = +8m, X trải dài từ -18m đến +18m
        for (int i = 1; i <= 10; i++)
        {
            string code = $"{floorId}-B{i:D2}";
            string status = i switch
            {
                2 or 6 => "OCCUPIED",
                5 => "RESERVED",
                9 => "UNAVAILABLE",
                _ => "EMPTY"
            };

            var slot = new ParkingSlotDto
            {
                Id = ++idCounter,
                LotId = lotId,
                LotName = lotName,
                FloorId = floorId,
                FloorName = floorName,
                SlotCode = code,
                PhysicalStatus = status,
                VehicleType = i <= 6 ? "ev" : "car",
                PositionX = -20 + (i * 3.6),
                PositionY = 0,
                PositionZ = 8,
                RotationY = 180,
                CurrentCarPlate = status == "OCCUPIED" ? $"30H-{rng.Next(100, 999)}.{rng.Next(10, 99)}" :
                                  status == "RESERVED" ? $"51K-{rng.Next(100, 999)}.{rng.Next(10, 99)}" : null,
                VehicleColor = status == "OCCUPIED" ? "emerald" : null,
                VehicleModel = status == "OCCUPIED" ? "VinFast VF8" : null,
                ParkedSinceUtc = status == "OCCUPIED" ? DateTimeOffset.UtcNow.AddMinutes(-rng.Next(20, 120)) : null
            };
            _slotStore[code] = slot;
        }

        // Cũng khởi tạo thêm mã định dạng A1-012, B2-005 như trong quy chuẩn Sprint 3
        if (floorId == "B1" && lotId == 1)
        {
            _slotStore["A1-012"] = new ParkingSlotDto
            {
                Id = 1012,
                LotId = 1,
                LotName = lotName,
                FloorId = "B1",
                FloorName = "Hầm B1",
                SlotCode = "A1-012",
                PhysicalStatus = "OCCUPIED",
                VehicleType = "car",
                PositionX = -20 + (12 * 3.6),
                PositionY = 0,
                PositionZ = -8,
                RotationY = 0,
                CurrentCarPlate = "51F-123.45",
                VehicleColor = "white",
                VehicleModel = "Toyota Vios",
                ParkedSinceUtc = DateTimeOffset.UtcNow.AddMinutes(-45)
            };
        }
    }
}

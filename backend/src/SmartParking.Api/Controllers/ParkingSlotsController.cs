using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Events.Dtos;
using SmartParking.Domain.Entities;

namespace SmartParking.Api.Controllers;

/// <summary>
/// API quản lý và cung cấp danh sách ô đỗ xe cho giao diện 3D / 2D (Three.js & Web Viewer).
/// Hỗ trợ cả 3 bãi mẫu theo yêu cầu Sprint 3:
///   - Bãi 1: Mô hình hầm ngầm Sketchfab (2 tầng: B1, B2) - Tổng slot theo DB (64 slots)
///   - Bãi 2: 3 tầng (B1, B2, B3) - Số slot random
///   - Bãi 3: 4 tầng (B1, B2, B3, B4) - Số slot random
/// </summary>
[ApiController]
[Produces("application/json")]
public class ParkingSlotsController : ControllerBase
{
    private readonly IParkingSessionRepository _sessionRepo;
    private readonly ILogger<ParkingSlotsController> _logger;

    // Cache bộ nhớ lưu trạng thái của các ô trong 3 bãi
    private static readonly ConcurrentDictionary<string, ParkingSlotDto> _slotStore = new(StringComparer.OrdinalIgnoreCase);
    private static bool _initialized = false;
    private static readonly object _initLock = new();

    public record ParkingLotMetadata(int Id, string Name, string Description, int FloorCount, string[] Floors, int TotalSlots);

    public ParkingSlotsController(
        IParkingSessionRepository sessionRepo,
        ILogger<ParkingSlotsController> logger)
    {
        _sessionRepo = sessionRepo;
        _logger = logger;
        EnsureDefaultSlotsInitialized();
    }

    /// <summary>
    /// Lấy danh sách 3 bãi đỗ xe mẫu kèm thông tin số tầng:
    /// GET /api/ParkingSlots/lots
    /// </summary>
    [HttpGet("/api/ParkingSlots/lots")]
    public IActionResult GetLotMetadata()
    {
        var lots = new List<ParkingLotMetadata>
        {
            new(
                1,
                "Bãi 1: Hầm ngầm FPT Q9 (Mô hình Sketchfab)",
                "Mô hình hầm đỗ xe ngầm 2 tầng theo chuẩn DB (64 slots, 2 tầng B1-B2)",
                2,
                ["B1", "B2"],
                _slotStore.Values.Count(s => s.LotId == 1)
            ),
            new(
                2,
                "Bãi 2: Vincom Landmark 81 Underground",
                "Mô hình bãi xe ngầm 3 tầng (B1, B2, B3) với số lượng slot ngẫu nhiên",
                3,
                ["B1", "B2", "B3"],
                _slotStore.Values.Count(s => s.LotId == 2)
            ),
            new(
                3,
                "Bãi 3: Aeon Mall Mega Parking Hub",
                "Mô hình đại siêu thị 4 tầng ngầm (B1, B2, B3, B4) với số lượng slot ngẫu nhiên",
                4,
                ["B1", "B2", "B3", "B4"],
                _slotStore.Values.Count(s => s.LotId == 3)
            )
        };

        return Ok(new { success = true, lots });
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
    /// Cập nhật nhanh trạng thái vật lý của một ô đỗ:
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

            var rng = new Random(20261028);

            // =========================================================
            // BÃI 1: Mô hình hầm ngầm Sketchfab (2 tầng: B1, B2)
            // Tổng slot theo DB: Mỗi tầng 32 ô (Tổng cộng 64 slots)
            // =========================================================
            GenerateStructuredSlots(lotId: 1, lotName: "Bãi 1: Hầm ngầm FPT Q9", floorId: "B1", floorName: "Hầm B1", countPerRow: 16, rng);
            GenerateStructuredSlots(lotId: 1, lotName: "Bãi 1: Hầm ngầm FPT Q9", floorId: "B2", floorName: "Hầm B2", countPerRow: 16, rng);

            // Thêm mã mẫu chuẩn Sprint 3: A1-012 ở Bãi 1 Tầng B1
            _slotStore["1-A1-012"] = new ParkingSlotDto
            {
                Id = 1012,
                LotId = 1,
                LotName = "Bãi 1: Hầm ngầm FPT Q9",
                FloorId = "B1",
                FloorName = "Hầm B1",
                SlotCode = "A1-012",
                PhysicalStatus = "OCCUPIED",
                VehicleType = "car",
                PositionX = -22 + (12 * 3.4),
                PositionY = 0,
                PositionZ = -9,
                RotationY = 0,
                CurrentCarPlate = "51F-123.45",
                VehicleColor = "white",
                VehicleModel = "Toyota Vios",
                ParkedSinceUtc = DateTimeOffset.UtcNow.AddMinutes(-45)
            };

            // =========================================================
            // BÃI 2: 3 Tầng (B1, B2, B3) - Số lượng slot đậu random
            // =========================================================
            // B1: 28 slots (14 x 2), B2: 24 slots (12 x 2), B3: 30 slots (15 x 2) -> Tổng: 82 slots
            GenerateStructuredSlots(lotId: 2, lotName: "Bãi 2: Vincom Landmark 81", floorId: "B1", floorName: "Hầm B1", countPerRow: 14, rng);
            GenerateStructuredSlots(lotId: 2, lotName: "Bãi 2: Vincom Landmark 81", floorId: "B2", floorName: "Hầm B2", countPerRow: 12, rng);
            GenerateStructuredSlots(lotId: 2, lotName: "Bãi 2: Vincom Landmark 81", floorId: "B3", floorName: "Hầm B3", countPerRow: 15, rng);

            // =========================================================
            // BÃI 3: 4 Tầng (B1, B2, B3, B4) - Số lượng slot đậu random
            // =========================================================
            // B1: 26 slots (13 x 2), B2: 30 slots (15 x 2), B3: 24 slots (12 x 2), B4: 32 slots (16 x 2) -> Tổng: 112 slots
            GenerateStructuredSlots(lotId: 3, lotName: "Bãi 3: Aeon Mall Mega Hub", floorId: "B1", floorName: "Hầm B1", countPerRow: 13, rng);
            GenerateStructuredSlots(lotId: 3, lotName: "Bãi 3: Aeon Mall Mega Hub", floorId: "B2", floorName: "Hầm B2", countPerRow: 15, rng);
            GenerateStructuredSlots(lotId: 3, lotName: "Bãi 3: Aeon Mall Mega Hub", floorId: "B3", floorName: "Hầm B3", countPerRow: 12, rng);
            GenerateStructuredSlots(lotId: 3, lotName: "Bãi 3: Aeon Mall Mega Hub", floorId: "B4", floorName: "Hầm B4", countPerRow: 16, rng);

            _initialized = true;
        }
    }

    private static void GenerateStructuredSlots(int lotId, string lotName, string floorId, string floorName, int countPerRow, Random rng)
    {
        long idCounter = lotId * 10000L + (floorId.GetHashCode() & 0xFF) * 100L;
        double spacing = 3.3;
        double startX = -((countPerRow - 1) * spacing) / 2.0;

        // Dãy A (Phía Bắc): Tọa độ Z = -9m
        for (int i = 1; i <= countPerRow; i++)
        {
            string code = $"{floorId}-A{i:D2}";
            double randVal = rng.NextDouble();
            string status = randVal < 0.38 ? "OCCUPIED" :
                            randVal < 0.48 ? "RESERVED" :
                            randVal < 0.54 ? "MAINTENANCE" :
                            randVal < 0.58 ? "UNAVAILABLE" : "EMPTY";

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
                PositionX = startX + ((i - 1) * spacing),
                PositionY = 0,
                PositionZ = -9,
                RotationY = 0,
                CurrentCarPlate = status == "OCCUPIED" ? $"51F-{rng.Next(100, 999)}.{rng.Next(10, 99)}" :
                                  status == "RESERVED" ? $"72A-{rng.Next(100, 999)}.{rng.Next(10, 99)}" : null,
                VehicleColor = status == "OCCUPIED" ? (i % 4 == 0 ? "white" : i % 4 == 1 ? "black" : i % 4 == 2 ? "blue" : "red") : null,
                VehicleModel = status == "OCCUPIED" ? (i % 2 == 0 ? "Toyota Vios" : "Mazda 3") : null,
                ParkedSinceUtc = status == "OCCUPIED" ? DateTimeOffset.UtcNow.AddMinutes(-rng.Next(15, 180)) : null
            };
            _slotStore[$"{lotId}-{code}"] = slot;
        }

        // Dãy B (Phía Nam): Tọa độ Z = +9m
        for (int j = 1; j <= countPerRow; j++)
        {
            string code = $"{floorId}-B{j:D2}";
            double randVal = rng.NextDouble();
            string status = randVal < 0.40 ? "OCCUPIED" :
                            randVal < 0.50 ? "RESERVED" :
                            randVal < 0.55 ? "MAINTENANCE" : "EMPTY";

            var slot = new ParkingSlotDto
            {
                Id = ++idCounter,
                LotId = lotId,
                LotName = lotName,
                FloorId = floorId,
                FloorName = floorName,
                SlotCode = code,
                PhysicalStatus = status,
                VehicleType = j <= 4 ? "ev" : "car",
                PositionX = startX + ((j - 1) * spacing),
                PositionY = 0,
                PositionZ = 9,
                RotationY = 180,
                CurrentCarPlate = status == "OCCUPIED" ? $"30H-{rng.Next(100, 999)}.{rng.Next(10, 99)}" :
                                  status == "RESERVED" ? $"51K-{rng.Next(100, 999)}.{rng.Next(10, 99)}" : null,
                VehicleColor = status == "OCCUPIED" ? "emerald" : null,
                VehicleModel = status == "OCCUPIED" ? (j <= 4 ? "VinFast VF8" : "Honda Civic") : null,
                ParkedSinceUtc = status == "OCCUPIED" ? DateTimeOffset.UtcNow.AddMinutes(-rng.Next(20, 120)) : null
            };
            _slotStore[$"{lotId}-{code}"] = slot;
        }
    }
}

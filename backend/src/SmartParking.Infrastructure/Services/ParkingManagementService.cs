using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Parking;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Services;

public class ParkingManagementService : IParkingManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IApplicationDbContext _context;
    private readonly ILogger<ParkingManagementService> _logger;

    public ParkingManagementService(IApplicationDbContext context, ILogger<ParkingManagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Parking Lots

    public async Task<PagedResult<ParkingLotDto>> GetLotsAsync(ParkingLotFilterDto filter, bool isAdminOrStaff, CancellationToken ct = default)
    {
        var query = _context.ParkingLots
            .Include(l => l.Floors)
                .ThenInclude(f => f.Slots)
                    .ThenInclude(s => s.AssignedDispatches)
            .AsNoTracking()
            .AsQueryable();

        // Guest / Public users can only view active lots
        if (!isAdminOrStaff)
        {
            query = query.Where(l => l.IsActive);
        }
        else if (filter.IsActive.HasValue)
        {
            query = query.Where(l => l.IsActive == filter.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(l => l.Name.ToLower().Contains(search) || (l.Address != null && l.Address.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(ct);

        var lots = await query
            .OrderBy(l => l.Name)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        var dtos = lots.Select(l =>
        {
            var allSlots = l.Floors.SelectMany(f => f.Slots).ToList();
            var hasConfiguredSlots = allSlots.Any();
            var usableSlots = allSlots.Where(s => s.IsActive && (s.Floor == null || s.Floor.IsActive) &&
                                                  s.PhysicalStatus != SlotPhysicalStatuses.Maintenance &&
                                                  s.PhysicalStatus != SlotPhysicalStatuses.Unavailable).ToList();

            var availableCount = allSlots.Count(s => s.IsActive && s.PhysicalStatus == SlotPhysicalStatuses.Empty && !s.AssignedDispatches.Any(d => d.Status == DispatchStatuses.Assigned));
            var occupiedCount = allSlots.Count(s => s.PhysicalStatus == SlotPhysicalStatuses.Occupied);
            var totalCapacity = usableSlots.Count;
            var availableSlotsCount = (l.IsActive && hasConfiguredSlots) ? availableCount : 0;

            return new ParkingLotDto
            {
                Id = l.Id,
                Name = l.Name,
                Address = l.Address,
                TotalCapacity = totalCapacity,
                TotalFloors = l.Floors.Count,
                AvailableSlotsCount = availableSlotsCount,
                OccupiedSlotsCount = occupiedCount,
                IsActive = l.IsActive,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt,
                PhysicalSlotsCount = allSlots.Count,
                ConfiguredCapacity = l.TotalCapacity
            };
        }).ToList();

        return PagedResult<ParkingLotDto>.Create(dtos, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<ParkingLotDetailDto?> GetLotByIdAsync(int id, bool isAdminOrStaff, CancellationToken ct = default)
    {
        var lot = await _context.ParkingLots
            .Include(l => l.Floors)
                .ThenInclude(f => f.Slots)
                    .ThenInclude(s => s.AssignedDispatches)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (lot == null) return null;
        if (!isAdminOrStaff && !lot.IsActive) return null;

        var allSlots = lot.Floors.SelectMany(f => f.Slots).ToList();
        var availableCount = allSlots.Count(s => s.IsActive && s.PhysicalStatus == SlotPhysicalStatuses.Empty && !s.AssignedDispatches.Any(d => d.Status == DispatchStatuses.Assigned));
        var occupiedCount = allSlots.Count(s => s.PhysicalStatus == SlotPhysicalStatuses.Occupied);

        var floorSummaries = lot.Floors
            .OrderBy(f => f.FloorNumber)
            .Select(f => new ParkingFloorSummaryDto
            {
                Id = f.Id,
                FloorNumber = f.FloorNumber,
                Name = f.Name,
                TotalSlots = f.TotalSlots,
                AvailableSlotsCount = f.Slots.Count(s => s.IsActive && s.PhysicalStatus == SlotPhysicalStatuses.Empty && !s.AssignedDispatches.Any(d => d.Status == DispatchStatuses.Assigned)),
                OccupiedSlotsCount = f.Slots.Count(s => s.PhysicalStatus == SlotPhysicalStatuses.Occupied),
                IsActive = f.IsActive
            }).ToList();

        var hasConfiguredSlots = allSlots.Any();
        var usableSlots = allSlots.Where(s => s.IsActive && (s.Floor == null || s.Floor.IsActive) &&
                                              s.PhysicalStatus != SlotPhysicalStatuses.Maintenance &&
                                              s.PhysicalStatus != SlotPhysicalStatuses.Unavailable).ToList();
        var totalCapacity = usableSlots.Count;
        var availableSlotsCount = (lot.IsActive && hasConfiguredSlots) ? availableCount : 0;

        return new ParkingLotDetailDto
        {
            Id = lot.Id,
            Name = lot.Name,
            Address = lot.Address,
            TotalCapacity = totalCapacity,
            TotalFloors = lot.Floors.Count,
            AvailableSlotsCount = availableSlotsCount,
            OccupiedSlotsCount = occupiedCount,
            IsActive = lot.IsActive,
            CreatedAt = lot.CreatedAt,
            UpdatedAt = lot.UpdatedAt,
            PhysicalSlotsCount = allSlots.Count,
            ConfiguredCapacity = lot.TotalCapacity,
            Floors = floorSummaries
        };
    }

    public async Task<(bool Success, string Message, ParkingLotDto? Lot)> CreateLotAsync(CreateParkingLotRequestDto request, long? actorId, CancellationToken ct = default)
    {
        var trimmedName = request.Name.Trim();
        var exists = await _context.ParkingLots
            .AnyAsync(l => l.Name.ToLower() == trimmedName.ToLower(), ct);

        if (exists)
        {
            return (false, "Tên bãi đỗ xe đã tồn tại trên hệ thống.", null);
        }

        var lot = new ParkingLot
        {
            Name = trimmedName,
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            TotalCapacity = request.TotalCapacity,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ParkingLots.Add(lot);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(actorId, "CREATE_PARKING_LOT", "parking_lots", lot.Id.ToString(), null,
            JsonSerializer.Serialize(new { lot.Name, lot.Address, lot.TotalCapacity, lot.IsActive }), null, ct);

        var dto = new ParkingLotDto
        {
            Id = lot.Id,
            Name = lot.Name,
            Address = lot.Address,
            TotalCapacity = 0,
            TotalFloors = 0,
            AvailableSlotsCount = 0,
            OccupiedSlotsCount = 0,
            IsActive = lot.IsActive,
            CreatedAt = lot.CreatedAt,
            UpdatedAt = lot.UpdatedAt,
            PhysicalSlotsCount = 0,
            ConfiguredCapacity = lot.TotalCapacity
        };

        _logger.LogInformation("ParkingLot created successfully with ID {LotId} by Actor {ActorId}", lot.Id, actorId);
        return (true, "Tạo bãi đỗ xe thành công.", dto);
    }

    public async Task<(bool Success, string Message, ParkingLotDto? Lot)> UpdateLotAsync(int id, UpdateParkingLotRequestDto request, long? actorId, CancellationToken ct = default)
    {
        var lot = await _context.ParkingLots
            .Include(l => l.Floors)
                .ThenInclude(f => f.Slots)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (lot == null)
        {
            return (false, "Không tìm thấy bãi đỗ xe để cập nhật.", null);
        }

        var trimmedName = request.Name.Trim();
        var exists = await _context.ParkingLots
            .AnyAsync(l => l.Id != id && l.Name.ToLower() == trimmedName.ToLower(), ct);

        if (exists)
        {
            return (false, "Tên bãi đỗ xe đã trùng với một bãi đỗ khác.", null);
        }

        var oldValues = JsonSerializer.Serialize(new { lot.Name, lot.Address, lot.TotalCapacity, lot.IsActive });

        lot.Name = trimmedName;
        lot.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        lot.TotalCapacity = request.TotalCapacity;
        lot.IsActive = request.IsActive;
        lot.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        var newValues = JsonSerializer.Serialize(new { lot.Name, lot.Address, lot.TotalCapacity, lot.IsActive });
        await LogAuditAsync(actorId, "UPDATE_PARKING_LOT", "parking_lots", lot.Id.ToString(), oldValues, newValues, null, ct);

        var allSlots = lot.Floors.SelectMany(f => f.Slots).ToList();
        var hasConfiguredSlots = allSlots.Any();
        var usableSlots = allSlots.Where(s => s.IsActive && (s.Floor == null || s.Floor.IsActive) &&
                                              s.PhysicalStatus != SlotPhysicalStatuses.Maintenance &&
                                              s.PhysicalStatus != SlotPhysicalStatuses.Unavailable).ToList();
        var availableCount = allSlots.Count(s => s.IsActive && s.PhysicalStatus == SlotPhysicalStatuses.Empty);
        var occupiedCount = allSlots.Count(s => s.PhysicalStatus == SlotPhysicalStatuses.Occupied);
        var totalCapacity = usableSlots.Count;
        var availableSlotsCount = (lot.IsActive && hasConfiguredSlots) ? availableCount : 0;

        var dto = new ParkingLotDto
        {
            Id = lot.Id,
            Name = lot.Name,
            Address = lot.Address,
            TotalCapacity = totalCapacity,
            TotalFloors = lot.Floors.Count,
            AvailableSlotsCount = availableSlotsCount,
            OccupiedSlotsCount = occupiedCount,
            IsActive = lot.IsActive,
            CreatedAt = lot.CreatedAt,
            UpdatedAt = lot.UpdatedAt,
            PhysicalSlotsCount = allSlots.Count,
            ConfiguredCapacity = lot.TotalCapacity
        };

        _logger.LogInformation("ParkingLot {LotId} updated successfully by Actor {ActorId}", lot.Id, actorId);
        return (true, "Cập nhật bãi đỗ xe thành công.", dto);
    }

    public async Task<(bool Success, string Message, ParkingLotDto? Lot)> UpdateLotStatusAsync(int id, UpdateLotStatusRequestDto request, long? actorId, CancellationToken ct = default)
    {
        var lot = await _context.ParkingLots
            .Include(l => l.Floors)
                .ThenInclude(f => f.Slots)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (lot == null)
        {
            return (false, "Không tìm thấy bãi đỗ xe.", null);
        }

        var oldValues = JsonSerializer.Serialize(new { lot.IsActive });
        lot.IsActive = request.IsActive;
        lot.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        var newValues = JsonSerializer.Serialize(new { lot.IsActive, request.Reason }, JsonOptions);
        await LogAuditAsync(actorId, "UPDATE_LOT_STATUS", "parking_lots", lot.Id.ToString(), oldValues, newValues, null, ct);

        var allSlots = lot.Floors.SelectMany(f => f.Slots).ToList();
        var hasConfiguredSlots = allSlots.Any();
        var usableSlots = allSlots.Where(s => s.IsActive && (s.Floor == null || s.Floor.IsActive) &&
                                              s.PhysicalStatus != SlotPhysicalStatuses.Maintenance &&
                                              s.PhysicalStatus != SlotPhysicalStatuses.Unavailable).ToList();
        var availableCount = allSlots.Count(s => s.IsActive && s.PhysicalStatus == SlotPhysicalStatuses.Empty);
        var occupiedCount = allSlots.Count(s => s.PhysicalStatus == SlotPhysicalStatuses.Occupied);
        var totalCapacity = usableSlots.Count;
        var availableSlotsCount = (lot.IsActive && hasConfiguredSlots) ? availableCount : 0;

        var dto = new ParkingLotDto
        {
            Id = lot.Id,
            Name = lot.Name,
            Address = lot.Address,
            TotalCapacity = totalCapacity,
            TotalFloors = lot.Floors.Count,
            AvailableSlotsCount = availableSlotsCount,
            OccupiedSlotsCount = occupiedCount,
            IsActive = lot.IsActive,
            CreatedAt = lot.CreatedAt,
            UpdatedAt = lot.UpdatedAt,
            PhysicalSlotsCount = allSlots.Count,
            ConfiguredCapacity = lot.TotalCapacity
        };

        var statusText = request.IsActive ? "Mở tiếp nhận xe" : "Đóng tiếp nhận xe";
        _logger.LogInformation("ParkingLot {LotId} status changed to {Status} by Actor {ActorId}", lot.Id, statusText, actorId);
        return (true, $"{statusText} thành công.", dto);
    }

    public async Task<(bool Success, string Message)> DeleteLotAsync(int id, long? actorId, CancellationToken ct = default)
    {
        var lot = await _context.ParkingLots.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lot == null)
        {
            return (false, "Không tìm thấy bãi đỗ xe để xóa.");
        }

        // Business Rule: Không xóa cứng bãi có dữ liệu liên quan
        var hasFloors = await _context.ParkingFloors.AnyAsync(f => f.LotId == id, ct);
        var hasSessions = await _context.ParkingSessions.AnyAsync(s => s.LotId == id, ct);

        if (hasFloors || hasSessions)
        {
            return (false, "Không thể xóa bãi đỗ xe đang chứa các tầng hoặc lịch sử gửi xe. Vui lòng vô hiệu hóa trạng thái (chuyển sang Không hoạt động).");
        }

        var oldValues = JsonSerializer.Serialize(new { lot.Name, lot.Address, lot.TotalCapacity });
        _context.ParkingLots.Remove(lot);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(actorId, "DELETE_PARKING_LOT", "parking_lots", id.ToString(), oldValues, null, null, ct);
        _logger.LogInformation("ParkingLot {LotId} deleted by Actor {ActorId}", id, actorId);

        return (true, "Xóa bãi đỗ xe thành công.");
    }

    #endregion

    #region Parking Floors

    public async Task<IReadOnlyList<ParkingFloorDto>> GetFloorsByLotIdAsync(int lotId, bool? isActiveOnly, CancellationToken ct = default)
    {
        var query = _context.ParkingFloors
            .Include(f => f.Lot)
            .Include(f => f.Slots)
                .ThenInclude(s => s.AssignedDispatches)
            .Where(f => f.LotId == lotId)
            .AsNoTracking()
            .AsQueryable();

        if (isActiveOnly.HasValue)
        {
            query = query.Where(f => f.IsActive == isActiveOnly.Value);
        }

        var floors = await query.OrderBy(f => f.FloorNumber).ToListAsync(ct);

        return floors.Select(f => new ParkingFloorDto
        {
            Id = f.Id,
            LotId = f.LotId,
            LotName = f.Lot.Name,
            FloorNumber = f.FloorNumber,
            Name = f.Name,
            TotalSlots = f.TotalSlots,
            AvailableSlotsCount = f.Slots.Count(s => s.IsActive && s.PhysicalStatus == SlotPhysicalStatuses.Empty && !s.AssignedDispatches.Any(d => d.Status == DispatchStatuses.Assigned)),
            OccupiedSlotsCount = f.Slots.Count(s => s.PhysicalStatus == SlotPhysicalStatuses.Occupied),
            IsActive = f.IsActive,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        }).ToList();
    }

    public async Task<ParkingFloorDto?> GetFloorByIdAsync(int id, CancellationToken ct = default)
    {
        var floor = await _context.ParkingFloors
            .Include(f => f.Lot)
            .Include(f => f.Slots)
                .ThenInclude(s => s.AssignedDispatches)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, ct);

        if (floor == null) return null;

        return new ParkingFloorDto
        {
            Id = floor.Id,
            LotId = floor.LotId,
            LotName = floor.Lot.Name,
            FloorNumber = floor.FloorNumber,
            Name = floor.Name,
            TotalSlots = floor.TotalSlots,
            AvailableSlotsCount = floor.Slots.Count(s => s.IsActive && s.PhysicalStatus == SlotPhysicalStatuses.Empty && !s.AssignedDispatches.Any(d => d.Status == DispatchStatuses.Assigned)),
            OccupiedSlotsCount = floor.Slots.Count(s => s.PhysicalStatus == SlotPhysicalStatuses.Occupied),
            IsActive = floor.IsActive,
            CreatedAt = floor.CreatedAt,
            UpdatedAt = floor.UpdatedAt
        };
    }

    public async Task<(bool Success, string Message, ParkingFloorDto? Floor)> CreateFloorAsync(int lotId, CreateParkingFloorRequestDto request, long? actorId, CancellationToken ct = default)
    {
        var lot = await _context.ParkingLots.FirstOrDefaultAsync(l => l.Id == lotId, ct);
        if (lot == null)
        {
            return (false, "Bãi đỗ xe chỉ định không tồn tại.", null);
        }

        var trimmedName = request.Name.Trim();
        var floorNumExists = await _context.ParkingFloors.AnyAsync(f => f.LotId == lotId && f.FloorNumber == request.FloorNumber, ct);
        if (floorNumExists)
        {
            return (false, $"Số thứ tự tầng {request.FloorNumber} đã tồn tại trong bãi xe này.", null);
        }

        var nameExists = await _context.ParkingFloors.AnyAsync(f => f.LotId == lotId && f.Name.ToLower() == trimmedName.ToLower(), ct);
        if (nameExists)
        {
            return (false, $"Tên tầng '{trimmedName}' đã tồn tại trong bãi xe này.", null);
        }

        var floor = new ParkingFloor
        {
            LotId = lotId,
            FloorNumber = request.FloorNumber,
            Name = trimmedName,
            TotalSlots = request.TotalSlots,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ParkingFloors.Add(floor);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(actorId, "CREATE_PARKING_FLOOR", "parking_floors", floor.Id.ToString(), null,
            JsonSerializer.Serialize(new { floor.LotId, floor.FloorNumber, floor.Name, floor.TotalSlots, floor.IsActive }), null, ct);

        var dto = new ParkingFloorDto
        {
            Id = floor.Id,
            LotId = floor.LotId,
            LotName = lot.Name,
            FloorNumber = floor.FloorNumber,
            Name = floor.Name,
            TotalSlots = floor.TotalSlots,
            AvailableSlotsCount = 0,
            OccupiedSlotsCount = 0,
            IsActive = floor.IsActive,
            CreatedAt = floor.CreatedAt,
            UpdatedAt = floor.UpdatedAt
        };

        _logger.LogInformation("ParkingFloor created with ID {FloorId} on Lot {LotId} by Actor {ActorId}", floor.Id, lotId, actorId);
        return (true, "Tạo tầng thành công.", dto);
    }

    public async Task<(bool Success, string Message, ParkingFloorDto? Floor)> UpdateFloorAsync(int id, UpdateParkingFloorRequestDto request, long? actorId, CancellationToken ct = default)
    {
        var floor = await _context.ParkingFloors
            .Include(f => f.Lot)
            .Include(f => f.Slots)
            .FirstOrDefaultAsync(f => f.Id == id, ct);

        if (floor == null)
        {
            return (false, "Không tìm thấy tầng để cập nhật.", null);
        }

        var trimmedName = request.Name.Trim();
        var floorNumExists = await _context.ParkingFloors
            .AnyAsync(f => f.LotId == floor.LotId && f.Id != id && f.FloorNumber == request.FloorNumber, ct);
        if (floorNumExists)
        {
            return (false, $"Số thứ tự tầng {request.FloorNumber} đã trùng với tầng khác trong cùng bãi.", null);
        }

        var nameExists = await _context.ParkingFloors
            .AnyAsync(f => f.LotId == floor.LotId && f.Id != id && f.Name.ToLower() == trimmedName.ToLower(), ct);
        if (nameExists)
        {
            return (false, $"Tên tầng '{trimmedName}' đã trùng với tầng khác trong cùng bãi.", null);
        }

        // Safety Guard: Cannot deactivate floor when active sessions or dispatches exist on its slots
        if (!request.IsActive && floor.IsActive)
        {
            var floorSlotIds = floor.Slots.Select(s => s.Id).ToList();
            if (floorSlotIds.Any())
            {
                var hasActiveSession = await _context.ParkingSessions
                    .AnyAsync(s => s.CurrentSlotId != null && floorSlotIds.Contains(s.CurrentSlotId.Value) &&
                                   (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid), ct);
                if (hasActiveSession)
                {
                    return (false, "Không thể vô hiệu hóa tầng khi đang có xe đỗ tại các ô thuộc tầng này.", null);
                }

                var hasActiveDispatch = await _context.ParkingDispatches
                    .AnyAsync(d => floorSlotIds.Contains(d.AssignedSlotId) && d.Status == DispatchStatuses.Assigned, ct);
                if (hasActiveDispatch)
                {
                    return (false, "Không thể vô hiệu hóa tầng khi đang có lệnh điều phối xe tới các ô thuộc tầng này.", null);
                }
            }
        }

        var oldValues = JsonSerializer.Serialize(new { floor.FloorNumber, floor.Name, floor.TotalSlots, floor.IsActive });

        floor.FloorNumber = request.FloorNumber;
        floor.Name = trimmedName;
        floor.TotalSlots = request.TotalSlots;
        floor.IsActive = request.IsActive;
        floor.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        var newValues = JsonSerializer.Serialize(new { floor.FloorNumber, floor.Name, floor.TotalSlots, floor.IsActive });
        await LogAuditAsync(actorId, "UPDATE_PARKING_FLOOR", "parking_floors", floor.Id.ToString(), oldValues, newValues, null, ct);

        var dto = new ParkingFloorDto
        {
            Id = floor.Id,
            LotId = floor.LotId,
            LotName = floor.Lot.Name,
            FloorNumber = floor.FloorNumber,
            Name = floor.Name,
            TotalSlots = floor.TotalSlots,
            AvailableSlotsCount = floor.Slots.Count(s => s.IsActive && s.PhysicalStatus == SlotPhysicalStatuses.Empty),
            OccupiedSlotsCount = floor.Slots.Count(s => s.PhysicalStatus == SlotPhysicalStatuses.Occupied),
            IsActive = floor.IsActive,
            CreatedAt = floor.CreatedAt,
            UpdatedAt = floor.UpdatedAt
        };

        _logger.LogInformation("ParkingFloor {FloorId} updated by Actor {ActorId}", floor.Id, actorId);
        return (true, "Cập nhật thông tin tầng thành công.", dto);
    }

    public async Task<(bool Success, string Message)> DeleteFloorAsync(int id, long? actorId, CancellationToken ct = default)
    {
        var floor = await _context.ParkingFloors.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (floor == null)
        {
            return (false, "Không tìm thấy tầng để xóa.");
        }

        var hasSlots = await _context.ParkingSlots.AnyAsync(s => s.FloorId == id, ct);
        if (hasSlots)
        {
            return (false, "Không thể xóa tầng đang chứa các ô đỗ xe. Vui lòng vô hiệu hóa tầng (IsActive = false) để đảm bảo toàn vẹn dữ liệu.");
        }

        var oldValues = JsonSerializer.Serialize(new { floor.LotId, floor.FloorNumber, floor.Name });
        _context.ParkingFloors.Remove(floor);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(actorId, "DELETE_PARKING_FLOOR", "parking_floors", id.ToString(), oldValues, null, null, ct);
        _logger.LogInformation("ParkingFloor {FloorId} deleted by Actor {ActorId}", id, actorId);

        return (true, "Xóa tầng thành công.");
    }

    #endregion

    #region Parking Slots

    public async Task<PagedResult<ParkingSlotDto>> GetSlotsByFloorIdAsync(int floorId, ParkingSlotFilterDto filter, CancellationToken ct = default)
    {
        var query = _context.ParkingSlots
            .Include(s => s.Floor)
                .ThenInclude(f => f.Lot)
            .Include(s => s.AssignedDispatches)
            .Where(s => s.FloorId == floorId)
            .AsNoTracking()
            .AsQueryable();

        if (filter.IsActive.HasValue)
        {
            query = query.Where(s => s.IsActive == filter.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.PhysicalStatus))
        {
            var status = filter.PhysicalStatus.Trim().ToUpperInvariant();
            query = query.Where(s => s.PhysicalStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToUpperInvariant();
            query = query.Where(s => s.SlotCode.Contains(search));
        }

        var totalCount = await query.CountAsync(ct);

        var slots = await query
            .OrderBy(s => s.SlotCode)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        var dtos = slots.Select(s => new ParkingSlotDto
        {
            Id = s.Id,
            FloorId = s.FloorId,
            FloorName = s.Floor.Name,
            LotId = s.Floor.LotId,
            LotName = s.Floor.Lot.Name,
            SlotCode = s.SlotCode,
            PhysicalStatus = s.PhysicalStatus,
            IsAvailable = s.IsActive && s.PhysicalStatus == SlotPhysicalStatuses.Empty && !s.AssignedDispatches.Any(d => d.Status == DispatchStatuses.Assigned),
            PositionX = s.PositionX,
            PositionY = s.PositionY,
            PositionZ = s.PositionZ,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList();

        return PagedResult<ParkingSlotDto>.Create(dtos, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<ParkingSlotDetailDto?> GetSlotByIdAsync(long id, bool includeInternalDetails, CancellationToken ct = default)
    {
        var slot = await _context.ParkingSlots
            .Include(s => s.Floor)
                .ThenInclude(f => f.Lot)
            .Include(s => s.AssignedDispatches)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (slot == null) return null;

        string? currentVehiclePlate = null;
        long? currentSessionId = null;

        // Privacy rule: Only internal authorized users (Admin, Manager, Staff) can see current plate / session details
        if (includeInternalDetails)
        {
            var activeSession = await _context.ParkingSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(ps => ps.CurrentSlotId == id && (ps.Status == SessionStatuses.Active || ps.Status == SessionStatuses.Paid), ct);

            if (activeSession != null)
            {
                currentVehiclePlate = activeSession.PlateNumber;
                currentSessionId = activeSession.Id;
            }
            else
            {
                var assignedDispatch = await _context.ParkingDispatches
                    .Include(d => d.Session)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.AssignedSlotId == id && d.Status == DispatchStatuses.Assigned, ct);

                if (assignedDispatch != null)
                {
                    currentVehiclePlate = assignedDispatch.Session.PlateNumber;
                    currentSessionId = assignedDispatch.SessionId;
                }
            }
        }

        return new ParkingSlotDetailDto
        {
            Id = slot.Id,
            FloorId = slot.FloorId,
            FloorName = slot.Floor.Name,
            LotId = slot.Floor.LotId,
            LotName = slot.Floor.Lot.Name,
            SlotCode = slot.SlotCode,
            PhysicalStatus = slot.PhysicalStatus,
            IsAvailable = slot.IsActive && slot.PhysicalStatus == SlotPhysicalStatuses.Empty && !slot.AssignedDispatches.Any(d => d.Status == DispatchStatuses.Assigned),
            PositionX = slot.PositionX,
            PositionY = slot.PositionY,
            PositionZ = slot.PositionZ,
            IsActive = slot.IsActive,
            CreatedAt = slot.CreatedAt,
            UpdatedAt = slot.UpdatedAt,
            CurrentVehiclePlate = currentVehiclePlate,
            CurrentSessionId = currentSessionId
        };
    }

    public async Task<(bool Success, string Message, ParkingSlotDto? Slot)> CreateSlotAsync(int floorId, CreateParkingSlotRequestDto request, long? actorId, CancellationToken ct = default)
    {
        var floor = await _context.ParkingFloors
            .Include(f => f.Lot)
            .FirstOrDefaultAsync(f => f.Id == floorId, ct);

        if (floor == null)
        {
            return (false, "Tầng chỉ định không tồn tại.", null);
        }

        var trimmedCode = request.SlotCode.Trim().ToUpperInvariant();
        var exists = await _context.ParkingSlots
            .AnyAsync(s => s.FloorId == floorId && s.SlotCode == trimmedCode, ct);

        if (exists)
        {
            return (false, $"Mã ô đỗ '{trimmedCode}' đã tồn tại trong tầng này.", null);
        }

        var physicalStatus = request.PhysicalStatus?.Trim().ToUpperInvariant() ?? SlotPhysicalStatuses.Empty;
        if (!SlotPhysicalStatuses.All.Contains(physicalStatus))
        {
            return (false, $"Trạng thái vật lý không hợp lệ. Phải là một trong: {string.Join(", ", SlotPhysicalStatuses.All)}", null);
        }

        var slot = new ParkingSlot
        {
            FloorId = floorId,
            SlotCode = trimmedCode,
            PhysicalStatus = physicalStatus,
            PositionX = request.PositionX,
            PositionY = request.PositionY,
            PositionZ = request.PositionZ,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ParkingSlots.Add(slot);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(actorId, "CREATE_PARKING_SLOT", "parking_slots", slot.Id.ToString(), null,
            JsonSerializer.Serialize(new { slot.FloorId, slot.SlotCode, slot.PhysicalStatus, slot.IsActive }), null, ct);

        var dto = new ParkingSlotDto
        {
            Id = slot.Id,
            FloorId = slot.FloorId,
            FloorName = floor.Name,
            LotId = floor.LotId,
            LotName = floor.Lot.Name,
            SlotCode = slot.SlotCode,
            PhysicalStatus = slot.PhysicalStatus,
            IsAvailable = slot.IsActive && slot.PhysicalStatus == SlotPhysicalStatuses.Empty,
            PositionX = slot.PositionX,
            PositionY = slot.PositionY,
            PositionZ = slot.PositionZ,
            IsActive = slot.IsActive,
            CreatedAt = slot.CreatedAt,
            UpdatedAt = slot.UpdatedAt
        };

        _logger.LogInformation("ParkingSlot {SlotCode} created with ID {SlotId} on Floor {FloorId} by Actor {ActorId}", trimmedCode, slot.Id, floorId, actorId);
        return (true, "Tạo ô đỗ xe thành công.", dto);
    }

    public async Task<(bool Success, string Message, ParkingSlotDto? Slot)> UpdateSlotAsync(long id, UpdateParkingSlotRequestDto request, long? actorId, CancellationToken ct = default)
    {
        var slot = await _context.ParkingSlots
            .Include(s => s.Floor)
                .ThenInclude(f => f.Lot)
            .Include(s => s.AssignedDispatches)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (slot == null)
        {
            return (false, "Không tìm thấy ô đỗ xe để cập nhật.", null);
        }

        var trimmedCode = request.SlotCode.Trim().ToUpperInvariant();
        var exists = await _context.ParkingSlots
            .AnyAsync(s => s.FloorId == slot.FloorId && s.Id != id && s.SlotCode == trimmedCode, ct);

        if (exists)
        {
            return (false, $"Mã ô đỗ '{trimmedCode}' đã trùng với ô đỗ khác trong cùng tầng.", null);
        }

        // Safety Guard: Cannot deactivate slot when active session or dispatch exists on it
        if (!request.IsActive && slot.IsActive)
        {
            var hasActiveSession = await _context.ParkingSessions
                .AnyAsync(s => s.CurrentSlotId == id && (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid), ct);
            if (hasActiveSession)
            {
                return (false, "Không thể vô hiệu hóa ô đỗ khi đang có xe đỗ trong phiên hoạt động.", null);
            }

            var hasActiveDispatch = await _context.ParkingDispatches
                .AnyAsync(d => d.AssignedSlotId == id && d.Status == DispatchStatuses.Assigned, ct);
            if (hasActiveDispatch)
            {
                return (false, "Không thể vô hiệu hóa ô đỗ khi đang có lệnh điều phối xe tới ô này.", null);
            }
        }

        var oldValues = JsonSerializer.Serialize(new { slot.SlotCode, slot.PositionX, slot.PositionY, slot.PositionZ, slot.IsActive });

        slot.SlotCode = trimmedCode;
        slot.PositionX = request.PositionX;
        slot.PositionY = request.PositionY;
        slot.PositionZ = request.PositionZ;
        slot.IsActive = request.IsActive;
        slot.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        var newValues = JsonSerializer.Serialize(new { slot.SlotCode, slot.PositionX, slot.PositionY, slot.PositionZ, slot.IsActive });
        await LogAuditAsync(actorId, "UPDATE_PARKING_SLOT", "parking_slots", slot.Id.ToString(), oldValues, newValues, null, ct);

        var dto = new ParkingSlotDto
        {
            Id = slot.Id,
            FloorId = slot.FloorId,
            FloorName = slot.Floor.Name,
            LotId = slot.Floor.LotId,
            LotName = slot.Floor.Lot.Name,
            SlotCode = slot.SlotCode,
            PhysicalStatus = slot.PhysicalStatus,
            IsAvailable = slot.IsActive && slot.PhysicalStatus == SlotPhysicalStatuses.Empty && !slot.AssignedDispatches.Any(d => d.Status == DispatchStatuses.Assigned),
            PositionX = slot.PositionX,
            PositionY = slot.PositionY,
            PositionZ = slot.PositionZ,
            IsActive = slot.IsActive,
            CreatedAt = slot.CreatedAt,
            UpdatedAt = slot.UpdatedAt
        };

        _logger.LogInformation("ParkingSlot {SlotId} updated by Actor {ActorId}", slot.Id, actorId);
        return (true, "Cập nhật thông tin ô đỗ thành công.", dto);
    }

    public async Task<(bool Success, string Message, ParkingSlotDto? Slot)> UpdateSlotPhysicalStatusAsync(
        long id, UpdateSlotPhysicalStatusRequestDto request, long? actorId, string actorRole, string? ipAddress, CancellationToken ct = default)
    {
        var slot = await _context.ParkingSlots
            .Include(s => s.Floor)
                .ThenInclude(f => f.Lot)
            .Include(s => s.AssignedDispatches)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (slot == null)
        {
            return (false, "Không tìm thấy ô đỗ xe.", null);
        }

        var targetStatus = request.PhysicalStatus.Trim().ToUpperInvariant();
        if (!SlotPhysicalStatuses.All.Contains(targetStatus))
        {
            return (false, $"Trạng thái vật lý '{request.PhysicalStatus}' không hợp lệ. Phải là một trong: {string.Join(", ", SlotPhysicalStatuses.All)}", null);
        }

        // =========================================================================
        // STRICT BUSINESS RULE: "Không tự chuyển ô OCCUPIED thành EMPTY nếu xe chưa rời bãi."
        // =========================================================================
        if (slot.PhysicalStatus == SlotPhysicalStatuses.Occupied)
        {
            var hasActiveSession = await _context.ParkingSessions
                .AnyAsync(s => s.CurrentSlotId == id && (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid), ct);

            if (hasActiveSession)
            {
                _logger.LogWarning("Rejected manual status change for OCCUPIED slot {SlotId} because an active parking session is occupying it.", id);
                return (false, "Không thể chuyển trạng thái ô đỗ đang có xe (OCCUPIED) khi xe chưa hoàn tất rời bãi qua quy trình check-out.", null);
            }
        }

        // Check if attempting to set to MAINTENANCE or UNAVAILABLE while slot has active session or dispatch
        if (targetStatus == SlotPhysicalStatuses.Maintenance || targetStatus == SlotPhysicalStatuses.Unavailable)
        {
            var hasActiveSession = await _context.ParkingSessions
                .AnyAsync(s => s.CurrentSlotId == id && (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid), ct);

            if (hasActiveSession)
            {
                return (false, "Không thể đưa ô đỗ vào bảo trì/ngưng sử dụng khi đang có xe đỗ trong ô.", null);
            }

            var hasActiveDispatch = await _context.ParkingDispatches
                .AnyAsync(d => d.AssignedSlotId == id && d.Status == DispatchStatuses.Assigned, ct);

            if (hasActiveDispatch)
            {
                return (false, "Không thể đưa ô đỗ vào bảo trì/ngưng sử dụng khi đang có lệnh điều phối xe tới ô này.", null);
            }
        }

        var oldValues = JsonSerializer.Serialize(new { slot.PhysicalStatus });
        slot.PhysicalStatus = targetStatus;
        slot.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        var newValues = JsonSerializer.Serialize(new { slot.PhysicalStatus, request.Reason, role = actorRole }, JsonOptions);
        await LogAuditAsync(actorId, "UPDATE_SLOT_PHYSICAL_STATUS", "parking_slots", slot.Id.ToString(), oldValues, newValues, ipAddress, ct);

        var dto = new ParkingSlotDto
        {
            Id = slot.Id,
            FloorId = slot.FloorId,
            FloorName = slot.Floor.Name,
            LotId = slot.Floor.LotId,
            LotName = slot.Floor.Lot.Name,
            SlotCode = slot.SlotCode,
            PhysicalStatus = slot.PhysicalStatus,
            IsAvailable = slot.IsActive && slot.PhysicalStatus == SlotPhysicalStatuses.Empty && !slot.AssignedDispatches.Any(d => d.Status == DispatchStatuses.Assigned),
            PositionX = slot.PositionX,
            PositionY = slot.PositionY,
            PositionZ = slot.PositionZ,
            IsActive = slot.IsActive,
            CreatedAt = slot.CreatedAt,
            UpdatedAt = slot.UpdatedAt
        };

        _logger.LogInformation("Slot {SlotId} physical status changed from {Old} to {New} by Actor {ActorId} ({Role})", id, oldValues, targetStatus, actorId, actorRole);
        return (true, $"Cập nhật trạng thái ô đỗ thành '{targetStatus}' thành công.", dto);
    }

    public async Task<(bool Success, string Message)> DeleteSlotAsync(long id, long? actorId, CancellationToken ct = default)
    {
        var slot = await _context.ParkingSlots.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (slot == null)
        {
            return (false, "Không tìm thấy ô đỗ để xóa.");
        }

        // Business Rule: Không xóa cứng ô đỗ đã có lịch sử phiên hoặc điều phối
        var hasSessions = await _context.ParkingSessions.AnyAsync(s => s.CurrentSlotId == id, ct);
        var hasDispatches = await _context.ParkingDispatches.AnyAsync(d => d.AssignedSlotId == id || d.ActualSlotId == id, ct);

        if (hasSessions || hasDispatches)
        {
            return (false, "Không thể xóa ô đỗ đã có lịch sử phiên gửi xe hoặc điều phối. Vui lòng chuyển sang Không hoạt động (IsActive = false).");
        }

        var oldValues = JsonSerializer.Serialize(new { slot.FloorId, slot.SlotCode, slot.PhysicalStatus });
        _context.ParkingSlots.Remove(slot);
        await _context.SaveChangesAsync(ct);

        await LogAuditAsync(actorId, "DELETE_PARKING_SLOT", "parking_slots", id.ToString(), oldValues, null, null, ct);
        _logger.LogInformation("ParkingSlot {SlotId} deleted by Actor {ActorId}", id, actorId);

        return (true, "Xóa ô đỗ thành công.");
    }

    #endregion

    #region Helper

    private async Task LogAuditAsync(long? actorId, string action, string targetEntity, string? targetId, string? oldValues, string? newValues, string? ipAddress, CancellationToken ct)
    {
        try
        {
            var audit = new AuditLog
            {
                ActorId = actorId,
                Action = action,
                TargetEntity = targetEntity,
                TargetId = targetId,
                OldValues = oldValues,
                NewValues = newValues,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for action {Action} on {TargetEntity}:{TargetId}", action, targetEntity, targetId);
        }
    }

    #endregion
}

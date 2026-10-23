using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Dispatches;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Services;

public class ParkingDispatchService : IParkingDispatchService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ParkingDispatchService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public ParkingDispatchService(IApplicationDbContext context, ILogger<ParkingDispatchService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(bool Success, string Message, ParkingDispatchDto? Data)> CreateDispatchAsync(
        CreateDispatchRequestDto request,
        long? actorId,
        string actorRole,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var session = await _context.ParkingSessions
            .Include(s => s.Lot)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, ct);

        if (session == null)
        {
            return (false, $"Không tìm thấy phiên gửi xe với ID: {request.SessionId}.", null);
        }

        if (session.Status != SessionStatuses.Active)
        {
            return (false, $"Chỉ có thể tạo lệnh điều phối cho phiên gửi xe đang hoạt động (ACTIVE). Trạng thái hiện tại: {session.Status}.", null);
        }

        // Chống trùng lặp lệnh điều phối đang hiệu lực cho cùng một phiên
        var hasActiveDispatch = await _context.ParkingDispatches
            .AnyAsync(d => d.SessionId == session.Id && (d.Status == DispatchStatuses.Pending || d.Status == DispatchStatuses.Assigned), ct);

        if (hasActiveDispatch)
        {
            return (false, $"Phiên đỗ xe #{session.Id} hiện đang có một lệnh điều phối chưa hoàn tất (PENDING hoặc ASSIGNED).", null);
        }

        ParkingSlot? slot = null;

        if (request.AssignedSlotId.HasValue)
        {
            slot = await _context.ParkingSlots
                .Include(s => s.Floor)
                .FirstOrDefaultAsync(s => s.Id == request.AssignedSlotId.Value, ct);

            if (slot == null)
            {
                return (false, $"Không tìm thấy ô đỗ với ID: {request.AssignedSlotId.Value}.", null);
            }

            if (!slot.IsActive)
            {
                return (false, $"Ô đỗ '{slot.SlotCode}' hiện đang tạm ngừng hoạt động.", null);
            }

            if (slot.Floor.LotId != session.LotId)
            {
                return (false, $"Ô đỗ '{slot.SlotCode}' không thuộc bãi đỗ xe '{session.Lot.Name}'.", null);
            }

            if (slot.PhysicalStatus != SlotPhysicalStatuses.Empty)
            {
                return (false, $"Ô đỗ '{slot.SlotCode}' hiện không khả dụng (Trạng thái vật lý: {slot.PhysicalStatus}).", null);
            }

            var isAlreadyAssigned = await _context.ParkingDispatches
                .AnyAsync(d => d.AssignedSlotId == slot.Id && d.Status == DispatchStatuses.Assigned, ct);

            if (isAlreadyAssigned)
            {
                return (false, $"Ô đỗ '{slot.SlotCode}' hiện đang được phân công cho một phương tiện khác.", null);
            }
        }
        else
        {
            // Auto-dispatch: Tự động tìm ô đỗ trống khả dụng đầu tiên trong bãi
            var assignedSlotIds = _context.ParkingDispatches
                .Where(d => d.Status == DispatchStatuses.Assigned)
                .Select(d => d.AssignedSlotId);

            slot = await _context.ParkingSlots
                .Include(s => s.Floor)
                .Where(s => s.Floor.LotId == session.LotId &&
                            s.IsActive &&
                            s.PhysicalStatus == SlotPhysicalStatuses.Empty &&
                            !assignedSlotIds.Contains(s.Id))
                .OrderBy(s => s.Floor.FloorNumber)
                .ThenBy(s => s.SlotCode)
                .FirstOrDefaultAsync(ct);

            if (slot == null)
            {
                return (false, $"Bãi đỗ xe '{session.Lot.Name}' hiện không còn ô đỗ nào trống khả dụng để phân công.", null);
            }
        }

        var mode = request.DispatchMode?.ToUpperInvariant() switch
        {
            DispatchModes.Auto => DispatchModes.Auto,
            DispatchModes.Manual => DispatchModes.Manual,
            _ => request.AssignedSlotId.HasValue ? DispatchModes.Manual : DispatchModes.Auto
        };

        await using var transaction = await _context.BeginTransactionAsync(ct);
        try
        {
            var dispatch = new ParkingDispatch
            {
                SessionId = session.Id,
                AssignedSlotId = slot.Id,
                Status = DispatchStatuses.Assigned,
                DispatchMode = mode,
                DispatchedAt = DateTime.UtcNow,
                Notes = request.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Lưu ý nghiệp vụ: Check-in và Dispatch không tự đổi trạng thái vật lý của ô đỗ sang OCCUPIED.
            // Ô đỗ vật lý vẫn giữ EMPTY cho tới khi có xác nhận đỗ thực tế.
            session.CurrentSlotId = slot.Id;
            session.UpdatedAt = DateTime.UtcNow;

            _context.ParkingDispatches.Add(dispatch);
            await _context.SaveChangesAsync(ct);

            await LogAuditAsync(
                action: "CREATE_DISPATCH",
                targetEntity: "parking_dispatches",
                targetId: dispatch.Id.ToString(),
                oldValues: null,
                newValues: new
                {
                    dispatch.Id,
                    dispatch.SessionId,
                    session.PlateNumber,
                    dispatch.AssignedSlotId,
                    slot.SlotCode,
                    dispatch.Status,
                    dispatch.DispatchMode,
                    ActorRole = actorRole,
                    IpAddress = ipAddress
                },
                actorId: actorId,
                ct: ct
            );

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var dto = MapToDto(dispatch, session, slot, null);
            return (true, "Tạo lệnh điều phối và phân công ô đỗ thành công.", dto);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(ct);
            if (IsPostgresUniqueViolation(ex, "uq_parking_dispatches_assigned_slot"))
            {
                return (false, $"Xung đột gán ô: Ô đỗ '{slot.SlotCode}' vừa được phân công cho một phương tiện khác.", null);
            }
            if (IsPostgresUniqueViolation(ex, "uq_parking_dispatches_active_session"))
            {
                return (false, $"Phiên đỗ xe #{session.Id} hiện đã có một lệnh điều phối đang hiệu lực.", null);
            }

            _logger.LogError(ex, "Lỗi cơ sở dữ liệu khi tạo lệnh điều phối cho phiên {SessionId}", session.Id);
            throw;
        }
    }

    public async Task<PagedResult<ParkingDispatchDto>> GetDispatchesAsync(
        ParkingDispatchFilterDto filter,
        long? currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var query = _context.ParkingDispatches
            .AsNoTracking()
            .Include(d => d.Session)
                .ThenInclude(s => s.Lot)
            .Include(d => d.Session)
                .ThenInclude(s => s.Vehicle)
            .Include(d => d.AssignedSlot)
                .ThenInclude(s => s.Floor)
            .Include(d => d.ActualSlot)
                .ThenInclude(s => s!.Floor)
            .AsQueryable();

        if (!isAdmin)
        {
            if (currentUserId == null)
            {
                return PagedResult<ParkingDispatchDto>.Create([], 0, filter.Page, filter.PageSize);
            }
            query = query.Where(d => d.Session.Vehicle != null && d.Session.Vehicle.UserId == currentUserId.Value);
        }

        if (filter.LotId.HasValue)
        {
            query = query.Where(d => d.Session.LotId == filter.LotId.Value);
        }

        if (filter.SessionId.HasValue)
        {
            query = query.Where(d => d.SessionId == filter.SessionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var normalizedStatus = filter.Status.Trim().ToUpperInvariant();
            query = query.Where(d => d.Status == normalizedStatus);
        }

        var totalCount = await query.CountAsync(ct);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(d => d.DispatchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new ParkingDispatchDto
            {
                Id = d.Id,
                SessionId = d.SessionId,
                PlateNumber = d.Session.PlateNumber,
                LotId = d.Session.LotId,
                LotName = d.Session.Lot.Name,
                AssignedSlotId = d.AssignedSlotId,
                AssignedSlotCode = d.AssignedSlot.SlotCode,
                AssignedFloorNumber = d.AssignedSlot.Floor.FloorNumber,
                AssignedFloorName = d.AssignedSlot.Floor.Name,
                AssignedPositionX = d.AssignedSlot.PositionX,
                AssignedPositionY = d.AssignedSlot.PositionY,
                AssignedPositionZ = d.AssignedSlot.PositionZ,
                ActualSlotId = d.ActualSlotId,
                ActualSlotCode = d.ActualSlot != null ? d.ActualSlot.SlotCode : null,
                ActualFloorNumber = d.ActualSlot != null ? d.ActualSlot.Floor.FloorNumber : (int?)null,
                ActualPositionX = d.ActualSlot != null ? d.ActualSlot.PositionX : (decimal?)null,
                ActualPositionY = d.ActualSlot != null ? d.ActualSlot.PositionY : (decimal?)null,
                ActualPositionZ = d.ActualSlot != null ? d.ActualSlot.PositionZ : (decimal?)null,
                Status = d.Status,
                DispatchMode = d.DispatchMode,
                DispatchedAt = d.DispatchedAt,
                ConfirmedAt = d.ConfirmedAt,
                Notes = d.Notes,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            })
            .ToListAsync(ct);

        return PagedResult<ParkingDispatchDto>.Create(items, totalCount, page, pageSize);
    }

    public async Task<ParkingDispatchDto?> GetDispatchByIdAsync(
        long id,
        long? currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var query = _context.ParkingDispatches
            .AsNoTracking()
            .Include(d => d.Session)
                .ThenInclude(s => s.Lot)
            .Include(d => d.Session)
                .ThenInclude(s => s.Vehicle)
            .Include(d => d.AssignedSlot)
                .ThenInclude(s => s.Floor)
            .Include(d => d.ActualSlot)
                .ThenInclude(s => s!.Floor)
            .Where(d => d.Id == id);

        var dispatch = await query.FirstOrDefaultAsync(ct);
        if (dispatch == null)
        {
            return null;
        }

        if (!isAdmin)
        {
            if (currentUserId == null || dispatch.Session.Vehicle == null || dispatch.Session.Vehicle.UserId != currentUserId.Value)
            {
                return null;
            }
        }

        return MapToDto(dispatch, dispatch.Session, dispatch.AssignedSlot, dispatch.ActualSlot);
    }

    public async Task<ParkingDispatchDto?> GetActiveDispatchBySessionIdAsync(
        long sessionId,
        long? currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var query = _context.ParkingDispatches
            .AsNoTracking()
            .Include(d => d.Session)
                .ThenInclude(s => s.Lot)
            .Include(d => d.Session)
                .ThenInclude(s => s.Vehicle)
            .Include(d => d.AssignedSlot)
                .ThenInclude(s => s.Floor)
            .Include(d => d.ActualSlot)
                .ThenInclude(s => s!.Floor)
            .Where(d => d.SessionId == sessionId && (d.Status == DispatchStatuses.Pending || d.Status == DispatchStatuses.Assigned));

        var dispatch = await query.FirstOrDefaultAsync(ct);
        if (dispatch == null)
        {
            return null;
        }

        if (!isAdmin)
        {
            if (currentUserId == null || dispatch.Session.Vehicle == null || dispatch.Session.Vehicle.UserId != currentUserId.Value)
            {
                return null;
            }
        }

        return MapToDto(dispatch, dispatch.Session, dispatch.AssignedSlot, dispatch.ActualSlot);
    }

    public async Task<(bool Success, string Message, ParkingDispatchDto? Data)> ReassignSlotAsync(
        long dispatchId,
        AssignSlotRequestDto request,
        long? actorId,
        string actorRole,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var dispatch = await _context.ParkingDispatches
            .Include(d => d.Session)
                .ThenInclude(s => s.Lot)
            .Include(d => d.AssignedSlot)
                .ThenInclude(s => s.Floor)
            .FirstOrDefaultAsync(d => d.Id == dispatchId, ct);

        if (dispatch == null)
        {
            return (false, $"Không tìm thấy lệnh điều phối với ID: {dispatchId}.", null);
        }

        if (dispatch.Status != DispatchStatuses.Pending && dispatch.Status != DispatchStatuses.Assigned)
        {
            return (false, $"Không thể điều phối lại khi lệnh đã ở trạng thái {dispatch.Status}.", null);
        }

        var newSlot = await _context.ParkingSlots
            .Include(s => s.Floor)
            .FirstOrDefaultAsync(s => s.Id == request.SlotId, ct);

        if (newSlot == null)
        {
            return (false, $"Không tìm thấy ô đỗ mới với ID: {request.SlotId}.", null);
        }

        if (!newSlot.IsActive)
        {
            return (false, $"Ô đỗ '{newSlot.SlotCode}' hiện đang tạm ngừng hoạt động.", null);
        }

        if (newSlot.Floor.LotId != dispatch.Session.LotId)
        {
            return (false, $"Ô đỗ mới '{newSlot.SlotCode}' không thuộc bãi đỗ xe '{dispatch.Session.Lot.Name}'.", null);
        }

        if (newSlot.PhysicalStatus != SlotPhysicalStatuses.Empty)
        {
            return (false, $"Ô đỗ mới '{newSlot.SlotCode}' hiện không khả dụng (Trạng thái vật lý: {newSlot.PhysicalStatus}).", null);
        }

        var isAlreadyAssigned = await _context.ParkingDispatches
            .AnyAsync(d => d.AssignedSlotId == newSlot.Id && d.Status == DispatchStatuses.Assigned && d.Id != dispatch.Id, ct);

        if (isAlreadyAssigned)
        {
            return (false, $"Ô đỗ '{newSlot.SlotCode}' hiện đang được phân công cho một phương tiện khác.", null);
        }

        var oldSlotCode = dispatch.AssignedSlot.SlotCode;
        var oldSlotId = dispatch.AssignedSlotId;

        await using var transaction = await _context.BeginTransactionAsync(ct);
        try
        {
            dispatch.AssignedSlotId = newSlot.Id;
            dispatch.AssignedSlot = newSlot;
            dispatch.Status = DispatchStatuses.Assigned;
            dispatch.DispatchMode = DispatchModes.Manual;
            dispatch.DispatchedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                dispatch.Notes = request.Notes.Trim();
            }
            dispatch.UpdatedAt = DateTime.UtcNow;

            dispatch.Session.CurrentSlotId = newSlot.Id;
            dispatch.Session.UpdatedAt = DateTime.UtcNow;

            await LogAuditAsync(
                action: "ASSIGN_DISPATCH_SLOT",
                targetEntity: "parking_dispatches",
                targetId: dispatch.Id.ToString(),
                oldValues: new { OldSlotId = oldSlotId, OldSlotCode = oldSlotCode },
                newValues: new
                {
                    NewSlotId = newSlot.Id,
                    NewSlotCode = newSlot.SlotCode,
                    dispatch.Status,
                    dispatch.DispatchMode,
                    ActorRole = actorRole,
                    IpAddress = ipAddress
                },
                actorId: actorId,
                ct: ct
            );

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var dto = MapToDto(dispatch, dispatch.Session, newSlot, null);
            return (true, $"Điều phối lại ô đỗ thành công từ '{oldSlotCode}' sang '{newSlot.SlotCode}'.", dto);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(ct);
            if (IsPostgresUniqueViolation(ex, "uq_parking_dispatches_assigned_slot"))
            {
                return (false, $"Xung đột gán ô: Ô đỗ '{newSlot.SlotCode}' vừa được phân công cho một phương tiện khác.", null);
            }
            throw;
        }
    }

    public async Task<(bool Success, string Message, ParkingDispatchDto? Data)> ConfirmOccupiedAsync(
        long dispatchId,
        ConfirmOccupiedRequestDto request,
        long? actorId,
        string actorRole,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var dispatch = await _context.ParkingDispatches
            .Include(d => d.Session)
                .ThenInclude(s => s.Lot)
            .Include(d => d.AssignedSlot)
                .ThenInclude(s => s.Floor)
            .FirstOrDefaultAsync(d => d.Id == dispatchId, ct);

        if (dispatch == null)
        {
            return (false, $"Không tìm thấy lệnh điều phối với ID: {dispatchId}.", null);
        }

        if (dispatch.Status != DispatchStatuses.Assigned)
        {
            return (false, $"Chỉ có thể xác nhận đỗ xe khi lệnh đang ở trạng thái ASSIGNED. Trạng thái hiện tại: {dispatch.Status}.", null);
        }

        if (dispatch.AssignedSlot.PhysicalStatus == SlotPhysicalStatuses.Occupied)
        {
            return (false, $"Xung đột chiếm ô: Ô đỗ '{dispatch.AssignedSlot.SlotCode}' hiện đã có xe khác đỗ (OCCUPIED).", null);
        }

        if (dispatch.AssignedSlot.PhysicalStatus == SlotPhysicalStatuses.Maintenance || dispatch.AssignedSlot.PhysicalStatus == SlotPhysicalStatuses.Unavailable)
        {
            return (false, $"Ô đỗ '{dispatch.AssignedSlot.SlotCode}' hiện đang ở trạng thái '{dispatch.AssignedSlot.PhysicalStatus}', không thể tiếp nhận xe.", null);
        }

        await using var transaction = await _context.BeginTransactionAsync(ct);
        try
        {
            // Xe đỗ đúng ô được chỉ định
            dispatch.Status = DispatchStatuses.Occupied;
            dispatch.ActualSlotId = dispatch.AssignedSlotId;
            dispatch.ActualSlot = dispatch.AssignedSlot;
            dispatch.ConfirmedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                dispatch.Notes = request.Notes.Trim();
            }
            dispatch.UpdatedAt = DateTime.UtcNow;

            // Lúc này ô đỗ vật lý mới chính thức chuyển sang OCCUPIED theo đúng 03_BUSINESS_RULES.md
            dispatch.AssignedSlot.PhysicalStatus = SlotPhysicalStatuses.Occupied;
            dispatch.AssignedSlot.UpdatedAt = DateTime.UtcNow;

            dispatch.Session.CurrentSlotId = dispatch.AssignedSlotId;
            dispatch.Session.UpdatedAt = DateTime.UtcNow;

            await LogAuditAsync(
                action: "CONFIRM_DISPATCH_OCCUPIED",
                targetEntity: "parking_dispatches",
                targetId: dispatch.Id.ToString(),
                oldValues: new { Status = DispatchStatuses.Assigned, SlotPhysicalStatus = SlotPhysicalStatuses.Empty },
                newValues: new
                {
                    dispatch.Status,
                    SlotPhysicalStatus = SlotPhysicalStatuses.Occupied,
                    dispatch.ActualSlotId,
                    dispatch.ConfirmedAt,
                    ConfirmationType = "MANUAL_STAFF_VERIFIED",
                    ActorRole = actorRole,
                    IpAddress = ipAddress
                },
                actorId: actorId,
                ct: ct
            );

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var dto = MapToDto(dispatch, dispatch.Session, dispatch.AssignedSlot, dispatch.AssignedSlot);
            return (true, $"Xác nhận xe đã đỗ đúng ô '{dispatch.AssignedSlot.SlotCode}' thành công (xác nhận thủ công của nhân viên).", dto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Lỗi khi xác nhận xe đỗ đúng ô cho dispatch {DispatchId}", dispatchId);
            throw;
        }
    }

    public async Task<(bool Success, string Message, ParkingDispatchDto? Data)> ReportWrongSlotAsync(
        long dispatchId,
        ReportWrongSlotRequestDto request,
        long? actorId,
        string actorRole,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var dispatch = await _context.ParkingDispatches
            .Include(d => d.Session)
                .ThenInclude(s => s.Lot)
            .Include(d => d.AssignedSlot)
                .ThenInclude(s => s.Floor)
            .FirstOrDefaultAsync(d => d.Id == dispatchId, ct);

        if (dispatch == null)
        {
            return (false, $"Không tìm thấy lệnh điều phối với ID: {dispatchId}.", null);
        }

        if (dispatch.Status != DispatchStatuses.Assigned)
        {
            return (false, $"Chỉ có thể ghi nhận đỗ sai ô khi lệnh đang ở trạng thái ASSIGNED. Trạng thái hiện tại: {dispatch.Status}.", null);
        }

        var actualSlot = await _context.ParkingSlots
            .Include(s => s.Floor)
            .FirstOrDefaultAsync(s => s.Id == request.ActualSlotId, ct);

        if (actualSlot == null)
        {
            return (false, $"Không tìm thấy ô đỗ thực tế với ID: {request.ActualSlotId}.", null);
        }

        if (actualSlot.Floor.LotId != dispatch.Session.LotId)
        {
            return (false, $"Ô đỗ thực tế '{actualSlot.SlotCode}' không thuộc bãi đỗ xe '{dispatch.Session.Lot.Name}'.", null);
        }

        if (!actualSlot.IsActive)
        {
            return (false, $"Ô đỗ thực tế '{actualSlot.SlotCode}' hiện đang tạm ngừng hoạt động.", null);
        }

        if (actualSlot.PhysicalStatus == SlotPhysicalStatuses.Occupied)
        {
            return (false, $"Xung đột chiếm ô: Ô đỗ thực tế '{actualSlot.SlotCode}' hiện đã có xe khác đang đỗ (OCCUPIED).", null);
        }

        if (actualSlot.PhysicalStatus == SlotPhysicalStatuses.Maintenance || actualSlot.PhysicalStatus == SlotPhysicalStatuses.Unavailable)
        {
            return (false, $"Ô đỗ thực tế '{actualSlot.SlotCode}' đang ở trạng thái '{actualSlot.PhysicalStatus}', không thể tiếp nhận xe.", null);
        }

        var isActualSlotAssigned = await _context.ParkingDispatches
            .AnyAsync(d => d.AssignedSlotId == actualSlot.Id && d.Status == DispatchStatuses.Assigned && d.Id != dispatch.Id, ct);
        if (isActualSlotAssigned)
        {
            return (false, $"Xung đột gán ô: Ô đỗ thực tế '{actualSlot.SlotCode}' hiện đang được phân công cho một phương tiện khác đang di chuyển tới.", null);
        }

        await using var transaction = await _context.BeginTransactionAsync(ct);
        try
        {
            // Xe đỗ sai ô: cập nhật status WRONG_SLOT, actualSlotId
            dispatch.Status = DispatchStatuses.WrongSlot;
            dispatch.ActualSlotId = actualSlot.Id;
            dispatch.ActualSlot = actualSlot;
            dispatch.ConfirmedAt = DateTime.UtcNow;
            dispatch.Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? $"Đỗ sai ô (Được chỉ định '{dispatch.AssignedSlot.SlotCode}', thực tế đỗ '{actualSlot.SlotCode}')"
                : request.Notes.Trim();
            dispatch.UpdatedAt = DateTime.UtcNow;

            // Ô thực tế xe đang đỗ -> chuyển sang OCCUPIED
            actualSlot.PhysicalStatus = SlotPhysicalStatuses.Occupied;
            actualSlot.UpdatedAt = DateTime.UtcNow;

            // Ô được chỉ định ban đầu -> vẫn giữ trạng thái EMPTY và được giải phóng khỏi ASSIGNED để xe khác vào
            dispatch.AssignedSlot.PhysicalStatus = SlotPhysicalStatuses.Empty;
            dispatch.AssignedSlot.UpdatedAt = DateTime.UtcNow;

            // Cập nhật phiên sang vị trí thực tế
            dispatch.Session.CurrentSlotId = actualSlot.Id;
            dispatch.Session.UpdatedAt = DateTime.UtcNow;

            await LogAuditAsync(
                action: "REPORT_WRONG_SLOT",
                targetEntity: "parking_dispatches",
                targetId: dispatch.Id.ToString(),
                oldValues: new { dispatch.AssignedSlotId, AssignedSlotCode = dispatch.AssignedSlot.SlotCode },
                newValues: new
                {
                    dispatch.Status,
                    ActualSlotId = actualSlot.Id,
                    ActualSlotCode = actualSlot.SlotCode,
                    dispatch.ConfirmedAt,
                    dispatch.Notes,
                    ActorRole = actorRole,
                    IpAddress = ipAddress
                },
                actorId: actorId,
                ct: ct
            );

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var dto = MapToDto(dispatch, dispatch.Session, dispatch.AssignedSlot, actualSlot);
            return (true, $"Ghi nhận xe đỗ sai ô thành công: Được chỉ định '{dispatch.AssignedSlot.SlotCode}', thực tế đỗ '{actualSlot.SlotCode}'.", dto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Lỗi khi ghi nhận xe đỗ sai ô cho dispatch {DispatchId}", dispatchId);
            throw;
        }
    }

    public async Task<(bool Success, string Message)> CancelDispatchAsync(
        long dispatchId,
        CancelDispatchRequestDto request,
        long? actorId,
        string actorRole,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var dispatch = await _context.ParkingDispatches
            .Include(d => d.Session)
            .Include(d => d.AssignedSlot)
            .FirstOrDefaultAsync(d => d.Id == dispatchId, ct);

        if (dispatch == null)
        {
            return (false, $"Không tìm thấy lệnh điều phối với ID: {dispatchId}.");
        }

        if (dispatch.Status != DispatchStatuses.Pending && dispatch.Status != DispatchStatuses.Assigned)
        {
            return (false, $"Chỉ có thể hủy lệnh điều phối khi ở trạng thái PENDING hoặc ASSIGNED. Trạng thái hiện tại: {dispatch.Status}.");
        }

        await using var transaction = await _context.BeginTransactionAsync(ct);
        try
        {
            var oldStatus = dispatch.Status;
            dispatch.Status = DispatchStatuses.Cancelled;
            if (!string.IsNullOrWhiteSpace(request.Reason))
            {
                dispatch.Notes = $"[HỦY] {request.Reason.Trim()}";
            }
            dispatch.UpdatedAt = DateTime.UtcNow;

            // Giải phóng phiên khỏi slot được chỉ định nếu chưa thực sự đỗ
            if (dispatch.Session.CurrentSlotId == dispatch.AssignedSlotId)
            {
                dispatch.Session.CurrentSlotId = null;
                dispatch.Session.UpdatedAt = DateTime.UtcNow;
            }

            // Không được vô tình giải phóng ô nếu ô đang có xe (OCCUPIED) hoặc đang bảo trì (MAINTENANCE/UNAVAILABLE)
            if (dispatch.AssignedSlot.PhysicalStatus != SlotPhysicalStatuses.Occupied &&
                dispatch.AssignedSlot.PhysicalStatus != SlotPhysicalStatuses.Maintenance &&
                dispatch.AssignedSlot.PhysicalStatus != SlotPhysicalStatuses.Unavailable)
            {
                dispatch.AssignedSlot.PhysicalStatus = SlotPhysicalStatuses.Empty;
                dispatch.AssignedSlot.UpdatedAt = DateTime.UtcNow;
            }

            await LogAuditAsync(
                action: "CANCEL_DISPATCH",
                targetEntity: "parking_dispatches",
                targetId: dispatch.Id.ToString(),
                oldValues: new { Status = oldStatus, dispatch.AssignedSlotId },
                newValues: new
                {
                    dispatch.Status,
                    dispatch.Notes,
                    ActorRole = actorRole,
                    IpAddress = ipAddress
                },
                actorId: actorId,
                ct: ct
            );

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return (true, "Hủy lệnh điều phối thành công. Ô đỗ đã được giải phóng.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Lỗi khi hủy lệnh điều phối {DispatchId}", dispatchId);
            throw;
        }
    }

    private static ParkingDispatchDto MapToDto(ParkingDispatch dispatch, ParkingSession session, ParkingSlot assignedSlot, ParkingSlot? actualSlot)
    {
        return new ParkingDispatchDto
        {
            Id = dispatch.Id,
            SessionId = dispatch.SessionId,
            PlateNumber = session.PlateNumber,
            LotId = session.LotId,
            LotName = session.Lot?.Name ?? string.Empty,
            AssignedSlotId = dispatch.AssignedSlotId,
            AssignedSlotCode = assignedSlot.SlotCode,
            AssignedFloorNumber = assignedSlot.Floor.FloorNumber,
            AssignedFloorName = assignedSlot.Floor.Name,
            AssignedPositionX = assignedSlot.PositionX,
            AssignedPositionY = assignedSlot.PositionY,
            AssignedPositionZ = assignedSlot.PositionZ,
            ActualSlotId = dispatch.ActualSlotId,
            ActualSlotCode = actualSlot?.SlotCode,
            ActualFloorNumber = actualSlot?.Floor?.FloorNumber,
            ActualPositionX = actualSlot?.PositionX,
            ActualPositionY = actualSlot?.PositionY,
            ActualPositionZ = actualSlot?.PositionZ,
            Status = dispatch.Status,
            DispatchMode = dispatch.DispatchMode,
            DispatchedAt = dispatch.DispatchedAt,
            ConfirmedAt = dispatch.ConfirmedAt,
            Notes = dispatch.Notes,
            CreatedAt = dispatch.CreatedAt,
            UpdatedAt = dispatch.UpdatedAt
        };
    }

    private static bool IsPostgresUniqueViolation(DbUpdateException ex, string constraintName)
    {
        if (ex.InnerException is Npgsql.PostgresException pgEx)
        {
            return pgEx.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation &&
                   (string.IsNullOrEmpty(constraintName) || pgEx.ConstraintName?.Equals(constraintName, StringComparison.OrdinalIgnoreCase) == true);
        }

        var innerMsg = ex.InnerException?.Message ?? ex.Message;
        return innerMsg.Contains("23505") || innerMsg.Contains(constraintName, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LogAuditAsync(string action, string targetEntity, string targetId, object? oldValues, object? newValues, long? actorId, CancellationToken ct)
    {
        var audit = new AuditLog
        {
            Action = action,
            TargetEntity = targetEntity,
            TargetId = targetId,
            ActorId = actorId,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, JsonOptions) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues, JsonOptions) : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(audit);
    }
}

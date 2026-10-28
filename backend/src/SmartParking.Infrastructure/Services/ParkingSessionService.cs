using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartParking.Application.Common.Helpers;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Sessions;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Common;

namespace SmartParking.Infrastructure.Services;

public class ParkingSessionService : IParkingSessionService
{
    private readonly IApplicationDbContext _context;
    private readonly IParkingAuthorizationService _authService;
    private readonly ILogger<ParkingSessionService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public ParkingSessionService(
        IApplicationDbContext context,
        IParkingAuthorizationService authService,
        ILogger<ParkingSessionService> logger)
    {
        _context = context;
        _authService = authService;
        _logger = logger;
    }

    public async Task<(bool Success, string Message, ParkingSessionDto? Data)> CheckInAsync(
        CheckInRequestDto request,
        long? actorId,
        string actorRole,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var normalizedPlate = PlateNumberHelper.Normalize(request.PlateNumber);
        if (!PlateNumberHelper.IsValidPlateFormat(normalizedPlate))
        {
            return (false, "Biển số xe không hợp lệ. Độ dài phải từ 3 đến 20 ký tự.", null);
        }

        var lot = await _context.ParkingLots.FirstOrDefaultAsync(l => l.Id == request.LotId, ct);
        if (lot == null)
        {
            return (false, $"Không tìm thấy bãi đỗ xe với ID: {request.LotId}.", null);
        }

        if (!lot.IsActive)
        {
            return (false, $"Bãi đỗ xe '{lot.Name}' hiện đang đóng hoặc tạm ngừng hoạt động.", null);
        }

        if (!await _authService.CanOperateSessionOrDispatchAsync(actorId ?? 0, actorRole, request.LotId, ct))
        {
            return (false, "Bạn không có quyền thực hiện check-in tại bãi đỗ xe này.", null);
        }

        // Anti-Passback check: Biển số xe không được phép có phiên ACTIVE hoặc PAID đang mở
        var hasActiveSession = await _context.ParkingSessions
            .AnyAsync(s => s.PlateNumber == normalizedPlate && (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid), ct);

        if (hasActiveSession)
        {
            return (false, $"Phương tiện mang biển số '{normalizedPlate}' đang có phiên đỗ xe chưa kết thúc (ACTIVE/PAID). Không thể check-in phiên mới.", null);
        }

        var lotLock = ParkingLotLockHelper.GetSemaphore(lot.Id);
        await lotLock.WaitAsync(ct);
        try
        {
            await using var transaction = await _context.BeginTransactionAsync(ct);
            try
            {
                if (_context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
                {
                    long lockKey = ParkingLotLockHelper.LotLockBase + lot.Id;
                    await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0});", lockKey, ct);
                }

                // Kiểm tra xem phương tiện có lượt đặt chỗ PENDING hợp lệ tại bãi này hay không
                var booking = await _context.Bookings
                    .Where(b => b.LotId == lot.Id && b.PlateNumber == normalizedPlate && b.Status == BookingStatuses.Pending)
                    .OrderBy(b => b.CreatedAt)
                    .FirstOrDefaultAsync(ct);

                if (booking == null)
                {
                    // Xe vãng lai: Kiểm tra sức chứa khả dụng theo thẩm quyền bãi xe (áp dụng Conservative IoT Guard & slot fallback)
                    var calc = await ParkingLotCapacityHelper.CalculateAsync(_context, lot.Id, lot, ct);

                    if (calc.AvailableCapacity <= 0)
                    {
                        await transaction.RollbackAsync(ct);
                        return (false, $"Bãi đỗ xe '{lot.Name}' đã hết chỗ khả dụng cho khách vãng lai. Toàn bộ vị trí còn lại đã được đặt trước hoặc đã có xe đỗ.", null);
                    }
                }
                else
                {
                    // Xe có đặt chỗ: Chuyển đổi trạng thái Booking sang CHECKED_IN atomically mà không cộng dồn công suất kép
                    booking.Status = BookingStatuses.CheckedIn;
                    booking.CheckedInAt = DateTime.UtcNow;
                    booking.UpdatedAt = DateTime.UtcNow;
                }

                // Tra cứu hoặc tự động tạo phương tiện vãng lai nếu chưa có
                var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.PlateNumber == normalizedPlate, ct);
                if (vehicle == null)
                {
                    vehicle = new Vehicle
                    {
                        PlateNumber = normalizedPlate,
                        UserId = booking?.UserId, // Gắn với người dùng đặt chỗ nếu có
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Vehicles.Add(vehicle);
                    await _context.SaveChangesAsync(ct);
                }
                else if (vehicle.UserId == null && booking?.UserId != null)
                {
                    vehicle.UserId = booking.UserId;
                    vehicle.UpdatedAt = DateTime.UtcNow;
                }

                ParkingSlot? slot = null;
                if (request.SlotId.HasValue)
                {
                    slot = await _context.ParkingSlots
                        .Include(sl => sl.Floor)
                        .FirstOrDefaultAsync(sl => sl.Id == request.SlotId.Value, ct);

                    if (slot == null)
                    {
                        await transaction.RollbackAsync(ct);
                        return (false, $"Không tìm thấy ô đỗ với ID: {request.SlotId.Value}.", null);
                    }

                    if (!slot.IsActive)
                    {
                        await transaction.RollbackAsync(ct);
                        return (false, $"Ô đỗ '{slot.SlotCode}' hiện đang tạm ngừng hoạt động.", null);
                    }

                    if (slot.Floor.LotId != request.LotId)
                    {
                        await transaction.RollbackAsync(ct);
                        return (false, $"Ô đỗ '{slot.SlotCode}' không thuộc bãi đỗ xe '{lot.Name}'.", null);
                    }

                    if (slot.PhysicalStatus != SlotPhysicalStatuses.Empty)
                    {
                        await transaction.RollbackAsync(ct);
                        return (false, $"Ô đỗ '{slot.SlotCode}' hiện không khả dụng (Trạng thái: {slot.PhysicalStatus}).", null);
                    }

                    // Không gán ô đang được phân công cho một lệnh điều phối ASSIGNED khác
                    var isAlreadyAssigned = await _context.ParkingDispatches
                        .AnyAsync(d => d.AssignedSlotId == slot.Id && d.Status == DispatchStatuses.Assigned, ct);
                    if (isAlreadyAssigned)
                    {
                        await transaction.RollbackAsync(ct);
                        return (false, $"Ô đỗ '{slot.SlotCode}' hiện đang được phân công cho một phương tiện khác.", null);
                    }

                    // Lưu ý theo Core Business Invariant: Check-in tại cổng KHÔNG tự động chuyển trạng thái vật lý của Slot sang OCCUPIED.
                    // Slot vẫn giữ trạng thái vật lý EMPTY cho tới khi cảm biến IoT hoặc module Dispatch ghi nhận xe đã đỗ thực tế vào ô.
                }

                var session = new ParkingSession
                {
                    LotId = lot.Id,
                    VehicleId = vehicle.Id,
                    BookingId = booking?.Id,
                    PlateNumber = normalizedPlate,
                    CheckInTime = DateTime.UtcNow,
                    CurrentSlotId = slot?.Id,
                    Status = SessionStatuses.Active,
                    TotalFee = 0m,
                    CheckInImageUrl = request.CheckInImageUrl?.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.ParkingSessions.Add(session);
                await _context.SaveChangesAsync(ct);

                await LogAuditAsync(
                    action: "CHECK_IN_SESSION",
                    targetEntity: "parking_sessions",
                    targetId: session.Id.ToString(),
                    oldValues: null,
                    newValues: new
                    {
                        session.Id,
                        session.LotId,
                        session.BookingId,
                        session.PlateNumber,
                        session.CurrentSlotId,
                        session.CheckInTime,
                        session.Status,
                        ActorRole = actorRole,
                        IpAddress = ipAddress
                    },
                    actorId: actorId,
                    ct: ct
                );

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                var dto = MapToDto(session, lot.Name, slot?.SlotCode, slot?.Floor?.FloorNumber);
                return (true, "Check-in phương tiện vào bãi thành công.", dto);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(ct);
                if (IsPostgresUniqueViolation(ex, "uq_parking_sessions_active_plate"))
                {
                    return (false, $"Phương tiện mang biển số '{normalizedPlate}' đang có phiên đỗ xe chưa kết thúc (ACTIVE/PAID). Không thể check-in phiên mới.", null);
                }

                _logger.LogError(ex, "Lỗi cơ sở dữ liệu khi check-in phiên đỗ xe cho biển số {PlateNumber}", normalizedPlate);
                throw;
            }
        }
        finally
        {
            lotLock.Release();
        }
    }

    public async Task<(bool Success, string Message, ParkingSessionDto? Data)> CheckOutAsync(
        long sessionId,
        CheckOutRequestDto request,
        long? actorId,
        string actorRole,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var session = await _context.ParkingSessions
            .Include(s => s.Lot)
            .Include(s => s.CurrentSlot)
                .ThenInclude(sl => sl!.Floor)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null)
        {
            return (false, $"Không tìm thấy phiên gửi xe với ID: {sessionId}.", null);
        }

        if (session.Status == SessionStatuses.Completed || session.Status == SessionStatuses.Cancelled)
        {
            return (false, $"Phiên gửi xe này đã kết thúc trước đó với trạng thái: {session.Status}.", null);
        }

        if (!await _authService.CanOperateSessionOrDispatchAsync(actorId ?? 0, actorRole, session.LotId, ct))
        {
            return (false, "Bạn không có quyền thực hiện check-out tại bãi đỗ xe này.", null);
        }

        var lotLock = ParkingLotLockHelper.GetSemaphore(session.LotId);
        await lotLock.WaitAsync(ct);
        try
        {
            await using var transaction = await _context.BeginTransactionAsync(ct);
            try
            {
                if (_context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
                {
                    long lockKey = ParkingLotLockHelper.LotLockBase + session.LotId;
                    await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0});", lockKey, ct);
                }

                var oldStatus = session.Status;
                session.CheckOutTime = DateTime.UtcNow;
                session.Status = SessionStatuses.Completed;
                if (!string.IsNullOrWhiteSpace(request.CheckOutImageUrl))
                {
                    session.CheckOutImageUrl = request.CheckOutImageUrl.Trim();
                }
                session.UpdatedAt = DateTime.UtcNow;

                // Không để dispatch PENDING/ASSIGNED tồn đọng sau khi session COMPLETED
                var activeDispatches = await _context.ParkingDispatches
                    .Where(d => d.SessionId == session.Id && (d.Status == DispatchStatuses.Pending || d.Status == DispatchStatuses.Assigned))
                    .ToListAsync(ct);

                foreach (var d in activeDispatches)
                {
                    d.Status = DispatchStatuses.Cancelled;
                    d.Notes = string.IsNullOrWhiteSpace(d.Notes)
                        ? "[CHECK-OUT] Tự động hủy lệnh điều phối do phiên gửi xe đã hoàn tất check-out."
                        : $"{d.Notes} | [CHECK-OUT] Tự động hủy do phiên check-out.";
                    d.UpdatedAt = DateTime.UtcNow;
                }

                // Giải phóng đúng ô đỗ mà xe thực sự đỗ nếu trước đó ô đỗ đã được ghi nhận OCCUPIED
                var slotToRelease = session.CurrentSlot;
                if (slotToRelease == null && session.CurrentSlotId.HasValue)
                {
                    slotToRelease = await _context.ParkingSlots.FirstOrDefaultAsync(s => s.Id == session.CurrentSlotId.Value, ct);
                }

                if (slotToRelease != null && slotToRelease.PhysicalStatus == SlotPhysicalStatuses.Occupied)
                {
                    slotToRelease.PhysicalStatus = SlotPhysicalStatuses.Empty;
                    slotToRelease.UpdatedAt = DateTime.UtcNow;
                }

                await LogAuditAsync(
                    action: "CHECK_OUT_SESSION",
                    targetEntity: "parking_sessions",
                    targetId: session.Id.ToString(),
                    oldValues: new { session.Id, Status = oldStatus, session.CheckInTime },
                    newValues: new
                    {
                        session.Id,
                        session.Status,
                        session.CheckOutTime,
                        ReleasedSlotId = session.CurrentSlotId,
                        ActorRole = actorRole,
                        IpAddress = ipAddress,
                        ConfirmationType = "MANUAL_STAFF_VERIFIED"
                    },
                    actorId: actorId,
                    ct: ct
                );

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                var dto = MapToDto(session, session.Lot?.Name ?? string.Empty, session.CurrentSlot?.SlotCode, session.CurrentSlot?.Floor?.FloorNumber);
                return (true, "Xác nhận xe rời bãi thành công (thao tác thủ công của nhân viên/quản trị viên, chờ tích hợp cảm biến cổng ra).", dto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Lỗi giao dịch khi check-out phiên gửi xe {SessionId}", sessionId);
                throw;
            }
        }
        finally
        {
            lotLock.Release();
        }
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

    public async Task<PagedResult<ParkingSessionDto>> GetActiveSessionsAsync(
        ParkingSessionFilterDto filter,
        long? currentUserId,
        string currentUserRole,
        CancellationToken ct = default)
    {
        var query = _context.ParkingSessions
            .AsNoTracking()
            .Include(s => s.Lot)
            .Include(s => s.CurrentSlot)
                .ThenInclude(sl => sl!.Floor)
            .Include(s => s.Vehicle)
            .Where(s => s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid);

        if (currentUserRole == Roles.Admin)
        {
            if (filter.LotId.HasValue)
            {
                query = query.Where(s => s.LotId == filter.LotId.Value);
            }
        }
        else if (currentUserRole == Roles.Manager || currentUserRole == Roles.Staff)
        {
            var assignedLotIds = await _authService.GetAssignedLotIdsAsync(currentUserId ?? 0, currentUserRole, ct);
            query = query.Where(s => assignedLotIds.Contains(s.LotId));

            if (filter.LotId.HasValue)
            {
                if (!assignedLotIds.Contains(filter.LotId.Value))
                {
                    return PagedResult<ParkingSessionDto>.Create([], 0, filter.Page, filter.PageSize);
                }
                query = query.Where(s => s.LotId == filter.LotId.Value);
            }
        }
        else
        {
            if (currentUserId == null)
            {
                return PagedResult<ParkingSessionDto>.Create([], 0, filter.Page, filter.PageSize);
            }
            query = query.Where(s => s.Vehicle != null && s.Vehicle.UserId == currentUserId.Value);

            if (filter.LotId.HasValue)
            {
                query = query.Where(s => s.LotId == filter.LotId.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.PlateNumber))
        {
            var normalizedPlate = PlateNumberHelper.Normalize(filter.PlateNumber);
            query = query.Where(s => s.PlateNumber.Contains(normalizedPlate));
        }

        var totalCount = await query.CountAsync(ct);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(s => s.CheckInTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ParkingSessionDto
            {
                Id = s.Id,
                LotId = s.LotId,
                LotName = s.Lot.Name,
                VehicleId = s.VehicleId,
                PlateNumber = s.PlateNumber,
                CheckInTime = s.CheckInTime,
                CheckOutTime = s.CheckOutTime,
                CurrentSlotId = s.CurrentSlotId,
                SlotCode = s.CurrentSlot != null ? s.CurrentSlot.SlotCode : null,
                FloorNumber = s.CurrentSlot != null ? s.CurrentSlot.Floor.FloorNumber : (int?)null,
                Status = s.Status,
                TotalFee = s.TotalFee,
                PaidAt = s.PaidAt,
                CheckInImageUrl = s.CheckInImageUrl,
                CheckOutImageUrl = s.CheckOutImageUrl,
                DurationMinutes = (DateTime.UtcNow - s.CheckInTime).TotalMinutes,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(ct);

        return PagedResult<ParkingSessionDto>.Create(items, totalCount, page, pageSize);
    }

    public async Task<PagedResult<ParkingSessionDto>> GetUserSessionsAsync(
        long userId,
        ParkingSessionFilterDto filter,
        CancellationToken ct = default)
    {
        var query = _context.ParkingSessions
            .AsNoTracking()
            .Include(s => s.Lot)
            .Include(s => s.CurrentSlot)
                .ThenInclude(sl => sl!.Floor)
            .Include(s => s.Vehicle)
            .Where(s => s.Vehicle != null && s.Vehicle.UserId == userId);

        if (filter.LotId.HasValue)
        {
            query = query.Where(s => s.LotId == filter.LotId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(s => s.Status == filter.Status.ToUpperInvariant());
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(s => s.CheckInTime >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(s => s.CheckInTime <= filter.ToDate.Value);
        }

        var totalCount = await query.CountAsync(ct);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(s => s.CheckInTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ParkingSessionDto
            {
                Id = s.Id,
                LotId = s.LotId,
                LotName = s.Lot.Name,
                VehicleId = s.VehicleId,
                PlateNumber = s.PlateNumber,
                CheckInTime = s.CheckInTime,
                CheckOutTime = s.CheckOutTime,
                CurrentSlotId = s.CurrentSlotId,
                SlotCode = s.CurrentSlot != null ? s.CurrentSlot.SlotCode : null,
                FloorNumber = s.CurrentSlot != null ? s.CurrentSlot.Floor.FloorNumber : (int?)null,
                Status = s.Status,
                TotalFee = s.TotalFee,
                PaidAt = s.PaidAt,
                CheckInImageUrl = s.CheckInImageUrl,
                CheckOutImageUrl = s.CheckOutImageUrl,
                DurationMinutes = ((s.CheckOutTime ?? DateTime.UtcNow) - s.CheckInTime).TotalMinutes,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(ct);

        return PagedResult<ParkingSessionDto>.Create(items, totalCount, page, pageSize);
    }

    public async Task<ParkingSessionDto?> GetSessionByIdAsync(long id, long? currentUserId, string currentUserRole, CancellationToken ct = default)
    {
        var query = _context.ParkingSessions
            .AsNoTracking()
            .Include(s => s.Lot)
            .Include(s => s.CurrentSlot)
                .ThenInclude(sl => sl!.Floor)
            .Include(s => s.Vehicle)
            .Where(s => s.Id == id);

        var session = await query.FirstOrDefaultAsync(ct);
        if (session == null)
        {
            return null;
        }

        if (currentUserRole != Roles.Admin)
        {
            if (currentUserRole == Roles.Manager || currentUserRole == Roles.Staff)
            {
                var hasAccess = await _authService.HasLotAccessAsync(currentUserId ?? 0, currentUserRole, session.LotId, ct);
                if (!hasAccess)
                {
                    return null;
                }
            }
            else
            {
                if (currentUserId == null || session.Vehicle == null || session.Vehicle.UserId != currentUserId.Value)
                {
                    return null;
                }
            }
        }

        return MapToDto(session, session.Lot?.Name ?? string.Empty, session.CurrentSlot?.SlotCode, session.CurrentSlot?.Floor?.FloorNumber);
    }

    public async Task<ParkingSessionDto?> GetActiveSessionByPlateAsync(string plateNumber, long? currentUserId, string currentUserRole, CancellationToken ct = default)
    {
        var normalizedPlate = PlateNumberHelper.Normalize(plateNumber);
        if (string.IsNullOrWhiteSpace(normalizedPlate))
        {
            return null;
        }

        var query = _context.ParkingSessions
            .AsNoTracking()
            .Include(s => s.Lot)
            .Include(s => s.CurrentSlot)
                .ThenInclude(sl => sl!.Floor)
            .Include(s => s.Vehicle)
            .Where(s => s.PlateNumber == normalizedPlate && (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid));

        var session = await query.FirstOrDefaultAsync(ct);
        if (session == null)
        {
            return null;
        }

        if (currentUserRole != Roles.Admin)
        {
            if (currentUserRole == Roles.Manager || currentUserRole == Roles.Staff)
            {
                var hasAccess = await _authService.HasLotAccessAsync(currentUserId ?? 0, currentUserRole, session.LotId, ct);
                if (!hasAccess)
                {
                    return null;
                }
            }
            else
            {
                if (currentUserId == null || session.Vehicle == null || session.Vehicle.UserId != currentUserId.Value)
                {
                    return null;
                }
            }
        }

        return MapToDto(session, session.Lot?.Name ?? string.Empty, session.CurrentSlot?.SlotCode, session.CurrentSlot?.Floor?.FloorNumber);
    }

    private static ParkingSessionDto MapToDto(ParkingSession session, string lotName, string? slotCode, int? floorNumber)
    {
        var duration = ((session.CheckOutTime ?? DateTime.UtcNow) - session.CheckInTime).TotalMinutes;

        return new ParkingSessionDto
        {
            Id = session.Id,
            LotId = session.LotId,
            LotName = lotName,
            VehicleId = session.VehicleId,
            BookingId = session.BookingId,
            PlateNumber = session.PlateNumber,
            CheckInTime = session.CheckInTime,
            CheckOutTime = session.CheckOutTime,
            CurrentSlotId = session.CurrentSlotId,
            SlotCode = slotCode,
            FloorNumber = floorNumber,
            Status = session.Status,
            TotalFee = session.TotalFee,
            PaidAt = session.PaidAt,
            CheckInImageUrl = session.CheckInImageUrl,
            CheckOutImageUrl = session.CheckOutImageUrl,
            DurationMinutes = Math.Round(duration, 2),
            CreatedAt = session.CreatedAt
        };
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

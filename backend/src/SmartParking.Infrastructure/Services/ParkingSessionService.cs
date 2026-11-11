using SmartParking.Persistence.Data;
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

    public async Task<MonthlyTrafficStatsDto> GetMonthlyTrafficStatsAsync(int year, int month, int? lotId, CancellationToken ct = default)
    {
        // Dữ liệu lưu UTC, thống kê theo ngày giờ Việt Nam (UTC+7).
        var offset = TimeSpan.FromHours(7);
        var startLocal = new DateTime(year, month, 1);
        var endLocal = startLocal.AddMonths(1);
        var startUtc = startLocal - offset;
        var endUtc = endLocal - offset;

        var query = _context.ParkingSessions.AsNoTracking().Where(s => s.Status != "CANCELLED");
        if (lotId.HasValue) query = query.Where(s => s.LotId == lotId.Value);

        var rows = await query
            .Where(s => (s.CheckInTime >= startUtc && s.CheckInTime < endUtc)
                     || (s.CheckOutTime != null && s.CheckOutTime >= startUtc && s.CheckOutTime < endUtc)
                     || (s.PaidAt != null && s.PaidAt >= startUtc && s.PaidAt < endUtc))
            .Select(s => new { s.CheckInTime, s.CheckOutTime, s.PaidAt, s.TotalFee })
            .ToListAsync(ct);

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var days = Enumerable.Range(1, daysInMonth)
            .Select(d => new DailyTrafficDto { Date = new DateTime(year, month, d).ToString("yyyy-MM-dd") })
            .ToList();
        var byHour = new int[24];

        bool InMonth(DateTime utc) => utc >= startUtc && utc < endUtc;
        foreach (var r in rows)
        {
            if (InMonth(r.CheckInTime))
            {
                var local = r.CheckInTime + offset;
                days[local.Day - 1].CheckIns++;
                byHour[local.Hour]++;
            }
            if (r.CheckOutTime is { } outTime && InMonth(outTime))
                days[(outTime + offset).Day - 1].CheckOuts++;
            if (r.PaidAt is { } paid && InMonth(paid))
                days[(paid + offset).Day - 1].Revenue += r.TotalFee;
        }

        return new MonthlyTrafficStatsDto
        {
            Year = year,
            Month = month,
            LotId = lotId,
            TotalCheckIns = days.Sum(d => d.CheckIns),
            TotalCheckOuts = days.Sum(d => d.CheckOuts),
            TotalRevenue = days.Sum(d => d.Revenue),
            Days = days,
            CheckInsByHour = byHour
        };
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

        if (!await _authService.CanOperateGateInAsync(actorId ?? 0, actorRole, request.LotId, ct))
        {
            return (false, "Bạn không có quyền thực hiện nghiệp vụ cổng (GATE) tại bãi đỗ xe này.", null);
        }

        if (await _context.ParkingZones.AnyAsync(z => z.LotId == request.LotId, ct))
            return (false, "Bãi đã có khu. Tiếp nhận bằng vé/EventId qua API operations/entries.", null);
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
                    await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0});", new object[] { lockKey }, cancellationToken: ct);
                }

                var pendingBooking = await _context.Bookings.AnyAsync(
                    b => b.LotId == lot.Id
                        && b.PlateNumber == normalizedPlate
                        && b.Status == BookingStatuses.Pending,
                    ct);
                if (pendingBooking)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, "Xe có booking đang chờ. Hãy xác minh xe tại ô đỗ từ danh sách booking, không check-in tại cổng.", null);
                }

                var calc = await ParkingLotCapacityHelper.CalculateAsync(_context, lot.Id, lot, ct);
                if (calc.AvailableCapacity <= 0)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Bãi đỗ xe '{lot.Name}' đã hết chỗ khả dụng cho khách vãng lai. Toàn bộ vị trí còn lại đã được đặt trước hoặc đã có xe đỗ.", null);
                }

                // Tra cứu hoặc tự động tạo phương tiện vãng lai nếu chưa có
                var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.PlateNumber == normalizedPlate, ct);
                if (vehicle == null)
                {
                    vehicle = new Vehicle
                    {
                        PlateNumber = normalizedPlate,
                        UserId = null,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Vehicles.Add(vehicle);
                    await _context.SaveChangesAsync(ct);
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

                    if (slot.PhysicalStatus == SlotPhysicalStatuses.Maintenance || slot.PhysicalStatus == SlotPhysicalStatuses.Unavailable)
                    {
                        await transaction.RollbackAsync(ct);
                        return (false, $"Ô đỗ '{slot.SlotCode}' hiện đang bảo trì hoặc ngừng phục vụ ({slot.PhysicalStatus}). Không thể gán ô đỗ này.", null);
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

                    // Không gán ô đang có phiên đỗ xe ACTIVE hoặc PAID chiếm dụng (chống gán trùng lặp ô)
                    var isSlotOccupiedBySession = await _context.ParkingSessions
                        .AnyAsync(s => s.CurrentSlotId == slot.Id && (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid), ct);
                    if (isSlotOccupiedBySession)
                    {
                        await transaction.RollbackAsync(ct);
                        return (false, $"Ô đỗ '{slot.SlotCode}' hiện đang được sử dụng bởi một phiên đỗ xe khác.", null);
                    }

                    // Lưu ý theo Core Business Invariant: Check-in tại cổng KHÔNG tự động chuyển trạng thái vật lý của Slot sang OCCUPIED.
                    // Slot vẫn giữ trạng thái vật lý EMPTY cho tới khi cảm biến IoT hoặc module Dispatch ghi nhận xe đã đỗ thực tế vào ô.
                }

                var session = new ParkingSession
                {
                    LotId = lot.Id,
                    VehicleId = vehicle.Id,
                    PolicyId = (await ParkingPolicyResolver.ResolveAsync(_context, lot.Id, null, DateTime.UtcNow, ct))?.Id,
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

        if (!await _authService.CanOperateGateOutAsync(actorId ?? 0, actorRole, session.LotId, ct))
        {
            return (false, "Bạn không có quyền thực hiện nghiệp vụ cổng ra (GATE) tại bãi đỗ xe này.", null);
        }
        if (!await _authService.CanOperateAreaAsync(actorId ?? 0, actorRole, session.LotId, StaffDuties.GateOut,
            session.CurrentSlot?.FloorId, session.ZoneId ?? session.CurrentSlot?.Floor.ZoneId, ct))
            return (false, "Phiên xe nằm ngoài tầng hoặc khu vực được phân công.", null);

        // Kiểm tra nhận diện biển số tại cổng ra nếu có cung cấp (chuẩn hóa đối chiếu theo PlateNumberHelper.IsMatch)
        if (!string.IsNullOrWhiteSpace(request.ConfirmedPlateNumber))
        {
            if (!PlateNumberHelper.IsMatch(request.ConfirmedPlateNumber, session.PlateNumber))
            {
                return (false, $"Biển số xe xác nhận tại cổng ra ({request.ConfirmedPlateNumber.Trim()}) không khớp với phiên gửi xe #{session.Id} (Biển số: {session.PlateNumber}).", null);
            }
        }

        // Financial Clearance Invariant:
        // 1. Xe đã thanh toán hoàn tất (Status == PAID): Đủ điều kiện tài chính để xác nhận ra bãi.
        // 2. Nếu phiên có thu phí (TotalFee > 0) mà chưa thanh toán (Status != PAID):
        //    - Nhân viên cổng (STAFF) tuyệt đối không được phép check-out (BLOCKED).
        //    - STAFF không được tự ý bypass bằng allowExitWithoutPayment.
        //    - Chỉ ADMIN hoặc MANAGER mới có quyền phê duyệt miễn trừ thanh toán với lý do cụ thể.
        // 3. Nếu phiên có TotalFee == 0:
        //    - ADMIN và MANAGER có thẩm quyền xử lý phiên miễn phí / kiểm thử / quản trị.
        //    - STAFF không được tự tiện miễn phí nếu phiên chưa được xác nhận thanh toán/miễn phí hợp lệ.
        if (session.Status != SessionStatuses.Paid)
        {
            if (session.TotalFee > 0)
            {
                if (request.AllowExitWithoutPayment == true)
                {
                    if (actorRole != Roles.Admin && actorRole != Roles.Manager)
                    {
                        return (false, "Nhân viên cổng (STAFF) không có thẩm quyền tự miễn phí hoặc cho phép xe chưa thanh toán xuất bãi. Yêu cầu phê duyệt từ Quản lý (MANAGER) hoặc Quản trị viên (ADMIN).", null);
                    }

                    if (string.IsNullOrWhiteSpace(request.ExemptionReason))
                    {
                        return (false, "Yêu cầu cung cấp lý do miễn trừ/phê duyệt xuất bãi hợp lệ khi mở cổng cho xe chưa thanh toán.", null);
                    }
                }
                else
                {
                    return (false, $"Phiên gửi xe #{session.Id} chưa được xác nhận thanh toán (Phí: {session.TotalFee:N0} VNĐ, Trạng thái: {session.Status}). Check-out có thu phí đang bị khóa cho tới khi thanh toán hoàn tất hoặc có phê duyệt miễn trừ từ Quản lý.", null);
                }
            }
            else
            {
                // TotalFee == 0
                if (actorRole == Roles.Staff)
                {
                    if (request.AllowExitWithoutPayment == true)
                    {
                        return (false, "Nhân viên cổng (STAFF) không có thẩm quyền tự miễn phí xuất bãi. Yêu cầu phê duyệt từ Quản lý (MANAGER) hoặc Quản trị viên (ADMIN).", null);
                    }
                    else
                    {
                        return (false, $"Phiên gửi xe #{session.Id} chưa hoàn tất tính phí và thanh toán (Trạng thái: ACTIVE). Cần thanh toán hoặc có xác nhận miễn phí từ Quản lý trước khi mở cổng.", null);
                    }
                }
            }
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
                    await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0});", new object[] { lockKey }, cancellationToken: ct);
                }

                // Double-checked locking: Re-check session status within lock to prevent duplicate checkout race
                if (_context is DbContext dbContext)
                {
                    await dbContext.Entry(session).ReloadAsync(ct);
                }
                else
                {
                    var freshStatus = await _context.ParkingSessions
                        .Where(s => s.Id == session.Id)
                        .Select(s => s.Status)
                        .FirstOrDefaultAsync(ct);
                    if (freshStatus != null)
                    {
                        session.Status = freshStatus;
                    }
                }

                if (session.Status == SessionStatuses.Completed || session.Status == SessionStatuses.Cancelled)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Phiên gửi xe này đã kết thúc trước đó với trạng thái: {session.Status}.", null);
                }

                var oldStatus = session.Status;
                // Ticketed admissions cannot exit before slot verification and a valid ticket check.
                if (session.EntryEventId.HasValue)
                {
                    if (session.VerificationStatus != "VERIFIED" || !PlateNumberHelper.IsMatch(request.ConfirmedPlateNumber, session.PlateNumber))
                        return (false, "Cần xác minh xe tại ô và đối chiếu biển số trước khi ra bãi.", null);
                    var exitTicket = await _context.ParkingTickets.FirstOrDefaultAsync(t => t.SessionId == session.Id && t.Status == "ISSUED", ct);
                    if (exitTicket == null || exitTicket.Code != request.TicketCode?.Trim().ToUpperInvariant())
                        return (false, "Vé thiếu, mất, sai hoặc đã thu hồi. Cần xử lý vé trước khi cho xe ra.", null);
                    exitTicket.Status = "REVOKED";
                    exitTicket.ClosedBy = actorId;
                    exitTicket.ClosedAt = DateTime.UtcNow;
                    exitTicket.Reason = "Thu hồi sau khi xác nhận xe thực sự ra bãi.";
                    session.Revision++;
                }
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
                var slotToRelease = session.CurrentSlotId.HasValue
                    ? await _context.ParkingSlots.FirstOrDefaultAsync(s => s.Id == session.CurrentSlotId.Value, ct)
                    : null;
                if (slotToRelease != null && _context is DbContext slotContext)
                    await slotContext.Entry(slotToRelease).ReloadAsync(ct);

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
                        ConfirmationType = "MANUAL_STAFF_VERIFIED",
                        AllowExitWithoutPayment = request.AllowExitWithoutPayment,
                        ExemptionReason = request.ExemptionReason
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

    public async Task<IReadOnlyList<UnmatchedSessionDto>> GetUnmatchedSessionsAsync(
        int lotId,
        long? actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        if (!await _authService.CanOperateSlotVerificationAsync(actorId ?? 0, actorRole, lotId, ct))
        {
            return Array.Empty<UnmatchedSessionDto>();
        }

        var sessions = await _context.ParkingSessions
            .AsNoTracking()
            .Include(s => s.Lot)
            .Where(s => s.LotId == lotId && s.Status == SessionStatuses.Active && s.CurrentSlotId == null)
            .OrderBy(s => s.CheckInTime)
            .Select(s => new UnmatchedSessionDto
            {
                SessionId = s.Id,
                LotId = s.LotId,
                LotName = s.Lot.Name,
                PlateNumber = s.PlateNumber,
                CheckInTime = s.CheckInTime,
                Status = s.Status,
                CheckInImageUrl = s.CheckInImageUrl,
                BookingId = s.BookingId
            })
            .ToListAsync(ct);

        return sessions;
    }

    public async Task<IReadOnlyList<UnmatchedSlotDto>> GetUnmatchedOccupiedSlotsAsync(
        int lotId,
        long? actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        if (!await _authService.CanOperateSlotVerificationAsync(actorId ?? 0, actorRole, lotId, ct))
        {
            return Array.Empty<UnmatchedSlotDto>();
        }

        var assignedSlotIds = await _context.ParkingSessions
            .AsNoTracking()
            .Where(s => s.LotId == lotId && (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid) && s.CurrentSlotId != null)
            .Select(s => s.CurrentSlotId!.Value)
            .ToListAsync(ct);

        var slots = await _context.ParkingSlots
            .AsNoTracking()
            .Include(sl => sl.Floor)
                .ThenInclude(f => f.Lot)
            .Where(sl => sl.Floor.LotId == lotId && sl.PhysicalStatus == SlotPhysicalStatuses.Occupied && !assignedSlotIds.Contains(sl.Id))
            .OrderBy(sl => sl.Floor.FloorNumber)
            .ThenBy(sl => sl.SlotCode)
            .Select(sl => new UnmatchedSlotDto
            {
                SlotId = sl.Id,
                LotId = sl.Floor.LotId,
                LotName = sl.Floor.Lot.Name,
                SlotCode = sl.SlotCode,
                FloorNumber = sl.Floor.FloorNumber,
                PhysicalStatus = sl.PhysicalStatus,
                LastStatusChangedAt = sl.UpdatedAt
            })
            .ToListAsync(ct);

        return slots;
    }

    public async Task<(bool Success, string Message, ParkingSessionDto? Data)> ConfirmBookingParkedAsync(
        long bookingId,
        ConfirmBookingParkedRequestDto request,
        long? actorId,
        string actorRole,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var normalizedPlate = PlateNumberHelper.Normalize(request.PlateNumber);
        if (!PlateNumberHelper.IsValidPlateFormat(normalizedPlate))
            return (false, "Biển số xe không hợp lệ. Độ dài phải từ 3 đến 20 ký tự.", null);
        var imageUrl = request.CheckInImageUrl?.Trim() ?? "";
        var databaseImage = imageUrl.StartsWith("/api/v1/images/content/", StringComparison.Ordinal)
            && Guid.TryParseExact(imageUrl["/api/v1/images/content/".Length..], "N", out _);
        var externalImage = Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri)
            && (imageUri.Scheme == Uri.UriSchemeHttps || imageUri.Scheme == Uri.UriSchemeHttp);
        if (!databaseImage && !externalImage)
            return (false, "Ảnh biển số xe chưa được lưu hợp lệ.", null);
        if (databaseImage && !await _context.StoredImages.AnyAsync(
                image => image.Url == imageUrl && image.Content != null && image.UserId == actorId, ct))
            return (false, "Ảnh biển số chưa được lưu hoặc không thuộc nhân viên xác nhận.", null);

        var bookingLotId = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => (int?)b.LotId)
            .FirstOrDefaultAsync(ct);
        if (!bookingLotId.HasValue)
            return (false, $"Không tìm thấy booking với ID: {bookingId}.", null);

        if (!await _authService.CanOperateSlotVerificationAsync(actorId ?? 0, actorRole, bookingLotId.Value, ct))
            return (false, "Bạn không có quyền xác minh xe và ô đỗ (SLOT_VERIFIER) tại bãi này.", null);

        var lotLock = ParkingLotLockHelper.GetSemaphore(bookingLotId.Value);
        await lotLock.WaitAsync(ct);
        try
        {
            await using var transaction = await _context.BeginTransactionAsync(ct);
            try
            {
                if (_context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
                {
                    long lockKey = ParkingLotLockHelper.LotLockBase + bookingLotId.Value;
                    await _context.Database.ExecuteSqlRawAsync(
                        "SELECT pg_advisory_xact_lock({0});",
                        new object[] { lockKey },
                        cancellationToken: ct);
                }

                var booking = await _context.Bookings
                    .Include(b => b.Lot)
                    .Include(b => b.Policy)
                    .FirstOrDefaultAsync(b => b.Id == bookingId, ct);
                if (booking == null)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Không tìm thấy booking với ID: {bookingId}.", null);
                }
                if (booking.Status != BookingStatuses.Pending)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Booking #{booking.Id} không còn chờ xác minh xe (trạng thái: {booking.Status}).", null);
                }
                if (!PlateNumberHelper.IsMatch(booking.PlateNumber, normalizedPlate))
                {
                    await transaction.RollbackAsync(ct);
                    return (false, "Biển số xe thực tế không khớp với booking. Không thể xác nhận xe.", null);
                }
                normalizedPlate = PlateNumberHelper.Normalize(booking.PlateNumber);
                if (!booking.Lot.IsActive)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Bãi đỗ xe '{booking.Lot.Name}' hiện đang đóng.", null);
                }

                var now = DateTime.UtcNow;
                var graceMinutes = booking.Policy?.LateArrivalMinutes ?? BookingStatuses.NoShowGracePeriodMinutes;
                if (booking.StartTime <= now.AddMinutes(-graceMinutes))
                {
                    booking.Status = BookingStatuses.Expired;
                    booking.UpdatedAt = now;
                    _context.AuditLogs.Add(new AuditLog
                    {
                        Action = "EXPIRE_BOOKING_NO_SHOW",
                        TargetEntity = "bookings",
                        TargetId = booking.Id.ToString(),
                        OldValues = JsonSerializer.Serialize(new { Status = BookingStatuses.Pending }, JsonOptions),
                        NewValues = JsonSerializer.Serialize(new
                        {
                            Status = BookingStatuses.Expired,
                            Reason = $"Không xác nhận xe trong vòng {graceMinutes} phút kể từ giờ bắt đầu.",
                            ExpiredAt = now
                        }, JsonOptions),
                        CreatedAt = now
                    });
                    await _context.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return (false, $"Booking đã hết hạn vì quá {graceMinutes} phút kể từ giờ bắt đầu.", null);
                }

                var activePlateSession = await _context.ParkingSessions
                    .AnyAsync(s => s.PlateNumber == normalizedPlate
                        && (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid), ct);
                if (activePlateSession)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Xe '{normalizedPlate}' đã có phiên gửi đang hoạt động.", null);
                }

                var slot = await _context.ParkingSlots
                    .Include(s => s.Floor)
                    .FirstOrDefaultAsync(s => s.Id == request.SlotId, ct);
                if (slot == null)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Không tìm thấy ô đỗ với ID: {request.SlotId}.", null);
                }
                if (!slot.IsActive || !slot.Floor.IsActive || slot.Floor.LotId != booking.LotId)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, "Ô đỗ không hoạt động hoặc không thuộc bãi của booking.", null);
                }
                if (slot.PhysicalStatus is SlotPhysicalStatuses.Maintenance or SlotPhysicalStatuses.Unavailable or SlotPhysicalStatuses.Reserved)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Ô đỗ '{slot.SlotCode}' đang bảo trì hoặc không khả dụng.", null);
                }

                if (!await _authService.CanOperateAreaAsync(actorId ?? 0, actorRole, booking.LotId,
                    StaffDuties.SlotVerifier, slot.FloorId, slot.Floor.ZoneId, ct))
                {
                    await transaction.RollbackAsync(ct);
                    return (false, "Ô nằm ngoài tầng hoặc khu vực được phân công.", null);
                }

                var slotSession = await _context.ParkingSessions
                    .FirstOrDefaultAsync(s => s.CurrentSlotId == slot.Id
                        && (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid), ct);
                if (slotSession != null)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Ô đỗ '{slot.SlotCode}' đang được xe {slotSession.PlateNumber} sử dụng.", null);
                }
                var assignedDispatch = await _context.ParkingDispatches
                    .AnyAsync(d => d.AssignedSlotId == slot.Id
                        && d.Status == DispatchStatuses.Assigned, ct);
                if (assignedDispatch)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Ô đỗ '{slot.SlotCode}' đã được phân cho xe khác.", null);
                }

                var vehicle = await _context.Vehicles
                    .FirstOrDefaultAsync(v => v.Id == booking.VehicleId, ct);
                if (vehicle == null)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, "Không tìm thấy phương tiện của booking.", null);
                }

                var bookingStatusBefore = booking.Status;
                var physicalStatusBefore = slot.PhysicalStatus;
                var session = new ParkingSession
                {
                    LotId = booking.LotId,
                    VehicleId = vehicle.Id,
                    BookingId = booking.Id,
                    PolicyId = booking.PolicyId,
                    ZoneId = slot.Floor.ZoneId,
                    VerificationStatus = "VERIFIED",
                    VerifiedBy = actorId,
                    VerifiedAt = now,
                    PlateNumber = normalizedPlate,
                    CheckInTime = now,
                    CurrentSlotId = slot.Id,
                    Status = SessionStatuses.Active,
                    CheckInImageUrl = databaseImage ? imageUrl : imageUri!.AbsoluteUri,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                booking.Status = BookingStatuses.CheckedIn;
                booking.CheckedInAt = now;
                booking.UpdatedAt = now;
                slot.PhysicalStatus = SlotPhysicalStatuses.Occupied;
                slot.UpdatedAt = now;
                _context.ParkingSessions.Add(session);
                _context.ParkingDispatches.Add(new ParkingDispatch
                {
                    Session = session,
                    AssignedSlotId = slot.Id,
                    ActualSlotId = slot.Id,
                    Status = DispatchStatuses.Occupied,
                    DispatchMode = DispatchModes.Manual,
                    DispatchedAt = now,
                    ConfirmedAt = now,
                    Notes = "Staff xác nhận booking và xe đã đỗ tại ô thực tế.",
                    CreatedAt = now,
                    UpdatedAt = now
                });

                await LogAuditAsync(
                    action: "CONFIRM_BOOKING_PARKED",
                    targetEntity: "bookings",
                    targetId: booking.Id.ToString(),
                    oldValues: new
                    {
                        Status = bookingStatusBefore,
                        SlotPhysicalStatus = physicalStatusBefore
                    },
                    newValues: new
                    {
                        BookingId = booking.Id,
                        session.Id,
                        session.PlateNumber,
                        SlotId = slot.Id,
                        slot.SlotCode,
                        FloorNumber = slot.Floor.FloorNumber,
                        BookingStatus = booking.Status,
                        PhysicalStatus = slot.PhysicalStatus,
                        session.CheckInImageUrl,
                        ActorRole = actorRole,
                        IpAddress = ipAddress
                    },
                    actorId: actorId,
                    ct: ct);

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                var dto = MapToDto(session, booking.Lot.Name, slot.SlotCode, slot.Floor.FloorNumber);
                return (true, $"Đã xác nhận xe '{session.PlateNumber}' đỗ tại ô '{slot.SlotCode}'.", dto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Lỗi xác nhận booking {BookingId} đã đỗ tại ô {SlotId}", bookingId, request.SlotId);
                throw;
            }
        }
        finally
        {
            lotLock.Release();
        }
    }

    public async Task<(bool Success, string Message, ParkingSessionDto? Data)> VerifySessionSlotAsync(
        long sessionId,
        VerifySessionSlotRequestDto request,
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

        if (session.EntryEventId.HasValue)
            return (false, "Lượt có vé phải dùng API operations để xác minh xe, nhiên liệu và ô thực tế.", null);
        if (session.Status != SessionStatuses.Active)
        {
            return (false, $"Chỉ có thể xác minh ô đỗ cho phiên gửi xe đang hoạt động (ACTIVE). Trạng thái hiện tại: {session.Status}.", null);
        }

        if (!await _authService.CanOperateSlotVerificationAsync(actorId ?? 0, actorRole, session.LotId, ct))
        {
            return (false, "Bạn không có quyền thực hiện nghiệp vụ xác minh ô đỗ (SLOT_VERIFIER) tại bãi đỗ xe này.", null);
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
                    await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0});", new object[] { lockKey }, cancellationToken: ct);
                }

                var slot = await _context.ParkingSlots
                    .Include(sl => sl.Floor)
                    .FirstOrDefaultAsync(sl => sl.Id == request.SlotId, ct);

                if (slot == null)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Không tìm thấy ô đỗ với ID: {request.SlotId}.", null);
                }

                if (!slot.IsActive)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Ô đỗ '{slot.SlotCode}' hiện đang tạm ngừng hoạt động.", null);
                }

                if (slot.Floor.LotId != session.LotId)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Ô đỗ '{slot.SlotCode}' không thuộc cùng bãi đỗ xe với phiên gửi xe này.", null);
                }

                if (slot.PhysicalStatus == SlotPhysicalStatuses.Maintenance || slot.PhysicalStatus == SlotPhysicalStatuses.Unavailable)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Ô đỗ '{slot.SlotCode}' hiện đang bảo trì hoặc không khả dụng ({slot.PhysicalStatus}). Không thể xác minh ô đỗ.", null);
                }

                var conflictingSession = await _context.ParkingSessions
                    .FirstOrDefaultAsync(s => s.Id != session.Id && s.CurrentSlotId == slot.Id && (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid), ct);

                if (conflictingSession != null)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Ô đỗ '{slot.SlotCode}' hiện đang được liên kết với phiên gửi xe #{conflictingSession.Id} (Biển số: {conflictingSession.PlateNumber}). Không thể gán trùng lặp.", null);
                }

                var oldSlotId = session.CurrentSlotId;
                var previousSensorStatus = slot.PhysicalStatus;
                slot.PhysicalStatus = SlotPhysicalStatuses.Occupied;
                slot.UpdatedAt = DateTime.UtcNow;
                session.CurrentSlotId = slot.Id;
                session.UpdatedAt = DateTime.UtcNow;

                var dispatch = await _context.ParkingDispatches
                    .Where(d => d.SessionId == session.Id && (d.Status == DispatchStatuses.Pending || d.Status == DispatchStatuses.Assigned))
                    .OrderByDescending(d => d.CreatedAt)
                    .FirstOrDefaultAsync(ct);

                if (dispatch != null)
                {
                    dispatch.ActualSlotId = slot.Id;
                    dispatch.Status = DispatchStatuses.Occupied;
                    dispatch.ConfirmedAt = DateTime.UtcNow;
                    dispatch.Notes = string.IsNullOrWhiteSpace(request.Notes)
                        ? dispatch.Notes
                        : (string.IsNullOrWhiteSpace(dispatch.Notes) ? request.Notes : $"{dispatch.Notes} | {request.Notes}");
                    dispatch.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    dispatch = new ParkingDispatch
                    {
                        SessionId = session.Id,
                        AssignedSlotId = slot.Id,
                        ActualSlotId = slot.Id,
                        Status = DispatchStatuses.Occupied,
                        DispatchMode = DispatchModes.Manual,
                        DispatchedAt = DateTime.UtcNow,
                        ConfirmedAt = DateTime.UtcNow,
                        Notes = request.Notes ?? "Xác minh khớp phiên và ô đỗ bởi nhân viên kiểm tra ô.",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ParkingDispatches.Add(dispatch);
                }

                await LogAuditAsync(
                    action: "VERIFY_SESSION_SLOT",
                    targetEntity: "parking_sessions",
                    targetId: session.Id.ToString(),
                    oldValues: new
                    {
                        session.Id,
                        PreviousSlotId = oldSlotId,
                        SensorPhysicalStatusBeforeVerification = previousSensorStatus
                    },
                    newValues: new
                    {
                        session.Id,
                        VerifiedSlotId = slot.Id,
                        slot.SlotCode,
                        FloorNumber = slot.Floor?.FloorNumber,
                        SensorStatusBeforeVerification = previousSensorStatus,
                        PhysicalStatusAfterVerification = slot.PhysicalStatus,
                        DiscrepancyWithSensor = previousSensorStatus != SlotPhysicalStatuses.Occupied,
                        ActorRole = actorRole,
                        IpAddress = ipAddress,
                        Notes = request.Notes
                    },
                    actorId: actorId,
                    ct: ct
                );

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                var dto = MapToDto(session, session.Lot?.Name ?? string.Empty, slot.SlotCode, slot.Floor?.FloorNumber);
                return (true, $"Xác nhận liên kết xe '{session.PlateNumber}' vào ô đỗ '{slot.SlotCode}' thành công.", dto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Lỗi giao dịch khi xác minh ô đỗ cho phiên {SessionId}", sessionId);
                throw;
            }
        }
        finally
        {
            lotLock.Release();
        }
    }

    public async Task<(bool Success, string Message, ParkingSessionDto? Data)> ReportSessionWrongSlotAsync(
        long sessionId,
        ReportWrongSlotSessionRequestDto request,
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

        if (session.EntryEventId.HasValue)
            return (false, "Lượt có vé phải dùng API operations để xác minh xe, nhiên liệu và ô thực tế.", null);
        if (session.Status != SessionStatuses.Active)
        {
            return (false, $"Chỉ có thể báo sai ô đỗ cho phiên gửi xe đang hoạt động (ACTIVE). Trạng thái hiện tại: {session.Status}.", null);
        }

        if (!await _authService.CanOperateSlotVerificationAsync(actorId ?? 0, actorRole, session.LotId, ct))
        {
            return (false, "Bạn không có quyền thực hiện nghiệp vụ kiểm tra ô đỗ (SLOT_VERIFIER) tại bãi đỗ xe này.", null);
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
                    await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0});", new object[] { lockKey }, cancellationToken: ct);
                }

                var actualSlot = await _context.ParkingSlots
                    .Include(sl => sl.Floor)
                    .FirstOrDefaultAsync(sl => sl.Id == request.ActualSlotId, ct);

                if (actualSlot == null)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Không tìm thấy ô đỗ thực tế với ID: {request.ActualSlotId}.", null);
                }

                if (!actualSlot.IsActive)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Ô đỗ thực tế '{actualSlot.SlotCode}' hiện đang tạm ngừng hoạt động.", null);
                }

                if (actualSlot.Floor.LotId != session.LotId)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Ô đỗ thực tế '{actualSlot.SlotCode}' không thuộc cùng bãi đỗ xe.", null);
                }

                if (actualSlot.PhysicalStatus == SlotPhysicalStatuses.Maintenance || actualSlot.PhysicalStatus == SlotPhysicalStatuses.Unavailable)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Ô đỗ thực tế '{actualSlot.SlotCode}' hiện đang bảo trì hoặc không khả dụng ({actualSlot.PhysicalStatus}). Không thể chuyển xe vào ô này.", null);
                }

                var conflictingSession = await _context.ParkingSessions
                    .FirstOrDefaultAsync(s => s.Id != session.Id && s.CurrentSlotId == actualSlot.Id && (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid), ct);

                if (conflictingSession != null)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, $"Ô đỗ thực tế '{actualSlot.SlotCode}' đang được liên kết với phiên #{conflictingSession.Id} (Biển số: {conflictingSession.PlateNumber}). Vui lòng xử lý xung đột của phiên này trước.", null);
                }

                var previousActualSlotStatus = actualSlot.PhysicalStatus;
                actualSlot.PhysicalStatus = SlotPhysicalStatuses.Occupied;
                actualSlot.UpdatedAt = DateTime.UtcNow;

                var oldSlotId = session.CurrentSlotId;
                ParkingSlot? oldSlot = null;
                string? conflictNote = null;
                if (oldSlotId.HasValue && oldSlotId.Value != actualSlot.Id)
                {
                    oldSlot = await _context.ParkingSlots.FirstOrDefaultAsync(sl => sl.Id == oldSlotId.Value, ct);
                    if (oldSlot != null)
                    {
                        // Invariant: Không tự ý đặt ô cũ EMPTY khi chưa có xác nhận/telemetry vật lý ô đã trống.
                        // Ô cũ giữ nguyên PhysicalStatus, ghi nhận conflict/audit trail chờ cảm biến hoặc nhân viên xác nhận thực tế.
                        conflictNote = $"Phiên #{session.Id} chuyển sang ô {actualSlot.SlotCode}. Ô cũ {oldSlot.SlotCode} giữ nguyên trạng thái vật lý '{oldSlot.PhysicalStatus}' chờ đối soát thực địa.";
                        oldSlot.UpdatedAt = DateTime.UtcNow;
                    }
                }

                session.CurrentSlotId = actualSlot.Id;
                session.UpdatedAt = DateTime.UtcNow;

                var dispatch = await _context.ParkingDispatches
                    .Where(d => d.SessionId == session.Id && (d.Status == DispatchStatuses.Pending || d.Status == DispatchStatuses.Assigned || d.Status == DispatchStatuses.Occupied))
                    .OrderByDescending(d => d.CreatedAt)
                    .FirstOrDefaultAsync(ct);

                if (dispatch != null)
                {
                    dispatch.ActualSlotId = actualSlot.Id;
                    dispatch.Status = DispatchStatuses.WrongSlot;
                    dispatch.ConfirmedAt = DateTime.UtcNow;
                    dispatch.Notes = string.IsNullOrWhiteSpace(request.Reason)
                        ? $"{dispatch.Notes} | [WRONG_SLOT] Phát hiện đỗ sai ô, điều chỉnh sang {actualSlot.SlotCode}"
                        : $"{dispatch.Notes} | [WRONG_SLOT] {request.Reason} (Chuyển sang {actualSlot.SlotCode})";
                    dispatch.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    dispatch = new ParkingDispatch
                    {
                        SessionId = session.Id,
                        AssignedSlotId = actualSlot.Id,
                        ActualSlotId = actualSlot.Id,
                        Status = DispatchStatuses.WrongSlot,
                        DispatchMode = DispatchModes.Manual,
                        DispatchedAt = DateTime.UtcNow,
                        ConfirmedAt = DateTime.UtcNow,
                        Notes = $"[WRONG_SLOT] Phát hiện đỗ sai ô: {request.Reason ?? "Không có lý do chi tiết"}",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ParkingDispatches.Add(dispatch);
                }

                await LogAuditAsync(
                    action: "REPORT_SESSION_WRONG_SLOT",
                    targetEntity: "parking_sessions",
                    targetId: session.Id.ToString(),
                    oldValues: new
                    {
                        session.Id,
                        PreviousSlotId = oldSlotId,
                        PreviousSlotCode = oldSlot?.SlotCode,
                        PreviousActualSlotPhysicalStatus = previousActualSlotStatus,
                        PreviousSlotPhysicalStatus = oldSlot?.PhysicalStatus
                    },
                    newValues: new
                    {
                        session.Id,
                        ActualSlotId = actualSlot.Id,
                        actualSlot.SlotCode,
                        SensorStatusBeforeVerification = previousActualSlotStatus,
                        PhysicalStatusAfterVerification = actualSlot.PhysicalStatus,
                        DiscrepancyWithSensor = previousActualSlotStatus != SlotPhysicalStatuses.Occupied,
                        Reason = request.Reason,
                        ConflictNote = conflictNote,
                        ActorRole = actorRole,
                        IpAddress = ipAddress
                    },
                    actorId: actorId,
                    ct: ct
                );

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                var dto = MapToDto(session, session.Lot?.Name ?? string.Empty, actualSlot.SlotCode, actualSlot.Floor?.FloorNumber);
                return (true, $"Cập nhật đỗ sai ô cho xe '{session.PlateNumber}' sang ô thực tế '{actualSlot.SlotCode}' thành công.", dto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Lỗi giao dịch khi báo sai ô đỗ cho phiên {SessionId}", sessionId);
                throw;
            }
        }
        finally
        {
            lotLock.Release();
        }
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
            VerificationStatus = session.VerificationStatus,
            ZoneId = session.ZoneId,
            PolicyId = session.PolicyId,
            Revision = session.Revision,
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

    public async Task<(bool Success, string Message, ParkingSessionDto? Data)> RecordPaymentClearanceAsync(
        long sessionId,
        decimal amountPaid,
        string transactionRef,
        string? paymentMethod,
        long? actorId,
        string actorRole,
        string? ipAddress,
        bool isTrustedPaymentServerFlow = false,
        CancellationToken ct = default)
    {
        // 0. Fail-Closed Boundary: Không chấp nhận thanh toán tự khai báo từ client / người dùng / staff / admin.
        // Bắt buộc phải đến từ luồng Server-to-Server đáng tin cậy (Trusted Server Flow) của module Payment do Nam Anh sở hữu.
        if (!isTrustedPaymentServerFlow)
        {
            return (false, "PAYMENT INTEGRATION BLOCKED: Luồng xác nhận thanh toán phía máy chủ (Server-side Trusted Payment Flow) của Nam Anh chưa được tích hợp. Không chấp nhận yêu cầu thanh toán tự khai báo từ client hoặc tài khoản người dùng/nhân viên/quản trị viên.", null);
        }

        // 1. Phân quyền: Chỉ Quản trị viên (ADMIN) hoặc Dịch vụ Thanh toán hệ thống mới có quyền ghi nhận xác nhận tài chính
        if (actorRole != Roles.Admin)
        {
            return (false, "Chỉ Quản trị viên (ADMIN) hoặc Dịch vụ Thanh toán của hệ thống mới có quyền ghi nhận xác nhận tài chính (Financial Clearance). Nhân viên cổng hoặc tài khoản khác không có thẩm quyền.", null);
        }

        // 2. Kiểm tra số tiền thanh toán hợp lệ (> 0)
        if (amountPaid <= 0)
        {
            return (false, "Số tiền thanh toán phải lớn hơn 0 VNĐ.", null);
        }

        if (string.IsNullOrWhiteSpace(transactionRef))
        {
            return (false, "Mã giao dịch thanh toán (transactionRef) không được để trống.", null);
        }

        var trimmedRef = transactionRef.Trim();

        var session = await _context.ParkingSessions
            .Include(s => s.Lot)
            .Include(s => s.CurrentSlot)
                .ThenInclude(slot => slot != null ? slot.Floor : null!)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null)
        {
            return (false, $"Không tìm thấy phiên gửi xe với ID: {sessionId}.", null);
        }

        var lotName = session.Lot?.Name ?? string.Empty;
        var slotCode = session.CurrentSlot?.SlotCode;
        var floorNumber = session.CurrentSlot?.Floor?.FloorNumber;

        // 3. Vòng đời phiên: Phiên đã kết thúc hoặc bị hủy không thể ghi nhận thanh toán
        if (session.Status == SessionStatuses.Completed)
        {
            return (false, $"Phiên gửi xe #{sessionId} đã kết thúc trước đó, không thể ghi nhận thanh toán.", null);
        }

        if (session.Status == SessionStatuses.Cancelled)
        {
            return (false, $"Phiên gửi xe #{sessionId} đã bị hủy, không thể ghi nhận thanh toán.", null);
        }

        // 4. Authoritative TotalFee Invariant: Không cho phép số tiền ghi nhận thấp hơn phí đã được tính của phiên
        if (session.TotalFee > 0 && amountPaid < session.TotalFee)
        {
            return (false, $"Số tiền thanh toán ({amountPaid:N0} VNĐ) không đủ so với phí gửi xe cần thanh toán ({session.TotalFee:N0} VNĐ).", null);
        }

        // 5. Idempotency & Duplicate Reference Invariant:
        // Nếu phiên đã PAID, kiểm tra xem có phải gọi lại cùng mã giao dịch hay không (idempotent retry)
        if (session.Status == SessionStatuses.Paid)
        {
            var existingClearance = await _context.AuditLogs
                .FirstOrDefaultAsync(a => a.Action == "PAYMENT_CLEARED" && a.TargetId == session.Id.ToString() && a.NewValues != null && a.NewValues.Contains(trimmedRef), ct);

            if (existingClearance != null)
            {
                return (true, $"Phiên gửi xe #{session.Id} đã được ghi nhận thanh toán hợp lệ trước đó với mã giao dịch '{trimmedRef}'.", MapToDto(session, lotName, slotCode, floorNumber));
            }

            return (false, $"Phiên gửi xe #{session.Id} đã hoàn tất thanh toán trước đó với một giao dịch khác.", null);
        }

        // Chống sử dụng trùng lặp mã giao dịch trên phiên gửi xe khác
        var isDuplicateRefUsed = await _context.AuditLogs
            .AnyAsync(a => a.Action == "PAYMENT_CLEARED" && a.TargetId != session.Id.ToString() && a.NewValues != null && a.NewValues.Contains(trimmedRef), ct);

        if (isDuplicateRefUsed)
        {
            return (false, $"Mã giao dịch '{trimmedRef}' đã được sử dụng cho một phiên gửi xe khác.", null);
        }

        var oldState = new { session.Status, session.TotalFee, session.PaidAt };

        session.TotalFee = amountPaid;
        session.PaidAt = DateTime.UtcNow;
        session.Status = SessionStatuses.Paid;
        session.UpdatedAt = DateTime.UtcNow;

        var newState = new { session.Status, session.TotalFee, session.PaidAt, TransactionRef = trimmedRef, PaymentMethod = paymentMethod };

        await LogAuditAsync("PAYMENT_CLEARED", nameof(ParkingSession), session.Id.ToString(), oldState, newState, actorId, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Financial clearance recorded for session {SessionId}: Amount {Amount:N0} VNĐ, Ref {Ref}, Method {Method}",
            session.Id, amountPaid, trimmedRef, paymentMethod ?? "N/A");

        return (true, $"Ghi nhận thanh toán hoàn tất cho phiên #{session.Id}.", MapToDto(session, lotName, slotCode, floorNumber));
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

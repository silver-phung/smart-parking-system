using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartParking.Application.Common.Helpers;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Bookings;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Common;

namespace SmartParking.Infrastructure.Services;

public class BookingService : IBookingService
{
    private readonly IApplicationDbContext _context;
    private readonly IParkingAuthorizationService _authService;
    private readonly ILogger<BookingService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public BookingService(
        IApplicationDbContext context,
        IParkingAuthorizationService authService,
        ILogger<BookingService> logger)
    {
        _context = context;
        _authService = authService;
        _logger = logger;
    }

    public async Task<(bool Success, int StatusCode, string Message, BookingDto? Data)> CreateBookingAsync(
        CreateBookingRequestDto request,
        long userId,
        string userRole,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var lot = await _context.ParkingLots.FirstOrDefaultAsync(l => l.Id == request.LotId, ct);
        if (lot == null)
        {
            return (false, 404, $"Không tìm thấy bãi đỗ xe với ID: {request.LotId}.", null);
        }

        if (!lot.IsActive)
        {
            return (false, 400, $"Bãi đỗ xe '{lot.Name}' hiện đang đóng cửa hoặc tạm ngừng hoạt động.", null);
        }

        var startTime = request.StartTime == default ? DateTime.UtcNow : request.StartTime;
        if (request.StartTime != default && request.StartTime < DateTime.UtcNow.AddMinutes(-15))
        {
            return (false, 400, "Thời gian bắt đầu đặt chỗ không được ở quá khứ.", null);
        }

        if (request.EndTime.HasValue && request.EndTime.Value <= startTime)
        {
            return (false, 400, "Thời gian kết thúc đặt chỗ phải sau thời gian bắt đầu.", null);
        }

        // 1. Resolve vehicle and validate ownership
        Vehicle? vehicle = null;
        string normalizedPlate = string.Empty;

        if (request.VehicleId.HasValue)
        {
            vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == request.VehicleId.Value, ct);
            if (vehicle == null)
            {
                return (false, 404, $"Không tìm thấy phương tiện với ID: {request.VehicleId.Value}.", null);
            }

            if (!vehicle.IsActive)
            {
                return (false, 400, "Phương tiện này hiện đang bị vô hiệu hóa.", null);
            }

            if (userRole == Roles.User && vehicle.UserId.HasValue && vehicle.UserId.Value != userId)
            {
                return (false, 403, "Bạn không có quyền đặt chỗ cho phương tiện của người dùng khác.", null);
            }

            normalizedPlate = vehicle.PlateNumber;
        }
        else if (!string.IsNullOrWhiteSpace(request.PlateNumber))
        {
            normalizedPlate = PlateNumberHelper.Normalize(request.PlateNumber);
            if (!PlateNumberHelper.IsValidPlateFormat(normalizedPlate))
            {
                return (false, 400, "Biển số xe không đúng định dạng. Độ dài hợp lệ từ 3 đến 20 ký tự.", null);
            }

            vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.PlateNumber == normalizedPlate, ct);
            if (vehicle != null)
            {
                if (userRole == Roles.User && vehicle.UserId.HasValue && vehicle.UserId.Value != userId)
                {
                    return (false, 403, "Phương tiện này đã được liên kết với một tài khoản người dùng khác.", null);
                }

                if (userRole == Roles.User && !vehicle.UserId.HasValue)
                {
                    vehicle.UserId = userId; // Gán phương tiện cho người dùng đặt chỗ
                }
            }
            else
            {
                vehicle = new Vehicle
                {
                    PlateNumber = normalizedPlate,
                    UserId = userRole == Roles.User ? userId : null,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Vehicles.Add(vehicle);
                await _context.SaveChangesAsync(ct);
            }
        }
        else
        {
            return (false, 400, "Vui lòng chọn phương tiện hoặc nhập biển số xe cần đặt chỗ.", null);
        }

        // 2. Anti-Passback check: Vehicle cannot create booking if it has an open session
        var hasOpenSession = await _context.ParkingSessions
            .AnyAsync(s => s.PlateNumber == normalizedPlate && (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid), ct);

        if (hasOpenSession)
        {
            return (false, 409, $"Phương tiện mang biển số '{normalizedPlate}' hiện đang có phiên đỗ xe chưa kết thúc trong bãi. Không thể tạo đặt chỗ mới.", null);
        }

        // 3. Duplicate booking check: Vehicle cannot have more than one PENDING booking
        var hasActiveBooking = await _context.Bookings
            .AnyAsync(b => b.VehicleId == vehicle.Id && b.Status == BookingStatuses.Pending, ct);

        if (hasActiveBooking)
        {
            return (false, 409, $"Phương tiện mang biển số '{normalizedPlate}' đã có một lượt đặt chỗ đang có hiệu lực (PENDING).", null);
        }

        // 4. Concurrency lock per lot to prevent race conditions & overbooking
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

                // Re-calculate authoritative capacity inside the lock with conservative IoT guard and strict slot fallback
                var calc = await ParkingLotCapacityHelper.CalculateAsync(_context, lot.Id, lot, ct);

                if (calc.AvailableCapacity <= 0)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, 409, $"Bãi đỗ xe '{lot.Name}' đã hết chỗ khả dụng để đặt trước (Sức chứa: {calc.TotalCapacity}, Đang sử dụng hiệu dụng: {calc.EffectiveOccupied}, Đang giữ chỗ: {calc.PendingBookingsCount}).", null);
                }

                var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
                var username = user?.Username ?? "user";

                var booking = new Booking
                {
                    LotId = lot.Id,
                    UserId = userId,
                    VehicleId = vehicle.Id,
                    PlateNumber = normalizedPlate,
                    StartTime = startTime,
                    EndTime = request.EndTime,
                    Status = BookingStatuses.Pending,
                    Notes = request.Notes?.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync(ct);

                var audit = new AuditLog
                {
                    Action = "CREATE_BOOKING",
                    TargetEntity = "bookings",
                    TargetId = booking.Id.ToString(),
                    NewValues = JsonSerializer.Serialize(new
                    {
                        booking.Id,
                        booking.LotId,
                        booking.UserId,
                        booking.VehicleId,
                        booking.PlateNumber,
                        booking.StartTime,
                        booking.EndTime,
                        booking.Status,
                        IpAddress = ipAddress
                    }, JsonOptions),
                    ActorId = userId,
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.UtcNow
                };
                _context.AuditLogs.Add(audit);
                await _context.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);

                _logger.LogInformation("User {UserId} reserved capacity at lot {LotId} for vehicle {PlateNumber}. Booking ID: {BookingId}",
                    userId, lot.Id, normalizedPlate, booking.Id);

                var dto = MapToDto(booking, lot.Name, lot.Address, username);
                return (true, 201, "Đặt chỗ giữ sức chứa thành công.", dto);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(ct);
                if (IsPostgresUniqueViolation(ex, "uq_bookings_pending_vehicle"))
                {
                    return (false, 409, $"Phương tiện mang biển số '{normalizedPlate}' đã có một lượt đặt chỗ đang có hiệu lực (PENDING).", null);
                }
                _logger.LogError(ex, "Lỗi cơ sở dữ liệu khi đặt chỗ cho phương tiện {PlateNumber}", normalizedPlate);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Lỗi khi đặt chỗ cho phương tiện {PlateNumber}", normalizedPlate);
                throw;
            }
        }
        finally
        {
            lotLock.Release();
        }
    }

    public async Task<PagedResult<BookingDto>> GetMyBookingsAsync(
        long userId,
        BookingFilterDto filter,
        CancellationToken ct = default)
    {
        var query = _context.Bookings
            .Include(b => b.Lot)
            .Include(b => b.User)
            .Include(b => b.Vehicle)
            .Include(b => b.Session)
            .AsNoTracking()
            .Where(b => b.UserId == userId);

        if (filter.LotId.HasValue)
        {
            query = query.Where(b => b.LotId == filter.LotId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim().ToUpperInvariant();
            query = query.Where(b => b.Status == status);
        }

        var totalCount = await query.CountAsync(ct);

        var page = filter.PageNumber < 1 ? 1 : filter.PageNumber;
        var pageSize = filter.PageSize < 1 ? 10 : (filter.PageSize > 100 ? 100 : filter.PageSize);

        var bookings = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = bookings.Select(b => MapToDto(b, b.Lot.Name, b.Lot.Address, b.User.Username)).ToList();
        return PagedResult<BookingDto>.Create(dtos, totalCount, page, pageSize);
    }

    public async Task<(bool Success, int StatusCode, string Message, BookingDetailDto? Data)> GetBookingByIdAsync(
        long bookingId,
        long currentUserId,
        string currentUserRole,
        CancellationToken ct = default)
    {
        var booking = await _context.Bookings
            .Include(b => b.Lot)
            .Include(b => b.User)
            .Include(b => b.Vehicle)
            .Include(b => b.Session)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking == null)
        {
            return (false, 404, $"Không tìm thấy lượt đặt chỗ với ID: {bookingId}.", null);
        }

        // Authorization check
        if (currentUserRole == Roles.User)
        {
            if (booking.UserId != currentUserId)
            {
                return (false, 403, "Bạn không có quyền xem thông tin lượt đặt chỗ của người khác.", null);
            }
        }
        else if (currentUserRole == Roles.Manager || currentUserRole == Roles.Staff)
        {
            var hasAccess = await _authService.HasLotAccessAsync(currentUserId, currentUserRole, booking.LotId, ct);
            if (!hasAccess)
            {
                return (false, 403, "Bạn không được phân công quản lý bãi đỗ xe của lượt đặt chỗ này.", null);
            }
        }

        var detail = MapToDetailDto(booking);
        return (true, 200, "Lấy thông tin lượt đặt chỗ thành công.", detail);
    }

    public async Task<(bool Success, int StatusCode, string Message, BookingDto? Data)> CancelBookingAsync(
        long bookingId,
        CancelBookingRequestDto request,
        long currentUserId,
        string currentUserRole,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var booking = await _context.Bookings
            .Include(b => b.Lot)
            .Include(b => b.User)
            .Include(b => b.Vehicle)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking == null)
        {
            return (false, 404, $"Không tìm thấy lượt đặt chỗ với ID: {bookingId}.", null);
        }

        // Authorization check
        if (currentUserRole == Roles.User)
        {
            if (booking.UserId != currentUserId)
            {
                return (false, 403, "Bạn không có quyền hủy lượt đặt chỗ của người dùng khác.", null);
            }
        }
        else if (currentUserRole == Roles.Manager || currentUserRole == Roles.Staff)
        {
            var hasAccess = await _authService.HasLotAccessAsync(currentUserId, currentUserRole, booking.LotId, ct);
            if (!hasAccess)
            {
                return (false, 403, "Bạn không được phân công quản lý bãi đỗ xe của lượt đặt chỗ này.", null);
            }
        }

        var lotLock = ParkingLotLockHelper.GetSemaphore(booking.LotId);
        await lotLock.WaitAsync(ct);
        try
        {
            await using var transaction = await _context.BeginTransactionAsync(ct);
            try
            {
                if (_context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
                {
                    long lockKey = ParkingLotLockHelper.LotLockBase + booking.LotId;
                    await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0});", lockKey, ct);
                }

                // Re-fetch booking inside transaction to guard against race with CheckIn
                var lockedBooking = await _context.Bookings
                    .Include(b => b.Lot)
                    .Include(b => b.User)
                    .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

                if (lockedBooking == null)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, 404, $"Không tìm thấy lượt đặt chỗ với ID: {bookingId}.", null);
                }

                // State machine validation
                if (lockedBooking.Status != BookingStatuses.Pending)
                {
                    await transaction.RollbackAsync(ct);
                    if (lockedBooking.Status == BookingStatuses.CheckedIn)
                    {
                        return (false, 400, "Lượt đặt chỗ này đã được sử dụng (phương tiện đã check-in vào bãi), không thể hủy.", null);
                    }
                    if (lockedBooking.Status == BookingStatuses.Cancelled)
                    {
                        return (false, 400, "Lượt đặt chỗ này đã bị hủy trước đó.", null);
                    }
                    if (lockedBooking.Status == BookingStatuses.Expired)
                    {
                        return (false, 400, "Lượt đặt chỗ này đã hết hạn, không thể hủy.", null);
                    }
                    return (false, 400, $"Không thể hủy đặt chỗ ở trạng thái '{lockedBooking.Status}'.", null);
                }

                var reason = string.IsNullOrWhiteSpace(request.Reason)
                    ? (currentUserRole == Roles.User ? "Người dùng yêu cầu hủy đặt chỗ." : "Nhân sự quản lý hủy đặt chỗ theo yêu cầu vận hành.")
                    : request.Reason.Trim();

                lockedBooking.Status = BookingStatuses.Cancelled;
                lockedBooking.CancelledAt = DateTime.UtcNow;
                lockedBooking.CancellationReason = reason;
                lockedBooking.UpdatedAt = DateTime.UtcNow;

                var audit = new AuditLog
                {
                    Action = "CANCEL_BOOKING",
                    TargetEntity = "bookings",
                    TargetId = lockedBooking.Id.ToString(),
                    OldValues = JsonSerializer.Serialize(new { Status = BookingStatuses.Pending }, JsonOptions),
                    NewValues = JsonSerializer.Serialize(new
                    {
                        Status = BookingStatuses.Cancelled,
                        CancellationReason = reason,
                        CancelledByUserId = currentUserId,
                        CancelledByRole = currentUserRole,
                        IpAddress = ipAddress
                    }, JsonOptions),
                    ActorId = currentUserId,
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.UtcNow
                };
                _context.AuditLogs.Add(audit);

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation("Booking {BookingId} for vehicle {PlateNumber} cancelled by user {UserId} ({UserRole})",
                    lockedBooking.Id, lockedBooking.PlateNumber, currentUserId, currentUserRole);

                var dto = MapToDto(lockedBooking, lockedBooking.Lot.Name, lockedBooking.Lot.Address, lockedBooking.User.Username);
                return (true, 200, "Hủy lượt đặt chỗ thành công. Sức chứa giữ chỗ đã được giải phóng.", dto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Lỗi khi hủy đặt chỗ ID {BookingId}", bookingId);
                throw;
            }
        }
        finally
        {
            lotLock.Release();
        }
    }

    public async Task<PagedResult<BookingDto>> GetBookingsAsync(
        BookingFilterDto filter,
        long currentUserId,
        string currentUserRole,
        CancellationToken ct = default)
    {
        var query = _context.Bookings
            .Include(b => b.Lot)
            .Include(b => b.User)
            .Include(b => b.Vehicle)
            .Include(b => b.Session)
            .AsNoTracking()
            .AsQueryable();

        // RBAC lot scope enforcement
        if (currentUserRole == Roles.Manager || currentUserRole == Roles.Staff)
        {
            var assignedLots = await _authService.GetAssignedLotIdsAsync(currentUserId, currentUserRole, ct);
            if (assignedLots.Count == 0)
            {
                return PagedResult<BookingDto>.Create([], 0, filter.PageNumber, filter.PageSize);
            }

            if (filter.LotId.HasValue)
            {
                if (!assignedLots.Contains(filter.LotId.Value))
                {
                    return PagedResult<BookingDto>.Create([], 0, filter.PageNumber, filter.PageSize);
                }
                query = query.Where(b => b.LotId == filter.LotId.Value);
            }
            else
            {
                query = query.Where(b => assignedLots.Contains(b.LotId));
            }
        }
        else if (filter.LotId.HasValue)
        {
            query = query.Where(b => b.LotId == filter.LotId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim().ToUpperInvariant();
            query = query.Where(b => b.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(b => b.PlateNumber.ToLower().Contains(search) || b.User.Username.ToLower().Contains(search));
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(b => b.StartTime >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(b => b.StartTime <= filter.ToDate.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var page = filter.PageNumber < 1 ? 1 : filter.PageNumber;
        var pageSize = filter.PageSize < 1 ? 10 : (filter.PageSize > 100 ? 100 : filter.PageSize);

        var bookings = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = bookings.Select(b => MapToDto(b, b.Lot.Name, b.Lot.Address, b.User.Username)).ToList();
        return PagedResult<BookingDto>.Create(dtos, totalCount, page, pageSize);
    }

    public async Task<LotCapacityDto?> GetLotCapacityAsync(
        int lotId,
        long? currentUserId = null,
        string? currentUserRole = null,
        CancellationToken ct = default)
    {
        var lot = await _context.ParkingLots.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lotId, ct);
        if (lot == null)
        {
            return null;
        }

        var includeDiagnostics = false;
        if (currentUserId.HasValue
            && (currentUserRole == Roles.Admin || currentUserRole == Roles.Manager || currentUserRole == Roles.Staff))
        {
            includeDiagnostics = await _authService.HasLotAccessAsync(currentUserId.Value, currentUserRole, lotId, ct);
        }

        var calc = await ParkingLotCapacityHelper.CalculateAsync(_context, lotId, lot, ct);

        return new LotCapacityDto
        {
            LotId = lot.Id,
            LotName = lot.Name,
            LotAddress = lot.Address,
            TotalCapacity = calc.TotalCapacity,
            AvailableForWalkIn = calc.AvailableCapacity,
            AvailableForBooking = calc.AvailableCapacity,
            IsFull = calc.AvailableCapacity <= 0,
            ActiveSessionsCount = includeDiagnostics ? calc.ActiveSessionsCount : null,
            ActiveReservationsCount = includeDiagnostics ? calc.PendingBookingsCount : null,
            TotalOccupiedOrReserved = includeDiagnostics ? calc.TotalOccupiedOrReserved : null,
            PhysicalOccupiedSlotsCount = includeDiagnostics ? calc.PhysicalOccupiedSlotsCount : null,
            PhysicalEmptySlotsCount = includeDiagnostics ? calc.PhysicalEmptySlotsCount : null,
            DiscrepancyNotes = includeDiagnostics ? calc.DiscrepancyNotes : null
        };
    }

    private static bool IsPostgresUniqueViolation(DbUpdateException ex, string constraintName)
    {
        if (ex.InnerException is PostgresException pgEx)
        {
            return pgEx.SqlState == PostgresErrorCodes.UniqueViolation &&
                   (string.IsNullOrEmpty(constraintName) || pgEx.ConstraintName?.Equals(constraintName, StringComparison.OrdinalIgnoreCase) == true);
        }

        var innerMsg = ex.InnerException?.Message ?? ex.Message;
        if (!innerMsg.Contains("23505"))
        {
            return false;
        }

        return string.IsNullOrEmpty(constraintName) || innerMsg.Contains(constraintName, StringComparison.OrdinalIgnoreCase);
    }

    private static BookingDto MapToDto(Booking b, string lotName, string? lotAddress, string username)
    {
        return new BookingDto
        {
            Id = b.Id,
            LotId = b.LotId,
            LotName = lotName,
            LotAddress = lotAddress,
            UserId = b.UserId,
            Username = username,
            VehicleId = b.VehicleId,
            PlateNumber = b.PlateNumber,
            StartTime = b.StartTime,
            EndTime = b.EndTime,
            Status = b.Status,
            SessionId = b.Session?.Id,
            CheckedInAt = b.CheckedInAt,
            CancelledAt = b.CancelledAt,
            CancellationReason = b.CancellationReason,
            Notes = b.Notes,
            CreatedAt = b.CreatedAt,
            UpdatedAt = b.UpdatedAt
        };
    }

    private static BookingDetailDto MapToDetailDto(Booking b)
    {
        return new BookingDetailDto
        {
            Id = b.Id,
            LotId = b.LotId,
            LotName = b.Lot?.Name ?? string.Empty,
            LotAddress = b.Lot?.Address,
            UserId = b.UserId,
            Username = b.User?.Username ?? string.Empty,
            VehicleId = b.VehicleId,
            PlateNumber = b.PlateNumber,
            StartTime = b.StartTime,
            EndTime = b.EndTime,
            Status = b.Status,
            SessionId = b.Session?.Id,
            CheckedInAt = b.CheckedInAt,
            CancelledAt = b.CancelledAt,
            CancellationReason = b.CancellationReason,
            Notes = b.Notes,
            CreatedAt = b.CreatedAt,
            UpdatedAt = b.UpdatedAt,
            VehicleBrand = b.Vehicle?.Brand,
            VehicleModel = b.Vehicle?.Model,
            VehicleColor = b.Vehicle?.Color
        };
    }
}

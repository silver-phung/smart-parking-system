using Microsoft.EntityFrameworkCore;
using ParkingOperations.Application.CashHandovers;
using ParkingOperations.Domain.Entities;
using ParkingOperations.Persistence.Data;

namespace ParkingOperations.Infrastructure;

public sealed class ParkingShiftService(OperationsDbContext db, ICoreStaffAccessClient coreAccess) : IParkingShiftService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<(IReadOnlyList<ParkingShiftDto> Items, int TotalCount)> ListAsync(
        int lotId, DateOnly? businessDate, int page, int pageSize, long actorId, string role, string token, CancellationToken ct)
    {
        if (role is not ("MANAGER" or "STAFF") ||
            !await coreAccess.IsCallerAssignedToLotAsync(token, lotId, ct))
            throw new UnauthorizedAccessException("Bạn không có quyền xem ca tại bãi này.");

        var query = db.ParkingShifts.AsNoTracking().Include(x => x.Assignments).Where(x => x.LotId == lotId);
        if (businessDate.HasValue)
            query = query.Where(x => x.BusinessDate == businessDate.Value);
        if (role == "STAFF")
            query = query.Where(x => x.Assignments.Any(a => a.StaffId == actorId));

        var total = await query.CountAsync(ct);
        var shifts = await query.OrderByDescending(x => x.BusinessDate).ThenBy(x => x.StartsAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (shifts.Select(ToDto).ToList(), total);
    }

    public async Task<ShiftOperationResult> CreateAsync(CreateShiftRequest request, long managerId, string token, CancellationToken ct)
    {
        if (!await coreAccess.IsCallerAssignedToLotAsync(token, request.LotId, ct))
            return Fail(403, "Bạn không được phân công quản lý bãi này.");

        var (start, end) = GetShiftWindow(request.BusinessDate, request.ShiftCode);
        var shift = new ParkingShift
        {
            Id = Guid.NewGuid(),
            LotId = request.LotId,
            BusinessDate = request.BusinessDate,
            Code = request.ShiftCode,
            StartsAtUtc = start,
            EndsAtUtc = end,
            Status = ParkingShiftStatuses.Planned,
            CreatedByManagerId = managerId,
            CreatedAtUtc = DateTime.UtcNow,
            Version = 1
        };

        db.ParkingShifts.Add(shift);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Fail(409, "Ca này đã tồn tại cho bãi và ngày nghiệp vụ đã chọn.");
        }
        return Ok(ToDto(shift), "Đã tạo ca làm việc.");
    }

    public async Task<ShiftOperationResult> AssignAsync(Guid shiftId, AssignStaffToShiftRequest request, long managerId, string token, CancellationToken ct)
    {
        var shift = await db.ParkingShifts.Include(x => x.Assignments).FirstOrDefaultAsync(x => x.Id == shiftId, ct);
        if (shift is null)
            return Fail(404, "Không tìm thấy ca làm việc.");
        if (shift.Status != ParkingShiftStatuses.Planned)
            return Fail(409, "Chỉ được thay đổi phân công khi ca đang được lên lịch.");
        if (!ParkingShiftDuties.IsValid(request.DutyCode))
            return Fail(400, "dutyCode phải là GATE, SLOT_VERIFIER hoặc BOTH.");
        if (!await coreAccess.IsCallerAssignedToLotAsync(token, shift.LotId, ct))
            return Fail(403, "Bạn không được phân công quản lý bãi này.");
        if (!await coreAccess.IsActiveStaffAtLotAsync(token, shift.LotId, request.StaffId, ct))
            return Fail(400, "Nhân viên phải có phân công STAFF đang hiệu lực tại bãi này trong Core.");
        if (shift.Assignments.Any(x => x.StaffId == request.StaffId))
            return Fail(409, "Nhân viên đã được xếp vào ca này.");

        shift.Assignments.Add(new ShiftAssignment
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StaffId = request.StaffId,
            DutyCode = request.DutyCode,
            AssignedByManagerId = managerId,
            AssignedAtUtc = DateTime.UtcNow
        });
        shift.Version++;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail(409, "Ca vừa được cập nhật bởi yêu cầu khác. Hãy tải lại dữ liệu.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Fail(409, "Nhân viên vừa được xếp vào ca này bởi yêu cầu khác.");
        }
        return Ok(ToDto(shift), "Đã phân công nhân viên vào ca.");
    }

    public async Task<ShiftOperationResult> StartAsync(Guid shiftId, long managerId, string token, CancellationToken ct)
    {
        var shift = await db.ParkingShifts.Include(x => x.Assignments).FirstOrDefaultAsync(x => x.Id == shiftId, ct);
        if (shift is null) return Fail(404, "Không tìm thấy ca làm việc.");
        if (!await coreAccess.IsCallerAssignedToLotAsync(token, shift.LotId, ct))
            return Fail(403, "Bạn không được phân công quản lý bãi này.");
        if (shift.Status != ParkingShiftStatuses.Planned)
            return Fail(409, "Chỉ ca đang được lên lịch mới có thể bắt đầu.");
        if (shift.Assignments.Count == 0)
            return Fail(409, "Hãy phân công ít nhất một nhân viên trước khi bắt đầu ca.");

        shift.Status = ParkingShiftStatuses.InProgress;
        shift.StartedAtUtc = DateTime.UtcNow;
        shift.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Fail(409, "Ca vừa được cập nhật bởi yêu cầu khác. Hãy tải lại dữ liệu."); }
        return Ok(ToDto(shift), "Ca đã bắt đầu.");
    }

    public async Task<ShiftOperationResult> CompleteAsync(Guid shiftId, long managerId, string token, CancellationToken ct)
    {
        var shift = await db.ParkingShifts.Include(x => x.Assignments).FirstOrDefaultAsync(x => x.Id == shiftId, ct);
        if (shift is null) return Fail(404, "Không tìm thấy ca làm việc.");
        if (!await coreAccess.IsCallerAssignedToLotAsync(token, shift.LotId, ct))
            return Fail(403, "Bạn không được phân công quản lý bãi này.");
        if (shift.Status != ParkingShiftStatuses.InProgress)
            return Fail(409, "Chỉ ca đang diễn ra mới có thể chốt.");

        shift.Status = ParkingShiftStatuses.Completed;
        shift.CompletedAtUtc = DateTime.UtcNow;
        shift.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Fail(409, "Ca vừa được cập nhật bởi yêu cầu khác. Hãy tải lại dữ liệu."); }
        return Ok(ToDto(shift), "Ca đã được chốt.");
    }

    private static (DateTime StartUtc, DateTime EndUtc) GetShiftWindow(DateOnly businessDate, string code)
    {
        var (startHour, endHour, endDayOffset) = code switch
        {
            ParkingShiftCodes.Morning => (5, 13, 0),
            ParkingShiftCodes.Afternoon => (13, 21, 0),
            ParkingShiftCodes.Night => (21, 5, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(code))
        };
        var localStart = new DateTimeOffset(businessDate.ToDateTime(new TimeOnly(startHour, 0)), VietnamOffset);
        var localEndDate = businessDate.AddDays(endDayOffset);
        var localEnd = new DateTimeOffset(localEndDate.ToDateTime(new TimeOnly(endHour, 0)), VietnamOffset);
        return (localStart.UtcDateTime, localEnd.UtcDateTime);
    }

    private static ParkingShiftDto ToDto(ParkingShift shift) => new(
        shift.Id, shift.LotId, shift.BusinessDate, shift.Code, shift.StartsAtUtc, shift.EndsAtUtc,
        shift.Status, shift.CreatedByManagerId, shift.CreatedAtUtc, shift.StartedAtUtc,
        shift.CompletedAtUtc, shift.Version,
        shift.Assignments.OrderBy(x => x.StaffId)
            .Select(x => new ShiftAssignmentDto(x.Id, x.StaffId, x.DutyCode, x.AssignedByManagerId, x.AssignedAtUtc)).ToList());

    private static ShiftOperationResult Ok(ParkingShiftDto item, string message) => new(true, 200, message, item);
    private static ShiftOperationResult Fail(int status, string message) => new(false, status, message);
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation };
}

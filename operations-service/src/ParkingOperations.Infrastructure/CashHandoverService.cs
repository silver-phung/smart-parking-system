using Microsoft.EntityFrameworkCore;
using ParkingOperations.Application.CashHandovers;
using ParkingOperations.Domain.Entities;
using ParkingOperations.Persistence.Data;

namespace ParkingOperations.Infrastructure;

public sealed class CashHandoverService(OperationsDbContext db, ICoreStaffAccessClient coreAccess) : ICashHandoverService
{
    public async Task<CashHandoverOperationResult> CreateAsync(CreateCashHandoverRequest request, long managerId, string token, CancellationToken ct)
    {
        if (!await coreAccess.IsCallerAssignedToLotAsync(token, request.LotId, ct))
            return Fail(403, "Bạn không được phân công quản lý bãi này.");

        if (!await coreAccess.AreActiveStaffAtLotAsync(token, request.LotId, request.OutgoingStaffId, request.IncomingStaffId, ct))
            return Fail(400, "Nhân viên giao và nhận phải đang được phân công tại bãi này.");

        var handover = new CashHandover
        {
            Id = Guid.NewGuid(),
            LotId = request.LotId,
            BusinessDate = request.BusinessDate,
            FromShiftCode = request.FromShiftCode,
            ToShiftCode = request.ToShiftCode,
            OutgoingStaffId = request.OutgoingStaffId,
            IncomingStaffId = request.IncomingStaffId,
            CreatedByManagerId = managerId,
            CreatedAtUtc = DateTime.UtcNow,
            Status = CashHandoverStatuses.AwaitingConfirmations,
            Version = 1
        };

        db.CashHandovers.Add(handover);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Fail(409, "Đã có handover cho ca giao này trong bãi và ngày nghiệp vụ đã chọn.");
        }

        return Ok(handover, "Đã tạo handover. Chờ nhân viên giao và nhận tự xác nhận số tiền.");
    }

    public async Task<(IReadOnlyList<CashHandoverDto> Items, int TotalCount)> ListAsync(int lotId, DateOnly? businessDate, int page, int pageSize, string token, long actorId, string role, CancellationToken ct)
    {
        if (role is not ("MANAGER" or "STAFF") || !await coreAccess.IsCallerAssignedToLotAsync(token, lotId, ct))
            throw new UnauthorizedAccessException("Bạn không có quyền xem handover của bãi này.");

        var query = db.CashHandovers.AsNoTracking().Where(x => x.LotId == lotId);
        if (role == "STAFF")
            query = query.Where(x => x.OutgoingStaffId == actorId || x.IncomingStaffId == actorId);
        if (businessDate.HasValue)
            query = query.Where(x => x.BusinessDate == businessDate.Value);

        var total = await query.CountAsync(ct);
        var entities = await query.OrderByDescending(x => x.BusinessDate).ThenByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        var items = entities.Select(CashHandoverDto.From).ToList();
        return (items, total);
    }

    public async Task<CashHandoverOperationResult> ConfirmAsync(Guid id, decimal countedAmount, long actorId, string role, string token, CancellationToken ct)
    {
        var handover = await db.CashHandovers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (handover is null)
            return Fail(404, "Không tìm thấy handover.");
        if (role is not ("STAFF" or "MANAGER"))
            return Fail(403, "Chỉ nhân viên giao/nhận mới được xác nhận handover.");
        if (!await coreAccess.IsCallerAssignedToLotAsync(token, handover.LotId, ct))
            return Fail(403, "Bạn không được phân công tại bãi này.");

        var isOutgoing = actorId == handover.OutgoingStaffId;
        var isIncoming = actorId == handover.IncomingStaffId;
        if (!isOutgoing && !isIncoming)
            return Fail(403, "Bạn không phải nhân viên giao hoặc nhận handover này.");

        if (isOutgoing && handover.OutgoingCountedAmount.HasValue)
            return handover.OutgoingCountedAmount.Value == countedAmount
                ? Ok(handover, "Xác nhận giao ca đã được ghi nhận.")
                : Fail(409, "Bạn đã xác nhận số tiền giao ca với giá trị khác.");
        if (isIncoming && handover.IncomingCountedAmount.HasValue)
            return handover.IncomingCountedAmount.Value == countedAmount
                ? Ok(handover, "Xác nhận nhận ca đã được ghi nhận.")
                : Fail(409, "Bạn đã xác nhận số tiền nhận ca với giá trị khác.");
        if (handover.Status != CashHandoverStatuses.AwaitingConfirmations)
            return Fail(409, "Handover không còn ở trạng thái chờ xác nhận.");

        var now = DateTime.UtcNow;
        if (isOutgoing)
        {
            handover.OutgoingCountedAmount = countedAmount;
            handover.OutgoingConfirmedBy = actorId;
            handover.OutgoingConfirmedAtUtc = now;
        }
        else
        {
            handover.IncomingCountedAmount = countedAmount;
            handover.IncomingConfirmedBy = actorId;
            handover.IncomingConfirmedAtUtc = now;
        }

        if (handover.OutgoingCountedAmount.HasValue && handover.IncomingCountedAmount.HasValue)
        {
            if (handover.OutgoingCountedAmount.Value == handover.IncomingCountedAmount.Value)
            {
                handover.Status = CashHandoverStatuses.Completed;
            }
            else
            {
                handover.Status = CashHandoverStatuses.Disputed;
                handover.DisputedBy = actorId;
                handover.DisputedAtUtc = now;
                handover.DisputeReason = "Số tiền hai bên kiểm đếm không khớp.";
            }
        }

        handover.Version++;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail(409, "Handover vừa được cập nhật bởi yêu cầu khác. Hãy tải lại trạng thái mới nhất.");
        }

        return Ok(handover, handover.Status == CashHandoverStatuses.Disputed
            ? "Đã ghi nhận hai lượt kiểm đếm; số tiền đang lệch và cần Manager xử lý."
            : handover.Status == CashHandoverStatuses.Completed
                ? "Hai bên đã xác nhận cùng số tiền. Handover hoàn tất."
                : "Đã ghi nhận xác nhận. Đang chờ bên còn lại.");
    }

    public async Task<CashHandoverOperationResult> DisputeAsync(Guid id, string reason, long actorId, string role, string token, CancellationToken ct)
    {
        var handover = await db.CashHandovers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (handover is null)
            return Fail(404, "Không tìm thấy handover.");
        if (role is not ("STAFF" or "MANAGER"))
            return Fail(403, "Không có quyền báo từ chối handover.");
        if (!await coreAccess.IsCallerAssignedToLotAsync(token, handover.LotId, ct))
            return Fail(403, "Bạn không được phân công tại bãi này.");
        if (actorId != handover.OutgoingStaffId && actorId != handover.IncomingStaffId)
            return Fail(403, "Chỉ người giao hoặc người nhận mới được báo từ chối.");
        if (handover.Status != CashHandoverStatuses.AwaitingConfirmations)
            return Fail(409, "Handover không còn ở trạng thái chờ xác nhận.");

        handover.Status = CashHandoverStatuses.Rejected;
        handover.DisputeReason = reason.Trim();
        handover.DisputedBy = actorId;
        handover.DisputedAtUtc = DateTime.UtcNow;
        handover.Version++;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail(409, "Handover vừa được cập nhật bởi yêu cầu khác. Hãy tải lại trạng thái mới nhất.");
        }
        return Ok(handover, "Đã ghi nhận handover bị từ chối và lý do.");
    }

    private static CashHandoverOperationResult Ok(CashHandover item, string message) =>
        new(true, 200, message, CashHandoverDto.From(item));

    private static CashHandoverOperationResult Fail(int status, string message) => new(false, status, message);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation };
}

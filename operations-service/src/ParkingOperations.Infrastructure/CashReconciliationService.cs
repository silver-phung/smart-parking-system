using Microsoft.EntityFrameworkCore;
using ParkingOperations.Application.CashHandovers;
using ParkingOperations.Domain.Entities;
using ParkingOperations.Persistence.Data;

namespace ParkingOperations.Infrastructure;

public sealed class CashReconciliationService(OperationsDbContext db, ICoreStaffAccessClient coreAccess) : ICashReconciliationService
{
    public async Task<(IReadOnlyList<CashReconciliationDto> Items, int TotalCount)> ListAsync(int lotId, string token, CancellationToken ct)
    {
        if (!await coreAccess.IsCallerAssignedToLotAsync(token, lotId, ct)) throw new UnauthorizedAccessException();
        var query = db.CashReconciliations.AsNoTracking().Include(x => x.Shift).Where(x => x.Shift.LotId == lotId);
        var total = await query.CountAsync(ct);
        var entities = await query.OrderByDescending(x => x.Shift.BusinessDate).ThenByDescending(x => x.SubmittedAtUtc)
            .Take(100).ToListAsync(ct);
        return (entities.Select(CashReconciliationDto.From).ToList(), total);
    }

    public async Task<CashReconciliationOperationResult> SubmitAsync(SubmitCashReconciliationRequest request, long managerId, string token, CancellationToken ct)
    {
        var shift = await db.ParkingShifts.FirstOrDefaultAsync(x => x.Id == request.ShiftId, ct);
        if (shift is null) return Fail(404, "Không tìm thấy ca.");
        if (shift.Status != ParkingShiftStatuses.Completed) return Fail(409, "Chỉ đối soát ca đã hoàn tất.");
        if (!await coreAccess.IsCallerAssignedToLotAsync(token, shift.LotId, ct)) return Fail(403, "Bạn không được phân công quản lý bãi này.");
        if (await db.CashReconciliations.AnyAsync(x => x.ShiftId == shift.Id, ct)) return Fail(409, "Ca này đã có phiếu đối soát.");
        var expected = request.OpeningCash + request.CashReceipts - request.CashRefunds - request.CashDisbursements;
        if (expected < 0) return Fail(400, "Tiền cuối ca dự kiến không thể âm.");
        if (request.CountedClosingCash != expected && string.IsNullOrWhiteSpace(request.VarianceReason)) return Fail(400, "Cần ghi lý do chênh lệch.");
        var item = new CashReconciliation
        {
            Id = Guid.NewGuid(), ShiftId = shift.Id, OpeningCash = request.OpeningCash,
            CashReceipts = request.CashReceipts, CashRefunds = request.CashRefunds,
            CashDisbursements = request.CashDisbursements, ExpectedClosingCash = expected,
            CountedClosingCash = request.CountedClosingCash, Variance = request.CountedClosingCash - expected,
            PaymentSummaryReference = request.PaymentSummaryReference.Trim(),
            PaymentSummaryCapturedAtUtc = request.PaymentSummaryCapturedAtUtc,
            VarianceReason = string.IsNullOrWhiteSpace(request.VarianceReason) ? null : request.VarianceReason.Trim(),
            SubmittedByManagerId = managerId, SubmittedAtUtc = DateTime.UtcNow, Version = 1
        };
        db.CashReconciliations.Add(item);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        { return Fail(409, "Ca này đã có phiếu đối soát."); }
        item.Shift = shift;
        return Ok(item, "Đã gửi đối soát chờ Manager khác duyệt.");
    }

    public async Task<CashReconciliationOperationResult> ApproveAsync(Guid id, ApproveCashReconciliationRequest request, long managerId, string token, CancellationToken ct)
    {
        var item = await db.CashReconciliations.Include(x => x.Shift).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return Fail(404, "Không tìm thấy phiếu đối soát.");
        if (!await coreAccess.IsCallerAssignedToLotAsync(token, item.Shift.LotId, ct)) return Fail(403, "Bạn không được phân công quản lý bãi này.");
        if (item.SubmittedByManagerId == managerId) return Fail(403, "Manager lập phiếu không thể tự duyệt.");
        if (item.Status != CashReconciliationStatuses.PendingReview) return Fail(409, "Phiếu này đã được xử lý.");
        if (item.Variance != 0 && string.IsNullOrWhiteSpace(request.ApprovalNote)) return Fail(400, "Cần ghi chú duyệt khi có chênh lệch.");
        item.Status = CashReconciliationStatuses.Approved; item.ApprovedByManagerId = managerId;
        item.ApprovedAtUtc = DateTime.UtcNow; item.ApprovalNote = string.IsNullOrWhiteSpace(request.ApprovalNote) ? null : request.ApprovalNote.Trim(); item.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Fail(409, "Phiếu vừa được cập nhật. Tải lại rồi thử lại."); }
        return Ok(item, "Đã duyệt đối soát.");
    }

    private static CashReconciliationOperationResult Ok(CashReconciliation x, string message) => new(true, 200, message, CashReconciliationDto.From(x));
    private static CashReconciliationOperationResult Fail(int status, string message) => new(false, status, message);
}

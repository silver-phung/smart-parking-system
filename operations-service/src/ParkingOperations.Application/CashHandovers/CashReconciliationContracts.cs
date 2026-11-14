using System.ComponentModel.DataAnnotations;
using ParkingOperations.Domain.Entities;

namespace ParkingOperations.Application.CashHandovers;

public sealed class SubmitCashReconciliationRequest : IValidatableObject
{
    public Guid ShiftId { get; init; }
    [Range(typeof(decimal), "0", "999999999999.99")] public decimal OpeningCash { get; init; }
    [Range(typeof(decimal), "0", "999999999999.99")] public decimal CashReceipts { get; init; }
    [Range(typeof(decimal), "0", "999999999999.99")] public decimal CashRefunds { get; init; }
    [Range(typeof(decimal), "0", "999999999999.99")] public decimal CashDisbursements { get; init; }
    [Range(typeof(decimal), "0", "999999999999.99")] public decimal CountedClosingCash { get; init; }
    [Required, StringLength(200, MinimumLength = 3)] public string PaymentSummaryReference { get; init; } = string.Empty;
    public DateTime PaymentSummaryCapturedAtUtc { get; init; }
    [StringLength(500)] public string? VarianceReason { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ShiftId == Guid.Empty) yield return new ValidationResult("Chọn ca cần đối soát.", [nameof(ShiftId)]);
        if (PaymentSummaryCapturedAtUtc == default || PaymentSummaryCapturedAtUtc.Kind != DateTimeKind.Utc)
            yield return new ValidationResult("Thời điểm chụp báo cáo Payment phải là UTC.", [nameof(PaymentSummaryCapturedAtUtc)]);
        var expected = OpeningCash + CashReceipts - CashRefunds - CashDisbursements;
        if (expected < 0) yield return new ValidationResult("Tiền cuối ca dự kiến không thể âm.");
        if (CountedClosingCash != expected && string.IsNullOrWhiteSpace(VarianceReason))
            yield return new ValidationResult("Cần ghi lý do khi số tiền thực đếm lệch dự kiến.", [nameof(VarianceReason)]);
    }
}

public sealed class ApproveCashReconciliationRequest
{
    [StringLength(500)] public string? ApprovalNote { get; init; }
}

public sealed record CashReconciliationDto(Guid Id, Guid ShiftId, int LotId, DateOnly BusinessDate, string ShiftCode,
    decimal OpeningCash, decimal CashReceipts, decimal CashRefunds, decimal CashDisbursements,
    decimal ExpectedClosingCash, decimal CountedClosingCash, decimal Variance, string PaymentSummaryReference,
    DateTime PaymentSummaryCapturedAtUtc, string? VarianceReason, string Status, long SubmittedByManagerId,
    DateTime SubmittedAtUtc, long? ApprovedByManagerId, DateTime? ApprovedAtUtc, string? ApprovalNote, int Version)
{
    public static CashReconciliationDto From(CashReconciliation x) => new(x.Id, x.ShiftId, x.Shift.LotId,
        x.Shift.BusinessDate, x.Shift.Code, x.OpeningCash, x.CashReceipts, x.CashRefunds, x.CashDisbursements,
        x.ExpectedClosingCash, x.CountedClosingCash, x.Variance, x.PaymentSummaryReference,
        x.PaymentSummaryCapturedAtUtc, x.VarianceReason, x.Status, x.SubmittedByManagerId, x.SubmittedAtUtc,
        x.ApprovedByManagerId, x.ApprovedAtUtc, x.ApprovalNote, x.Version);
}

public sealed record CashReconciliationOperationResult(bool Success, int StatusCode, string Message, CashReconciliationDto? Data = null);

public interface ICashReconciliationService
{
    Task<(IReadOnlyList<CashReconciliationDto> Items, int TotalCount)> ListAsync(int lotId, string token, CancellationToken ct);
    Task<CashReconciliationOperationResult> SubmitAsync(SubmitCashReconciliationRequest request, long managerId, string token, CancellationToken ct);
    Task<CashReconciliationOperationResult> ApproveAsync(Guid id, ApproveCashReconciliationRequest request, long managerId, string token, CancellationToken ct);
}

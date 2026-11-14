namespace ParkingOperations.Domain.Entities;

public static class CashReconciliationStatuses
{
    public const string PendingReview = "PENDING_REVIEW";
    public const string Approved = "APPROVED";
}

public sealed class CashReconciliation
{
    public Guid Id { get; set; }
    public Guid ShiftId { get; set; }
    public ParkingShift Shift { get; set; } = null!;
    public decimal OpeningCash { get; set; }
    public decimal CashReceipts { get; set; }
    public decimal CashRefunds { get; set; }
    public decimal CashDisbursements { get; set; }
    public decimal ExpectedClosingCash { get; set; }
    public decimal CountedClosingCash { get; set; }
    public decimal Variance { get; set; }
    public string PaymentSummaryReference { get; set; } = string.Empty;
    public DateTime PaymentSummaryCapturedAtUtc { get; set; }
    public string? VarianceReason { get; set; }
    public string Status { get; set; } = CashReconciliationStatuses.PendingReview;
    public long SubmittedByManagerId { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public long? ApprovedByManagerId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovalNote { get; set; }
    public int Version { get; set; }
}

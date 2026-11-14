namespace ParkingOperations.Domain.Entities;

public static class ParkingShiftCodes
{
    public const string Morning = "MORNING";       // 05:00–13:00
    public const string Afternoon = "AFTERNOON";   // 13:00–21:00
    public const string Night = "NIGHT";           // 21:00–05:00

    public static bool IsValid(string? value) => value is Morning or Afternoon or Night;

    public static bool IsNext(string from, string to) =>
        (from, to) is (Morning, Afternoon) or (Afternoon, Night) or (Night, Morning);
}

public static class CashHandoverStatuses
{
    public const string AwaitingConfirmations = "AWAITING_CONFIRMATIONS";
    public const string Completed = "COMPLETED";
    public const string Disputed = "DISPUTED";
    public const string Rejected = "REJECTED";
}

public sealed class CashHandover
{
    public Guid Id { get; set; }
    public int LotId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string FromShiftCode { get; set; } = string.Empty;
    public string ToShiftCode { get; set; } = string.Empty;
    public long OutgoingStaffId { get; set; }
    public long IncomingStaffId { get; set; }
    public long CreatedByManagerId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public decimal? OutgoingCountedAmount { get; set; }
    public long? OutgoingConfirmedBy { get; set; }
    public DateTime? OutgoingConfirmedAtUtc { get; set; }
    public decimal? IncomingCountedAmount { get; set; }
    public long? IncomingConfirmedBy { get; set; }
    public DateTime? IncomingConfirmedAtUtc { get; set; }
    public string? DisputeReason { get; set; }
    public long? DisputedBy { get; set; }
    public DateTime? DisputedAtUtc { get; set; }
    public string Status { get; set; } = CashHandoverStatuses.AwaitingConfirmations;
    public int Version { get; set; }
}

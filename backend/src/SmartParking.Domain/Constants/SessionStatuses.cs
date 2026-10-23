namespace SmartParking.Domain.Constants;

public static class SessionStatuses
{
    public const string Active = "ACTIVE";
    public const string Paid = "PAID";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All = [Active, Paid, Completed, Cancelled];
}

namespace SmartParking.Domain.Constants;

public static class BookingStatuses
{
    public const int NoShowGracePeriodMinutes = 30;
    public const string Pending = "PENDING";
    public const string CheckedIn = "CHECKED_IN";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";

    public static readonly IReadOnlyList<string> All = [Pending, CheckedIn, Cancelled, Expired];
}

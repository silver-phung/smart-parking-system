namespace SmartParking.Domain.Constants;

public static class SlotPhysicalStatuses
{
    public const string Empty = "EMPTY";
    public const string Occupied = "OCCUPIED";
    public const string Maintenance = "MAINTENANCE";
    public const string Unavailable = "UNAVAILABLE";

    public static readonly IReadOnlyList<string> All = [Empty, Occupied, Maintenance, Unavailable];
}

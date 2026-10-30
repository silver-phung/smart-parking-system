namespace SmartParking.Domain.Entities;

public class ParkingDispatch
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public ParkingSession Session { get; set; } = null!;
    public long AssignedSlotId { get; set; }
    public ParkingSlot AssignedSlot { get; set; } = null!;
    public long? ActualSlotId { get; set; }
    public ParkingSlot? ActualSlot { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, ASSIGNED, OCCUPIED, WRONG_SLOT, CANCELLED
    public string DispatchMode { get; set; } = "AUTO"; // AUTO, MANUAL
    public DateTime DispatchedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

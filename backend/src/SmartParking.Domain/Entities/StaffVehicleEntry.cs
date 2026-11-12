namespace SmartParking.Domain.Entities;

/// <summary>Short lived staff workflow from gate recognition until the vehicle is parked.</summary>
public sealed class StaffVehicleEntry
{
    public long Id { get; set; }
    public long RecognitionId { get; set; }
    public int LotId { get; set; }
    public long? ParkingSessionId { get; set; }
    public long StaffUserId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "SEARCHING_FOR_SLOT";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ParkedAt { get; set; }
}

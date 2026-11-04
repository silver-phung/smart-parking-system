namespace SmartParking.Domain.Entities;

public class ParkingSession
{
    public long Id { get; set; }
    public int LotId { get; set; }
    public ParkingLot Lot { get; set; } = null!;
    public long? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    private string _plateNumber = string.Empty;
    public string PlateNumber
    {
        get => _plateNumber;
        set => _plateNumber = value?.Trim().ToUpperInvariant() ?? string.Empty;
    }
    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
    public DateTime? CheckOutTime { get; set; }
    public long? CurrentSlotId { get; set; }
    public ParkingSlot? CurrentSlot { get; set; }
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, PAID, COMPLETED, CANCELLED
    public decimal TotalFee { get; set; } = 0m;
    public DateTime? PaidAt { get; set; }
    public string? CheckInImageUrl { get; set; }
    public string? CheckOutImageUrl { get; set; }
    public long? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int? ZoneId { get; set; }
    public ParkingZone? Zone { get; set; }
    public long? PolicyId { get; set; }
    public ParkingPolicy? Policy { get; set; }
    public Guid? EntryEventId { get; set; }
    public string? EntryRequestHash { get; set; }
    public long? RecognitionId { get; set; }
    public string? TemporaryPlateNumber { get; set; }
    public long? EntryImageId { get; set; }
    public string VerificationStatus { get; set; } = "LEGACY";
    public string? FuelType { get; set; }
    public long? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public long Revision { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ParkingDispatch> Dispatches { get; set; } = new List<ParkingDispatch>();
}

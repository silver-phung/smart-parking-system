namespace SmartParking.Domain.Entities;

public class ParkingSlot
{
    public long Id { get; set; }
    public int FloorId { get; set; }
    public ParkingFloor Floor { get; set; } = null!;
    public string SlotCode { get; set; } = null!;
    public string PhysicalStatus { get; set; } = "EMPTY"; // EMPTY, OCCUPIED, MAINTENANCE, UNAVAILABLE
    public decimal? PositionX { get; set; }
    public decimal? PositionY { get; set; }
    public decimal? PositionZ { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ParkingSession> CurrentSessions { get; set; } = new List<ParkingSession>();
    public ICollection<ParkingDispatch> AssignedDispatches { get; set; } = new List<ParkingDispatch>();
    public ICollection<ParkingDispatch> ActualDispatches { get; set; } = new List<ParkingDispatch>();
}

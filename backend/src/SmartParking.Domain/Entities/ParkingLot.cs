namespace SmartParking.Domain.Entities;

public class ParkingLot
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public int TotalCapacity { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ParkingFloor> Floors { get; set; } = new List<ParkingFloor>();
    public ICollection<ParkingSession> Sessions { get; set; } = new List<ParkingSession>();
    public ICollection<StaffLotAssignment> StaffAssignments { get; set; } = new List<StaffLotAssignment>();
}

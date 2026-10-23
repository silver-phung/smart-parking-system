namespace SmartParking.Domain.Entities;

public class ParkingFloor
{
    public int Id { get; set; }
    public int LotId { get; set; }
    public ParkingLot Lot { get; set; } = null!;
    public int FloorNumber { get; set; }
    public string Name { get; set; } = null!;
    public int TotalSlots { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ParkingSlot> Slots { get; set; } = new List<ParkingSlot>();
}

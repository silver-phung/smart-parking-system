namespace SmartParking.Domain.Entities;

public class ParkingZone
{
    public int Id { get; set; }
    public int LotId { get; set; }
    public ParkingLot Lot { get; set; } = null!;
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "TRANSIENT";
    public string Layout { get; set; } = "INSIDE";
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

namespace SmartParking.Domain.Entities;

public class ParkingLot
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AvailableSlots { get; set; }
    public decimal HourlyRate { get; set; }
}

namespace SmartParking.Domain.Entities;

public class Vehicle
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public User? User { get; set; }
    private string _plateNumber = string.Empty;
    public string PlateNumber
    {
        get => _plateNumber;
        set => _plateNumber = value?.Trim().ToUpperInvariant() ?? string.Empty;
    }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ParkingSession> Sessions { get; set; } = new List<ParkingSession>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

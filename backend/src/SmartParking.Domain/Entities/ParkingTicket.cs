namespace SmartParking.Domain.Entities;

public class ParkingTicket
{
    public long Id { get; set; }
    public int LotId { get; set; }
    public string Code { get; set; } = "";
    public long SessionId { get; set; }
    public ParkingSession Session { get; set; } = null!;
    public string Status { get; set; } = "ISSUED";
    public long IssuedBy { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public long? ClosedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Reason { get; set; }
}

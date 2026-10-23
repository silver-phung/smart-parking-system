namespace SmartParking.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public long? ActorId { get; set; }
    public User? Actor { get; set; }
    public string Action { get; set; } = null!;
    public string TargetEntity { get; set; } = null!;
    public string? TargetId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

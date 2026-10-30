using SmartParking.Domain.Constants;

namespace SmartParking.Domain.Entities;

public class StaffLotAssignment
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public int LotId { get; set; }
    public ParkingLot Lot { get; set; } = null!;

    public long AssignedBy { get; set; }
    public User AssignedByUser { get; set; } = null!;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
    public string Duty { get; set; } = StaffDuties.Gate; // Default least-privilege duty: GATE

    public long? RevokedBy { get; set; }
    public User? RevokedByUser { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

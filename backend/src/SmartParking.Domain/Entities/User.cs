namespace SmartParking.Domain.Entities;

public class User
{
    public long Id { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public string FullName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; } = false;
    public string ApprovalStatus { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED

    public long? ApprovedBy { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutEndAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<UserToken> UserTokens { get; set; } = new List<UserToken>();
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<StaffLotAssignment> StaffLotAssignments { get; set; } = new List<StaffLotAssignment>();
    public ICollection<StaffLotAssignment> AssignedStaffAssignments { get; set; } = new List<StaffLotAssignment>();
    public ICollection<StaffLotAssignment> RevokedStaffAssignments { get; set; } = new List<StaffLotAssignment>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

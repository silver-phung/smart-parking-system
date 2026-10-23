namespace SmartParking.Domain.Entities;

public class RefreshToken
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = null!; // SHA-256
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

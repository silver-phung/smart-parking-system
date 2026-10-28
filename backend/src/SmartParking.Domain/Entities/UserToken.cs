namespace SmartParking.Domain.Entities;

public class UserToken
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenType { get; set; } = null!; // EMAIL_VERIFICATION, PASSWORD_RESET
    public string TokenHash { get; set; } = null!; // HMAC-SHA256
    public int AttemptsCount { get; set; } = 0;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

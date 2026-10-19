namespace WMMS.Domain.Entities.Identity;

public class RefreshToken
{
    public long TokenId { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive => !IsRevoked && ExpiresAt > DateTime.UtcNow;
}

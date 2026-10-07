namespace FundTrack.core.Entities.Security;

public class PasswordReset
{
    public long PasswordResetId { get; set; }
    public long AppUserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

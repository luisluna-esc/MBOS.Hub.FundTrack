namespace FundTrack.core.Entities.Security;

public class AppUser
{
    public long AppUserId { get; set; }
    public string? Ci { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    /// <summary>Con lo que se entra al sistema: nombre.apellido, en minúsculas y sin tildes.</summary>
    public string Username { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public long? PastorId { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLogin { get; set; }
    public long? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

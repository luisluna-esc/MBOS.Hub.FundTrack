namespace FundTrack.Application.Dtos.Security;

public class AppUserDto
{
    public long AppUserId { get; set; }
    public string? Ci { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public long? PastorId { get; set; }

    // Write-only: plain text password for insert/update, hashed by AppUserService before persisting.
    // Never populated when mapping back from the entity, so it never appears in a GET response.
    public string? Password { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLogin { get; set; }
    public long? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

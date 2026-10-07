namespace FundTrack.core.Entities.Security;

public class AppUserRole
{
    public long AppUserId { get; set; }
    public long RoleId { get; set; }
    public long? AssignedById { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

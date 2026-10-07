namespace FundTrack.core.QueryFilters.Security;

public class AppUserQueryFilter
{
    public long? AppUserId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Ci { get; set; }
    public bool? IsActive { get; set; }
}

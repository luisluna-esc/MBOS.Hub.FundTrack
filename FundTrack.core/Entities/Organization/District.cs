namespace FundTrack.core.Entities.Organization;

public class District
{
    public long DistrictId { get; set; }
    public long RegionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsMissionary { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

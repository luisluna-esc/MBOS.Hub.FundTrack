namespace FundTrack.core.Entities.Organization;

public class Region
{
    public long RegionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal PendingBalance { get; set; }
}

namespace FundTrack.Application.Dtos.Organization;

public class RegionDto
{
    public long RegionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal PendingBalance { get; set; }
}

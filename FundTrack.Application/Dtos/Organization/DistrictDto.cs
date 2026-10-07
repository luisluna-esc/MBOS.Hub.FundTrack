namespace FundTrack.Application.Dtos.Organization;

public class DistrictDto
{
    public long DistrictId { get; set; }
    public long RegionId { get; set; }
    public string? RegionName { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsMissionary { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CurrentPastorId { get; set; }
    public string? CurrentPastorName { get; set; }
    public int ChurchCount { get; set; }
}

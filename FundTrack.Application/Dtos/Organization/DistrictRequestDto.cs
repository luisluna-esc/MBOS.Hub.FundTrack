namespace FundTrack.Application.Dtos.Organization;

public class DistrictRequestDto
{
    public long RegionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsMissionary { get; set; }
    public bool IsActive { get; set; } = true;
}

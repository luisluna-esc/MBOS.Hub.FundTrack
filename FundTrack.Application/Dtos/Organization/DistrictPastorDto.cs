namespace FundTrack.Application.Dtos.Organization;

public class DistrictPastorDto
{
    public long DistrictPastorId { get; set; }
    public long DistrictId { get; set; }
    public string? DistrictName { get; set; }
    public long PastorId { get; set; }
    public string? PastorName { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

namespace FundTrack.Application.Dtos.Organization;

public class DistrictPastorAssignRequestDto
{
    public long PastorId { get; set; }
    public DateOnly StartDate { get; set; }
}

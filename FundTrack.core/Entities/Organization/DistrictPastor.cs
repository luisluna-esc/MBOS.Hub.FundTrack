namespace FundTrack.core.Entities.Organization;

public class DistrictPastor
{
    public long DistrictPastorId { get; set; }
    public long DistrictId { get; set; }
    public long PastorId { get; set; }
    public DateOnly StartDate { get; set; }
    /// <summary>Null mientras la asignación sigue vigente.</summary>
    public DateOnly? EndDate { get; set; }
}

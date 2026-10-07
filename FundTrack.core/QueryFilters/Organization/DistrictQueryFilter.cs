namespace FundTrack.core.QueryFilters.Organization;

public class DistrictQueryFilter
{
    public long? DistrictId { get; set; }
    public long? RegionId { get; set; }
    public bool? IsMissionary { get; set; }
    public bool? IsActive { get; set; }
}

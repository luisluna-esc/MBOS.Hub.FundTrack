namespace FundTrack.core.QueryFilters.Charts;

public class ChartsQueryFilter
{
    /// <summary>Por defecto, el año actual.</summary>
    public int? Year { get; set; }
    public long? RegionId { get; set; }
    public long? DistrictId { get; set; }
}

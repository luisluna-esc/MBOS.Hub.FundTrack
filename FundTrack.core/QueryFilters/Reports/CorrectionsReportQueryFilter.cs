namespace FundTrack.core.QueryFilters.Reports;

public class CorrectionsReportQueryFilter
{
    /// <summary>Año del período donde se aplicó la corrección.</summary>
    public int Year { get; set; }
    public long? RegionId { get; set; }
    public long? DistrictId { get; set; }
}

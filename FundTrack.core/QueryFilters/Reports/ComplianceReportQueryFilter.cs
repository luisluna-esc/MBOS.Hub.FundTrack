namespace FundTrack.core.QueryFilters.Reports;

public class ComplianceReportQueryFilter
{
    public int Year { get; set; }
    public long? RegionId { get; set; }
    public long? DistrictId { get; set; }
}

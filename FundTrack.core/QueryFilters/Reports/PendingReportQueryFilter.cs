namespace FundTrack.core.QueryFilters.Reports;

public class PendingReportQueryFilter
{
    public long PeriodId { get; set; }
    public long? RegionId { get; set; }
    public long? DistrictId { get; set; }
}

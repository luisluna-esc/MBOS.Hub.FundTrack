namespace FundTrack.core.QueryFilters.Reports;

public class NegativeAcmsReportQueryFilter
{
    public int Year { get; set; }
    public int? MonthFrom { get; set; }
    public int? MonthTo { get; set; }
    public long? RegionId { get; set; }
    public long? DistrictId { get; set; }
}

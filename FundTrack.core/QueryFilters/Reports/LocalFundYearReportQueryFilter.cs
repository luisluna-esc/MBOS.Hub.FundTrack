namespace FundTrack.core.QueryFilters.Reports;

public class LocalFundYearReportQueryFilter
{
    public int Year { get; set; }
    /// <summary>Primer mes del reporte (1 = enero). Por defecto enero.</summary>
    public int? MonthFrom { get; set; }
    /// <summary>Último mes del reporte (12 = diciembre). Por defecto diciembre.</summary>
    public int? MonthTo { get; set; }
    public long? RegionId { get; set; }
    public long? DistrictId { get; set; }
}

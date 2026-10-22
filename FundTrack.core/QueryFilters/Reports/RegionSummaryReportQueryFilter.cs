namespace FundTrack.core.QueryFilters.Reports;

public class RegionSummaryReportQueryFilter
{
    public int Year { get; set; }
    public int? MonthFrom { get; set; }
    public int? MonthTo { get; set; }
    /// <summary>"region" (por defecto) o "district".</summary>
    public string? GroupBy { get; set; }
    /// <summary>Solo al agrupar por distrito: los distritos de esa región.</summary>
    public long? RegionId { get; set; }
}

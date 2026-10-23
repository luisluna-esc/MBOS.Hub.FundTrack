namespace FundTrack.core.QueryFilters.Reports;

public class DifferencesReportQueryFilter
{
    public int Year { get; set; }
    /// <summary>"pending" o "resolved"; vacío = todas.</summary>
    public string? Status { get; set; }
    public long? RegionId { get; set; }
    public long? DistrictId { get; set; }
    /// <summary>Una sola iglesia (vacío = todas).</summary>
    public long? ChurchId { get; set; }
}

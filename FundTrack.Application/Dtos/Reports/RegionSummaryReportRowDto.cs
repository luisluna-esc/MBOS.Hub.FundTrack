namespace FundTrack.Application.Dtos.Reports;

/// <summary>Fila de sp_report_region_summary: una región en un mes (igual que el Inicio).</summary>
public class RegionSummaryReportRowDto
{
    public int PeriodMonth { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public int ActiveChurches { get; set; }
    public int Submitted { get; set; }
    public int NotSubmitted { get; set; }
    public int Missing { get; set; }
    public int DifferencesPending { get; set; }
    public int DifferencesResolved { get; set; }
    public int NegativeAcms { get; set; }
    public decimal TotalAcmsBalance { get; set; }
    public decimal TotalCounted { get; set; }
    public decimal TotalDifference { get; set; }
}

/// <summary>Fila de sp_report_district_summary: un distrito en un mes.</summary>
public class DistrictSummaryReportRowDto : RegionSummaryReportRowDto
{
    public string DistrictName { get; set; } = string.Empty;
    public string? PastorName { get; set; }
}

namespace FundTrack.Application.Dtos.Reports;

/// <summary>Fila de sp_report_differences: un arqueo que no cuadra con el ACMS.</summary>
public class DifferencesReportRowDto
{
    public string RegionName { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string? PastorName { get; set; }
    public string ChurchCode { get; set; } = string.Empty;
    public string ChurchName { get; set; } = string.Empty;
    public string ChurchType { get; set; } = string.Empty;
    public int PeriodMonth { get; set; }
    public decimal AcmsBalance { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Difference { get; set; }
    public string? DifferenceStatus { get; set; }
    public string? Observation { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastFollowUpAt { get; set; }
    public string? LastJustification { get; set; }
    public string? LastFollowUpBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

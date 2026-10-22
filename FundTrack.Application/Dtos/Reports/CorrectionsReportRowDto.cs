namespace FundTrack.Application.Dtos.Reports;

/// <summary>Fila de sp_report_corrections: una corrección de un mes cerrado.</summary>
public class CorrectionsReportRowDto
{
    public DateTime RegisteredAt { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string ChurchCode { get; set; } = string.Empty;
    public string ChurchName { get; set; } = string.Empty;
    public int CountYear { get; set; }
    public int CountMonth { get; set; }
    public int AppliedYear { get; set; }
    public int AppliedMonth { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public decimal PreviousAmount { get; set; }
    public decimal CorrectedAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AppUserFullName { get; set; }
}

namespace FundTrack.Application.Dtos.Reports;

/// <summary>Fila de sp_report_compliance: un distrito en un mes.</summary>
public class ComplianceReportRowDto
{
    public string RegionName { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string? PastorName { get; set; }
    public int PeriodMonth { get; set; }
    public DateOnly DueDate { get; set; }
    /// <summary>Iglesias y Grupos que debían registrar arqueo.</summary>
    public int Expected { get; set; }
    public int Registered { get; set; }
    /// <summary>Registrados hasta la fecha límite del período (día de Bolivia).</summary>
    public int OnTime { get; set; }
    public int NotSubmitted { get; set; }
}

namespace FundTrack.Application.Dtos.Reports;

/// <summary>Fila de sp_report_pending (Scrips/storedProcedures.sql).</summary>
public class PendingReportRowDto
{
    public string RegionName { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string? PastorName { get; set; }
    public long ChurchId { get; set; }
    public string ChurchCode { get; set; } = string.Empty;
    public string ChurchName { get; set; } = string.Empty;
    public string ChurchType { get; set; } = string.Empty;
    /// <summary>"missing" (sin arqueo registrado), "not_submitted" (no presentó) o "difference" (diferencia sin resolver).</summary>
    public string PendingKind { get; set; } = string.Empty;
    public decimal? AcmsBalance { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? Difference { get; set; }
    public string? DifferenceStatus { get; set; }
}

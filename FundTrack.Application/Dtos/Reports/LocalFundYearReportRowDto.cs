namespace FundTrack.Application.Dtos.Reports;

/// <summary>
/// Fila de sp_report_local_fund_year (Scrips/storedProcedures.sql): una Iglesia/Grupo en un mes.
/// Month es null cuando la iglesia no tiene ningún arqueo en el rango pedido.
/// </summary>
public class LocalFundYearReportRowDto
{
    public string RegionName { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string? PastorName { get; set; }
    public long ChurchId { get; set; }
    public string ChurchCode { get; set; } = string.Empty;
    public string ChurchName { get; set; } = string.Empty;
    public string ChurchType { get; set; } = string.Empty;
    public int? Month { get; set; }
    public string? SubmissionStatus { get; set; }
    public decimal? AcmsBalance { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? Difference { get; set; }
    public string? DifferenceStatus { get; set; }
}

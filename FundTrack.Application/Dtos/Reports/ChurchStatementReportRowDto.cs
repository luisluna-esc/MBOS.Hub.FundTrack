namespace FundTrack.Application.Dtos.Reports;

/// <summary>Fila de sp_report_church_statement: un mes del rango, tenga o no arqueo.</summary>
public class ChurchStatementReportRowDto
{
    public string ChurchCode { get; set; } = string.Empty;
    public string ChurchName { get; set; } = string.Empty;
    public string ChurchType { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string RegionName { get; set; } = string.Empty;
    public string? PastorName { get; set; }
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public string PeriodStatus { get; set; } = string.Empty;
    public bool HasCount { get; set; }
    public string? SubmissionStatus { get; set; }
    public bool SummaryOnly { get; set; }
    public decimal AcmsBalance { get; set; }
    public decimal BankStatementBalance { get; set; }
    public decimal BankDeposit { get; set; }
    public decimal LocalCash { get; set; }
    public decimal UsdAmountBs { get; set; }
    public decimal DocumentsTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Difference { get; set; }
    public string? DifferenceStatus { get; set; }
    public DateOnly? CountDate { get; set; }
    public string? Observation { get; set; }
    public int CorrectionCount { get; set; }
}

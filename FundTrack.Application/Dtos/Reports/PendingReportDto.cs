namespace FundTrack.Application.Dtos.Reports;

public class PendingReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string PeriodStatus { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public int MissingCount { get; set; }
    public int NotSubmittedCount { get; set; }
    public int DifferenceCount { get; set; }
    public List<PendingReportRowDto> Rows { get; set; } = [];
}

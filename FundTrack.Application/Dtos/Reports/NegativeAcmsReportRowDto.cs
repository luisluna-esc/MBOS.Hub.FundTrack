namespace FundTrack.Application.Dtos.Reports;

/// <summary>Fila de sp_report_negative_acms: un mes con saldo ACMS negativo.</summary>
public class NegativeAcmsReportRowDto
{
    public string RegionName { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string? PastorName { get; set; }
    public long ChurchId { get; set; }
    public string ChurchCode { get; set; } = string.Empty;
    public string ChurchName { get; set; } = string.Empty;
    public string ChurchType { get; set; } = string.Empty;
    public int PeriodMonth { get; set; }
    public decimal AcmsBalance { get; set; }
}

/// <summary>Una iglesia con todos sus meses en negativo, ya agrupada.</summary>
public class NegativeAcmsChurchDto
{
    public string RegionName { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string? PastorName { get; set; }
    public string ChurchCode { get; set; } = string.Empty;
    public string ChurchName { get; set; } = string.Empty;
    public string ChurchType { get; set; } = string.Empty;
    public List<int> Months { get; set; } = [];
    /// <summary>La racha más larga de meses seguidos en negativo.</summary>
    public int LongestStreak { get; set; }
    public int LastMonth { get; set; }
    public decimal LastBalance { get; set; }
    public decimal LowestBalance { get; set; }
}

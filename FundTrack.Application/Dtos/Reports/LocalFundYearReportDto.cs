namespace FundTrack.Application.Dtos.Reports;

/// <summary>El reporte Fondo Local ya con los meses en columnas, igual que el Excel original.</summary>
public class LocalFundYearReportDto
{
    public int Year { get; set; }
    public int MonthFrom { get; set; }
    public int MonthTo { get; set; }
    public string Scope { get; set; } = string.Empty;
    public List<LocalFundYearChurchDto> Churches { get; set; } = [];
}

public class LocalFundYearChurchDto
{
    public string RegionName { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string? PastorName { get; set; }
    public long ChurchId { get; set; }
    public string ChurchCode { get; set; } = string.Empty;
    public string ChurchName { get; set; } = string.Empty;
    public string ChurchType { get; set; } = string.Empty;
    /// <summary>Un elemento por mes del rango; null si ese mes no tiene arqueo registrado.</summary>
    public List<LocalFundMonthDto?> Months { get; set; } = [];
}

public class LocalFundMonthDto
{
    public int Month { get; set; }
    public string SubmissionStatus { get; set; } = string.Empty;
    public decimal AcmsBalance { get; set; }
    /// <summary>Lo contado en el arqueo (efectivo más banco).</summary>
    public decimal TotalAmount { get; set; }
    public decimal Difference { get; set; }
    public string? DifferenceStatus { get; set; }
}

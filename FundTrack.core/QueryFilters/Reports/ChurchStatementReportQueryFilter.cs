namespace FundTrack.core.QueryFilters.Reports;

public class ChurchStatementReportQueryFilter
{
    /// <summary>La iglesia del reporte (obligatoria: el reporte es individual).</summary>
    public long? ChurchId { get; set; }
    /// <summary>Primer mes del rango.</summary>
    public long? FromPeriodId { get; set; }
    /// <summary>Último mes del rango.</summary>
    public long? ToPeriodId { get; set; }
}

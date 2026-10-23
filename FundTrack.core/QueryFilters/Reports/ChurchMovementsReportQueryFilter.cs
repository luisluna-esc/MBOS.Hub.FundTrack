namespace FundTrack.core.QueryFilters.Reports;

public class ChurchMovementsReportQueryFilter
{
    /// <summary>Por defecto, el 1 de enero del año actual.</summary>
    public DateOnly? DateFrom { get; set; }
    /// <summary>Por defecto, hoy.</summary>
    public DateOnly? DateTo { get; set; }
    /// <summary>"type", "district", "status" o "name"; vacío = todos.</summary>
    public string? ChangeType { get; set; }
    public long? RegionId { get; set; }
    public long? DistrictId { get; set; }
    /// <summary>Una sola iglesia (vacío = todas).</summary>
    public long? ChurchId { get; set; }
}

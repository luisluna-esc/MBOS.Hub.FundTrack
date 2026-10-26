namespace FundTrack.core.QueryFilters.Charts;

public class ChartsQueryFilter
{
    /// <summary>Por defecto, el año actual.</summary>
    public int? Year { get; set; }
    public long? RegionId { get; set; }
    public long? DistrictId { get; set; }
    /// <summary>Una sola iglesia: en vez del resumen de todas, su fondo local mes a mes.</summary>
    public long? ChurchId { get; set; }
}

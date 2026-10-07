namespace FundTrack.core.QueryFilters.Organization;

public class ChurchQueryFilter
{
    public long? ChurchId { get; set; }
    public string? Code { get; set; }
    public long? DistrictId { get; set; }
    public long? RegionId { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }
    /// <summary>Las Filiales de una Iglesia o Grupo.</summary>
    public long? ParentChurchId { get; set; }
}

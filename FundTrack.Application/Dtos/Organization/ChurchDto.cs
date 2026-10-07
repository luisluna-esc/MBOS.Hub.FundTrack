namespace FundTrack.Application.Dtos.Organization;

public class ChurchDto
{
    public long ChurchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long DistrictId { get; set; }
    public string? DistrictName { get; set; }
    public long? RegionId { get; set; }
    public string? RegionName { get; set; }
    public string? CurrentPastorName { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? TreasurerName { get; set; }
    public string? TreasurerPhone { get; set; }
    public string? ElderName { get; set; }
    public string? ElderPhone { get; set; }
    public string Status { get; set; } = string.Empty;
    public long? MergedIntoChurchId { get; set; }
    public string? MergedIntoChurchName { get; set; }
    public DateOnly? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? ParentChurchId { get; set; }
    public string? ParentChurchName { get; set; }
    /// <summary>Cuántas Filiales dependen de esta Iglesia o Grupo.</summary>
    public int FilialCount { get; set; }
}

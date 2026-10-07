namespace FundTrack.core.Entities.Organization;

public class Church
{
    public long ChurchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long DistrictId { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? TreasurerName { get; set; }
    public string? TreasurerPhone { get; set; }
    public string? ElderName { get; set; }
    public string? ElderPhone { get; set; }
    public string Status { get; set; } = string.Empty;
    public long? MergedIntoChurchId { get; set; }
    public DateOnly? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Solo para Filiales: la Iglesia o Grupo madre, con la que comparte código y arqueo.</summary>
    public long? ParentChurchId { get; set; }
}

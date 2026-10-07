namespace FundTrack.core.Entities.Organization;

public class Pastor
{
    public long PastorId { get; set; }
    /// <summary>Opcional: la lista oficial de pastores no lo trae. Único cuando se llena.</summary>
    public string? Ci { get; set; }
    public string FirstNames { get; set; } = string.Empty;
    public string LastNames { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

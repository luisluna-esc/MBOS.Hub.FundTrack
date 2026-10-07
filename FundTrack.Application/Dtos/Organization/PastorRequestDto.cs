namespace FundTrack.Application.Dtos.Organization;

public class PastorRequestDto
{
    /// <summary>Opcional. Único cuando se llena.</summary>
    public string? Ci { get; set; }
    public string FirstNames { get; set; } = string.Empty;
    public string LastNames { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
}

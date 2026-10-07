namespace FundTrack.Application.Dtos.Organization;

public class PastorDto
{
    public long PastorId { get; set; }
    public string? Ci { get; set; }
    public string FirstNames { get; set; } = string.Empty;
    public string LastNames { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CurrentDistrictId { get; set; }
    public string? CurrentDistrictName { get; set; }
}

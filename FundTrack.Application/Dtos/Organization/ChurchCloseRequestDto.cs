namespace FundTrack.Application.Dtos.Organization;

public class ChurchCloseRequestDto
{
    /// <summary>"closed" o "merged".</summary>
    public string Status { get; set; } = string.Empty;
    public DateOnly ClosedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>Obligatorio cuando Status es "merged".</summary>
    public long? MergedIntoChurchId { get; set; }
}

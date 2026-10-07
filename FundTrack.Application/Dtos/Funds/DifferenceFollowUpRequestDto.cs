namespace FundTrack.Application.Dtos.Funds;

public class DifferenceFollowUpRequestDto
{
    /// <summary>"justified" (desde pending) o "resolved" (desde justified).</summary>
    public string NewStatus { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
}

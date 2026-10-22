namespace FundTrack.Application.Dtos.Funds;

public class DifferenceFollowUpRequestDto
{
    /// <summary>"resolved" (desde pending): la Misión da la diferencia por cerrada.</summary>
    public string NewStatus { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
}

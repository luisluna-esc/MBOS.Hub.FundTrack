namespace FundTrack.core.Entities.Funds;

public class DifferenceFollowUp
{
    public long DifferenceFollowUpId { get; set; }
    public long LocalFundCountId { get; set; }
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public long? AppUserId { get; set; }
    public DateTime FollowedUpAt { get; set; } = DateTime.UtcNow;
}

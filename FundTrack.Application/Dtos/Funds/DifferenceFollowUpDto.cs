namespace FundTrack.Application.Dtos.Funds;

public class DifferenceFollowUpDto
{
    public long DifferenceFollowUpId { get; set; }
    public long LocalFundCountId { get; set; }
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public long? AppUserId { get; set; }
    public string? AppUserFullName { get; set; }
    public DateTime FollowedUpAt { get; set; }
}

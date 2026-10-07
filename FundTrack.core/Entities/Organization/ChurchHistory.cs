namespace FundTrack.core.Entities.Organization;

public class ChurchHistory
{
    public long ChurchHistoryId { get; set; }
    public long ChurchId { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateOnly ChangedAt { get; set; }
    public string? Reason { get; set; }
    public long? AppUserId { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}

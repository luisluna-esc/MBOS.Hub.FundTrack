namespace FundTrack.core.Entities.Organization;

public class Period
{
    public long PeriodId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ClosedAt { get; set; }
    public long? ClosedById { get; set; }
}

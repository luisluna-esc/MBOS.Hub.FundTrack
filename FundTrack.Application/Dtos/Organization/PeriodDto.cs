namespace FundTrack.Application.Dtos.Organization;

public class PeriodDto
{
    public long PeriodId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ClosedAt { get; set; }
    public long? ClosedById { get; set; }
    public string? ClosedByFullName { get; set; }
}

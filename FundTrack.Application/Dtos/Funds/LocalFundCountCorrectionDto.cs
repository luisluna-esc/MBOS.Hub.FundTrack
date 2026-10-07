namespace FundTrack.Application.Dtos.Funds;

public class LocalFundCountCorrectionDto
{
    public long LocalFundCountCorrectionId { get; set; }
    public long LocalFundCountId { get; set; }
    public long AppliedPeriodId { get; set; }
    public int AppliedPeriodYear { get; set; }
    public int AppliedPeriodMonth { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public decimal PreviousAmount { get; set; }
    public decimal CorrectedAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long? AppUserId { get; set; }
    public string? AppUserFullName { get; set; }
    public DateTime RegisteredAt { get; set; }
}

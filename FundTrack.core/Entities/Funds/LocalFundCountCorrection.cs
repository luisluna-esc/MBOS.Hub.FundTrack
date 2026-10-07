namespace FundTrack.core.Entities.Funds;

public class LocalFundCountCorrection
{
    public long LocalFundCountCorrectionId { get; set; }
    public long LocalFundCountId { get; set; }
    public long AppliedPeriodId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public decimal PreviousAmount { get; set; }
    public decimal CorrectedAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long? AppUserId { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}

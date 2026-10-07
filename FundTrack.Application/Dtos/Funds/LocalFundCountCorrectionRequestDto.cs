namespace FundTrack.Application.Dtos.Funds;

public class LocalFundCountCorrectionRequestDto
{
    /// <summary>"acms_balance", "cash_amount" o "bank_amount".</summary>
    public string FieldName { get; set; } = string.Empty;
    public decimal CorrectedAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

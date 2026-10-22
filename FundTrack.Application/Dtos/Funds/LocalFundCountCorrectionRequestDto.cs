namespace FundTrack.Application.Dtos.Funds;

public class LocalFundCountCorrectionRequestDto
{
    /// <summary>"acms_balance" o "total_amount" (total fondos en custodia).</summary>
    public string FieldName { get; set; } = string.Empty;
    public decimal CorrectedAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

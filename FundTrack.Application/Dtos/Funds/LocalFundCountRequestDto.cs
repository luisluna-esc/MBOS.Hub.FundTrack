namespace FundTrack.Application.Dtos.Funds;

public class LocalFundCountRequestDto
{
    public long ChurchId { get; set; }
    public long PeriodId { get; set; }
    /// <summary>"submitted" o "not_submitted". Si no presentó, los montos del arqueo se ignoran.</summary>
    public string SubmissionStatus { get; set; } = string.Empty;
    /// <summary>Saldo del ACMS al cierre del mes, digitado a mano.</summary>
    public decimal AcmsBalance { get; set; }
    public decimal CashAmount { get; set; }
    public decimal BankAmount { get; set; }
    public DateOnly? CountDate { get; set; }
    /// <summary>Si no se mandan, se toman los de la iglesia.</summary>
    public string? TreasurerName { get; set; }
    public string? ElderName { get; set; }
    public bool SignedByPastor { get; set; }
    public bool SignedByElder { get; set; }
    public string? Observation { get; set; }
}

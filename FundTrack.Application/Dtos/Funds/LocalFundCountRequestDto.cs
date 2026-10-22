namespace FundTrack.Application.Dtos.Funds;

public class LocalFundCountRequestDto
{
    public long ChurchId { get; set; }
    public long PeriodId { get; set; }
    /// <summary>"submitted" o "not_submitted". Si no presentó, los montos del arqueo se ignoran.</summary>
    public string SubmissionStatus { get; set; } = string.Empty;
    /// <summary>Saldo del ACMS al cierre del mes, digitado a mano.</summary>
    public decimal AcmsBalance { get; set; }
    // Los 4 montos del arqueo; todos opcionales (0 si no hay).
    public decimal BankStatementBalance { get; set; }
    public decimal BankDeposit { get; set; }
    /// <summary>Billetes y monedas en moneda nacional (fondo local).</summary>
    public decimal LocalCash { get; set; }
    public decimal UsdAmount { get; set; }
    /// <summary>Tipo de cambio usado para pasar los dólares a Bs. Obligatorio si hay dólares.</summary>
    public decimal ExchangeRate { get; set; }
    public DateOnly? CountDate { get; set; }
    /// <summary>Si no se mandan, se toman los de la iglesia.</summary>
    public string? TreasurerName { get; set; }
    public string? ElderName { get; set; }
    public bool SignedByPastor { get; set; }
    public bool SignedByElder { get; set; }
    /// <summary>Por qué hay diferencia. Obligatoria cuando el arqueo no cuadra con el ACMS.</summary>
    public string? Observation { get; set; }
    /// <summary>Documentos a rendir. Al editar reemplazan a los que había.</summary>
    public List<LocalFundCountDocumentRequestDto> Documents { get; set; } = [];
}

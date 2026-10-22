namespace FundTrack.core.Entities.Funds;

/// <summary>Documento a rendir del arqueo (factura, recibo u otro): su monto se suma al total en custodia.</summary>
public class LocalFundCountDocument
{
    public long LocalFundCountDocumentId { get; set; }
    public long LocalFundCountId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string? DocumentNumber { get; set; }
    public string Detail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

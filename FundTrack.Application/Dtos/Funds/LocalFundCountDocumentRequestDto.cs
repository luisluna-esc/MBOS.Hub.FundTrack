namespace FundTrack.Application.Dtos.Funds;

/// <summary>Factura, recibo u otro documento a rendir dentro del arqueo.</summary>
public class LocalFundCountDocumentRequestDto
{
    public DateOnly DocumentDate { get; set; }
    public string? DocumentNumber { get; set; }
    public string Detail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

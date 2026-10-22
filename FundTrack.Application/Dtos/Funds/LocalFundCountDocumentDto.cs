namespace FundTrack.Application.Dtos.Funds;

public class LocalFundCountDocumentDto
{
    public long LocalFundCountDocumentId { get; set; }
    public long LocalFundCountId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string? DocumentNumber { get; set; }
    public string Detail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

namespace FundTrack.core.Entities.Funds;

public class LocalFundCount
{
    public long LocalFundCountId { get; set; }
    public long ChurchId { get; set; }
    public long PeriodId { get; set; }
    // DistrictId, PastorId, ChurchType, TreasurerName y ElderName son una foto del momento del arqueo.
    public long DistrictId { get; set; }
    public long? PastorId { get; set; }
    public string ChurchType { get; set; } = string.Empty;
    public string SubmissionStatus { get; set; } = string.Empty;
    public decimal AcmsBalance { get; set; }
    public decimal CashAmount { get; set; }
    public decimal BankAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Difference { get; set; }
    public string? DifferenceStatus { get; set; }
    public DateOnly? CountDate { get; set; }
    public string? TreasurerName { get; set; }
    public string? ElderName { get; set; }
    public bool SignedByPastor { get; set; }
    public bool SignedByElder { get; set; }
    public string? Observation { get; set; }
    public long? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? UpdatedById { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

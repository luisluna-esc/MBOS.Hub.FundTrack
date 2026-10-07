namespace FundTrack.Application.Dtos.Funds;

public class LocalFundCountDto
{
    public long LocalFundCountId { get; set; }
    public long ChurchId { get; set; }
    public string? ChurchCode { get; set; }
    public string? ChurchName { get; set; }
    public long PeriodId { get; set; }
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public string? PeriodStatus { get; set; }
    public long DistrictId { get; set; }
    public string? DistrictName { get; set; }
    public long? RegionId { get; set; }
    public string? RegionName { get; set; }
    public long? PastorId { get; set; }
    public string? PastorName { get; set; }
    public string ChurchType { get; set; } = string.Empty;
    public string SubmissionStatus { get; set; } = string.Empty;
    public decimal AcmsBalance { get; set; }
    public decimal CashAmount { get; set; }
    public decimal BankAmount { get; set; }
    public decimal TotalAmount { get; set; }
    /// <summary>TotalAmount menos AcmsBalance: positivo si sobra dinero, negativo si falta.</summary>
    public decimal Difference { get; set; }
    public string? DifferenceStatus { get; set; }
    public DateOnly? CountDate { get; set; }
    public string? TreasurerName { get; set; }
    public string? ElderName { get; set; }
    public bool SignedByPastor { get; set; }
    public bool SignedByElder { get; set; }
    public string? Observation { get; set; }
    public int AttachmentCount { get; set; }
    public int CorrectionCount { get; set; }
    public long? CreatedById { get; set; }
    public string? CreatedByFullName { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? UpdatedById { get; set; }
    public string? UpdatedByFullName { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

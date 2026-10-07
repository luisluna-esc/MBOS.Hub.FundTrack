namespace FundTrack.Application.Dtos.StoredProcedures;

/// <summary>
/// Fila plana del resultado de sp_get_period_counts (Scrips/storedProcedures.sql): la iglesia y,
/// si existe, su arqueo del mes (columnas del arqueo en null cuando no hay). LocalFundCountService
/// la convierte en PeriodCountRowDto con el arqueo anidado.
/// </summary>
public class PeriodCountGetAllRow
{
    public long ChurchId { get; set; }
    public string ChurchCode { get; set; } = string.Empty;
    public string ChurchName { get; set; } = string.Empty;
    public string ChurchType { get; set; } = string.Empty;
    public long DistrictId { get; set; }
    public string? DistrictName { get; set; }
    public long? RegionId { get; set; }
    public string? RegionName { get; set; }
    public long? PastorId { get; set; }
    public string? PastorName { get; set; }
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public string? PeriodStatus { get; set; }

    public long? LocalFundCountId { get; set; }
    public string? CountChurchType { get; set; }
    public string? SubmissionStatus { get; set; }
    public decimal? AcmsBalance { get; set; }
    public decimal? CashAmount { get; set; }
    public decimal? BankAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? Difference { get; set; }
    public string? DifferenceStatus { get; set; }
    public DateOnly? CountDate { get; set; }
    public string? TreasurerName { get; set; }
    public string? ElderName { get; set; }
    public bool? SignedByPastor { get; set; }
    public bool? SignedByElder { get; set; }
    public string? Observation { get; set; }
    public int AttachmentCount { get; set; }
    public int CorrectionCount { get; set; }
    public long? CreatedById { get; set; }
    public string? CreatedByFullName { get; set; }
    public DateTime? CreatedAt { get; set; }
    public long? UpdatedById { get; set; }
    public string? UpdatedByFullName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int FilialCount { get; set; }
}

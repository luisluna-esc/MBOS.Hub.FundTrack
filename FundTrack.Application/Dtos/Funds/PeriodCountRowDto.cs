namespace FundTrack.Application.Dtos.Funds;

/// <summary>Una fila de la vista tipo Excel: cada iglesia activa con su arqueo del mes, o sin arqueo si todavía no se registró.</summary>
public class PeriodCountRowDto
{
    public long ChurchId { get; set; }
    public string ChurchCode { get; set; } = string.Empty;
    public string ChurchName { get; set; } = string.Empty;
    public string ChurchType { get; set; } = string.Empty;
    public long DistrictId { get; set; }
    public string? DistrictName { get; set; }
    public long? RegionId { get; set; }
    public string? RegionName { get; set; }
    public string? PastorName { get; set; }
    /// <summary>Filiales activas que van dentro del arqueo de esta Iglesia o Grupo.</summary>
    public int FilialCount { get; set; }
    public LocalFundCountDto? LocalFundCount { get; set; }
}

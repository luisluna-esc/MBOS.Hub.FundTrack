namespace FundTrack.Application.Dtos.Reports;

/// <summary>Fila de sp_report_church_movements: un cambio del historial de una iglesia.</summary>
public class ChurchMovementsReportRowDto
{
    public DateOnly ChangedAt { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string RegionName { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string ChurchCode { get; set; } = string.Empty;
    public string ChurchName { get; set; } = string.Empty;
    public string ChurchType { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public string? AppUserFullName { get; set; }
}

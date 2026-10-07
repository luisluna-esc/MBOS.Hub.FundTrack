using FundTrack.Application.Dtos.Organization;

namespace FundTrack.Application.Dtos.Dashboard;

public class DashboardDto
{
    public PeriodDto? Period { get; set; }
    /// <summary>True si el período sigue abierto y ya pasó su fecha límite.</summary>
    public bool IsOverdue { get; set; }
    public DashboardRegionRowDto Totals { get; set; } = new();
    public List<DashboardRegionRowDto> Regions { get; set; } = [];
    /// <summary>Períodos abiertos cuya fecha límite ya pasó y siguen sin cerrarse.</summary>
    public List<PeriodDto> OverduePeriods { get; set; } = [];
}

namespace FundTrack.Application.Dtos.Dashboard;

/// <summary>
/// Fila de sp_get_dashboard_by_region (Scrips/storedProcedures.sql): el resumen de un mes en una
/// región. También se usa para el total general, sumando las filas de todas las regiones.
/// </summary>
public class DashboardRegionRowDto
{
    public long? RegionId { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public int ActiveChurches { get; set; }
    public int Submitted { get; set; }
    public int NotSubmitted { get; set; }
    /// <summary>Iglesias activas que todavía no tienen arqueo registrado ese mes.</summary>
    public int Missing { get; set; }
    public int DifferencesPending { get; set; }
    public int DifferencesJustified { get; set; }
    public int DifferencesResolved { get; set; }
    /// <summary>Arqueos con saldo ACMS negativo: la iglesia gastó más de lo que tenía.</summary>
    public int NegativeAcms { get; set; }
    public decimal TotalAcmsBalance { get; set; }
    public decimal TotalCounted { get; set; }
    public decimal TotalDifference { get; set; }
}

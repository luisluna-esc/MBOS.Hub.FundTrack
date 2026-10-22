namespace FundTrack.core.QueryFilters.Reports;

/// <summary>
/// Hoja "Arqueo del fondo local": un arqueo (LocalFundCountId, el botón Imprimir del registro) o los
/// de un mes (PeriodId) de una iglesia o de todo un distrito, una hoja por iglesia.
/// </summary>
public class LocalFundCountVoucherQueryFilter
{
    public long? LocalFundCountId { get; set; }
    public long? PeriodId { get; set; }
    public long? DistrictId { get; set; }
    public long? ChurchId { get; set; }
}

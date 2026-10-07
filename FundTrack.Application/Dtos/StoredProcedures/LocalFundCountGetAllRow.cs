using FundTrack.Application.Dtos.Funds;

namespace FundTrack.Application.Dtos.StoredProcedures;

/// <summary>
/// Fila del resultado de sp_get_local_fund_counts (Scrips/storedProcedures.sql). Usada únicamente
/// por LocalFundCountService (listado y detalle). Mismos campos que LocalFundCountDto más el total.
/// </summary>
public class LocalFundCountGetAllRow : LocalFundCountDto
{
    public int TotalRecords { get; set; }
}

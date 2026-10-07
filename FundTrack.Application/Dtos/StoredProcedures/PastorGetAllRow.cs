using FundTrack.Application.Dtos.Organization;

namespace FundTrack.Application.Dtos.StoredProcedures;

/// <summary>
/// Fila del resultado de sp_get_pastors (Scrips/storedProcedures.sql). Usada únicamente por
/// PastorService.GetAllPastors. Mismos campos que PastorDto más el total de registros.
/// </summary>
public class PastorGetAllRow : PastorDto
{
    public int TotalRecords { get; set; }
}

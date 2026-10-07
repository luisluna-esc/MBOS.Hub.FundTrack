using FundTrack.Application.Dtos.Organization;

namespace FundTrack.Application.Dtos.StoredProcedures;

/// <summary>
/// Fila del resultado de sp_get_churches (Scrips/storedProcedures.sql). Usada únicamente por
/// ChurchService.GetAllChurches. Mismos campos que ChurchDto más el total de registros.
/// </summary>
public class ChurchGetAllRow : ChurchDto
{
    public int TotalRecords { get; set; }
}

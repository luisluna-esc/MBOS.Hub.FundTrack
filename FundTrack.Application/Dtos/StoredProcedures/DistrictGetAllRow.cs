using FundTrack.Application.Dtos.Organization;

namespace FundTrack.Application.Dtos.StoredProcedures;

/// <summary>
/// Fila del resultado de sp_get_districts (Scrips/storedProcedures.sql). Usada únicamente por
/// DistrictService.GetAllDistricts para mapear el SqlQueryRaw del procedimiento almacenado.
/// Mismos campos que DistrictDto más el total de registros de la página.
/// </summary>
public class DistrictGetAllRow : DistrictDto
{
    public int TotalRecords { get; set; }
}

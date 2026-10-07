namespace FundTrack.Application.Interfaces.Security;

/// <summary>Resuelve qué parte de los datos puede ver el usuario actual.</summary>
public interface IDataScopeService
{
    /// <summary>
    /// Null si el usuario ve todo. Si solo tiene el rol Pastor, el id del distrito que tiene
    /// a cargo hoy; si no tiene distrito asignado devuelve 0, que no coincide con ninguno.
    /// </summary>
    Task<long?> GetRestrictedDistrictId();
}

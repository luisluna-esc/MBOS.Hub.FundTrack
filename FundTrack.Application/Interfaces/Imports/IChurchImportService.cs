using FundTrack.core.Entities.ResponseApi.DisplayFormat;

namespace FundTrack.Application.Interfaces.Imports;

public interface IChurchImportService
{
    /// <summary>La plantilla .xlsx vacía, con las columnas y una hoja de instrucciones.</summary>
    byte[] GetTemplate();

    /// <summary>
    /// Lee el Excel y crea las regiones, distritos, pastores, iglesias y asignaciones de pastor
    /// que no existan. Con dryRun solo revisa y devuelve lo que haría. Todo o nada.
    /// </summary>
    Task<ResponseGetObject> Import(Stream content, bool dryRun, DateOnly pastorStartDate);
}

namespace FundTrack.Application.Interfaces.Funds;

/// <summary>Dónde se guardan físicamente los adjuntos. Las rutas son relativas a la carpeta raíz configurada.</summary>
public interface IFileStorage
{
    /// <summary>Guarda el contenido y devuelve la ruta relativa con la que se vuelve a leer.</summary>
    Task<string> Save(string folder, string extension, Stream content);
    Stream? Open(string relativePath);
    void Delete(string relativePath);
}

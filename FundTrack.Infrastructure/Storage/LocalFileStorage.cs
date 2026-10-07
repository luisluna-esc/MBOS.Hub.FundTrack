using FundTrack.Application.Interfaces.Funds;
using FundTrack.core.Configuration;

namespace FundTrack.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(AttachmentOptions attachmentOptions)
    {
        _rootPath = Path.GetFullPath(Path.IsPathRooted(attachmentOptions.RootPath)
            ? attachmentOptions.RootPath
            : Path.Combine(Directory.GetCurrentDirectory(), attachmentOptions.RootPath));
    }

    public async Task<string> Save(string folder, string extension, Stream content)
    {
        // El nombre en disco es siempre generado: el nombre original solo se guarda en la base,
        // así un nombre malicioso nunca llega a usarse como ruta.
        var relativePath = Path.Combine(folder, $"{Guid.NewGuid():N}{extension}");
        var fullPath = ResolveInsideRoot(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream);

        return relativePath.Replace('\\', '/');
    }

    public Stream? Open(string relativePath)
    {
        var fullPath = ResolveInsideRoot(relativePath);
        return File.Exists(fullPath) ? File.OpenRead(fullPath) : null;
    }

    public void Delete(string relativePath)
    {
        var fullPath = ResolveInsideRoot(relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    private string ResolveInsideRoot(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        if (!fullPath.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Attachment path escapes the storage root.");

        return fullPath;
    }
}

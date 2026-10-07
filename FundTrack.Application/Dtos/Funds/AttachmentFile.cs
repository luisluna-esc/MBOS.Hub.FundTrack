namespace FundTrack.Application.Dtos.Funds;

/// <summary>El contenido de un adjunto listo para devolverlo como descarga.</summary>
public record AttachmentFile(Stream Content, string MimeType, string FileName);

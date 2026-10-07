namespace FundTrack.core.Configuration;

public class AttachmentOptions
{
    /// <summary>Carpeta donde se guardan los respaldos del arqueo. Si es relativa, se resuelve desde la carpeta del backend.</summary>
    public string RootPath { get; set; } = "App_Data/attachments";
    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024;
    public string[] AllowedContentTypes { get; set; } = ["application/pdf", "image/jpeg", "image/png"];
}

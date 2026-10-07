namespace FundTrack.core.Entities.Funds;

public class LocalFundCountAttachment
{
    public long LocalFundCountAttachmentId { get; set; }
    public long LocalFundCountId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public long? AppUserId { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}

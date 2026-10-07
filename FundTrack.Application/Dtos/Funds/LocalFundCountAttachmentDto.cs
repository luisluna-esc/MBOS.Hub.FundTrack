namespace FundTrack.Application.Dtos.Funds;

public class LocalFundCountAttachmentDto
{
    public long LocalFundCountAttachmentId { get; set; }
    public long LocalFundCountId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public long? AppUserId { get; set; }
    public string? AppUserFullName { get; set; }
    public DateTime RegisteredAt { get; set; }
}

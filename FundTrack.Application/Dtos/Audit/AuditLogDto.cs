namespace FundTrack.Application.Dtos.Audit;

public class AuditLogDto
{
    public long AuditLogId { get; set; }
    public long? AppUserId { get; set; }
    public string? AppUserFullName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Module { get; set; }
    public string TableName { get; set; } = string.Empty;
    public long? RecordId { get; set; }
    public string? Description { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}

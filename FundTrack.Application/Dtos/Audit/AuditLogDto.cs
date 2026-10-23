namespace FundTrack.Application.Dtos.Audit;

public class AuditLogDto
{
    public long AuditLogId { get; set; }
    public long? AppUserId { get; set; }
    public string? AppUserFullName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public long? RecordId { get; set; }
    /// <summary>De qué registro se trata: la iglesia y el mes de un arqueo, el nombre de una iglesia, etc.</summary>
    public string? RecordLabel { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

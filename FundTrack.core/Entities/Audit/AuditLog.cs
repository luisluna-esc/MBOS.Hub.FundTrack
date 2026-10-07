namespace FundTrack.core.Entities.Audit;

public class AuditLog
{
    public long AuditLogId { get; set; }
    public long? AppUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Module { get; set; }
    public string TableName { get; set; } = string.Empty;
    public long? RecordId { get; set; }
    public string? Description { get; set; }
    /// <summary>JSON con los valores antes del cambio (null en INSERT).</summary>
    public string? OldValue { get; set; }
    /// <summary>JSON con los valores después del cambio (null en DELETE).</summary>
    public string? NewValue { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

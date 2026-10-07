using FundTrack.Application.Dtos.Audit;

namespace FundTrack.Application.Dtos.StoredProcedures;

/// <summary>
/// Fila del resultado de sp_get_audit_logs (Scrips/storedProcedures.sql). Usada únicamente por
/// AuditLogService.GetAllAuditLogs. Mismos campos que AuditLogDto más el total de registros.
/// </summary>
public class AuditLogGetAllRow : AuditLogDto
{
    public int TotalRecords { get; set; }
}

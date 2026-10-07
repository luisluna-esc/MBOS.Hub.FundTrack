namespace FundTrack.Application.Dtos.StoredProcedures;

/// <summary>Resultado de sp_purge_audit_logs (Scrips/storedProcedures.sql).</summary>
public class PurgeAuditLogsRow
{
    public int DeletedRows { get; set; }
}

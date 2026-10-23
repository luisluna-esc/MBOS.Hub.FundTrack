using System.Net;
using FundTrack.Application.Dtos.Audit;
using FundTrack.Application.Dtos.StoredProcedures;
using FundTrack.Application.Interfaces.Audit;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Configuration;
using FundTrack.core.Entities.Audit;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Interfaces;
using FundTrack.core.QueryFilters.Audit;
using FundTrack.core.QueryFilters.Pagination;

namespace FundTrack.Application.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly AuditRetentionOptions _auditRetentionOptions;
    private readonly ICurrentUserService _currentUserService;

    public AuditLogService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        AuditRetentionOptions auditRetentionOptions,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _auditRetentionOptions = auditRetentionOptions;
        _currentUserService = currentUserService;
    }

    public async Task LogReportAccess(string reportName, string action, string description)
    {
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            AppUserId = _currentUserService.AppUserId,
            Action = action,
            Module = "reports",
            TableName = reportName,
            Description = description.Length > 500 ? description[..500] : description,
            Ip = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            CreatedAt = DateTime.UtcNow
        });
        await _unitOfWork.SaveChangesAsync();
    }

    private const string PurgeAuditLogsSql = """
        SELECT deleted_rows AS "DeletedRows" FROM sp_purge_audit_logs({0}::int)
        """;

    private const string GetAuditLogsSql = """
        SELECT
            audit_log_id AS "AuditLogId",
            app_user_id AS "AppUserId",
            app_user_full_name AS "AppUserFullName",
            action AS "Action",
            table_name AS "TableName",
            record_id AS "RecordId",
            record_label AS "RecordLabel",
            old_value AS "OldValue",
            new_value AS "NewValue",
            created_at AS "CreatedAt",
            total_records AS "TotalRecords"
        FROM sp_get_audit_logs(
            {0}::bigint, {1}::varchar, {2}::bigint, {3}::varchar, {4}::int, {5}::int
        )
        """;

    public async Task<ResponseGetObject> GetAllAuditLogs(PaginationQueryFilter paginationQueryFilter, AuditLogQueryFilter auditLogQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        // Una sola consulta (nombre del usuario en join) via sp_get_audit_logs (Scrips/storedProcedures.sql).
        var rows = await _unitOfWork.SqlQueryAsync<AuditLogGetAllRow>(
            GetAuditLogsSql,
            (object?)auditLogQueryFilter.AppUserId ?? DBNull.Value,
            (object?)auditLogQueryFilter.TableName ?? DBNull.Value,
            (object?)auditLogQueryFilter.RecordId ?? DBNull.Value,
            (object?)auditLogQueryFilter.Action ?? DBNull.Value,
            pageNumber,
            pageSize);

        var totalRecords = rows.Count > 0 ? rows[0].TotalRecords : 0;

        return new ResponseGetObject
        {
            Data = new PagedResult<AuditLogDto>
            {
                Items = rows,
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<int> PurgeExpiredLogs()
    {
        // Nunca menos de un año, aunque alguien configure 0 por error: sería borrar toda la auditoría.
        var retentionYears = Math.Max(1, _auditRetentionOptions.RetentionYears);

        var rows = await _unitOfWork.SqlQueryAsync<PurgeAuditLogsRow>(PurgeAuditLogsSql, retentionYears);
        return rows.Count > 0 ? rows[0].DeletedRows : 0;
    }
}

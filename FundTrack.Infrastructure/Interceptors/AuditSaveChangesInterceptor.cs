using System.Text.Json;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Entities.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FundTrack.Infrastructure.Interceptors;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    // Valores que nunca deben quedar legibles en la auditoría.
    private static readonly HashSet<string> SensitiveProperties = ["PasswordHash", "Token", "TokenHash"];
    private const string MaskedValue = "***";

    private readonly ICurrentUserService _currentUserService;
    private List<PendingAuditEntry> _pendingEntries = [];

    public AuditSaveChangesInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            // Los valores anteriores se toman antes de guardar: después de SaveChanges
            // EF ya los reemplazó por los nuevos.
            _pendingEntries = eventData.Context.ChangeTracker.Entries()
                .Where(e => e.Entity is not AuditLog && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(e => new PendingAuditEntry(
                    e,
                    ResolveAction(e.State),
                    e.Metadata.GetTableName() ?? e.Metadata.ClrType.Name,
                    e.State is EntityState.Modified or EntityState.Deleted ? SerializeValues(e, original: true) : null,
                    e.State == EntityState.Deleted))
                .ToList();
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null && _pendingEntries.Count > 0)
        {
            var appUserId = _currentUserService.AppUserId;
            var ip = _currentUserService.IpAddress;
            var userAgent = _currentUserService.UserAgent;

            // Los valores nuevos se toman después de guardar, así un INSERT ya trae su id generado.
            var logs = _pendingEntries.Select(pending => new AuditLog
            {
                AppUserId = appUserId,
                Action = pending.Action,
                TableName = pending.TableName,
                RecordId = GetRecordId(pending.Entry),
                OldValue = pending.OldValue,
                NewValue = pending.IsDelete ? null : SerializeValues(pending.Entry, original: false),
                Ip = ip,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _pendingEntries = [];

            eventData.Context.Set<AuditLog>().AddRange(logs);
            await eventData.Context.SaveChangesAsync(cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private static string ResolveAction(EntityState state) => state switch
    {
        EntityState.Added => "INSERT",
        EntityState.Modified => "UPDATE",
        EntityState.Deleted => "DELETE",
        _ => string.Empty
    };

    private static long? GetRecordId(EntityEntry entry)
    {
        // Las tablas con llave compuesta (app_user_role, menu_role) no tienen un único id;
        // sus columnas quedan igual en old_value / new_value.
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null || primaryKey.Properties.Count != 1)
            return null;

        var value = entry.Property(primaryKey.Properties[0].Name).CurrentValue;
        return value switch
        {
            long longValue => longValue,
            int intValue => intValue,
            _ => null
        };
    }

    private static string SerializeValues(EntityEntry entry, bool original)
    {
        var values = entry.Properties.ToDictionary(
            p => p.Metadata.Name,
            p => SensitiveProperties.Contains(p.Metadata.Name)
                ? MaskedValue
                : original ? p.OriginalValue : p.CurrentValue);

        return JsonSerializer.Serialize(values);
    }

    private record PendingAuditEntry(EntityEntry Entry, string Action, string TableName, string? OldValue, bool IsDelete);
}

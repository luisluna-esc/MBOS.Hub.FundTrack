using FundTrack.Application.Interfaces.Audit;
using FundTrack.core.Configuration;
using Microsoft.Extensions.Options;

namespace FundTrack.Web.BackgroundServices;

/// <summary>Borra la auditoría vencida al arrancar el backend y después cada IntervalHours.</summary>
public class AuditRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AuditRetentionOptions _auditRetentionOptions;
    private readonly ILogger<AuditRetentionBackgroundService> _logger;

    public AuditRetentionBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AuditRetentionOptions> auditRetentionOptions,
        ILogger<AuditRetentionBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _auditRetentionOptions = auditRetentionOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(Math.Max(1, _auditRetentionOptions.IntervalHours)));

        do
        {
            try
            {
                // El servicio es scoped (usa el DbContext), por eso se crea un scope por ejecución.
                using var scope = _serviceScopeFactory.CreateScope();
                var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

                var deletedRows = await auditLogService.PurgeExpiredLogs();
                if (deletedRows > 0)
                {
                    _logger.LogInformation("Audit retention: {DeletedRows} audit log rows older than {Years} years deleted.",
                        deletedRows, _auditRetentionOptions.RetentionYears);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Un fallo de limpieza no debe tumbar el backend; se reintenta en la próxima vuelta.
                _logger.LogError(exception, "Audit retention purge failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

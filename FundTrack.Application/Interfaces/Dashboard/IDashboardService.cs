using FundTrack.core.Entities.ResponseApi.DisplayFormat;

namespace FundTrack.Application.Interfaces.Dashboard;

public interface IDashboardService
{
    /// <summary>Resumen del período indicado, o del mes en curso si no se indica.</summary>
    Task<ResponseGetObject> GetSummary(long? periodId);
}

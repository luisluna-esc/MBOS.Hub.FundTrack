using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.QueryFilters.Charts;

namespace FundTrack.Application.Interfaces.Charts;

public interface IChartService
{
    /// <summary>Los datos de la pantalla Gráficas: el año mes a mes, por región y las iglesias con más faltante.</summary>
    Task<ResponseGetObject> GetCharts(ChartsQueryFilter filter);
}

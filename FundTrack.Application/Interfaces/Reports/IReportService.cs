using FundTrack.Application.Dtos.Reports;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.QueryFilters.Reports;

namespace FundTrack.Application.Interfaces.Reports;

public interface IReportService
{
    /// <summary>Fondo Local del año: saldo ACMS, arqueo y diferencia por iglesia y mes (reemplaza el Excel).</summary>
    Task<ResponseGetObject> GetLocalFundYearReport(LocalFundYearReportQueryFilter filter);
    Task<ReportResult> ExportLocalFundYearReport(LocalFundYearReportQueryFilter filter, ReportFormat format);

    /// <summary>Pendientes de un mes: iglesias sin arqueo, que no presentaron o con diferencia sin resolver.</summary>
    Task<ResponseGetObject> GetPendingReport(PendingReportQueryFilter filter);
    Task<ReportResult> ExportPendingReport(PendingReportQueryFilter filter, ReportFormat format);
}

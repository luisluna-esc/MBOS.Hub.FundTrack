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

    /// <summary>Diferencias del año por estado, con días abierta y último seguimiento.</summary>
    Task<ReportResult> ExportDifferencesReport(DifferencesReportQueryFilter filter, ReportFormat format);

    /// <summary>Porcentaje de arqueos registrados a tiempo por distrito y pastor, mes a mes.</summary>
    Task<ReportResult> ExportComplianceReport(ComplianceReportQueryFilter filter, ReportFormat format);

    /// <summary>Iglesias con saldo ACMS negativo y cuántos meses seguidos les pasó.</summary>
    Task<ReportResult> ExportNegativeAcmsReport(NegativeAcmsReportQueryFilter filter, ReportFormat format);

    /// <summary>El resumen del Inicio para un rango de meses, por región.</summary>
    Task<ReportResult> ExportRegionSummaryReport(RegionSummaryReportQueryFilter filter, ReportFormat format);

    /// <summary>Correcciones aplicadas a arqueos de meses cerrados.</summary>
    Task<ReportResult> ExportCorrectionsReport(CorrectionsReportQueryFilter filter, ReportFormat format);

    /// <summary>Cierres, fusiones y cambios de distrito, tipo o nombre de iglesias entre dos fechas.</summary>
    Task<ReportResult> ExportChurchMovementsReport(ChurchMovementsReportQueryFilter filter, ReportFormat format);

    /// <summary>Reporte individual de una iglesia: su saldo ACMS y su arqueo mes a mes en el rango elegido.</summary>
    Task<ReportResult> ExportChurchStatementReport(ChurchStatementReportQueryFilter filter, ReportFormat format);

    /// <summary>Una gráfica de la pantalla Gráficas como reporte: la gráfica y su tabla.</summary>
    Task<ReportResult> ExportChartReport(ChartReportRequestDto request, ReportFormat format);

    /// <summary>La hoja "Arqueo del fondo local" con las tres firmas: un arqueo o los de un mes de un distrito.</summary>
    Task<ReportResult> ExportLocalFundCountVoucher(LocalFundCountVoucherQueryFilter filter, ReportFormat format = ReportFormat.Pdf);
}

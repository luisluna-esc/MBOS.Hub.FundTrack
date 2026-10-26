using FundTrack.Application.Dtos.Reports;
using FundTrack.Application.Interfaces.Reports;
using FundTrack.core.QueryFilters.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundTrack.Web.Controllers;

// Todos los roles ven los reportes (el pastor solo de su distrito, lo filtra el servicio).
// Cada consulta y cada descarga queda en la auditoría.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("local-fund")]
    public async Task<IActionResult> GetLocalFundYear([FromQuery] LocalFundYearReportQueryFilter filter)
    {
        var result = await _reportService.GetLocalFundYearReport(filter);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("local-fund/pdf")]
    public Task<IActionResult> GetLocalFundYearPdf([FromQuery] LocalFundYearReportQueryFilter filter) =>
        Export(_reportService.ExportLocalFundYearReport(filter, ReportFormat.Pdf));

    [HttpGet("local-fund/excel")]
    public Task<IActionResult> GetLocalFundYearExcel([FromQuery] LocalFundYearReportQueryFilter filter) =>
        Export(_reportService.ExportLocalFundYearReport(filter, ReportFormat.Excel));

    [HttpGet("local-fund/txt")]
    public Task<IActionResult> GetLocalFundYearTxt([FromQuery] LocalFundYearReportQueryFilter filter) =>
        Export(_reportService.ExportLocalFundYearReport(filter, ReportFormat.Txt));

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending([FromQuery] PendingReportQueryFilter filter)
    {
        var result = await _reportService.GetPendingReport(filter);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("pending/pdf")]
    public Task<IActionResult> GetPendingPdf([FromQuery] PendingReportQueryFilter filter) =>
        Export(_reportService.ExportPendingReport(filter, ReportFormat.Pdf));

    [HttpGet("pending/excel")]
    public Task<IActionResult> GetPendingExcel([FromQuery] PendingReportQueryFilter filter) =>
        Export(_reportService.ExportPendingReport(filter, ReportFormat.Excel));

    [HttpGet("pending/txt")]
    public Task<IActionResult> GetPendingTxt([FromQuery] PendingReportQueryFilter filter) =>
        Export(_reportService.ExportPendingReport(filter, ReportFormat.Txt));

    [HttpGet("differences/pdf")]
    public Task<IActionResult> GetDifferencesPdf([FromQuery] DifferencesReportQueryFilter filter) =>
        Export(_reportService.ExportDifferencesReport(filter, ReportFormat.Pdf));

    [HttpGet("differences/excel")]
    public Task<IActionResult> GetDifferencesExcel([FromQuery] DifferencesReportQueryFilter filter) =>
        Export(_reportService.ExportDifferencesReport(filter, ReportFormat.Excel));

    [HttpGet("differences/txt")]
    public Task<IActionResult> GetDifferencesTxt([FromQuery] DifferencesReportQueryFilter filter) =>
        Export(_reportService.ExportDifferencesReport(filter, ReportFormat.Txt));

    [HttpGet("compliance/pdf")]
    public Task<IActionResult> GetCompliancePdf([FromQuery] ComplianceReportQueryFilter filter) =>
        Export(_reportService.ExportComplianceReport(filter, ReportFormat.Pdf));

    [HttpGet("compliance/excel")]
    public Task<IActionResult> GetComplianceExcel([FromQuery] ComplianceReportQueryFilter filter) =>
        Export(_reportService.ExportComplianceReport(filter, ReportFormat.Excel));

    [HttpGet("compliance/txt")]
    public Task<IActionResult> GetComplianceTxt([FromQuery] ComplianceReportQueryFilter filter) =>
        Export(_reportService.ExportComplianceReport(filter, ReportFormat.Txt));

    [HttpGet("negative-acms/pdf")]
    public Task<IActionResult> GetNegativeAcmsPdf([FromQuery] NegativeAcmsReportQueryFilter filter) =>
        Export(_reportService.ExportNegativeAcmsReport(filter, ReportFormat.Pdf));

    [HttpGet("negative-acms/excel")]
    public Task<IActionResult> GetNegativeAcmsExcel([FromQuery] NegativeAcmsReportQueryFilter filter) =>
        Export(_reportService.ExportNegativeAcmsReport(filter, ReportFormat.Excel));

    [HttpGet("negative-acms/txt")]
    public Task<IActionResult> GetNegativeAcmsTxt([FromQuery] NegativeAcmsReportQueryFilter filter) =>
        Export(_reportService.ExportNegativeAcmsReport(filter, ReportFormat.Txt));

    [HttpGet("region-summary/pdf")]
    public Task<IActionResult> GetRegionSummaryPdf([FromQuery] RegionSummaryReportQueryFilter filter) =>
        Export(_reportService.ExportRegionSummaryReport(filter, ReportFormat.Pdf));

    [HttpGet("region-summary/excel")]
    public Task<IActionResult> GetRegionSummaryExcel([FromQuery] RegionSummaryReportQueryFilter filter) =>
        Export(_reportService.ExportRegionSummaryReport(filter, ReportFormat.Excel));

    [HttpGet("region-summary/txt")]
    public Task<IActionResult> GetRegionSummaryTxt([FromQuery] RegionSummaryReportQueryFilter filter) =>
        Export(_reportService.ExportRegionSummaryReport(filter, ReportFormat.Txt));

    [HttpGet("corrections/pdf")]
    public Task<IActionResult> GetCorrectionsPdf([FromQuery] CorrectionsReportQueryFilter filter) =>
        Export(_reportService.ExportCorrectionsReport(filter, ReportFormat.Pdf));

    [HttpGet("corrections/excel")]
    public Task<IActionResult> GetCorrectionsExcel([FromQuery] CorrectionsReportQueryFilter filter) =>
        Export(_reportService.ExportCorrectionsReport(filter, ReportFormat.Excel));

    [HttpGet("corrections/txt")]
    public Task<IActionResult> GetCorrectionsTxt([FromQuery] CorrectionsReportQueryFilter filter) =>
        Export(_reportService.ExportCorrectionsReport(filter, ReportFormat.Txt));

    [HttpGet("church-movements/pdf")]
    public Task<IActionResult> GetChurchMovementsPdf([FromQuery] ChurchMovementsReportQueryFilter filter) =>
        Export(_reportService.ExportChurchMovementsReport(filter, ReportFormat.Pdf));

    [HttpGet("church-movements/excel")]
    public Task<IActionResult> GetChurchMovementsExcel([FromQuery] ChurchMovementsReportQueryFilter filter) =>
        Export(_reportService.ExportChurchMovementsReport(filter, ReportFormat.Excel));

    [HttpGet("church-movements/txt")]
    public Task<IActionResult> GetChurchMovementsTxt([FromQuery] ChurchMovementsReportQueryFilter filter) =>
        Export(_reportService.ExportChurchMovementsReport(filter, ReportFormat.Txt));

    [HttpGet("church-statement/pdf")]
    public Task<IActionResult> GetChurchStatementPdf([FromQuery] ChurchStatementReportQueryFilter filter) =>
        Export(_reportService.ExportChurchStatementReport(filter, ReportFormat.Pdf));

    [HttpGet("church-statement/excel")]
    public Task<IActionResult> GetChurchStatementExcel([FromQuery] ChurchStatementReportQueryFilter filter) =>
        Export(_reportService.ExportChurchStatementReport(filter, ReportFormat.Excel));

    [HttpGet("church-statement/txt")]
    public Task<IActionResult> GetChurchStatementTxt([FromQuery] ChurchStatementReportQueryFilter filter) =>
        Export(_reportService.ExportChurchStatementReport(filter, ReportFormat.Txt));

    /// <summary>Reporte de una gráfica (botón Reporte de la pantalla Gráficas). format: pdf, excel o txt.</summary>
    [HttpPost("chart/{format}")]
    public Task<IActionResult> PostChartReport(string format, [FromBody] ChartReportRequestDto request) =>
        Export(_reportService.ExportChartReport(request, format switch
        {
            "excel" => ReportFormat.Excel,
            "txt" => ReportFormat.Txt,
            _ => ReportFormat.Pdf
        }));

    [HttpGet("local-fund-count/pdf")]
    public Task<IActionResult> GetLocalFundCountVoucher([FromQuery] LocalFundCountVoucherQueryFilter filter) =>
        Export(_reportService.ExportLocalFundCountVoucher(filter, ReportFormat.Pdf));

    [HttpGet("local-fund-count/excel")]
    public Task<IActionResult> GetLocalFundCountExcel([FromQuery] LocalFundCountVoucherQueryFilter filter) =>
        Export(_reportService.ExportLocalFundCountVoucher(filter, ReportFormat.Excel));

    [HttpGet("local-fund-count/txt")]
    public Task<IActionResult> GetLocalFundCountTxt([FromQuery] LocalFundCountVoucherQueryFilter filter) =>
        Export(_reportService.ExportLocalFundCountVoucher(filter, ReportFormat.Txt));

    private async Task<IActionResult> Export(Task<ReportResult> export)
    {
        var result = await export;
        return result.File is { } file
            ? File(file.Content, file.ContentType, file.FileName)
            : StatusCode((int)result.Error!.StatusCode, result.Error);
    }
}

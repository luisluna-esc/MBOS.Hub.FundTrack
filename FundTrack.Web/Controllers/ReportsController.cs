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

    [HttpGet("differences/pdf")]
    public Task<IActionResult> GetDifferencesPdf([FromQuery] DifferencesReportQueryFilter filter) =>
        Export(_reportService.ExportDifferencesReport(filter, ReportFormat.Pdf));

    [HttpGet("differences/excel")]
    public Task<IActionResult> GetDifferencesExcel([FromQuery] DifferencesReportQueryFilter filter) =>
        Export(_reportService.ExportDifferencesReport(filter, ReportFormat.Excel));

    [HttpGet("compliance/pdf")]
    public Task<IActionResult> GetCompliancePdf([FromQuery] ComplianceReportQueryFilter filter) =>
        Export(_reportService.ExportComplianceReport(filter, ReportFormat.Pdf));

    [HttpGet("compliance/excel")]
    public Task<IActionResult> GetComplianceExcel([FromQuery] ComplianceReportQueryFilter filter) =>
        Export(_reportService.ExportComplianceReport(filter, ReportFormat.Excel));

    [HttpGet("negative-acms/pdf")]
    public Task<IActionResult> GetNegativeAcmsPdf([FromQuery] NegativeAcmsReportQueryFilter filter) =>
        Export(_reportService.ExportNegativeAcmsReport(filter, ReportFormat.Pdf));

    [HttpGet("negative-acms/excel")]
    public Task<IActionResult> GetNegativeAcmsExcel([FromQuery] NegativeAcmsReportQueryFilter filter) =>
        Export(_reportService.ExportNegativeAcmsReport(filter, ReportFormat.Excel));

    [HttpGet("region-summary/pdf")]
    public Task<IActionResult> GetRegionSummaryPdf([FromQuery] RegionSummaryReportQueryFilter filter) =>
        Export(_reportService.ExportRegionSummaryReport(filter, ReportFormat.Pdf));

    [HttpGet("region-summary/excel")]
    public Task<IActionResult> GetRegionSummaryExcel([FromQuery] RegionSummaryReportQueryFilter filter) =>
        Export(_reportService.ExportRegionSummaryReport(filter, ReportFormat.Excel));

    [HttpGet("corrections/pdf")]
    public Task<IActionResult> GetCorrectionsPdf([FromQuery] CorrectionsReportQueryFilter filter) =>
        Export(_reportService.ExportCorrectionsReport(filter, ReportFormat.Pdf));

    [HttpGet("corrections/excel")]
    public Task<IActionResult> GetCorrectionsExcel([FromQuery] CorrectionsReportQueryFilter filter) =>
        Export(_reportService.ExportCorrectionsReport(filter, ReportFormat.Excel));

    [HttpGet("church-movements/pdf")]
    public Task<IActionResult> GetChurchMovementsPdf([FromQuery] ChurchMovementsReportQueryFilter filter) =>
        Export(_reportService.ExportChurchMovementsReport(filter, ReportFormat.Pdf));

    [HttpGet("church-movements/excel")]
    public Task<IActionResult> GetChurchMovementsExcel([FromQuery] ChurchMovementsReportQueryFilter filter) =>
        Export(_reportService.ExportChurchMovementsReport(filter, ReportFormat.Excel));

    [HttpGet("local-fund-count/pdf")]
    public Task<IActionResult> GetLocalFundCountVoucher([FromQuery] LocalFundCountVoucherQueryFilter filter) =>
        Export(_reportService.ExportLocalFundCountVoucher(filter));

    private async Task<IActionResult> Export(Task<ReportResult> export)
    {
        var result = await export;
        return result.File is { } file
            ? File(file.Content, file.ContentType, file.FileName)
            : StatusCode((int)result.Error!.StatusCode, result.Error);
    }
}

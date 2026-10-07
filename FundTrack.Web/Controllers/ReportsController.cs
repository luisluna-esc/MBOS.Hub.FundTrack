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

    private async Task<IActionResult> Export(Task<ReportResult> export)
    {
        var result = await export;
        return result.File is { } file
            ? File(file.Content, file.ContentType, file.FileName)
            : StatusCode((int)result.Error!.StatusCode, result.Error);
    }
}

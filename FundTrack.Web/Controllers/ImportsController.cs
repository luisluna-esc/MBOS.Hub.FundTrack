using System.Net;
using FundTrack.Application.Interfaces.Imports;
using FundTrack.core.Constants;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundTrack.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.Writers)]
public class ImportsController : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const long MaxImportSizeBytes = 5 * 1024 * 1024;

    private readonly IChurchImportService _churchImportService;

    public ImportsController(IChurchImportService churchImportService)
    {
        _churchImportService = churchImportService;
    }

    [HttpGet("churches/template")]
    public IActionResult GetChurchTemplate()
    {
        return File(_churchImportService.GetTemplate(), XlsxContentType, "plantilla_iglesias.xlsx");
    }

    /// <param name="dryRun">True: solo revisa el archivo y dice qué crearía, sin guardar nada.</param>
    /// <param name="pastorStartDate">Desde cuándo rige la asignación pastor y distrito. Por defecto, el 1 de enero del año actual.</param>
    [HttpPost("churches")]
    [RequestSizeLimit(MaxImportSizeBytes + 1024 * 1024)]
    public async Task<IActionResult> ImportChurches(IFormFile file, [FromQuery] bool dryRun = true, [FromQuery] DateOnly? pastorStartDate = null)
    {
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) || file.Length > MaxImportSizeBytes)
        {
            var invalid = new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Sube la plantilla en formato .xlsx (máximo 5 MB)." }],
                StatusCode = HttpStatusCode.BadRequest
            };
            return StatusCode((int)invalid.StatusCode, invalid);
        }

        var startDate = pastorStartDate ?? new DateOnly(DateTime.UtcNow.Year, 1, 1);

        // ClosedXML necesita poder moverse por el archivo, por eso se copia a memoria primero.
        using var content = new MemoryStream();
        await file.CopyToAsync(content);
        content.Position = 0;

        var result = await _churchImportService.Import(content, dryRun, startDate);
        return StatusCode((int)result.StatusCode, result);
    }
}

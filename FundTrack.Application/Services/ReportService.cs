using System.Globalization;
using System.Net;
using FundTrack.Application.Dtos.Reports;
using FundTrack.Application.Interfaces.Audit;
using FundTrack.Application.Interfaces.Reports;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Constants;
using FundTrack.core.Entities.Organization;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Entities.Security;
using FundTrack.core.Interfaces;
using FundTrack.core.QueryFilters.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FundTrack.Application.Services;

// Reportes de FundTrack, mismo patrón que CheckInvoice: los datos salen de un procedimiento
// almacenado (una consulta), el PDF se arma aquí con QuestPDF y el Excel en ReportService.Excel.cs.
// Cada consulta o exportación queda en la auditoría, como pidió la Misión.
public partial class ReportService : IReportService
{
    private static readonly CultureInfo ReportCulture = new("es-ES");
    private static readonly string[] MonthNames =
        ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
    private static byte[]? _logoBytes;

    private const string LocalFundYearReportName = "report_local_fund_year";
    private const string PendingReportName = "report_pending";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDataScopeService _dataScopeService;
    private readonly IAuditLogService _auditLogService;

    public ReportService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDataScopeService dataScopeService,
        IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _dataScopeService = dataScopeService;
        _auditLogService = auditLogService;
    }

    private const string GetLocalFundYearSql = """
        SELECT
            region_name AS "RegionName",
            district_name AS "DistrictName",
            pastor_name AS "PastorName",
            church_id AS "ChurchId",
            church_code AS "ChurchCode",
            church_name AS "ChurchName",
            church_type AS "ChurchType",
            month AS "Month",
            submission_status AS "SubmissionStatus",
            acms_balance AS "AcmsBalance",
            total_amount AS "TotalAmount",
            difference AS "Difference",
            difference_status AS "DifferenceStatus"
        FROM sp_report_local_fund_year({0}::int, {1}::int, {2}::int, {3}::bigint, {4}::bigint, {5}::bigint, {6}::bigint)
        """;

    private const string GetPendingSql = """
        SELECT
            region_name AS "RegionName",
            district_name AS "DistrictName",
            pastor_name AS "PastorName",
            church_id AS "ChurchId",
            church_code AS "ChurchCode",
            church_name AS "ChurchName",
            church_type AS "ChurchType",
            pending_kind AS "PendingKind",
            acms_balance AS "AcmsBalance",
            total_amount AS "TotalAmount",
            difference AS "Difference",
            difference_status AS "DifferenceStatus"
        FROM sp_report_pending({0}::bigint, {1}::bigint, {2}::bigint, {3}::bigint, {4}::bigint)
        """;

    // ---------------------------------------------------------------------------------------
    // Fondo Local del año
    // ---------------------------------------------------------------------------------------

    public async Task<ResponseGetObject> GetLocalFundYearReport(LocalFundYearReportQueryFilter filter)
    {
        var (report, error) = await LoadLocalFundYear(filter);
        if (report is null)
            return error!;

        await _auditLogService.LogReportAccess(LocalFundYearReportName, "VIEW", $"Fondo Local {report.Year} ({report.Scope})");

        return new ResponseGetObject { Data = report, Messages = [], StatusCode = HttpStatusCode.OK };
    }

    public async Task<ReportResult> ExportLocalFundYearReport(LocalFundYearReportQueryFilter filter, ReportFormat format)
    {
        var (report, error) = await LoadLocalFundYear(filter);
        if (report is null)
            return new ReportResult(null, error);

        var username = await CurrentUserFullNameAsync();
        var fileBase = $"fondo-local-{report.Year}";

        var file = format == ReportFormat.Pdf
            ? new ReportFile(BuildLocalFundYearPdf(report, username), PdfContentType, $"{fileBase}.pdf")
            : SpreadsheetFile(BuildLocalFundYearExcel(report, username), format, fileBase);

        await _auditLogService.LogReportAccess(LocalFundYearReportName, "EXPORT", $"Fondo Local {report.Year} ({report.Scope}) en {format}");
        return new ReportResult(file, null);
    }

    private async Task<(LocalFundYearReportDto? Report, ResponseGetObject? Error)> LoadLocalFundYear(LocalFundYearReportQueryFilter filter)
    {
        var monthFrom = filter.MonthFrom ?? 1;
        var monthTo = filter.MonthTo ?? 12;

        if (filter.Year is < 2000 or > 2100 || monthFrom is < 1 or > 12 || monthTo is < 1 or > 12 || monthFrom > monthTo)
        {
            return (null, BadRequest("Indica un año válido y un rango de meses entre 1 y 12 (el mes inicial no puede ser mayor que el final)."));
        }

        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();

        var rows = await _unitOfWork.SqlQueryAsync<LocalFundYearReportRowDto>(
            GetLocalFundYearSql,
            filter.Year,
            monthFrom,
            monthTo,
            (object?)filter.RegionId ?? DBNull.Value,
            (object?)filter.DistrictId ?? DBNull.Value,
            (object?)restrictedDistrictId ?? DBNull.Value,
            (object?)filter.ChurchId ?? DBNull.Value);

        // De una fila por iglesia y mes a una fila por iglesia con los meses en columnas.
        var churches = rows
            .GroupBy(r => r.ChurchId)
            .Select(g =>
            {
                var first = g.First();
                var byMonth = g.Where(r => r.Month.HasValue).ToDictionary(r => r.Month!.Value);
                return new LocalFundYearChurchDto
                {
                    RegionName = first.RegionName,
                    DistrictName = first.DistrictName,
                    PastorName = first.PastorName,
                    ChurchId = first.ChurchId,
                    ChurchCode = first.ChurchCode,
                    ChurchName = first.ChurchName,
                    ChurchType = first.ChurchType,
                    Months = Enumerable.Range(monthFrom, monthTo - monthFrom + 1)
                        .Select(month => byMonth.TryGetValue(month, out var r)
                            ? new LocalFundMonthDto
                            {
                                Month = month,
                                SubmissionStatus = r.SubmissionStatus ?? string.Empty,
                                AcmsBalance = r.AcmsBalance ?? 0,
                                TotalAmount = r.TotalAmount ?? 0,
                                Difference = r.Difference ?? 0,
                                DifferenceStatus = r.DifferenceStatus
                            }
                            : null)
                        .ToList()
                };
            })
            .OrderBy(c => c.RegionName, StringComparer.Create(ReportCulture, false))
            .ThenBy(c => c.DistrictName, StringComparer.Create(ReportCulture, false))
            .ThenBy(c => c.ChurchName, StringComparer.Create(ReportCulture, false))
            .ToList();

        return (new LocalFundYearReportDto
        {
            Year = filter.Year,
            MonthFrom = monthFrom,
            MonthTo = monthTo,
            Scope = await ScopeTextAsync(filter.RegionId, filter.DistrictId, restrictedDistrictId),
            Churches = churches
        }, null);
    }

    private byte[] BuildLocalFundYearPdf(LocalFundYearReportDto report, string username)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var months = Enumerable.Range(report.MonthFrom, report.MonthTo - report.MonthFrom + 1).ToList();
        var columnCount = 5 + months.Count * 3;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                // Hasta 12 meses x 3 columnas: A3 apaisado para que entre como en el Excel.
                page.Size(months.Count > 4 ? PageSizes.A3.Landscape() : PageSizes.Letter.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(6.5f));

                page.Header().Element(c => ComposeHeader(c,
                    "DETALLE DE SALDO ACMS VS ARQUEO DE FONDO LOCAL",
                    [$"Año {report.Year}: {MonthNames[report.MonthFrom - 1]} a {MonthNames[report.MonthTo - 1]}", report.Scope]));

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(18);
                        columns.ConstantColumn(28);
                        columns.RelativeColumn(4);
                        columns.ConstantColumn(30);
                        columns.RelativeColumn(3);
                        foreach (var _ in months)
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1.6f);
                        }
                    });

                    table.Header(header =>
                    {
                        header.Cell().RowSpan(2).Element(HeaderCell).Text("N°");
                        header.Cell().RowSpan(2).Element(HeaderCell).Text("Cód.");
                        header.Cell().RowSpan(2).Element(HeaderCell).Text("Iglesia");
                        header.Cell().RowSpan(2).Element(HeaderCell).Text("Tipo");
                        header.Cell().RowSpan(2).Element(HeaderCell).Text("Pastor");
                        foreach (var month in months)
                        {
                            header.Cell().ColumnSpan(3).Element(HeaderCell).AlignCenter().Text(MonthNames[month - 1].ToUpper(ReportCulture));
                        }
                        foreach (var _ in months)
                        {
                            header.Cell().Element(HeaderCell).AlignRight().Text("ACMS");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Arqueo");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Dif.");
                        }
                    });

                    var number = 0;
                    foreach (var region in report.Churches.GroupBy(c => c.RegionName))
                    {
                        table.Cell().ColumnSpan((uint)columnCount).Element(c => c.PaddingTop(6).PaddingBottom(2))
                            .Text(region.Key).Bold().FontSize(9);

                        foreach (var district in region.GroupBy(c => c.DistrictName))
                        {
                            table.Cell().ColumnSpan((uint)columnCount).Element(c => c.PaddingTop(3).PaddingBottom(1))
                                .Text($"Distrito {district.Key}").Bold().FontSize(7.5f);

                            foreach (var church in district)
                            {
                                number++;
                                table.Cell().Element(BodyCell).Text(number.ToString(ReportCulture));
                                table.Cell().Element(BodyCell).Text(church.ChurchCode);
                                table.Cell().Element(BodyCell).Text(church.ChurchName);
                                table.Cell().Element(BodyCell).Text(church.ChurchType);
                                table.Cell().Element(BodyCell).Text(church.PastorName ?? "");

                                foreach (var month in church.Months)
                                {
                                    if (month is null)
                                    {
                                        // Así se ve de un vistazo qué iglesias faltan cada mes.
                                        table.Cell().ColumnSpan(3).Element(BodyCell).AlignCenter()
                                            .Text("Sin arqueo").Italic().FontColor(Colors.Grey.Darken1);
                                        continue;
                                    }

                                    table.Cell().Element(BodyCell).AlignRight().Text(Money(month.AcmsBalance));
                                    if (month.SubmissionStatus == SubmissionStatuses.NotSubmitted)
                                    {
                                        table.Cell().ColumnSpan(2).Element(BodyCell).AlignCenter().Text("No presentó").Italic();
                                    }
                                    else
                                    {
                                        table.Cell().Element(BodyCell).AlignRight().Text(Money(month.TotalAmount));
                                        var differenceCell = table.Cell().Element(BodyCell).AlignRight().Text(Money(month.Difference));
                                        if (month.Difference != 0)
                                            differenceCell.Bold().FontColor(month.Difference < 0 ? Colors.Red.Darken2 : Colors.Blue.Darken2);
                                    }
                                }
                            }

                            AddTotalsRow(table, $"Total distrito {district.Key}", district, months.Count, SubtotalCell);
                        }

                        AddTotalsRow(table, $"Total {region.Key}", region, months.Count, CategoryTotalCell);
                    }

                    if (report.Churches.Count > 0)
                    {
                        AddTotalsRow(table, "TOTAL GENERAL", report.Churches, months.Count, CategoryTotalCell);
                    }
                    else
                    {
                        table.Cell().ColumnSpan((uint)columnCount).Element(BodyCell).AlignCenter().Text("No hay iglesias para los filtros elegidos.");
                    }
                });

                page.Footer().Element(c => ComposeFooter(c, username));
            });
        });

        return document.GeneratePdf();
    }

    /// <summary>
    /// Fila de totales en dinero y, debajo, cuántas iglesias presentaron su arqueo ese mes de
    /// cuántas hay en el grupo (por ejemplo "8 de 10"). Las que faltan se ven en su fila como
    /// "Sin arqueo" o "No presentó".
    /// </summary>
    private static void AddTotalsRow(TableDescriptor table, string label, IEnumerable<LocalFundYearChurchDto> churches, int monthCount, Func<IContainer, IContainer> style)
    {
        var list = churches.ToList();
        table.Cell().ColumnSpan(5).Element(style).AlignRight().Text(label).Bold();

        for (var i = 0; i < monthCount; i++)
        {
            var monthValues = list.Select(c => c.Months[i]).Where(m => m is not null).ToList();
            table.Cell().Element(style).AlignRight().Text(Money(monthValues.Sum(m => m!.AcmsBalance))).Bold();
            table.Cell().Element(style).AlignRight().Text(Money(monthValues.Where(m => m!.SubmissionStatus != SubmissionStatuses.NotSubmitted).Sum(m => m!.TotalAmount))).Bold();
            table.Cell().Element(style).AlignRight().Text(Money(monthValues.Sum(m => m!.Difference))).Bold();
        }

        table.Cell().ColumnSpan(5).Element(style).AlignRight().Text("Presentaron su arqueo").Italic();
        for (var i = 0; i < monthCount; i++)
        {
            var presented = list.Count(c => c.Months[i]?.SubmissionStatus == SubmissionStatuses.Submitted);
            var cell = table.Cell().ColumnSpan(3).Element(style).AlignCenter().Text($"{presented} de {list.Count}").Bold();
            if (presented < list.Count)
                cell.FontColor(presented * 2 < list.Count ? DangerColor : "#B45309");
        }
    }

    // ---------------------------------------------------------------------------------------
    // Pendientes del mes
    // ---------------------------------------------------------------------------------------

    public async Task<ResponseGetObject> GetPendingReport(PendingReportQueryFilter filter)
    {
        var (report, error) = await LoadPending(filter);
        if (report is null)
            return error!;

        await _auditLogService.LogReportAccess(PendingReportName, "VIEW", $"Pendientes {report.Month}/{report.Year} ({report.Scope})");

        return new ResponseGetObject { Data = report, Messages = [], StatusCode = HttpStatusCode.OK };
    }

    public async Task<ReportResult> ExportPendingReport(PendingReportQueryFilter filter, ReportFormat format)
    {
        var (report, error) = await LoadPending(filter);
        if (report is null)
            return new ReportResult(null, error);

        var username = await CurrentUserFullNameAsync();
        var fileBase = $"pendientes-{report.Year}-{report.Month:00}";

        var file = format == ReportFormat.Pdf
            ? new ReportFile(BuildPendingPdf(report, username), PdfContentType, $"{fileBase}.pdf")
            : SpreadsheetFile(BuildPendingExcel(report, username), format, fileBase);

        await _auditLogService.LogReportAccess(PendingReportName, "EXPORT", $"Pendientes {report.Month}/{report.Year} ({report.Scope}) en {format}");
        return new ReportResult(file, null);
    }

    private async Task<(PendingReportDto? Report, ResponseGetObject? Error)> LoadPending(PendingReportQueryFilter filter)
    {
        var period = filter.PeriodId > 0 ? await _unitOfWork.Repository<Period>().GetByIdAsync(filter.PeriodId) : null;
        if (period is null)
        {
            return (null, new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Period not found." }],
                StatusCode = HttpStatusCode.NotFound
            });
        }

        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();

        var rows = await _unitOfWork.SqlQueryAsync<PendingReportRowDto>(
            GetPendingSql,
            period.PeriodId,
            (object?)filter.RegionId ?? DBNull.Value,
            (object?)filter.DistrictId ?? DBNull.Value,
            (object?)restrictedDistrictId ?? DBNull.Value,
            (object?)filter.ChurchId ?? DBNull.Value);

        return (new PendingReportDto
        {
            Year = period.Year,
            Month = period.Month,
            PeriodStatus = period.Status,
            Scope = await ScopeTextAsync(filter.RegionId, filter.DistrictId, restrictedDistrictId),
            MissingCount = rows.Count(r => r.PendingKind == "missing"),
            NotSubmittedCount = rows.Count(r => r.PendingKind == "not_submitted"),
            DifferenceCount = rows.Count(r => r.PendingKind == "difference"),
            Rows = rows
        }, null);
    }

    private byte[] BuildPendingPdf(PendingReportDto report, string username)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(8.5f));

                page.Header().Element(c => ComposeHeader(c,
                    "PENDIENTES DE FONDO LOCAL",
                    [
                        $"Mes: {MonthNames[report.Month - 1]} {report.Year} ({(report.PeriodStatus == PeriodStatuses.Closed ? "cerrado" : "abierto")})",
                        report.Scope,
                        $"Sin arqueo registrado: {report.MissingCount}   No presentaron: {report.NotSubmittedCount}   Diferencias sin resolver: {report.DifferenceCount}"
                    ]));

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(40);
                        columns.RelativeColumn(4);
                        columns.ConstantColumn(45);
                        columns.RelativeColumn(3);
                        columns.ConstantColumn(85);
                        columns.ConstantColumn(65);
                        columns.ConstantColumn(65);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Código");
                        header.Cell().Element(HeaderCell).Text("Iglesia");
                        header.Cell().Element(HeaderCell).Text("Tipo");
                        header.Cell().Element(HeaderCell).Text("Pastor");
                        header.Cell().Element(HeaderCell).Text("Situación");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Saldo ACMS");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Arqueo");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Diferencia");
                        header.Cell().Element(HeaderCell).Text("Estado");
                    });

                    if (report.Rows.Count == 0)
                    {
                        table.Cell().ColumnSpan(9).Element(BodyCell).AlignCenter().Text("No hay pendientes para este mes.");
                    }

                    foreach (var region in report.Rows.GroupBy(r => r.RegionName))
                    {
                        table.Cell().ColumnSpan(9).Element(c => c.PaddingTop(8).PaddingBottom(2)).Text(region.Key).Bold().FontSize(10);

                        foreach (var district in region.GroupBy(r => r.DistrictName))
                        {
                            table.Cell().ColumnSpan(9).Element(c => c.PaddingTop(3).PaddingBottom(1))
                                .Text($"Distrito {district.Key} ({district.Count()})").Bold();

                            foreach (var row in district)
                            {
                                table.Cell().Element(BodyCell).Text(row.ChurchCode);
                                table.Cell().Element(BodyCell).Text(row.ChurchName);
                                table.Cell().Element(BodyCell).Text(row.ChurchType);
                                table.Cell().Element(BodyCell).Text(row.PastorName ?? "");
                                table.Cell().Element(BodyCell).Text(PendingKindLabel(row.PendingKind));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.AcmsBalance.HasValue ? Money(row.AcmsBalance.Value) : "");
                                table.Cell().Element(BodyCell).AlignRight().Text(row.PendingKind == "difference" && row.TotalAmount.HasValue ? Money(row.TotalAmount.Value) : "");
                                table.Cell().Element(BodyCell).AlignRight().Text(row.PendingKind == "difference" && row.Difference.HasValue ? Money(row.Difference.Value) : "");
                                table.Cell().Element(BodyCell).Text(row.PendingKind == "difference" ? DifferenceStatusLabel(row.DifferenceStatus) : "");
                            }
                        }
                    }
                });

                page.Footer().Element(c => ComposeFooter(c, username));
            });
        });

        return document.GeneratePdf();
    }

    // ---------------------------------------------------------------------------------------
    // Piezas comunes
    // ---------------------------------------------------------------------------------------

    private const string PdfContentType = "application/pdf";
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Encabezado institucional igual en todos los reportes.</summary>
    private static void ComposeHeader(IContainer container, string title, IEnumerable<string> info)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(11);
                    c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL SUR").Bold().FontSize(11);
                    c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(11);
                });
                row.ConstantItem(140).Height(45).Image(GetLogoBytes()).FitArea();
            });

            column.Item().PaddingTop(8).AlignCenter().Text(title).Bold().FontSize(13);
            foreach (var line in info.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                column.Item().AlignCenter().Text(line).FontSize(9);
            }
            column.Item().AlignCenter().Text("(Expresado en Bolivianos)").FontSize(8).Italic();
        });
    }

    private static void ComposeFooter(IContainer container, string username)
    {
        container.PaddingTop(5).Row(row =>
        {
            row.RelativeItem().Text($"Usuario: {username}").FontSize(7);
            row.RelativeItem().AlignCenter().Text($"{BoliviaTime.Now:dd/MM/yyyy HH:mm}").FontSize(7);
            row.RelativeItem().AlignRight().Text(x =>
            {
                x.Span("Pág. ").FontSize(7);
                x.CurrentPageNumber().FontSize(7);
                x.Span(" de ").FontSize(7);
                x.TotalPages().FontSize(7);
            });
        });
    }

    private async Task<string> ScopeTextAsync(long? regionId, long? districtId, long? restrictedDistrictId)
    {
        var effectiveDistrictId = restrictedDistrictId ?? districtId;
        if (effectiveDistrictId.HasValue)
        {
            var district = await _unitOfWork.Repository<District>().GetByIdAsync(effectiveDistrictId.Value);
            return district is null ? "Distrito sin asignar" : $"Distrito: {district.Name}";
        }

        if (regionId.HasValue)
        {
            var region = await _unitOfWork.Repository<Region>().GetByIdAsync(regionId.Value);
            return region is null ? "Región no encontrada" : $"Región: {region.Name}";
        }

        return "Toda la Misión";
    }

    private async Task<string> CurrentUserFullNameAsync()
    {
        if (!_currentUserService.AppUserId.HasValue)
            return "";

        var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
        return user?.FullName ?? "";
    }

    private static string Money(decimal value) => value.ToString("N2", ReportCulture);

    private static string PendingKindLabel(string kind) => kind switch
    {
        "missing" => "Sin arqueo registrado",
        "not_submitted" => "No presentó",
        "difference" => "Diferencia",
        _ => kind
    };

    private static string DifferenceStatusLabel(string? status) => status switch
    {
        DifferenceStatuses.Pending => "Pendiente",
        DifferenceStatuses.Resolved => "Resuelta",
        DifferenceStatuses.None => "Sin diferencia",
        _ => ""
    };

    private static ResponseGetObject BadRequest(string description) => new()
    {
        Data = new(),
        Messages = [new Message { Type = MessageType.Error, Description = description }],
        StatusCode = HttpStatusCode.BadRequest
    };

    private static byte[] GetLogoBytes()
    {
        if (_logoBytes is not null)
            return _logoBytes;

        using var stream = typeof(ReportService).Assembly
            .GetManifestResourceStream("FundTrack.Application.Resources.mbos-logo.png")
            ?? throw new InvalidOperationException("Logo embebido (Resources/mbos-logo.png) no encontrado.");

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        _logoBytes = memoryStream.ToArray();
        return _logoBytes;
    }

    // Mismas celdas que CheckInvoice: grilla completa y encabezados a la misma altura.
    private static IContainer HeaderCell(IContainer container) =>
        container.Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.Grey.Lighten3).Padding(3).MinHeight(18).AlignMiddle().DefaultTextStyle(x => x.Bold());

    private static IContainer BodyCell(IContainer container) =>
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2);

    private static IContainer SubtotalCell(IContainer container) =>
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten2).Padding(2);

    private static IContainer CategoryTotalCell(IContainer container) =>
        container.Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.Grey.Medium).Padding(2);
}

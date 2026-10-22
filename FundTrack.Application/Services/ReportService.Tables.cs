using FundTrack.Application.Dtos.Reports;
using FundTrack.core.Constants;
using FundTrack.core.QueryFilters.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FundTrack.Application.Services;

// Los 6 reportes en tabla (diferencias, cumplimiento, ACMS negativo, resumen por región,
// correcciones y movimientos de iglesias). Cada uno lee un procedimiento almacenado
// (sp_report_*, una sola consulta) y describe sus columnas una vez: el mismo armado sirve para el
// PDF (BuildTablePdf) y para el Excel (AddSheet, en ReportService.Excel.cs).
public partial class ReportService
{
    private sealed record PdfColumn<T>(
        string Header,
        float Width,
        Func<T, string> Text,
        bool AlignRight = false,
        Func<T, decimal>? Total = null,
        bool IntegerTotal = false,
        Func<T, string?>? FontColor = null);

    private const string DangerColor = "#B91C1C";
    private const string InfoColor = "#0369A1";

    private static string? DifferenceColor(decimal difference) => difference < 0 ? DangerColor : difference > 0 ? InfoColor : null;

    // ---------------------------------------------------------------------------------------
    // Diferencias por estado
    // ---------------------------------------------------------------------------------------

    private const string GetDifferencesSql = """
        SELECT
            region_name AS "RegionName", district_name AS "DistrictName", pastor_name AS "PastorName",
            church_code AS "ChurchCode", church_name AS "ChurchName", church_type AS "ChurchType",
            period_month AS "PeriodMonth", acms_balance AS "AcmsBalance", total_amount AS "TotalAmount",
            difference AS "Difference", difference_status AS "DifferenceStatus", observation AS "Observation",
            registered_at AS "RegisteredAt", last_follow_up_at AS "LastFollowUpAt",
            last_justification AS "LastJustification", last_follow_up_by AS "LastFollowUpBy", resolved_at AS "ResolvedAt"
        FROM sp_report_differences({0}::int, {1}::varchar, {2}::bigint, {3}::bigint, {4}::bigint)
        """;

    public async Task<ReportResult> ExportDifferencesReport(DifferencesReportQueryFilter filter, ReportFormat format)
    {
        if (!ValidYear(filter.Year))
            return new ReportResult(null, BadRequest("Indica un año válido."));

        var status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status;
        if (status is not null && status is not (DifferenceStatuses.Pending or DifferenceStatuses.Resolved))
            return new ReportResult(null, BadRequest("El estado debe ser pendiente o resuelta."));

        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();
        var rows = await _unitOfWork.SqlQueryAsync<DifferencesReportRowDto>(
            GetDifferencesSql,
            filter.Year,
            (object?)status ?? DBNull.Value,
            (object?)filter.RegionId ?? DBNull.Value,
            (object?)filter.DistrictId ?? DBNull.Value,
            (object?)restrictedDistrictId ?? DBNull.Value);

        var scope = await ScopeTextAsync(filter.RegionId, filter.DistrictId, restrictedDistrictId);
        string[] info =
        [
            $"Año {filter.Year}: {(status is null ? "todas las diferencias" : $"diferencias {DifferenceStatusLabel(status).ToLower(ReportCulture)}s")}",
            scope,
            $"Pendientes: {rows.Count(r => r.DifferenceStatus == DifferenceStatuses.Pending)}   Resueltas: {rows.Count(r => r.DifferenceStatus == DifferenceStatuses.Resolved)}"
        ];

        var today = BoliviaTime.Today;
        int DaysOpen(DifferencesReportRowDto r) =>
            Math.Max(0, (int)((r.ResolvedAt.HasValue ? BoliviaTime.FromUtc(r.ResolvedAt.Value).Date : today) - BoliviaTime.FromUtc(r.RegisteredAt).Date).TotalDays);
        string LastFollowUp(DifferencesReportRowDto r) =>
            r.LastFollowUpAt.HasValue ? $"{BoliviaTime.FromUtc(r.LastFollowUpAt.Value):dd/MM/yyyy} {r.LastFollowUpBy}: {r.LastJustification}" : "";

        var pdfColumns = new List<PdfColumn<DifferencesReportRowDto>>
        {
            new("Mes", 1.4f, r => MonthNames[r.PeriodMonth - 1]),
            new("Código", 1f, r => r.ChurchCode),
            new("Iglesia", 3f, r => r.ChurchName),
            new("Pastor", 2.6f, r => r.PastorName ?? ""),
            new("Saldo ACMS", 1.6f, r => Money(r.AcmsBalance), true, r => r.AcmsBalance),
            new("En custodia", 1.6f, r => Money(r.TotalAmount), true, r => r.TotalAmount),
            new("Diferencia", 1.6f, r => Money(r.Difference), true, r => r.Difference, FontColor: r => DifferenceColor(r.Difference)),
            new("Estado", 1.4f, r => DifferenceStatusLabel(r.DifferenceStatus)),
            new("Días", 0.8f, r => DaysOpen(r).ToString(ReportCulture), true),
            new("Observación", 3f, r => r.Observation ?? ""),
            new("Último seguimiento", 4f, LastFollowUp)
        };

        var excelColumns = new List<ExcelColumn<DifferencesReportRowDto>>
        {
            new("Región", r => r.RegionName),
            new("Distrito", r => r.DistrictName),
            new("Pastor", r => r.PastorName),
            new("Mes", r => MonthNames[r.PeriodMonth - 1]),
            new("Código", r => r.ChurchCode),
            new("Iglesia", r => r.ChurchName),
            new("Tipo", r => r.ChurchType),
            new("Saldo ACMS", r => r.AcmsBalance, MoneyFormat, Total: true),
            new("Total en custodia", r => r.TotalAmount, MoneyFormat, Total: true),
            new("Diferencia", r => r.Difference, MoneyFormat, Total: true),
            new("Estado", r => DifferenceStatusLabel(r.DifferenceStatus)),
            new("Días abierta", r => DaysOpen(r)),
            new("Observación", r => r.Observation),
            new("Último seguimiento", r => LastFollowUp(r))
        };

        return await Export(format, "report_differences", $"diferencias-{filter.Year}", "DIFERENCIAS DE FONDO LOCAL", info,
            rows, pdfColumns, excelColumns, r => $"{r.RegionName}  ·  Distrito {r.DistrictName}", $"Diferencias {filter.Year} ({scope})");
    }

    // ---------------------------------------------------------------------------------------
    // Cumplimiento por distrito y pastor
    // ---------------------------------------------------------------------------------------

    private const string GetComplianceSql = """
        SELECT
            region_name AS "RegionName", district_name AS "DistrictName", pastor_name AS "PastorName",
            period_month AS "PeriodMonth", due_date AS "DueDate", expected AS "Expected",
            registered AS "Registered", on_time AS "OnTime", not_submitted AS "NotSubmitted"
        FROM sp_report_compliance({0}::int, {1}::bigint, {2}::bigint, {3}::bigint)
        """;

    public async Task<ReportResult> ExportComplianceReport(ComplianceReportQueryFilter filter, ReportFormat format)
    {
        if (!ValidYear(filter.Year))
            return new ReportResult(null, BadRequest("Indica un año válido."));

        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();
        // Los meses que todavía no vencen no cuentan: aún se puede registrar a tiempo.
        var today = DateOnly.FromDateTime(BoliviaTime.Today);
        var rows = (await _unitOfWork.SqlQueryAsync<ComplianceReportRowDto>(
                GetComplianceSql,
                filter.Year,
                (object?)filter.RegionId ?? DBNull.Value,
                (object?)filter.DistrictId ?? DBNull.Value,
                (object?)restrictedDistrictId ?? DBNull.Value))
            .Where(r => r.DueDate < today)
            .ToList();

        var scope = await ScopeTextAsync(filter.RegionId, filter.DistrictId, restrictedDistrictId);
        string[] info =
        [
            $"Año {filter.Year}: arqueos registrados hasta la fecha límite de cada mes (solo meses ya vencidos)",
            scope,
            $"A tiempo: {rows.Sum(r => r.OnTime)} de {rows.Sum(r => r.Expected)} ({Percent(rows.Sum(r => r.OnTime), rows.Sum(r => r.Expected))})"
        ];

        var username = await CurrentUserFullNameAsync();
        var fileBase = $"cumplimiento-{filter.Year}";
        ReportFile file;

        if (format == ReportFormat.Pdf)
        {
            file = new ReportFile(BuildCompliancePdf(rows, info, username), PdfContentType, $"{fileBase}.pdf");
        }
        else
        {
            var columns = new List<ExcelColumn<ComplianceReportRowDto>>
            {
                new("Región", r => r.RegionName),
                new("Distrito", r => r.DistrictName),
                new("Pastor", r => r.PastorName),
                new("Mes", r => MonthNames[r.PeriodMonth - 1]),
                new("Fecha límite", r => r.DueDate.ToDateTime(TimeOnly.MinValue), "dd/MM/yyyy"),
                new("Debían registrar", r => r.Expected, Total: true),
                new("Registrados", r => r.Registered, Total: true),
                new("A tiempo", r => r.OnTime, Total: true),
                new("Fuera de plazo", r => r.Registered - r.OnTime, Total: true),
                new("No presentaron", r => r.NotSubmitted, Total: true),
                new("Sin registrar", r => Math.Max(0, r.Expected - r.Registered), Total: true),
                new("% a tiempo", r => r.Expected == 0 ? 0 : (double)r.OnTime / r.Expected, "0%")
            };
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            AddSheet(workbook, $"Cumplimiento {filter.Year}", "CUMPLIMIENTO DE ARQUEOS", info, columns, rows, username);
            file = new ReportFile(SaveWorkbook(workbook), ExcelContentType, $"{fileBase}.xlsx");
        }

        await _auditLogService.LogReportAccess("report_compliance", "EXPORT", $"Cumplimiento {filter.Year} ({scope}) en {format}");
        return new ReportResult(file, null);
    }

    /// <summary>Una fila por distrito con el porcentaje a tiempo de cada mes y el del año.</summary>
    private byte[] BuildCompliancePdf(List<ComplianceReportRowDto> rows, IReadOnlyList<string> info, string username)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var months = rows.Select(r => r.PeriodMonth).Distinct().Order().ToList();
        var districts = rows.GroupBy(r => (r.RegionName, r.DistrictName)).ToList();
        var columnCount = 3 + months.Count + 1;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(7.5f));
                page.Header().Element(c => ComposeHeader(c, "CUMPLIMIENTO DE ARQUEOS", info));

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.2f);
                        columns.RelativeColumn(2.6f);
                        columns.RelativeColumn(3.2f);
                        foreach (var _ in months) columns.RelativeColumn(1f);
                        columns.RelativeColumn(2f);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Región");
                        header.Cell().Element(HeaderCell).Text("Distrito");
                        header.Cell().Element(HeaderCell).Text("Pastor");
                        foreach (var month in months)
                            header.Cell().Element(HeaderCell).AlignCenter().Text(MonthNames[month - 1][..3]);
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Año (a tiempo)");
                    });

                    if (districts.Count == 0)
                    {
                        table.Cell().ColumnSpan((uint)columnCount).Element(BodyCell).AlignCenter().Text("Todavía no hay meses vencidos para estos filtros.");
                    }

                    foreach (var district in districts)
                    {
                        var byMonth = district.ToDictionary(r => r.PeriodMonth);
                        table.Cell().Element(BodyCell).Text(district.Key.RegionName);
                        table.Cell().Element(BodyCell).Text(district.Key.DistrictName);
                        table.Cell().Element(BodyCell).Text(district.First().PastorName ?? "");
                        foreach (var month in months)
                        {
                            var cell = byMonth.TryGetValue(month, out var r) ? r : null;
                            var text = cell is null || cell.Expected == 0 ? "" : Percent(cell.OnTime, cell.Expected);
                            var span = table.Cell().Element(BodyCell).AlignCenter().Text(text);
                            if (cell is not null && cell.Expected > 0 && cell.OnTime < cell.Expected)
                                span.FontColor(cell.OnTime * 2 < cell.Expected ? DangerColor : "#B45309");
                        }
                        var onTime = district.Sum(r => r.OnTime);
                        var expected = district.Sum(r => r.Expected);
                        table.Cell().Element(BodyCell).AlignCenter().Text($"{onTime}/{expected} ({Percent(onTime, expected)})").Bold();
                    }
                });

                page.Footer().Element(c => ComposeFooter(c, username));
            });
        });

        return document.GeneratePdf();
    }

    // ---------------------------------------------------------------------------------------
    // Saldo ACMS negativo
    // ---------------------------------------------------------------------------------------

    private const string GetNegativeAcmsSql = """
        SELECT
            region_name AS "RegionName", district_name AS "DistrictName", pastor_name AS "PastorName",
            church_id AS "ChurchId", church_code AS "ChurchCode", church_name AS "ChurchName", church_type AS "ChurchType",
            period_month AS "PeriodMonth", acms_balance AS "AcmsBalance"
        FROM sp_report_negative_acms({0}::int, {1}::int, {2}::int, {3}::bigint, {4}::bigint, {5}::bigint)
        """;

    public async Task<ReportResult> ExportNegativeAcmsReport(NegativeAcmsReportQueryFilter filter, ReportFormat format)
    {
        var monthFrom = filter.MonthFrom ?? 1;
        var monthTo = filter.MonthTo ?? 12;
        if (!ValidYear(filter.Year) || !ValidMonths(monthFrom, monthTo))
            return new ReportResult(null, BadRequest("Indica un año válido y un rango de meses entre 1 y 12."));

        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();
        var rows = await _unitOfWork.SqlQueryAsync<NegativeAcmsReportRowDto>(
            GetNegativeAcmsSql,
            filter.Year, monthFrom, monthTo,
            (object?)filter.RegionId ?? DBNull.Value,
            (object?)filter.DistrictId ?? DBNull.Value,
            (object?)restrictedDistrictId ?? DBNull.Value);

        var churches = rows
            .GroupBy(r => r.ChurchId)
            .Select(g =>
            {
                var months = g.OrderBy(r => r.PeriodMonth).ToList();
                var last = months[^1];
                return new NegativeAcmsChurchDto
                {
                    RegionName = last.RegionName,
                    DistrictName = last.DistrictName,
                    PastorName = last.PastorName,
                    ChurchCode = last.ChurchCode,
                    ChurchName = last.ChurchName,
                    ChurchType = last.ChurchType,
                    Months = months.Select(m => m.PeriodMonth).ToList(),
                    LongestStreak = LongestStreak(months.Select(m => m.PeriodMonth).ToList()),
                    LastMonth = last.PeriodMonth,
                    LastBalance = last.AcmsBalance,
                    LowestBalance = months.Min(m => m.AcmsBalance)
                };
            })
            .OrderBy(c => c.RegionName, StringComparer.Create(ReportCulture, false))
            .ThenBy(c => c.DistrictName, StringComparer.Create(ReportCulture, false))
            .ThenByDescending(c => c.LongestStreak)
            .ThenBy(c => c.ChurchName, StringComparer.Create(ReportCulture, false))
            .ToList();

        var scope = await ScopeTextAsync(filter.RegionId, filter.DistrictId, restrictedDistrictId);
        string[] info =
        [
            $"Año {filter.Year}: {MonthNames[monthFrom - 1]} a {MonthNames[monthTo - 1]}",
            scope,
            $"Iglesias con saldo negativo: {churches.Count}   Con 3 meses o más seguidos: {churches.Count(c => c.LongestStreak >= 3)}"
        ];

        string MonthList(NegativeAcmsChurchDto c) => string.Join(", ", c.Months.Select(m => MonthNames[m - 1][..3]));

        var pdfColumns = new List<PdfColumn<NegativeAcmsChurchDto>>
        {
            new("Código", 1f, c => c.ChurchCode),
            new("Iglesia", 3f, c => c.ChurchName),
            new("Tipo", 1f, c => c.ChurchType),
            new("Pastor", 2.6f, c => c.PastorName ?? ""),
            new("Meses en negativo", 3.4f, MonthList),
            new("Seguidos", 1f, c => c.LongestStreak.ToString(ReportCulture), true, FontColor: c => c.LongestStreak >= 3 ? DangerColor : null),
            new("Último mes", 1.4f, c => MonthNames[c.LastMonth - 1]),
            new("Último saldo", 1.6f, c => Money(c.LastBalance), true, c => c.LastBalance, FontColor: _ => DangerColor),
            new("Saldo más bajo", 1.6f, c => Money(c.LowestBalance), true, FontColor: _ => DangerColor)
        };

        var excelColumns = new List<ExcelColumn<NegativeAcmsChurchDto>>
        {
            new("Región", c => c.RegionName),
            new("Distrito", c => c.DistrictName),
            new("Pastor", c => c.PastorName),
            new("Código", c => c.ChurchCode),
            new("Iglesia", c => c.ChurchName),
            new("Tipo", c => c.ChurchType),
            new("Meses en negativo", c => MonthList(c)),
            new("Cantidad de meses", c => c.Months.Count),
            new("Meses seguidos", c => c.LongestStreak),
            new("Último mes", c => MonthNames[c.LastMonth - 1]),
            new("Último saldo ACMS", c => c.LastBalance, MoneyFormat, Total: true),
            new("Saldo más bajo", c => c.LowestBalance, MoneyFormat)
        };

        return await Export(format, "report_negative_acms", $"acms-negativo-{filter.Year}", "IGLESIAS CON SALDO ACMS NEGATIVO", info,
            churches, pdfColumns, excelColumns, c => $"{c.RegionName}  ·  Distrito {c.DistrictName}", $"ACMS negativo {filter.Year} ({scope})");
    }

    /// <summary>La racha más larga de meses consecutivos (los meses vienen ordenados).</summary>
    private static int LongestStreak(List<int> months)
    {
        var longest = 0;
        var current = 0;
        for (var i = 0; i < months.Count; i++)
        {
            current = i > 0 && months[i] == months[i - 1] + 1 ? current + 1 : 1;
            longest = Math.Max(longest, current);
        }
        return longest;
    }

    // ---------------------------------------------------------------------------------------
    // Resumen mensual por región o por distrito
    // ---------------------------------------------------------------------------------------

    private const string SummaryCountColumns = """
            active_churches AS "ActiveChurches", submitted AS "Submitted", not_submitted AS "NotSubmitted",
            missing AS "Missing", differences_pending AS "DifferencesPending",
            differences_resolved AS "DifferencesResolved", negative_acms AS "NegativeAcms",
            total_acms_balance AS "TotalAcmsBalance", total_counted AS "TotalCounted", total_difference AS "TotalDifference"
        """;

    private const string GetRegionSummarySql = $$"""
        SELECT period_month AS "PeriodMonth", region_name AS "RegionName", {{SummaryCountColumns}}
        FROM sp_report_region_summary({0}::int, {1}::int, {2}::int, {3}::bigint)
        """;

    private const string GetDistrictSummarySql = $$"""
        SELECT period_month AS "PeriodMonth", region_name AS "RegionName", district_name AS "DistrictName",
            pastor_name AS "PastorName", {{SummaryCountColumns}}
        FROM sp_report_district_summary({0}::int, {1}::int, {2}::int, {3}::bigint, {4}::bigint)
        """;

    public async Task<ReportResult> ExportRegionSummaryReport(RegionSummaryReportQueryFilter filter, ReportFormat format)
    {
        var monthFrom = filter.MonthFrom ?? 1;
        var monthTo = filter.MonthTo ?? 12;
        if (!ValidYear(filter.Year) || !ValidMonths(monthFrom, monthTo))
            return new ReportResult(null, BadRequest("Indica un año válido y un rango de meses entre 1 y 12."));

        var byDistrict = string.Equals(filter.GroupBy, "district", StringComparison.OrdinalIgnoreCase);
        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();
        var regionId = byDistrict ? filter.RegionId : null;
        var scope = await ScopeTextAsync(regionId, null, restrictedDistrictId);
        string[] info = [$"Año {filter.Year}: {MonthNames[monthFrom - 1]} a {MonthNames[monthTo - 1]}", scope];

        if (!byDistrict)
        {
            var rows = await _unitOfWork.SqlQueryAsync<RegionSummaryReportRowDto>(
                GetRegionSummarySql, filter.Year, monthFrom, monthTo, (object?)restrictedDistrictId ?? DBNull.Value);

            var pdfColumns = new List<PdfColumn<RegionSummaryReportRowDto>> { new("Región", 2.4f, r => r.RegionName) };
            pdfColumns.AddRange(SummaryPdfColumns<RegionSummaryReportRowDto>());
            var excelColumns = new List<ExcelColumn<RegionSummaryReportRowDto>>
            {
                new("Mes", r => MonthNames[r.PeriodMonth - 1]),
                new("Región", r => r.RegionName)
            };
            excelColumns.AddRange(SummaryExcelColumns<RegionSummaryReportRowDto>());

            return await Export(format, "report_region_summary", $"resumen-regiones-{filter.Year}", "RESUMEN MENSUAL POR REGIÓN", info,
                rows, pdfColumns, excelColumns, r => MonthNames[r.PeriodMonth - 1].ToUpper(ReportCulture), $"Resumen por región {filter.Year} ({scope})");
        }

        var districtRows = await _unitOfWork.SqlQueryAsync<DistrictSummaryReportRowDto>(
            GetDistrictSummarySql, filter.Year, monthFrom, monthTo,
            (object?)regionId ?? DBNull.Value,
            (object?)restrictedDistrictId ?? DBNull.Value);

        var districtPdfColumns = new List<PdfColumn<DistrictSummaryReportRowDto>>
        {
            new("Distrito", 2.2f, r => r.DistrictName),
            new("Pastor", 2.6f, r => r.PastorName ?? "")
        };
        districtPdfColumns.AddRange(SummaryPdfColumns<DistrictSummaryReportRowDto>());
        var districtExcelColumns = new List<ExcelColumn<DistrictSummaryReportRowDto>>
        {
            new("Mes", r => MonthNames[r.PeriodMonth - 1]),
            new("Región", r => r.RegionName),
            new("Distrito", r => r.DistrictName),
            new("Pastor", r => r.PastorName)
        };
        districtExcelColumns.AddRange(SummaryExcelColumns<DistrictSummaryReportRowDto>());

        // En el PDF: un bloque por mes y región, con su subtotal, y el total general al final.
        return await Export(format, "report_district_summary", $"resumen-distritos-{filter.Year}", "RESUMEN MENSUAL POR DISTRITO", info,
            districtRows, districtPdfColumns, districtExcelColumns,
            r => $"{MonthNames[r.PeriodMonth - 1].ToUpper(ReportCulture)}  ·  {r.RegionName}", $"Resumen por distrito {filter.Year} ({scope})");
    }

    /// <summary>Las columnas de cantidades y montos, iguales por región y por distrito.</summary>
    private static IEnumerable<PdfColumn<T>> SummaryPdfColumns<T>() where T : RegionSummaryReportRowDto
    {
        static string N(int value) => value.ToString(ReportCulture);
        return
        [
            new("Activas", 1f, r => N(r.ActiveChurches), true, r => r.ActiveChurches, true),
            new("Presentaron", 1.2f, r => N(r.Submitted), true, r => r.Submitted, true),
            new("No present.", 1.2f, r => N(r.NotSubmitted), true, r => r.NotSubmitted, true),
            new("Sin registrar", 1.2f, r => N(r.Missing), true, r => r.Missing, true),
            new("Con dif.", 1.1f, r => N(r.DifferencesPending), true, r => r.DifferencesPending, true),
            new("Resueltas", 1.1f, r => N(r.DifferencesResolved), true, r => r.DifferencesResolved, true),
            new("ACMS neg.", 1f, r => N(r.NegativeAcms), true, r => r.NegativeAcms, true),
            new("Saldo ACMS", 1.8f, r => Money(r.TotalAcmsBalance), true, r => r.TotalAcmsBalance),
            new("En custodia", 1.8f, r => Money(r.TotalCounted), true, r => r.TotalCounted),
            new("Diferencia", 1.6f, r => Money(r.TotalDifference), true, r => r.TotalDifference, FontColor: r => DifferenceColor(r.TotalDifference))
        ];
    }

    private static IEnumerable<ExcelColumn<T>> SummaryExcelColumns<T>() where T : RegionSummaryReportRowDto =>
    [
        new("Iglesias activas", r => r.ActiveChurches, Total: true),
        new("Presentaron", r => r.Submitted, Total: true),
        new("No presentaron", r => r.NotSubmitted, Total: true),
        new("Sin registrar", r => r.Missing, Total: true),
        new("Con diferencia", r => r.DifferencesPending, Total: true),
        new("Dif. resueltas", r => r.DifferencesResolved, Total: true),
        new("ACMS negativo", r => r.NegativeAcms, Total: true),
        new("Saldo ACMS", r => r.TotalAcmsBalance, MoneyFormat, Total: true),
        new("Total en custodia", r => r.TotalCounted, MoneyFormat, Total: true),
        new("Diferencia", r => r.TotalDifference, MoneyFormat, Total: true)
    ];

    // ---------------------------------------------------------------------------------------
    // Correcciones aplicadas
    // ---------------------------------------------------------------------------------------

    private const string GetCorrectionsSql = """
        SELECT
            registered_at AS "RegisteredAt", region_name AS "RegionName", district_name AS "DistrictName",
            church_code AS "ChurchCode", church_name AS "ChurchName", count_year AS "CountYear", count_month AS "CountMonth",
            applied_year AS "AppliedYear", applied_month AS "AppliedMonth", field_name AS "FieldName",
            previous_amount AS "PreviousAmount", corrected_amount AS "CorrectedAmount", reason AS "Reason",
            app_user_full_name AS "AppUserFullName"
        FROM sp_report_corrections({0}::int, {1}::bigint, {2}::bigint, {3}::bigint)
        """;

    public async Task<ReportResult> ExportCorrectionsReport(CorrectionsReportQueryFilter filter, ReportFormat format)
    {
        if (!ValidYear(filter.Year))
            return new ReportResult(null, BadRequest("Indica un año válido."));

        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();
        var rows = await _unitOfWork.SqlQueryAsync<CorrectionsReportRowDto>(
            GetCorrectionsSql,
            filter.Year,
            (object?)filter.RegionId ?? DBNull.Value,
            (object?)filter.DistrictId ?? DBNull.Value,
            (object?)restrictedDistrictId ?? DBNull.Value);

        var scope = await ScopeTextAsync(filter.RegionId, filter.DistrictId, restrictedDistrictId);
        string[] info = [$"Correcciones aplicadas en {filter.Year}: {rows.Count}", scope];

        string Field(string name) => name == CorrectionFields.AcmsBalance ? "Saldo ACMS" : "Total en custodia";
        string Period(int month, int year) => $"{MonthNames[month - 1]} {year}";

        var pdfColumns = new List<PdfColumn<CorrectionsReportRowDto>>
        {
            new("Fecha", 1.4f, r => $"{BoliviaTime.FromUtc(r.RegisteredAt):dd/MM/yyyy}"),
            new("Código", 1f, r => r.ChurchCode),
            new("Iglesia", 2.8f, r => r.ChurchName),
            new("Arqueo de", 1.8f, r => Period(r.CountMonth, r.CountYear)),
            new("Aplicada en", 1.8f, r => Period(r.AppliedMonth, r.AppliedYear)),
            new("Monto", 1.8f, r => Field(r.FieldName)),
            new("Anterior", 1.5f, r => Money(r.PreviousAmount), true),
            new("Corregido", 1.5f, r => Money(r.CorrectedAmount), true),
            new("Variación", 1.5f, r => Money(r.CorrectedAmount - r.PreviousAmount), true, r => r.CorrectedAmount - r.PreviousAmount,
                FontColor: r => DifferenceColor(r.CorrectedAmount - r.PreviousAmount)),
            new("Motivo", 3.2f, r => r.Reason),
            new("Usuario", 2f, r => r.AppUserFullName ?? "")
        };

        var excelColumns = new List<ExcelColumn<CorrectionsReportRowDto>>
        {
            new("Fecha", r => BoliviaTime.FromUtc(r.RegisteredAt), "dd/MM/yyyy HH:mm"),
            new("Región", r => r.RegionName),
            new("Distrito", r => r.DistrictName),
            new("Código", r => r.ChurchCode),
            new("Iglesia", r => r.ChurchName),
            new("Arqueo de", r => Period(r.CountMonth, r.CountYear)),
            new("Aplicada en", r => Period(r.AppliedMonth, r.AppliedYear)),
            new("Monto corregido", r => Field(r.FieldName)),
            new("Anterior", r => r.PreviousAmount, MoneyFormat),
            new("Corregido", r => r.CorrectedAmount, MoneyFormat),
            new("Variación", r => r.CorrectedAmount - r.PreviousAmount, MoneyFormat, Total: true),
            new("Motivo", r => r.Reason),
            new("Usuario", r => r.AppUserFullName)
        };

        return await Export(format, "report_corrections", $"correcciones-{filter.Year}", "CORRECCIONES DE ARQUEOS DE MESES CERRADOS", info,
            rows, pdfColumns, excelColumns, r => $"{r.RegionName}  ·  Distrito {r.DistrictName}", $"Correcciones {filter.Year} ({scope})");
    }

    // ---------------------------------------------------------------------------------------
    // Movimientos de iglesias
    // ---------------------------------------------------------------------------------------

    private const string GetChurchMovementsSql = """
        SELECT
            changed_at AS "ChangedAt", change_type AS "ChangeType", region_name AS "RegionName",
            district_name AS "DistrictName", church_code AS "ChurchCode", church_name AS "ChurchName",
            church_type AS "ChurchType", old_value AS "OldValue", new_value AS "NewValue", reason AS "Reason",
            app_user_full_name AS "AppUserFullName"
        FROM sp_report_church_movements({0}::date, {1}::date, {2}::varchar, {3}::bigint, {4}::bigint, {5}::bigint)
        """;

    public async Task<ReportResult> ExportChurchMovementsReport(ChurchMovementsReportQueryFilter filter, ReportFormat format)
    {
        var today = DateOnly.FromDateTime(BoliviaTime.Today);
        var dateFrom = filter.DateFrom ?? new DateOnly(today.Year, 1, 1);
        var dateTo = filter.DateTo ?? today;
        if (dateFrom > dateTo)
            return new ReportResult(null, BadRequest("La fecha inicial no puede ser posterior a la final."));

        var changeType = string.IsNullOrWhiteSpace(filter.ChangeType) ? null : filter.ChangeType;
        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();
        var rows = await _unitOfWork.SqlQueryAsync<ChurchMovementsReportRowDto>(
            GetChurchMovementsSql,
            dateFrom, dateTo,
            (object?)changeType ?? DBNull.Value,
            (object?)filter.RegionId ?? DBNull.Value,
            (object?)filter.DistrictId ?? DBNull.Value,
            (object?)restrictedDistrictId ?? DBNull.Value);

        var scope = await ScopeTextAsync(filter.RegionId, filter.DistrictId, restrictedDistrictId);
        string[] info =
        [
            $"Del {dateFrom:dd/MM/yyyy} al {dateTo:dd/MM/yyyy}{(changeType is null ? "" : $": {ChangeTypeLabel(changeType).ToLower(ReportCulture)}")}",
            scope,
            $"Cierres y fusiones: {rows.Count(r => r.ChangeType == ChurchChangeTypes.Status)}   Cambios de distrito: {rows.Count(r => r.ChangeType == ChurchChangeTypes.District)}   Cambios de tipo: {rows.Count(r => r.ChangeType == ChurchChangeTypes.Type)}   Cambios de nombre: {rows.Count(r => r.ChangeType == ChurchChangeTypes.Name)}"
        ];

        string Value(ChurchMovementsReportRowDto r, string? value) =>
            r.ChangeType == ChurchChangeTypes.Status ? ChurchStatusLabel(value) : value ?? "";

        var pdfColumns = new List<PdfColumn<ChurchMovementsReportRowDto>>
        {
            new("Fecha", 1.3f, r => $"{r.ChangedAt:dd/MM/yyyy}"),
            new("Cambio", 1.8f, r => ChangeTypeLabel(r.ChangeType)),
            new("Código", 1f, r => r.ChurchCode),
            new("Iglesia", 2.8f, r => r.ChurchName),
            new("Tipo", 1f, r => r.ChurchType),
            new("Antes", 2.2f, r => Value(r, r.OldValue)),
            new("Después", 2.2f, r => Value(r, r.NewValue)),
            new("Motivo", 3.4f, r => r.Reason ?? ""),
            new("Usuario", 2f, r => r.AppUserFullName ?? "")
        };

        var excelColumns = new List<ExcelColumn<ChurchMovementsReportRowDto>>
        {
            new("Fecha", r => r.ChangedAt.ToDateTime(TimeOnly.MinValue), "dd/MM/yyyy"),
            new("Cambio", r => ChangeTypeLabel(r.ChangeType)),
            new("Región", r => r.RegionName),
            new("Distrito", r => r.DistrictName),
            new("Código", r => r.ChurchCode),
            new("Iglesia", r => r.ChurchName),
            new("Tipo", r => r.ChurchType),
            new("Antes", r => Value(r, r.OldValue)),
            new("Después", r => Value(r, r.NewValue)),
            new("Motivo", r => r.Reason),
            new("Usuario", r => r.AppUserFullName)
        };

        return await Export(format, "report_church_movements", $"movimientos-iglesias-{dateFrom:yyyyMMdd}-{dateTo:yyyyMMdd}", "MOVIMIENTOS DE IGLESIAS", info,
            rows, pdfColumns, excelColumns, r => $"{r.RegionName}  ·  Distrito {r.DistrictName}", $"Movimientos de iglesias {dateFrom:dd/MM/yyyy} a {dateTo:dd/MM/yyyy} ({scope})");
    }

    private static string ChangeTypeLabel(string changeType) => changeType switch
    {
        ChurchChangeTypes.Status => "Cierre o fusión",
        ChurchChangeTypes.District => "Cambio de distrito",
        ChurchChangeTypes.Type => "Cambio de tipo",
        ChurchChangeTypes.Name => "Cambio de nombre",
        _ => changeType
    };

    private static string ChurchStatusLabel(string? status) => status switch
    {
        ChurchStatuses.Active => "Activa",
        ChurchStatuses.Closed => "Cerrada",
        ChurchStatuses.Merged => "Fusionada",
        _ => status ?? ""
    };

    // ---------------------------------------------------------------------------------------
    // Armado común: PDF en tabla agrupada y Excel plano
    // ---------------------------------------------------------------------------------------

    private async Task<ReportResult> Export<T>(
        ReportFormat format, string reportName, string fileBase, string title, IReadOnlyList<string> info,
        IReadOnlyList<T> rows, IReadOnlyList<PdfColumn<T>> pdfColumns, IReadOnlyList<ExcelColumn<T>> excelColumns,
        Func<T, string> groupBy, string auditDescription)
    {
        var username = await CurrentUserFullNameAsync();
        ReportFile file;
        if (format == ReportFormat.Pdf)
        {
            file = new ReportFile(BuildTablePdf(title, info, pdfColumns, rows, username, groupBy), PdfContentType, $"{fileBase}.pdf");
        }
        else
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var sheetName = title.Length > 31 ? title[..31] : title;
            AddSheet(workbook, sheetName, title, info, excelColumns, rows, username);
            file = new ReportFile(SaveWorkbook(workbook), ExcelContentType, $"{fileBase}.xlsx");
        }

        await _auditLogService.LogReportAccess(reportName, "EXPORT", $"{auditDescription} en {format}");
        return new ReportResult(file, null);
    }

    /// <summary>Tabla con grilla completa: un título por grupo, subtotal del grupo y total general.</summary>
    private byte[] BuildTablePdf<T>(
        string title, IReadOnlyList<string> info, IReadOnlyList<PdfColumn<T>> columns, IReadOnlyList<T> rows,
        string username, Func<T, string> groupBy)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var columnCount = (uint)columns.Count;
        var hasTotals = columns.Any(c => c.Total is not null);
        var labelSpan = (uint)Math.Max(1, columns.TakeWhile(c => c.Total is null).Count());

        void TotalsRow(TableDescriptor table, string label, IReadOnlyList<T> items, Func<IContainer, IContainer> style)
        {
            table.Cell().ColumnSpan(labelSpan).Element(style).AlignRight().Text(label).Bold();
            foreach (var column in columns.Skip((int)labelSpan))
            {
                if (column.Total is null)
                {
                    table.Cell().Element(style).Text("");
                    continue;
                }
                var sum = items.Sum(column.Total);
                table.Cell().Element(style).AlignRight()
                    .Text(column.IntegerTotal ? sum.ToString("N0", ReportCulture) : Money(sum)).Bold();
            }
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(7.5f));
                page.Header().Element(c => ComposeHeader(c, title, info));

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(definition =>
                    {
                        foreach (var column in columns)
                            definition.RelativeColumn(column.Width);
                    });

                    table.Header(header =>
                    {
                        foreach (var column in columns)
                        {
                            var cell = header.Cell().Element(HeaderCell);
                            if (column.AlignRight) cell.AlignRight().Text(column.Header);
                            else cell.Text(column.Header);
                        }
                    });

                    if (rows.Count == 0)
                    {
                        table.Cell().ColumnSpan(columnCount).Element(BodyCell).AlignCenter().Text("No hay datos para los filtros elegidos.");
                        return;
                    }

                    foreach (var group in rows.GroupBy(groupBy))
                    {
                        var items = group.ToList();
                        table.Cell().ColumnSpan(columnCount).Element(c => c.PaddingTop(6).PaddingBottom(2))
                            .Text($"{group.Key} ({items.Count})").Bold().FontSize(8.5f);

                        foreach (var row in items)
                        {
                            foreach (var column in columns)
                            {
                                var container1 = table.Cell().Element(BodyCell);
                                if (column.AlignRight) container1 = container1.AlignRight();
                                var text = container1.Text(column.Text(row));
                                if (column.FontColor?.Invoke(row) is { } color)
                                    text.FontColor(color).Bold();
                            }
                        }

                        if (hasTotals)
                            TotalsRow(table, $"Total {group.Key}", items, SubtotalCell);
                    }

                    if (hasTotals)
                        TotalsRow(table, "TOTAL GENERAL", rows, CategoryTotalCell);
                });

                page.Footer().Element(c => ComposeFooter(c, username));
            });
        });

        return document.GeneratePdf();
    }

    private static bool ValidYear(int year) => year is >= 2000 and <= 2100;

    private static bool ValidMonths(int monthFrom, int monthTo) => monthFrom is >= 1 and <= 12 && monthTo is >= 1 and <= 12 && monthFrom <= monthTo;

    private static string Percent(int part, int total) => total == 0 ? "0%" : $"{Math.Round(part * 100m / total)}%";
}

using FundTrack.Application.Dtos.Reports;
using FundTrack.core.Constants;
using FundTrack.core.Entities.Organization;
using FundTrack.core.QueryFilters.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FundTrack.Application.Services;

// "Reporte por iglesia": la hoja individual de una iglesia en un rango de meses, con la misma
// estructura que la planilla de remesa de la Misión (datos de la iglesia, recuadros con lo
// principal, desglose mes a mes, resumen y firmas), pero con lo que registra FundTrack: el saldo
// ACMS y el arqueo de cada mes. Los meses sin arqueo también salen, para que se vea qué falta.
public partial class ReportService
{
    private const string ChurchStatementReportName = "report_church_statement";
    private const string StatementBrandColor = "#1D3456";
    private const string StatementSoftColor = "#E8EEF6";
    private const int MaxStatementMonths = 36;

    private const string GetChurchStatementSql = """
        SELECT
            church_code AS "ChurchCode", church_name AS "ChurchName", church_type AS "ChurchType",
            district_name AS "DistrictName", region_name AS "RegionName", pastor_name AS "PastorName",
            period_year AS "PeriodYear", period_month AS "PeriodMonth", period_status AS "PeriodStatus",
            has_count AS "HasCount", submission_status AS "SubmissionStatus", summary_only AS "SummaryOnly",
            acms_balance AS "AcmsBalance", bank_statement_balance AS "BankStatementBalance", bank_deposit AS "BankDeposit",
            local_cash AS "LocalCash", usd_amount_bs AS "UsdAmountBs", documents_total AS "DocumentsTotal",
            total_amount AS "TotalAmount", difference AS "Difference", difference_status AS "DifferenceStatus",
            count_date AS "CountDate", observation AS "Observation", correction_count AS "CorrectionCount"
        FROM sp_report_church_statement({0}::bigint, {1}::bigint, {2}::bigint, {3}::bigint)
        """;

    public async Task<ReportResult> ExportChurchStatementReport(ChurchStatementReportQueryFilter filter, ReportFormat format)
    {
        if (filter.ChurchId is null)
            return new ReportResult(null, BadRequest("Elige la iglesia del reporte."));
        if (filter.FromPeriodId is null || filter.ToPeriodId is null)
            return new ReportResult(null, BadRequest("Elige el mes inicial y el mes final."));

        var periods = _unitOfWork.Repository<Period>();
        var from = await periods.GetByIdAsync(filter.FromPeriodId.Value);
        var to = await periods.GetByIdAsync(filter.ToPeriodId.Value);
        if (from is null || to is null)
            return new ReportResult(null, BadRequest("El mes elegido no existe."));

        var monthCount = (to.Year * 12 + to.Month) - (from.Year * 12 + from.Month) + 1;
        if (monthCount < 1)
            return new ReportResult(null, BadRequest("El mes inicial debe ser anterior o igual al mes final."));
        if (monthCount > MaxStatementMonths)
            return new ReportResult(null, BadRequest($"El rango puede tener hasta {MaxStatementMonths} meses."));

        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();
        var rows = await _unitOfWork.SqlQueryAsync<ChurchStatementReportRowDto>(
            GetChurchStatementSql,
            filter.ChurchId.Value,
            filter.FromPeriodId.Value,
            filter.ToPeriodId.Value,
            (object?)restrictedDistrictId ?? DBNull.Value);

        if (rows.Count == 0)
            return new ReportResult(null, BadRequest("No hay datos de esa iglesia en el rango elegido."));

        var range = $"{MonthNames[from.Month - 1]} {from.Year} a {MonthNames[to.Month - 1]} {to.Year}";
        var username = await CurrentUserFullNameAsync();
        var church = rows[^1];
        var fileBase = $"reporte-iglesia-{church.ChurchCode}-{to.Year}-{to.Month:00}";

        var file = format == ReportFormat.Pdf
            ? new ReportFile(BuildChurchStatementPdf(rows, range, username), PdfContentType, $"{fileBase}.pdf")
            : SpreadsheetFile(BuildChurchStatementExcel(rows, range, username), format, fileBase);

        await _auditLogService.LogReportAccess(ChurchStatementReportName, "EXPORT", $"Reporte de {church.ChurchCode} {church.ChurchName}, {range} en {format}");
        return new ReportResult(file, null);
    }

    private static string StatementMonth(ChurchStatementReportRowDto row) => $"{MonthNames[row.PeriodMonth - 1]} {row.PeriodYear}";

    private static bool Submitted(ChurchStatementReportRowDto row) => row.HasCount && row.SubmissionStatus == SubmissionStatuses.Submitted;

    /// <summary>Mismo criterio que la pantalla: lo que manda es el monto de la diferencia.</summary>
    private static string StatementStatus(ChurchStatementReportRowDto row)
    {
        if (!row.HasCount) return "Sin registrar";
        if (row.SubmissionStatus == SubmissionStatuses.NotSubmitted) return NotSubmittedText;
        if (row.Difference == 0) return "Sin diferencia";
        return row.DifferenceStatus == DifferenceStatuses.Resolved ? "Resuelta" : "Con diferencia";
    }

    private sealed record StatementSummary(
        int Months, int Submitted, int NotSubmitted, int Missing, int WithDifference,
        decimal Shortage, decimal Surplus, ChurchStatementReportRowDto? Last);

    private static StatementSummary SummarizeStatement(IReadOnlyList<ChurchStatementReportRowDto> rows)
    {
        var submitted = rows.Where(Submitted).ToList();
        return new StatementSummary(
            rows.Count,
            submitted.Count,
            rows.Count(r => r.HasCount && r.SubmissionStatus == SubmissionStatuses.NotSubmitted),
            rows.Count(r => !r.HasCount),
            submitted.Count(r => r.Difference != 0),
            submitted.Where(r => r.Difference < 0).Sum(r => r.Difference),
            submitted.Where(r => r.Difference > 0).Sum(r => r.Difference),
            submitted.LastOrDefault());
    }

    private byte[] BuildChurchStatementPdf(IReadOnlyList<ChurchStatementReportRowDto> rows, string range, string username)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var church = rows[^1];
        var summary = SummarizeStatement(rows);
        var observations = rows.Where(r => !string.IsNullOrWhiteSpace(r.Observation)).ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(8.5f));
                page.Header().Element(c => ComposeHeader(c, "REPORTE DE FONDO LOCAL POR IGLESIA", [$"Rango: {range}"]));
                page.Footer().Element(c => ComposeFooter(c, username));

                page.Content().PaddingTop(10).Column(column =>
                {
                    column.Spacing(10);

                    // Datos de la iglesia.
                    column.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten5).Padding(8).Row(row =>
                    {
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text("Cód. ACMS / Nombre de la iglesia / Tipo").FontSize(7).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(t =>
                            {
                                t.Span(church.ChurchCode).Bold().FontSize(13).FontColor(StatementBrandColor);
                                t.Span($"  /  {church.ChurchName}  /  ").FontSize(11);
                                t.Span(church.ChurchType.ToUpper(ReportCulture)).SemiBold().FontSize(9).FontColor(StatementBrandColor);
                            });
                        });
                        row.RelativeItem(2).Column(c =>
                        {
                            c.Item().Text("Distrito / Región").FontSize(7).FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"{church.DistrictName} / {church.RegionName}").FontSize(10);
                            c.Item().PaddingTop(2).Text("Pastor de distrito").FontSize(7).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(church.PastorName ?? "Sin pastor asignado").FontSize(9);
                        });
                    });

                    // Recuadros con lo principal.
                    var last = summary.Last;
                    var lastLabel = last is null ? "Sin arqueos en el rango" : $"A {StatementMonth(last)}";
                    column.Item().Row(row =>
                    {
                        row.Spacing(8);
                        row.RelativeItem().Element(c => StatementCard(c, "ARQUEOS PRESENTADOS", $"{summary.Submitted} de {summary.Months}", "MESES DEL RANGO", null, false));
                        row.RelativeItem().Element(c => StatementCard(c, "SALDO ACMS", last is null ? "Bs 0,00" : $"Bs {Money(last.AcmsBalance)}", lastLabel, null, false));
                        row.RelativeItem().Element(c => StatementCard(c, "TOTAL EN CUSTODIA", last is null ? "Bs 0,00" : $"Bs {Money(last.TotalAmount)}", lastLabel, null, false));
                        row.RelativeItem().Element(c => StatementCard(c, "DIFERENCIA", last is null ? "Bs 0,00" : $"Bs {Money(last.Difference)}", lastLabel,
                            last is null ? null : DifferenceColor(last.Difference), true));
                    });

                    // Desglose por meses.
                    column.Item().PaddingTop(4).Text("Desglose por meses").FontSize(11).SemiBold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(d =>
                        {
                            d.RelativeColumn(1.7f);
                            d.RelativeColumn(1.3f);
                            d.RelativeColumn(1.2f);
                            d.RelativeColumn(1.2f);
                            d.RelativeColumn(1.1f);
                            d.RelativeColumn(1.2f);
                            d.RelativeColumn(1.4f);
                            d.RelativeColumn(1.2f);
                            d.RelativeColumn(1.3f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Mes / Año");
                            foreach (var title in new[] { "Saldo ACMS", "Banco", "Efectivo", "Dólares Bs", "Documentos", "Total custodia", "Diferencia" })
                                header.Cell().Element(HeaderCell).AlignRight().Text(title);
                            header.Cell().Element(HeaderCell).AlignCenter().Text("Estado");
                        });

                        foreach (var r in rows)
                        {
                            table.Cell().Element(BodyCell).Text(StatementMonth(r)).SemiBold();
                            if (!r.HasCount)
                            {
                                table.Cell().ColumnSpan(7).Element(BodyCell).AlignCenter().Text("Sin arqueo registrado").Italic().FontColor(Colors.Grey.Darken1);
                            }
                            else if (!Submitted(r))
                            {
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(r.AcmsBalance));
                                table.Cell().ColumnSpan(6).Element(BodyCell).AlignCenter().Text("La iglesia no presentó su arqueo").Italic().FontColor(DangerColor);
                            }
                            else
                            {
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(r.AcmsBalance));
                                if (r.SummaryOnly)
                                {
                                    table.Cell().ColumnSpan(4).Element(BodyCell).AlignCenter().Text("Arqueo presentado por la iglesia (solo total)").Italic().FontColor(Colors.Grey.Darken1);
                                }
                                else
                                {
                                    table.Cell().Element(BodyCell).AlignRight().Text(Money(r.BankStatementBalance + r.BankDeposit));
                                    table.Cell().Element(BodyCell).AlignRight().Text(Money(r.LocalCash));
                                    table.Cell().Element(BodyCell).AlignRight().Text(Money(r.UsdAmountBs));
                                    table.Cell().Element(BodyCell).AlignRight().Text(Money(r.DocumentsTotal));
                                }
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(r.TotalAmount)).SemiBold();
                                var difference = table.Cell().Element(BodyCell).AlignRight().Text(Money(r.Difference)).SemiBold();
                                if (DifferenceColor(r.Difference) is { } color) difference.FontColor(color);
                            }
                            table.Cell().Element(BodyCell).AlignCenter().Text(StatementStatus(r)).FontSize(7.5f);
                        }
                    });

                    // Resumen del rango.
                    column.Item().AlignRight().Width(260).Border(1).BorderColor(Colors.Grey.Lighten1).Column(box =>
                    {
                        void Line(string label, string value, string? color = null, bool strong = false)
                        {
                            box.Item().PaddingHorizontal(8).PaddingVertical(3).Row(row =>
                            {
                                var l = row.RelativeItem().AlignRight().Text(label);
                                var v = row.ConstantItem(90).AlignRight().Text(value);
                                if (strong) { l.SemiBold(); v.Bold(); }
                                if (color is not null) { l.FontColor(color); v.FontColor(color); }
                            });
                        }

                        Line("Meses del rango:", summary.Months.ToString(ReportCulture));
                        Line("Arqueos presentados:", summary.Submitted.ToString(ReportCulture));
                        Line("No presentó:", summary.NotSubmitted.ToString(ReportCulture), summary.NotSubmitted > 0 ? DangerColor : null);
                        Line("Sin registrar:", summary.Missing.ToString(ReportCulture));
                        Line("Meses con diferencia:", summary.WithDifference.ToString(ReportCulture));
                        Line("(-) Faltante en el rango:", $"Bs {Money(summary.Shortage)}", summary.Shortage < 0 ? DangerColor : null);
                        Line("(+) Sobrante en el rango:", $"Bs {Money(summary.Surplus)}", summary.Surplus > 0 ? InfoColor : null);
                        box.Item().Background(StatementSoftColor).PaddingHorizontal(8).PaddingVertical(5).Row(row =>
                        {
                            row.RelativeItem().AlignRight().Text("SALDO ACMS ACTUAL:").Bold();
                            row.ConstantItem(90).AlignRight().Text(last is null ? "Bs 0,00" : $"Bs {Money(last.AcmsBalance)}").Bold().FontColor(StatementBrandColor);
                        });
                    });

                    // Observaciones de los meses que las tienen.
                    if (observations.Count > 0)
                    {
                        column.Item().Text("Observaciones").FontSize(10).SemiBold();
                        column.Item().Column(list =>
                        {
                            foreach (var r in observations)
                                list.Item().Text(t =>
                                {
                                    t.Span($"{StatementMonth(r)}: ").SemiBold();
                                    t.Span(r.Observation!);
                                });
                        });
                    }

                    // Firmas.
                    column.Item().PaddingTop(50).ShowEntire().Row(row =>
                    {
                        row.Spacing(30);
                        row.RelativeItem().Element(c => SignatureBox(c, "TESORERO(A) IGLESIA", null, null));
                        row.RelativeItem().Element(c => SignatureBox(c, "PASTOR DE DISTRITO", church.PastorName, null));
                        row.RelativeItem().Element(c => SignatureBox(c, "SELLO IGLESIA", null, null));
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void StatementCard(IContainer container, string title, string value, string caption, string? valueColor, bool highlight)
    {
        container.Border(1).BorderColor(StatementBrandColor).Column(card =>
        {
            card.Item().Background(StatementBrandColor).PaddingVertical(4).AlignCenter().Text(title).FontSize(7).Bold().FontColor(Colors.White);
            card.Item().Background(highlight ? StatementSoftColor : Colors.White).PaddingVertical(6).Column(body =>
            {
                body.Item().AlignCenter().Text(value).FontSize(13).Bold().FontColor(valueColor ?? StatementBrandColor);
                body.Item().AlignCenter().Text(caption).FontSize(6.5f).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static byte[] BuildChurchStatementExcel(IReadOnlyList<ChurchStatementReportRowDto> rows, string range, string username)
    {
        var church = rows[^1];
        var summary = SummarizeStatement(rows);
        string[] info =
        [
            $"{church.ChurchCode} / {church.ChurchName} / {church.ChurchType}",
            $"Distrito {church.DistrictName}, {church.RegionName}. Pastor: {church.PastorName ?? "sin asignar"}",
            $"Rango: {range}",
            $"Arqueos presentados: {summary.Submitted} de {summary.Months}   No presentó: {summary.NotSubmitted}   Sin registrar: {summary.Missing}   Con diferencia: {summary.WithDifference}"
        ];

        decimal? Amount(ChurchStatementReportRowDto r, Func<ChurchStatementReportRowDto, decimal> value) =>
            Submitted(r) && !r.SummaryOnly ? value(r) : null;

        var columns = new List<ExcelColumn<ChurchStatementReportRowDto>>
        {
            new("Mes", r => MonthNames[r.PeriodMonth - 1]),
            new("Año", r => r.PeriodYear),
            new("Estado", StatementStatus),
            new("Arqueo", r => !Submitted(r) ? null : r.SummaryOnly ? "De la iglesia (solo total)" : "Detallado"),
            new("Fecha del arqueo", r => r.CountDate?.ToDateTime(TimeOnly.MinValue), "dd/MM/yyyy"),
            new("Saldo ACMS", r => r.HasCount ? r.AcmsBalance : null, MoneyFormat),
            new("Extracto bancario", r => Amount(r, x => x.BankStatementBalance), MoneyFormat),
            new("Depósito bancario", r => Amount(r, x => x.BankDeposit), MoneyFormat),
            new("Billetes y monedas", r => Amount(r, x => x.LocalCash), MoneyFormat),
            new("Dólares en Bs", r => Amount(r, x => x.UsdAmountBs), MoneyFormat),
            new("Documentos", r => Amount(r, x => x.DocumentsTotal), MoneyFormat),
            new("Total en custodia", r => Submitted(r) ? r.TotalAmount : null, MoneyFormat),
            new("Diferencia", r => Submitted(r) ? r.Difference : null, MoneyFormat),
            new("Correcciones", r => r.CorrectionCount > 0 ? r.CorrectionCount : null),
            new("Observación", r => r.Observation)
        };

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheetName = $"Iglesia {church.ChurchCode}";
        AddSheet(workbook, sheetName.Length > 31 ? sheetName[..31] : sheetName, "REPORTE DE FONDO LOCAL POR IGLESIA", info, columns, rows, username);
        return SaveWorkbook(workbook);
    }
}

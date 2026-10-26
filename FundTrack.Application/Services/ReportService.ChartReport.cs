using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using FundTrack.Application.Dtos.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FundTrack.Application.Services;

// Reporte de una gráfica (botón Reporte de la pantalla Gráficas). Recibe lo mismo que la pantalla
// dibuja y lo arma con las piezas de siempre: encabezado de la Misión con los filtros, la gráfica
// como SVG (mismos colores que la pantalla en modo claro), su leyenda y la tabla con los números.
// En Excel y TXT va la tabla. Queda en la auditoría como cualquier otro reporte.
public partial class ReportService
{
    private const int MaxChartRows = 500;
    private const int MaxChartSeries = 8;

    private static readonly Dictionary<string, string> ChartColors = new()
    {
        ["chart-1"] = "#2a78d6",
        ["chart-2"] = "#eb6834",
        ["chart-3"] = "#1baf7a",
        ["chart-neutral"] = "#c3c2b7",
        ["chart-pos"] = "#2a78d6",
        ["chart-neg"] = "#e34948",
    };

    public async Task<ReportResult> ExportChartReport(ChartReportRequestDto request, ReportFormat format)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return new ReportResult(null, BadRequest("Falta el título de la gráfica."));
        if (request.Headers.Count == 0 || request.Rows.Count > MaxChartRows || request.Series.Count > MaxChartSeries
            || request.Rows.Any(r => r.Count != request.Headers.Count))
            return new ReportResult(null, BadRequest("Los datos de la gráfica no son válidos."));

        var username = await CurrentUserFullNameAsync();
        var fileBase = Regex.Replace(request.FileName, "[^a-zA-Z0-9-]", "-").Trim('-');
        if (fileBase.Length == 0) fileBase = "grafica";

        var file = format == ReportFormat.Pdf
            ? new ReportFile(BuildChartReportPdf(request, username), PdfContentType, $"{fileBase}.pdf")
            : SpreadsheetFile(BuildChartReportExcel(request, username), format, fileBase);

        await _auditLogService.LogReportAccess("report_chart", "EXPORT", $"Gráfica {request.Title} ({string.Join(", ", request.Filters)}) en {format}");
        return new ReportResult(file, null);
    }

    private static string ColumnFormat(ChartReportRequestDto request, int column) =>
        column < request.ColumnFormats.Count ? request.ColumnFormats[column] : "text";

    private static decimal? ParseNumber(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static string FormatCell(string? value, string format)
    {
        if (format == "text" || ParseNumber(value) is not { } number) return value ?? "";
        return format switch
        {
            "money" => Money(number),
            "percent" => $"{number.ToString("0", ReportCulture)}%",
            _ => number.ToString("N0", ReportCulture)
        };
    }

    private byte[] BuildChartReportPdf(ChartReportRequestDto request, string username)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var info = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Subtitle)) info.Add(request.Subtitle);
        info.AddRange(request.Filters);
        var svg = ChartReportSvg(request, 530, request.ChartType == "bars" ? Math.Max(120, 22 * request.Categories.Count + 20) : 230);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(8.5f));
                page.Header().Element(c => ComposeHeader(c, request.Title.ToUpper(ReportCulture), info));
                page.Footer().Element(c => ComposeFooter(c, username));

                page.Content().PaddingTop(10).Column(column =>
                {
                    column.Spacing(10);

                    if (svg is not null)
                    {
                        column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(chart =>
                        {
                            chart.Spacing(4);
                            if (request.Series.Count > 1)
                            {
                                chart.Item().Row(row =>
                                {
                                    row.RelativeItem();
                                    foreach (var series in request.Series)
                                        row.AutoItem().PaddingLeft(10).Element(c => LegendEntry(c, SeriesColor(series), series.Label));
                                });
                            }
                            chart.Item().Svg(svg);
                        });
                    }

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(d =>
                        {
                            for (var i = 0; i < request.Headers.Count; i++)
                            {
                                if (ColumnFormat(request, i) == "text") d.RelativeColumn(2);
                                else d.RelativeColumn(1.2f);
                            }
                        });
                        table.Header(header =>
                        {
                            for (var i = 0; i < request.Headers.Count; i++)
                            {
                                var cell = header.Cell().Element(HeaderCell);
                                if (ColumnFormat(request, i) == "text") cell.Text(request.Headers[i]);
                                else cell.AlignRight().Text(request.Headers[i]);
                            }
                        });
                        foreach (var row in request.Rows)
                        {
                            for (var i = 0; i < row.Count; i++)
                            {
                                var format = ColumnFormat(request, i);
                                var cell = table.Cell().Element(BodyCell);
                                if (format == "text") cell.Text(row[i] ?? "");
                                else cell.AlignRight().Text(FormatCell(row[i], format));
                            }
                        }
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static byte[] BuildChartReportExcel(ChartReportRequestDto request, string username)
    {
        var info = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Subtitle)) info.Add(request.Subtitle);
        info.AddRange(request.Filters);

        var columns = request.Headers.Select((header, i) =>
        {
            var format = ColumnFormat(request, i);
            return new ExcelColumn<List<string?>>(
                header,
                row => format == "text" ? row[i] : format == "percent" ? (object?)(ParseNumber(row[i]) / 100) : ParseNumber(row[i]),
                format switch { "money" => MoneyFormat, "percent" => "0%", "integer" => "#,##0", _ => null });
        }).ToList();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheetName = Regex.Replace(request.Title, @"[\[\]\*\?/\\:]", " ");
        AddSheet(workbook, sheetName.Length > 31 ? sheetName[..31] : sheetName, request.Title.ToUpper(ReportCulture), info, columns, request.Rows, username);
        return SaveWorkbook(workbook);
    }

    private static string SeriesColor(ChartReportSeriesDto series) =>
        ChartColors.TryGetValue(series.Color, out var hex) ? hex : ChartColors["chart-1"];

    private static string ChartValue(decimal value, string format) => format switch
    {
        "money" => CompactMoney(value),
        "percent" => $"{value.ToString("0", ReportCulture)}%",
        _ => value.ToString("N0", ReportCulture)
    };

    /// <summary>La gráfica en SVG según su tipo; null si es solo tabla.</summary>
    private static string? ChartReportSvg(ChartReportRequestDto request, float width, float height)
    {
        if (request.Categories.Count == 0 || request.Series.Count == 0) return null;
        return request.ChartType switch
        {
            "line" => GenericLineSvg(request, width, height),
            "column" => GenericColumnSvg(request, width, height),
            "bars" => GenericBarsSvg(request, width, height),
            _ => null
        };
    }

    private static string SvgOpen(float width, float height) =>
        $"""<svg xmlns="http://www.w3.org/2000/svg" width="{F(width)}" height="{F(height)}" viewBox="0 0 {F(width)} {F(height)}" font-family="Arial, sans-serif">""";

    private static string Esc(string text) => WebUtility.HtmlEncode(text);

    private static void AppendValueGrid(StringBuilder svg, List<decimal> ticks, float width, Func<decimal, double> y, string format)
    {
        foreach (var tick in ticks)
        {
            svg.Append($"""<line x1="{F(ChartLeft)}" x2="{F(width - ChartRight)}" y1="{F(y(tick))}" y2="{F(y(tick))}" stroke="{(tick == 0 ? ChartAxisColor : ChartGridColor)}" stroke-width="0.8"/>""");
            svg.Append($"""<text x="{F(ChartLeft - 5)}" y="{F(y(tick) + 3)}" font-size="8" fill="{ChartInkColor}" text-anchor="end">{Esc(ChartValue(tick, format))}</text>""");
        }
    }

    private static string GenericLineSvg(ChartReportRequestDto request, float width, float height)
    {
        var values = request.Series.SelectMany(s => s.Values).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var ticks = ChartTicks(values.Count > 0 ? values.Min() : 0, values.Count > 0 ? values.Max() : 0);
        var plotHeight = height - ChartTop - ChartBottom;
        var step = (width - ChartLeft - ChartRight) / request.Categories.Count;
        double X(int i) => ChartLeft + step * (i + 0.5);
        double Y(decimal v) => ChartTop + (double)((ticks[^1] - v) / (ticks[^1] - ticks[0])) * plotHeight;

        var svg = new StringBuilder(SvgOpen(width, height));
        AppendValueGrid(svg, ticks, width, Y, request.ValueFormat);
        for (var i = 0; i < request.Categories.Count; i++)
            svg.Append($"""<text x="{F(X(i))}" y="{F(height - 5)}" font-size="8" fill="{ChartInkColor}" text-anchor="middle">{Esc(request.Categories[i])}</text>""");

        foreach (var series in Enumerable.Reverse(request.Series))
        {
            var color = SeriesColor(series);
            var path = new StringBuilder();
            var dots = new StringBuilder();
            var drawing = false;
            for (var i = 0; i < request.Categories.Count; i++)
            {
                var value = i < series.Values.Count ? series.Values[i] : null;
                if (value is null) { drawing = false; continue; }
                path.Append($"{(drawing ? "L" : "M")}{F(X(i))},{F(Y(value.Value))}");
                drawing = true;
                dots.Append($"""<circle cx="{F(X(i))}" cy="{F(Y(value.Value))}" r="2.6" fill="{color}" stroke="#ffffff" stroke-width="1.2"/>""");
            }
            svg.Append($"""<path d="{path}" fill="none" stroke="{color}" stroke-width="2" stroke-linejoin="round" stroke-linecap="round"/>""").Append(dots);
        }
        return svg.Append("</svg>").ToString();
    }

    private static string GenericColumnSvg(ChartReportRequestDto request, float width, float height)
    {
        var count = request.Categories.Count;
        decimal Value(ChartReportSeriesDto s, int i) => i < s.Values.Count ? s.Values[i] ?? 0 : 0;
        var up = Enumerable.Range(0, count).Select(i => request.Series.Sum(s => Math.Max(0, Value(s, i)))).ToList();
        var down = Enumerable.Range(0, count).Select(i => request.Series.Sum(s => Math.Min(0, Value(s, i)))).ToList();
        var ticks = ChartTicks(down.Min(), up.Max());
        var plotHeight = height - ChartTop - ChartBottom;
        var step = (width - ChartLeft - ChartRight) / count;
        var barWidth = Math.Min(18, step * 0.6);
        double X(int i) => ChartLeft + step * (i + 0.5);
        double Y(decimal v) => ChartTop + (double)((ticks[^1] - v) / (ticks[^1] - ticks[0])) * plotHeight;

        var svg = new StringBuilder(SvgOpen(width, height));
        AppendValueGrid(svg, ticks, width, Y, request.ValueFormat);
        for (var i = 0; i < count; i++)
        {
            svg.Append($"""<text x="{F(X(i))}" y="{F(height - 5)}" font-size="8" fill="{ChartInkColor}" text-anchor="middle">{Esc(request.Categories[i])}</text>""");
            decimal positive = 0, negative = 0;
            foreach (var series in request.Series)
            {
                var value = Value(series, i);
                if (value == 0) continue;
                var from = value > 0 ? positive : negative;
                var to = from + value;
                if (value > 0) positive = to; else negative = to;
                var top = Y(Math.Max(from, to));
                var bottom = Y(Math.Min(from, to));
                // 1px de aire entre segmentos apilados, como los 2px de la pantalla.
                svg.Append($"""<rect x="{F(X(i) - barWidth / 2)}" y="{F(top + 0.5)}" width="{F(barWidth)}" height="{F(Math.Max(0.8, bottom - top - 1))}" fill="{SeriesColor(series)}"/>""");
            }
        }
        return svg.Append("</svg>").ToString();
    }

    private static string GenericBarsSvg(ChartReportRequestDto request, float width, float height)
    {
        var series = request.Series[0];
        var color = SeriesColor(series);
        const float labelWidth = 190;
        const float rowHeight = 22;
        var values = request.Categories.Select((_, i) => i < series.Values.Count ? series.Values[i] ?? 0 : 0).ToList();
        var top = request.ValueFormat == "percent" ? 100 : Math.Max(1, values.Max(v => Math.Abs(v)));
        var track = width - labelWidth - 80;

        var svg = new StringBuilder(SvgOpen(width, height));
        for (var i = 0; i < values.Count; i++)
        {
            var y = 10 + i * rowHeight;
            var barWidth = Math.Max(1, (double)(Math.Abs(values[i]) / top) * track);
            svg.Append($"""<text x="0" y="{F(y + 9)}" font-size="8" fill="#0b0b0b">{Esc(request.Categories[i])}</text>""");
            svg.Append($"""<rect x="{F(labelWidth)}" y="{F(y + 1)}" width="{F(barWidth)}" height="11" rx="2" fill="{color}"/>""");
            svg.Append($"""<text x="{F(labelWidth + barWidth + 5)}" y="{F(y + 9)}" font-size="8" font-weight="bold" fill="#0b0b0b">{Esc(request.ValueFormat == "money" ? $"Bs {Money(values[i])}" : ChartValue(values[i], request.ValueFormat))}</text>""");
        }
        return svg.Append("</svg>").ToString();
    }
}

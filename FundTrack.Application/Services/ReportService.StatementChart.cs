using System.Globalization;
using System.Net;
using System.Text;
using FundTrack.Application.Dtos.Reports;

namespace FundTrack.Application.Services;

// Gráficas del Reporte por iglesia, dibujadas como SVG para el PDF (QuestPDF las pinta en vectores,
// así se imprimen nítidas). Mismos colores que la pantalla Gráficas en modo claro: saldo ACMS en
// azul, total en custodia en naranja, y la diferencia en azul (sobra) o rojo (falta). Un mes sin
// arqueo presentado no tiene punto: la línea se corta en vez de caer a cero.
public partial class ReportService
{
    private const string ChartAcmsColor = "#2a78d6";
    private const string ChartCustodyColor = "#eb6834";
    private const string ChartSurplusColor = "#2a78d6";
    private const string ChartShortageColor = "#e34948";
    private const string ChartGridColor = "#e1e0d9";
    private const string ChartAxisColor = "#c3c2b7";
    private const string ChartInkColor = "#52514e";

    private const float ChartLeft = 46;
    private const float ChartRight = 8;
    private const float ChartTop = 14;
    private const float ChartBottom = 18;

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string CompactMoney(decimal value)
    {
        var abs = Math.Abs(value);
        var text = abs >= 1_000_000 ? $"{(abs / 1_000_000).ToString("0.#", ReportCulture)} M"
            : abs >= 1_000 ? $"{(abs / 1_000).ToString("0.#", ReportCulture)} mil"
            : abs.ToString("0", ReportCulture);
        return value < 0 ? $"-{text}" : text;
    }

    private static List<decimal> ChartTicks(decimal min, decimal max)
    {
        var low = Math.Min(0, min);
        var high = Math.Max(0, max);
        if (low == high) high = 1;
        var raw = (double)(high - low) / 4;
        var power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var step = (decimal)(new[] { 1, 2, 2.5, 5, 10 }.Select(m => m * power).First(s => s >= raw));
        var ticks = new List<decimal>();
        for (var tick = Math.Floor(low / step) * step; tick <= high + step / 1000; tick += step)
            ticks.Add(tick);
        if (ticks[^1] < high) ticks.Add(ticks[^1] + step);
        return ticks;
    }

    private static string MonthShort(ChartMonthPoint point) => MonthNames[point.Month - 1][..3];

    private sealed record ChartMonthPoint(int Month, decimal? Acms, decimal? Custody, decimal Difference);

    private static List<ChartMonthPoint> ChartPoints(IReadOnlyList<ChurchStatementReportRowDto> rows) =>
        rows.Select(r => Submitted(r)
                ? new ChartMonthPoint(r.PeriodMonth, r.AcmsBalance, r.TotalAmount, r.Difference)
                : new ChartMonthPoint(r.PeriodMonth, null, null, 0))
            .ToList();

    /// <summary>Saldo ACMS y total en custodia mes a mes, con el mes más alto y el más bajo marcados.</summary>
    private static string StatementLineSvg(IReadOnlyList<ChurchStatementReportRowDto> rows, float width, float height)
    {
        var points = ChartPoints(rows);
        var values = points.SelectMany(p => new[] { p.Acms, p.Custody }).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var ticks = ChartTicks(values.Count > 0 ? values.Min() : 0, values.Count > 0 ? values.Max() : 0);
        var plotHeight = height - ChartTop - ChartBottom;
        var step = (width - ChartLeft - ChartRight) / Math.Max(1, points.Count);
        double X(int i) => ChartLeft + step * (i + 0.5);
        double Y(decimal v) => ChartTop + (double)((ticks[^1] - v) / (ticks[^1] - ticks[0])) * plotHeight;

        var svg = new StringBuilder($"""<svg xmlns="http://www.w3.org/2000/svg" width="{F(width)}" height="{F(height)}" viewBox="0 0 {F(width)} {F(height)}" font-family="Arial, sans-serif">""");
        AppendGrid(svg, ticks, width, Y);
        for (var i = 0; i < points.Count; i++)
            svg.Append($"""<text x="{F(X(i))}" y="{F(height - 5)}" font-size="8" fill="{ChartInkColor}" text-anchor="middle">{MonthShort(points[i])}</text>""");

        foreach (var (selector, color) in new (Func<ChartMonthPoint, decimal?>, string)[] { (p => p.Custody, ChartCustodyColor), (p => p.Acms, ChartAcmsColor) })
        {
            var path = new StringBuilder();
            var drawing = false;
            for (var i = 0; i < points.Count; i++)
            {
                var value = selector(points[i]);
                if (value is null) { drawing = false; continue; }
                path.Append($"{(drawing ? "L" : "M")}{F(X(i))},{F(Y(value.Value))}");
                drawing = true;
            }
            svg.Append($"""<path d="{path}" fill="none" stroke="{color}" stroke-width="2" stroke-linejoin="round" stroke-linecap="round"/>""");
            for (var i = 0; i < points.Count; i++)
            {
                if (selector(points[i]) is { } value)
                    svg.Append($"""<circle cx="{F(X(i))}" cy="{F(Y(value))}" r="2.6" fill="{color}" stroke="#ffffff" stroke-width="1.2"/>""");
            }
        }

        // El mes más alto y el más bajo del saldo ACMS, con su monto: las "altas y bajas" de la iglesia.
        var withAcms = points.Select((p, i) => (p, i)).Where(x => x.p.Acms.HasValue).ToList();
        if (withAcms.Count > 1)
        {
            var highest = withAcms.MaxBy(x => x.p.Acms!.Value);
            var lowest = withAcms.MinBy(x => x.p.Acms!.Value);
            if (highest.p.Acms != lowest.p.Acms)
            {
                AppendPointLabel(svg, X(highest.i), Y(highest.p.Acms!.Value) - 7, $"Más alto: Bs {Money(highest.p.Acms!.Value)}", width);
                AppendPointLabel(svg, X(lowest.i) + 6, Y(lowest.p.Acms!.Value) - 7, $"Más bajo: Bs {Money(lowest.p.Acms!.Value)}", width);
            }
        }

        return svg.Append("</svg>").ToString();
    }

    /// <summary>La diferencia de cada mes: la columna sube si sobró y baja si faltó.</summary>
    private static string StatementDifferenceSvg(IReadOnlyList<ChurchStatementReportRowDto> rows, float width, float height)
    {
        var points = ChartPoints(rows);
        var ticks = ChartTicks(points.Min(p => p.Difference), points.Max(p => p.Difference));
        var plotHeight = height - ChartTop - ChartBottom;
        var step = (width - ChartLeft - ChartRight) / Math.Max(1, points.Count);
        var barWidth = Math.Min(16, step * 0.55);
        double X(int i) => ChartLeft + step * (i + 0.5);
        double Y(decimal v) => ChartTop + (double)((ticks[^1] - v) / (ticks[^1] - ticks[0])) * plotHeight;

        var svg = new StringBuilder($"""<svg xmlns="http://www.w3.org/2000/svg" width="{F(width)}" height="{F(height)}" viewBox="0 0 {F(width)} {F(height)}" font-family="Arial, sans-serif">""");
        AppendGrid(svg, ticks, width, Y);
        for (var i = 0; i < points.Count; i++)
        {
            svg.Append($"""<text x="{F(X(i))}" y="{F(height - 5)}" font-size="8" fill="{ChartInkColor}" text-anchor="middle">{MonthShort(points[i])}</text>""");
            var difference = points[i].Difference;
            if (difference == 0) continue;
            var top = Y(Math.Max(difference, 0));
            var bottom = Y(Math.Min(difference, 0));
            var color = difference > 0 ? ChartSurplusColor : ChartShortageColor;
            svg.Append($"""<rect x="{F(X(i) - barWidth / 2)}" y="{F(top)}" width="{F(barWidth)}" height="{F(Math.Max(1, bottom - top))}" rx="2" fill="{color}"/>""");
        }
        return svg.Append("</svg>").ToString();
    }

    private static void AppendGrid(StringBuilder svg, List<decimal> ticks, float width, Func<decimal, double> y)
    {
        foreach (var tick in ticks)
        {
            svg.Append($"""<line x1="{F(ChartLeft)}" x2="{F(width - ChartRight)}" y1="{F(y(tick))}" y2="{F(y(tick))}" stroke="{(tick == 0 ? ChartAxisColor : ChartGridColor)}" stroke-width="0.8"/>""");
            svg.Append($"""<text x="{F(ChartLeft - 5)}" y="{F(y(tick) + 3)}" font-size="8" fill="{ChartInkColor}" text-anchor="end">{CompactMoney(tick)}</text>""");
        }
    }

    /// <summary>Etiqueta junto a un punto, corrida hacia adentro si se saldría por un costado.</summary>
    private static void AppendPointLabel(StringBuilder svg, double x, double y, string text, float width)
    {
        var anchor = x < ChartLeft + 60 ? "start" : x > width - 80 ? "end" : "middle";
        svg.Append($"""<text x="{F(x)}" y="{F(Math.Max(9, y))}" font-size="7.5" font-weight="bold" fill="#0b0b0b" text-anchor="{anchor}">{WebUtility.HtmlEncode(text)}</text>""");
    }
}

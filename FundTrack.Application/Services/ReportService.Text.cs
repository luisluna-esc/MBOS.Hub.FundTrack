using System.Text;
using ClosedXML.Excel;
using FundTrack.Application.Dtos.Reports;

namespace FundTrack.Application.Services;

// Descarga en texto plano (.txt): se arma desde el mismo Excel del reporte, así tiene exactamente
// los mismos datos. Cada hoja sale como una tabla en columnas alineadas (los números a la derecha),
// con los títulos de la hoja arriba. Se guarda en UTF-8 con BOM para que el Bloc de notas muestre
// bien las tildes.
public partial class ReportService
{
    private const string TextContentType = "text/plain; charset=utf-8";
    private const string ColumnGap = "   ";

    /// <summary>El Excel tal cual, o convertido a texto si se pidió TXT.</summary>
    private static ReportFile SpreadsheetFile(byte[] xlsx, ReportFormat format, string fileBase) =>
        format == ReportFormat.Txt
            ? new ReportFile(WorkbookToText(xlsx), TextContentType, $"{fileBase}.txt")
            : new ReportFile(xlsx, ExcelContentType, $"{fileBase}.xlsx");

    private static byte[] WorkbookToText(byte[] xlsx)
    {
        using var workbook = new XLWorkbook(new MemoryStream(xlsx));
        var text = new StringBuilder();

        foreach (var sheet in workbook.Worksheets)
        {
            var used = sheet.RangeUsed();
            if (used is null)
                continue;

            if (workbook.Worksheets.Count > 1)
            {
                if (text.Length > 0) text.AppendLine().AppendLine();
                text.AppendLine($"[{sheet.Name}]");
            }

            var firstColumn = used.FirstColumn().ColumnNumber();
            var lastColumn = used.LastColumn().ColumnNumber();
            var columnCount = lastColumn - firstColumn + 1;

            // Filas de título (una celda combinada a lo ancho): van solas, sin alinear, y no cuentan
            // para el ancho de las columnas.
            var lines = new List<(string? Title, string[] Cells, bool[] Numeric)>();
            foreach (var row in used.Rows())
            {
                var rowNumber = row.RowNumber();
                var first = sheet.Cell(rowNumber, firstColumn);
                var cells = new string[columnCount];
                var numeric = new bool[columnCount];
                for (var i = 0; i < columnCount; i++)
                {
                    var cell = sheet.Cell(rowNumber, firstColumn + i);
                    cells[i] = CellText(cell);
                    numeric[i] = IsNumber(cell);
                }

                var isTitle = first.IsMerged() && first.MergedRange().ColumnCount() > 1
                    && cells.Skip(first.MergedRange().ColumnCount()).All(string.IsNullOrEmpty);
                lines.Add((isTitle ? cells[0] : null, cells, numeric));
            }

            var widths = new int[columnCount];
            foreach (var line in lines.Where(l => l.Title is null))
                for (var i = 0; i < columnCount; i++)
                    widths[i] = Math.Max(widths[i], line.Cells[i].Length);

            foreach (var line in lines)
            {
                if (line.Title is not null)
                {
                    text.AppendLine(line.Title);
                    continue;
                }

                var parts = line.Cells.Select((value, i) => line.Numeric[i] ? value.PadLeft(widths[i]) : value.PadRight(widths[i]));
                text.AppendLine(string.Join(ColumnGap, parts).TrimEnd());
            }
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
    }

    // Las fórmulas (subtotales y totales) se calculan al leer; si ClosedXML no puede, queda vacío.
    private static string CellText(IXLCell cell)
    {
        try
        {
            return cell.GetFormattedString().Replace('\n', ' ').Replace('\r', ' ').Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsNumber(IXLCell cell)
    {
        try
        {
            return cell.Value.IsNumber;
        }
        catch
        {
            return false;
        }
    }
}

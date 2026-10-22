using FundTrack.Application.Dtos.Reports;
using FundTrack.core.Constants;
using ClosedXML.Excel;

namespace FundTrack.Application.Services;

// Versión Excel (.xlsx, ClosedXML) de los reportes, mismo criterio que CheckInvoice: usa los mismos
// datos que el PDF pero arma una tabla plana (una fila por registro, región y distrito como
// columnas), con filtros en el encabezado y totales con SUBTOTAL(9, ...), que se recalculan solos
// al filtrar. Así se puede ordenar, filtrar y armar tablas dinámicas, que es para lo que se baja un Excel.
public partial class ReportService
{
    private const string MoneyFormat = "#,##0.00";
    private const string NotSubmittedText = "No presentó";

    private sealed record ExcelColumn<T>(
        string Header,
        Func<T, object?> Value,
        string? Format = null,
        bool Total = false);

    private byte[] BuildLocalFundYearExcel(LocalFundYearReportDto report, string username)
    {
        var months = Enumerable.Range(report.MonthFrom, report.MonthTo - report.MonthFrom + 1).ToList();
        const int fixedColumns = 7;
        var columnCount = fixedColumns + months.Count * 3;

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add($"Fondo Local {report.Year}");

        var rowIndex = WriteHeaderLines(sheet, columnCount, "DETALLE DE SALDO ACMS VS ARQUEO DE FONDO LOCAL",
            [$"Año {report.Year}: {MonthNames[report.MonthFrom - 1]} a {MonthNames[report.MonthTo - 1]}", report.Scope], username);

        // Dos filas de encabezado: el mes arriba, y ACMS / Arqueo / Diferencia debajo.
        var monthRow = rowIndex + 1;
        var headerRow = monthRow + 1;
        string[] fixedHeaders = ["Región", "Distrito", "Pastor", "Código", "Iglesia", "Tipo", "N°"];
        for (var c = 0; c < fixedColumns; c++)
        {
            sheet.Range(monthRow, c + 1, headerRow, c + 1).Merge().Value = fixedHeaders[c];
        }

        for (var i = 0; i < months.Count; i++)
        {
            var first = fixedColumns + i * 3 + 1;
            sheet.Range(monthRow, first, monthRow, first + 2).Merge().Value = MonthNames[months[i] - 1].ToUpper(ReportCulture);
            sheet.Cell(headerRow, first).Value = "ACMS";
            sheet.Cell(headerRow, first + 1).Value = "Arqueo";
            sheet.Cell(headerRow, first + 2).Value = "Diferencia";
        }

        StyleHeader(sheet.Range(monthRow, 1, headerRow, columnCount));

        var firstDataRow = headerRow + 1;
        rowIndex = headerRow;
        var number = 0;
        foreach (var church in report.Churches)
        {
            rowIndex++;
            number++;
            sheet.Cell(rowIndex, 1).Value = church.RegionName;
            sheet.Cell(rowIndex, 2).Value = church.DistrictName;
            sheet.Cell(rowIndex, 3).Value = church.PastorName ?? "";
            sheet.Cell(rowIndex, 4).Value = church.ChurchCode;
            sheet.Cell(rowIndex, 5).Value = church.ChurchName;
            sheet.Cell(rowIndex, 6).Value = church.ChurchType;
            sheet.Cell(rowIndex, 7).Value = number;

            for (var i = 0; i < church.Months.Count; i++)
            {
                var month = church.Months[i];
                if (month is null)
                {
                    var missing = sheet.Cell(rowIndex, fixedColumns + i * 3 + 2);
                    missing.Value = "Sin arqueo";
                    missing.Style.Font.Italic = true;
                    missing.Style.Font.FontColor = XLColor.Gray;
                    continue;
                }

                var first = fixedColumns + i * 3 + 1;
                SetMoney(sheet.Cell(rowIndex, first), month.AcmsBalance);

                if (month.SubmissionStatus == SubmissionStatuses.NotSubmitted)
                {
                    // Texto en una columna de montos: SUBTOTAL lo ignora, así el total no cambia.
                    sheet.Cell(rowIndex, first + 1).Value = NotSubmittedText;
                    sheet.Cell(rowIndex, first + 1).Style.Font.Italic = true;
                }
                else
                {
                    SetMoney(sheet.Cell(rowIndex, first + 1), month.TotalAmount);
                    SetMoney(sheet.Cell(rowIndex, first + 2), month.Difference);
                    if (month.Difference != 0)
                    {
                        sheet.Cell(rowIndex, first + 2).Style.Font.Bold = true;
                        sheet.Cell(rowIndex, first + 2).Style.Font.FontColor = month.Difference < 0 ? XLColor.DarkRed : XLColor.DarkBlue;
                    }
                }
            }
        }

        var lastDataRow = rowIndex;
        rowIndex++;
        sheet.Cell(rowIndex, fixedColumns - 2).Value = "Total";
        for (var c = fixedColumns + 1; c <= columnCount; c++)
        {
            WriteSubtotal(sheet, rowIndex, c, firstDataRow, lastDataRow);
        }
        StyleTotals(sheet.Range(rowIndex, 1, rowIndex, columnCount));

        // Cuántas presentaron su arqueo cada mes: la Diferencia solo se llena cuando la iglesia
        // presentó, así que contar esa columna da las que presentaron; SUBTOTAL(3, ...) cuenta solo
        // las filas visibles, igual que los totales, y la columna Iglesia da cuántas hay.
        rowIndex++;
        sheet.Cell(rowIndex, fixedColumns - 2).Value = "Presentaron su arqueo";
        if (lastDataRow >= firstDataRow)
        {
            var churchColumn = $"{sheet.Cell(firstDataRow, 5).Address.ToStringRelative()}:{sheet.Cell(lastDataRow, 5).Address.ToStringRelative()}";
            for (var i = 0; i < months.Count; i++)
            {
                var diffColumn = fixedColumns + i * 3 + 3;
                var first = sheet.Cell(firstDataRow, diffColumn).Address.ToStringRelative();
                var last = sheet.Cell(lastDataRow, diffColumn).Address.ToStringRelative();
                var cell = sheet.Cell(rowIndex, fixedColumns + i * 3 + 1);
                sheet.Range(rowIndex, fixedColumns + i * 3 + 1, rowIndex, fixedColumns + i * 3 + 3).Merge();
                cell.FormulaA1 = $"SUBTOTAL(3,{first}:{last})&\" de \"&SUBTOTAL(3,{churchColumn})";
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
        }
        StyleTotals(sheet.Range(rowIndex, 1, rowIndex, columnCount));

        if (lastDataRow >= firstDataRow)
        {
            sheet.Range(headerRow, 1, lastDataRow, columnCount).SetAutoFilter();
        }
        sheet.SheetView.Freeze(headerRow, 5);

        sheet.Column(1).Width = 12;
        sheet.Column(2).Width = 20;
        sheet.Column(3).Width = 28;
        sheet.Column(4).Width = 8;
        sheet.Column(5).Width = 32;
        sheet.Column(6).Width = 9;
        sheet.Column(7).Width = 5;
        for (var c = fixedColumns + 1; c <= columnCount; c++)
        {
            sheet.Column(c).Width = 12;
        }

        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.PaperSize = XLPaperSize.A3Paper;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(monthRow, headerRow);

        return SaveWorkbook(workbook);
    }

    private byte[] BuildPendingExcel(PendingReportDto report, string username)
    {
        var columns = new List<ExcelColumn<PendingReportRowDto>>
        {
            new("Región", r => r.RegionName),
            new("Distrito", r => r.DistrictName),
            new("Pastor", r => r.PastorName),
            new("Código", r => r.ChurchCode),
            new("Iglesia", r => r.ChurchName),
            new("Tipo", r => r.ChurchType),
            new("Situación", r => PendingKindLabel(r.PendingKind)),
            new("Saldo ACMS", r => r.AcmsBalance, MoneyFormat, Total: true),
            new("Arqueo", r => r.PendingKind == "difference" ? r.TotalAmount : null, MoneyFormat, Total: true),
            new("Diferencia", r => r.PendingKind == "difference" ? r.Difference : null, MoneyFormat, Total: true),
            new("Estado diferencia", r => r.PendingKind == "difference" ? DifferenceStatusLabel(r.DifferenceStatus) : null)
        };

        string[] info =
        [
            $"Mes: {MonthNames[report.Month - 1]} {report.Year} ({(report.PeriodStatus == PeriodStatuses.Closed ? "cerrado" : "abierto")})",
            report.Scope,
            $"Sin arqueo registrado: {report.MissingCount}   No presentaron: {report.NotSubmittedCount}   Diferencias sin resolver: {report.DifferenceCount}"
        ];

        using var workbook = new XLWorkbook();
        AddSheet(workbook, $"Pendientes {report.Month:00}-{report.Year}", "PENDIENTES DE FONDO LOCAL", info, columns, report.Rows, username);
        return SaveWorkbook(workbook);
    }

    private static void AddSheet<T>(
        XLWorkbook workbook, string sheetName, string title, IReadOnlyList<string> info,
        IReadOnlyList<ExcelColumn<T>> columns, IReadOnlyList<T> rows, string username)
    {
        var sheet = workbook.Worksheets.Add(sheetName);
        var columnCount = columns.Count;
        var widths = columns.Select(c => (double)c.Header.Length).ToArray();

        var headerRow = WriteHeaderLines(sheet, columnCount, title, info, username) + 1;
        for (var c = 0; c < columnCount; c++)
        {
            sheet.Cell(headerRow, c + 1).Value = columns[c].Header;
        }
        StyleHeader(sheet.Range(headerRow, 1, headerRow, columnCount));

        var firstDataRow = headerRow + 1;
        var rowIndex = headerRow;
        foreach (var row in rows)
        {
            rowIndex++;
            for (var c = 0; c < columnCount; c++)
            {
                var value = columns[c].Value(row);
                var cell = sheet.Cell(rowIndex, c + 1);
                cell.Value = ToCellValue(value);
                if (columns[c].Format is { } format)
                {
                    cell.Style.NumberFormat.Format = format;
                }
                widths[c] = Math.Max(widths[c], DisplayLength(value));
            }
        }
        var lastDataRow = rowIndex;

        if (columns.Any(c => c.Total))
        {
            rowIndex++;
            var labelColumn = columns.TakeWhile(c => !c.Total).Count();
            if (labelColumn > 0)
            {
                sheet.Cell(rowIndex, labelColumn).Value = "Total";
            }

            for (var c = 0; c < columnCount; c++)
            {
                if (columns[c].Total)
                {
                    WriteSubtotal(sheet, rowIndex, c + 1, firstDataRow, lastDataRow);
                }
            }

            StyleTotals(sheet.Range(rowIndex, 1, rowIndex, columnCount));
        }

        if (lastDataRow >= firstDataRow)
        {
            sheet.Range(headerRow, 1, lastDataRow, columnCount).SetAutoFilter();
        }
        sheet.SheetView.FreezeRows(headerRow);

        // Ancho calculado a mano (no AdjustToContents): no depende de que el servidor tenga fuentes.
        for (var c = 0; c < columnCount; c++)
        {
            sheet.Column(c + 1).Width = Math.Clamp(widths[c] + 2, 8, 60);
        }

        sheet.PageSetup.PageOrientation = columnCount > 7 ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(headerRow, headerRow);
    }

    /// <summary>Encabezado institucional; devuelve la última fila usada.</summary>
    private static int WriteHeaderLines(IXLWorksheet sheet, int columnCount, string title, IEnumerable<string> info, string username)
    {
        var lines = new List<string>
        {
            "IGLESIA ADVENTISTA DEL SÉPTIMO DÍA, MISIÓN BOLIVIANA OCCIDENTAL SUR, LA PAZ, BOLIVIA",
            title
        };
        lines.AddRange(info.Where(l => !string.IsNullOrWhiteSpace(l)));
        lines.Add("(Expresado en Bolivianos)");
        lines.Add($"Generado el {BoliviaTime.Now:dd/MM/yyyy HH:mm} por {username}");

        var rowIndex = 0;
        foreach (var line in lines)
        {
            rowIndex++;
            var cell = sheet.Cell(rowIndex, 1);
            cell.Value = line;
            sheet.Range(rowIndex, 1, rowIndex, columnCount).Merge();
            if (rowIndex <= 2)
            {
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = rowIndex == 2 ? 14 : 11;
            }
        }

        return rowIndex;
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Alignment.WrapText = true;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void StyleTotals(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDEDED");
        range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
    }

    private static void WriteSubtotal(IXLWorksheet sheet, int row, int column, int firstDataRow, int lastDataRow)
    {
        var cell = sheet.Cell(row, column);
        if (lastDataRow >= firstDataRow)
        {
            // SUBTOTAL(9, ...) suma solo las filas visibles: el total sigue al filtro.
            var first = sheet.Cell(firstDataRow, column).Address.ToStringRelative();
            var last = sheet.Cell(lastDataRow, column).Address.ToStringRelative();
            cell.FormulaA1 = $"SUBTOTAL(9,{first}:{last})";
        }
        else
        {
            cell.Value = 0;
        }
        cell.Style.NumberFormat.Format = MoneyFormat;
    }

    private static void SetMoney(IXLCell cell, decimal value)
    {
        cell.Value = (double)value;
        cell.Style.NumberFormat.Format = MoneyFormat;
    }

    private static byte[] SaveWorkbook(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static XLCellValue ToCellValue(object? value) => value switch
    {
        null => Blank.Value,
        string text => text,
        decimal number => (double)number,
        double number => number,
        long number => number,
        int number => number,
        DateTime date => date,
        _ => value.ToString() ?? string.Empty
    };

    private static double DisplayLength(object? value) => value switch
    {
        null => 0,
        decimal number => number.ToString("N2", ReportCulture).Length,
        DateTime => 10,
        _ => value.ToString()?.Length ?? 0
    };
}

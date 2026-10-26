namespace FundTrack.Application.Dtos.Reports;

/// <summary>
/// Una gráfica de la pantalla Gráficas convertida en reporte (botón Reporte de cada gráfica): el
/// frontend manda lo que ya está dibujando (categorías, series y su tabla) y el backend arma el PDF
/// con el encabezado de la Misión, la gráfica y la tabla, o el Excel y el TXT con la tabla.
/// </summary>
public class ChartReportRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    /// <summary>Los filtros elegidos, una línea cada uno ("Año 2026", "Distrito: La Hermosa"...).</summary>
    public List<string> Filters { get; set; } = [];
    /// <summary>line, column (apiladas; los negativos bajan), bars (horizontales) o none (solo tabla).</summary>
    public string ChartType { get; set; } = "none";
    /// <summary>Formato de los valores de la gráfica: integer, money o percent.</summary>
    public string ValueFormat { get; set; } = "integer";
    public List<string> Categories { get; set; } = [];
    public List<ChartReportSeriesDto> Series { get; set; } = [];
    public List<string> Headers { get; set; } = [];
    /// <summary>Por columna: text, integer, money o percent. Los números llegan sin formato ("1234.5").</summary>
    public List<string> ColumnFormats { get; set; } = [];
    public List<List<string?>> Rows { get; set; } = [];
    /// <summary>Nombre del archivo, sin extensión.</summary>
    public string FileName { get; set; } = "grafica";
}

public class ChartReportSeriesDto
{
    public string Label { get; set; } = string.Empty;
    /// <summary>Token del tema: chart-1, chart-2, chart-3, chart-neutral, chart-pos o chart-neg.</summary>
    public string Color { get; set; } = "chart-1";
    /// <summary>Un valor por categoría; null = sin dato (la línea se corta).</summary>
    public List<decimal?> Values { get; set; } = [];
}

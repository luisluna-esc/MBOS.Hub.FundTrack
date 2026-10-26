namespace FundTrack.Application.Dtos.Charts;

/// <summary>Todo lo que dibuja la pantalla Gráficas para un año (solo los meses que ya empezaron).</summary>
public class ChartsDto
{
    public int Year { get; set; }
    public ChartCountsDto Totals { get; set; } = new();
    public List<ChartMonthDto> Months { get; set; } = [];
    public List<ChartRegionDto> Regions { get; set; } = [];
    /// <summary>Las iglesias con más faltante en el año (suma de sus diferencias negativas).</summary>
    public List<ChartChurchDto> TopShortages { get; set; } = [];
}

/// <summary>En qué quedaron las iglesias que debían presentar arqueo, y los montos.</summary>
public class ChartCountsDto
{
    public int Expected { get; set; }
    public int NoDifference { get; set; }
    public int WithDifference { get; set; }
    public int NotSubmitted { get; set; }
    public int Missing { get; set; }
    /// <summary>Saldo ACMS de los arqueos presentados, para compararlo con lo que había en custodia.</summary>
    public decimal AcmsSubmitted { get; set; }
    public decimal CustodyTotal { get; set; }
    /// <summary>Suma de las diferencias negativas (falta dinero).</summary>
    public decimal Shortage { get; set; }
    /// <summary>Suma de las diferencias positivas (sobra dinero).</summary>
    public decimal Surplus { get; set; }
}

public class ChartMonthDto : ChartCountsDto
{
    public int Month { get; set; }
}

public class ChartRegionDto : ChartCountsDto
{
    public long RegionId { get; set; }
    public string RegionName { get; set; } = string.Empty;
}

public class ChartChurchDto
{
    public string ChurchCode { get; set; } = string.Empty;
    public string ChurchName { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public int Months { get; set; }
    public decimal Shortage { get; set; }
}

/// <summary>Fila de sp_chart_counts: un mes y una región.</summary>
public class ChartCountsRowDto : ChartCountsDto
{
    public int PeriodMonth { get; set; }
    public long RegionId { get; set; }
    public string RegionName { get; set; } = string.Empty;
}

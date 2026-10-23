namespace FundTrack.Application.Dtos.Reports;

public enum ReportFormat
{
    Pdf,
    Excel,
    /// <summary>Texto plano en columnas, armado desde la misma hoja del Excel.</summary>
    Txt
}

/// <summary>El archivo generado, o el error si los filtros no son válidos.</summary>
public record ReportResult(ReportFile? File, FundTrack.core.Entities.ResponseApi.DisplayFormat.ResponseGetObject? Error);

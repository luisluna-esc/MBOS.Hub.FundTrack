namespace FundTrack.Application.Dtos.Reports;

/// <summary>Un reporte generado, listo para devolverlo como descarga.</summary>
public record ReportFile(byte[] Content, string ContentType, string FileName);

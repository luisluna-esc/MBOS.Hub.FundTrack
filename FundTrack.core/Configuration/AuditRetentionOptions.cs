namespace FundTrack.core.Configuration;

public class AuditRetentionOptions
{
    /// <summary>Años que se conserva la auditoría. La Misión pidió de 2 a 3.</summary>
    public int RetentionYears { get; set; } = 3;
    /// <summary>Cada cuántas horas se revisa y se borra lo vencido.</summary>
    public int IntervalHours { get; set; } = 24;
}

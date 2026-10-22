namespace FundTrack.core.Constants;

/// <summary>
/// Una diferencia queda pendiente y, si la Misión lo marca, pasa a resuelta. No hay paso de
/// "justificada": el ACMS es el que cubre lo que falta o sobra, este sistema solo lo registra.
/// </summary>
public static class DifferenceStatuses
{
    /// <summary>El arqueo coincide con el ACMS.</summary>
    public const string None = "none";
    public const string Pending = "pending";
    /// <summary>La Misión la dio por resuelta.</summary>
    public const string Resolved = "resolved";

    public static readonly string[] All = [None, Pending, Resolved];

    /// <summary>El siguiente estado permitido, o null si ya no puede avanzar.</summary>
    public static string? NextOf(string? current) => current switch
    {
        Pending => Resolved,
        _ => null
    };
}

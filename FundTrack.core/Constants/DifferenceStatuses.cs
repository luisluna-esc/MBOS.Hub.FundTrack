namespace FundTrack.core.Constants;

/// <summary>Una diferencia avanza solo en este orden: pending, justified, resolved.</summary>
public static class DifferenceStatuses
{
    /// <summary>El arqueo coincide con el ACMS.</summary>
    public const string None = "none";
    public const string Pending = "pending";
    /// <summary>El tesorero la justificó revisando el ACMS.</summary>
    public const string Justified = "justified";
    /// <summary>La Misión la dio por resuelta.</summary>
    public const string Resolved = "resolved";

    public static readonly string[] All = [None, Pending, Justified, Resolved];

    /// <summary>El siguiente estado permitido, o null si ya no puede avanzar.</summary>
    public static string? NextOf(string? current) => current switch
    {
        Pending => Justified,
        Justified => Resolved,
        _ => null
    };
}

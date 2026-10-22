namespace FundTrack.core.Constants;

/// <summary>Montos de un arqueo que se pueden corregir después del cierre del período.</summary>
public static class CorrectionFields
{
    public const string AcmsBalance = "acms_balance";
    /// <summary>Total fondos en custodia del tesorero.</summary>
    public const string TotalAmount = "total_amount";

    public static readonly string[] All = [AcmsBalance, TotalAmount];
}

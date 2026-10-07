namespace FundTrack.core.Constants;

/// <summary>Montos de un arqueo que se pueden corregir después del cierre del período.</summary>
public static class CorrectionFields
{
    public const string AcmsBalance = "acms_balance";
    public const string CashAmount = "cash_amount";
    public const string BankAmount = "bank_amount";

    public static readonly string[] All = [AcmsBalance, CashAmount, BankAmount];
}

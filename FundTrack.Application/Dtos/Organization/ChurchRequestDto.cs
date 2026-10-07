namespace FundTrack.Application.Dtos.Organization;

public class ChurchRequestDto
{
    /// <summary>Código ACMS. En una Filial se ignora: toma siempre el código de su madre.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Obligatorio si el tipo es Filial (su Iglesia o Grupo madre); vacío en los demás tipos.</summary>
    public long? ParentChurchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long DistrictId { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? TreasurerName { get; set; }
    public string? TreasurerPhone { get; set; }
    public string? ElderName { get; set; }
    public string? ElderPhone { get; set; }

    // Solo al editar: si cambia el tipo, el distrito o el nombre, se guardan en church_history.
    public DateOnly? ChangeDate { get; set; }
    public string? ChangeReason { get; set; }
}

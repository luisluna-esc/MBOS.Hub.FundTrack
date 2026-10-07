namespace FundTrack.Application.Dtos.Security;

public class MenuTreeItemDto
{
    public long MenuId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Clave de traducción opcional (ej. "nav.home"); si es null, el frontend usa <see cref="Name"/> literal.</summary>
    public string? TranslationKey { get; set; }
    public string? Route { get; set; }
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>Roles del usuario que tienen este item marcado en menu_role. Permite al frontend re-filtrar por "rol activo".</summary>
    public List<string> Roles { get; set; } = [];
    public List<MenuTreeItemDto> Children { get; set; } = [];
}

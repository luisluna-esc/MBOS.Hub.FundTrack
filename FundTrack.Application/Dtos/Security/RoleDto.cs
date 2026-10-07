namespace FundTrack.Application.Dtos.Security;

public class RoleDto
{
    public long RoleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

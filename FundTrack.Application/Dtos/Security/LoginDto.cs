namespace FundTrack.Application.Dtos.Security;

public class LoginDto
{
    /// <summary>nombre.apellido</summary>
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

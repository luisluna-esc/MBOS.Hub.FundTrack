namespace FundTrack.Application.Dtos.Security;

public class AuthResponseDto
{
    public long AppUserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>True cuando el usuario entra con una contraseña temporal y el frontend debe obligarlo a cambiarla.</summary>
    public bool MustChangePassword { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public IEnumerable<string> Roles { get; set; } = [];
}

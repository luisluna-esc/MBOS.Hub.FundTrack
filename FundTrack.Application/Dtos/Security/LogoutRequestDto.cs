namespace FundTrack.Application.Dtos.Security;

public class LogoutRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

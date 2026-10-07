namespace FundTrack.core.Configuration;

public class PasswordResetOptions
{
    public int TokenExpirationMinutes { get; set; } = 60;
    /// <summary>Solo para desarrollo, mientras no haya SMTP: escribe el token de recuperación en el log.</summary>
    public bool LogTokenToConsole { get; set; }
}

namespace FundTrack.Application.Interfaces.Security;

public interface ICurrentUserService
{
    long? AppUserId { get; }
    IReadOnlyList<string> Roles { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}

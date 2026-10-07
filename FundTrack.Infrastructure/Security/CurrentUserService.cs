using System.Security.Claims;
using FundTrack.Application.Interfaces.Security;
using Microsoft.AspNetCore.Http;

namespace FundTrack.Infrastructure.Security;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long? AppUserId
    {
        get
        {
            var claimValue = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(claimValue, out var appUserId) ? appUserId : null;
        }
    }

    public IReadOnlyList<string> Roles =>
        _httpContextAccessor.HttpContext?.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var userAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();
            if (string.IsNullOrEmpty(userAgent))
                return null;

            return userAgent.Length > 500 ? userAgent[..500] : userAgent;
        }
    }
}

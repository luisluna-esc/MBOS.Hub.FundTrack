using FundTrack.core.Entities.Security;

namespace FundTrack.Application.Interfaces.Security;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(AppUser appUser, IEnumerable<string> roles);
    string GenerateRefreshToken();
}

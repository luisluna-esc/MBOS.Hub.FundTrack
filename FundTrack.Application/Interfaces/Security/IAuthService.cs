using FundTrack.Application.Dtos.Security;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;

namespace FundTrack.Application.Interfaces.Security;

public interface IAuthService
{
    Task<ResponseGetObject> Login(LoginDto loginDto, string? ipAddress);
    Task<ResponseGetObject> RefreshToken(RefreshRequestDto refreshRequestDto, string? ipAddress);
    Task<ResponsePost> Logout(LogoutRequestDto logoutRequestDto);
}

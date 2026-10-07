using FundTrack.Application.Dtos.Security;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;

namespace FundTrack.Application.Interfaces.Security;

public interface IPasswordResetService
{
    Task<ResponsePost> RequestReset(ForgotPasswordRequestDto forgotPasswordRequestDto);
    Task<ResponsePost> ConfirmReset(ResetPasswordRequestDto resetPasswordRequestDto);
    Task<ResponsePost> ChangeMyPassword(ChangePasswordRequestDto changePasswordRequestDto);
}

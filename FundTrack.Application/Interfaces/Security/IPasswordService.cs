using FundTrack.Application.Dtos.Security;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;

namespace FundTrack.Application.Interfaces.Security;

// FundTrack no tiene "olvidé mi contraseña": M-BOS crea los usuarios con una contraseña temporal
// y el usuario la cambia aquí.
public interface IPasswordService
{
    Task<ResponsePost> ChangeMyPassword(ChangePasswordRequestDto changePasswordRequestDto);
}

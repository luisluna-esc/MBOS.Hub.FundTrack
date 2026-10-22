using System.Net;
using FundTrack.Application.Dtos.Security;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Entities.Security;
using FundTrack.core.Interfaces;
using FluentValidation;

namespace FundTrack.Application.Services;

public class PasswordService : IPasswordService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<ChangePasswordRequestDto> _changeValidator;

    public PasswordService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IValidator<ChangePasswordRequestDto> changeValidator)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _changeValidator = changeValidator;
    }

    public async Task<ResponsePost> ChangeMyPassword(ChangePasswordRequestDto changePasswordRequestDto)
    {
        var validationResult = await _changeValidator.ValidateAsync(changePasswordRequestDto);
        if (!validationResult.IsValid)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = validationResult.Errors
                    .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
                    .ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var appUserId = _currentUserService.AppUserId;
        var appUser = appUserId.HasValue
            ? await _unitOfWork.Repository<AppUser>().GetByIdAsync(appUserId.Value)
            : null;

        if (appUser is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "User not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (!BCrypt.Net.BCrypt.Verify(changePasswordRequestDto.CurrentPassword, appUser.PasswordHash))
        {
            return new ResponsePost
            {
                Id = appUser.AppUserId,
                Messages = [new Message { Type = MessageType.Error, Description = "La contraseña actual no es correcta." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        appUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(changePasswordRequestDto.NewPassword);
        appUser.MustChangePassword = false;
        appUser.PasswordChangedAt = DateTime.UtcNow;

        _unitOfWork.Repository<AppUser>().Update(appUser);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = appUser.AppUserId,
            Messages = [new Message { Type = MessageType.Success, Description = "Contraseña actualizada correctamente." }],
            StatusCode = HttpStatusCode.OK
        };
    }
}

using System.Net;
using System.Security.Cryptography;
using System.Text;
using FundTrack.Application.Dtos.Security;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Configuration;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Entities.Security;
using FundTrack.core.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FundTrack.Application.Services;

public class PasswordResetService : IPasswordResetService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly PasswordResetOptions _passwordResetOptions;
    private readonly IValidator<ResetPasswordRequestDto> _resetValidator;
    private readonly IValidator<ChangePasswordRequestDto> _changeValidator;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        PasswordResetOptions passwordResetOptions,
        IValidator<ResetPasswordRequestDto> resetValidator,
        IValidator<ChangePasswordRequestDto> changeValidator,
        ILogger<PasswordResetService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _passwordResetOptions = passwordResetOptions;
        _resetValidator = resetValidator;
        _changeValidator = changeValidator;
        _logger = logger;
    }

    public async Task<ResponsePost> RequestReset(ForgotPasswordRequestDto forgotPasswordRequestDto)
    {
        // La respuesta es la misma exista o no el correo, para no revelar qué correos están registrados.
        var genericResponse = new ResponsePost
        {
            Id = 0,
            Messages = [new Message { Type = MessageType.Success, Description = "Si el correo está registrado, recibirás las instrucciones para recuperar tu contraseña." }],
            StatusCode = HttpStatusCode.OK
        };

        var email = forgotPasswordRequestDto.Email.Trim().ToLower();
        var appUser = await _unitOfWork.Repository<AppUser>().Query()
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

        if (appUser is null)
        {
            return genericResponse;
        }

        var repository = _unitOfWork.Repository<PasswordReset>();

        // Solo el último token pedido sirve: los anteriores que no se usaron se invalidan.
        var previousResets = await repository.Query()
            .Where(pr => pr.AppUserId == appUser.AppUserId && !pr.IsUsed)
            .ToListAsync();

        foreach (var previousReset in previousResets)
        {
            previousReset.IsUsed = true;
            repository.Update(previousReset);
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await repository.AddAsync(new PasswordReset
        {
            AppUserId = appUser.AppUserId,
            TokenHash = HashToken(token),
            ExpiresAt = DateTime.UtcNow.AddMinutes(_passwordResetOptions.TokenExpirationMinutes),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        });
        await _unitOfWork.SaveChangesAsync();

        // TODO: enviar el token por correo cuando haya SMTP configurado.
        if (_passwordResetOptions.LogTokenToConsole)
        {
            _logger.LogWarning("Password reset token for {Email}: {Token}", appUser.Email, token);
        }

        return genericResponse;
    }

    public async Task<ResponsePost> ConfirmReset(ResetPasswordRequestDto resetPasswordRequestDto)
    {
        var validationResult = await _resetValidator.ValidateAsync(resetPasswordRequestDto);
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

        var tokenHash = HashToken(resetPasswordRequestDto.Token);
        var repository = _unitOfWork.Repository<PasswordReset>();
        var existingReset = await repository.Query()
            .FirstOrDefaultAsync(pr => pr.TokenHash == tokenHash);

        if (existingReset is null || existingReset.IsUsed || existingReset.ExpiresAt < DateTime.UtcNow)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "El enlace de recuperación no es válido o ya venció. Pide uno nuevo." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var appUser = await _unitOfWork.Repository<AppUser>().GetByIdAsync(existingReset.AppUserId);
        if (appUser is null || !appUser.IsActive)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Este usuario está inactivo. Contacta al administrador." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var trackedReset = await repository.GetByIdAsync(existingReset.PasswordResetId);
        trackedReset!.IsUsed = true;
        repository.Update(trackedReset);

        ApplyNewPassword(appUser, resetPasswordRequestDto.NewPassword);
        appUser.FailedLoginAttempts = 0;
        appUser.LockedUntil = null;
        _unitOfWork.Repository<AppUser>().Update(appUser);

        await RevokeAllSessions(appUser.AppUserId);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = appUser.AppUserId,
            Messages = [new Message { Type = MessageType.Success, Description = "Contraseña actualizada. Ya puedes iniciar sesión." }],
            StatusCode = HttpStatusCode.OK
        };
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

        ApplyNewPassword(appUser, changePasswordRequestDto.NewPassword);
        _unitOfWork.Repository<AppUser>().Update(appUser);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = appUser.AppUserId,
            Messages = [new Message { Type = MessageType.Success, Description = "Contraseña actualizada correctamente." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static void ApplyNewPassword(AppUser appUser, string newPassword)
    {
        appUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        appUser.MustChangePassword = false;
        appUser.PasswordChangedAt = DateTime.UtcNow;
    }

    private async Task RevokeAllSessions(long appUserId)
    {
        var repository = _unitOfWork.Repository<RefreshToken>();
        var activeTokens = await repository.Query()
            .Where(rt => rt.AppUserId == appUserId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var activeToken in activeTokens)
        {
            activeToken.IsRevoked = true;
            repository.Update(activeToken);
        }
    }

    // Solo se guarda el hash: si alguien lee la tabla no puede usar los tokens.
    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

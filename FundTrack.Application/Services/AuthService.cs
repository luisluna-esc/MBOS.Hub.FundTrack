using System.Net;
using FundTrack.Application.Dtos.Security;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Configuration;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Entities.Security;
using FundTrack.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

public class AuthService : IAuthService
{
    private const int MaxFailedLoginAttempts = 5;
    private const int LockoutMinutes = 15;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly JwtOptions _jwtOptions;

    public AuthService(IUnitOfWork unitOfWork, IJwtTokenGenerator jwtTokenGenerator, JwtOptions jwtOptions)
    {
        _unitOfWork = unitOfWork;
        _jwtTokenGenerator = jwtTokenGenerator;
        _jwtOptions = jwtOptions;
    }

    public async Task<ResponseGetObject> Login(LoginDto loginDto, string? ipAddress)
    {
        var email = loginDto.Email.Trim().ToLower();
        var repository = _unitOfWork.Repository<AppUser>();

        var existingUser = await repository.Query().FirstOrDefaultAsync(u => u.Email == email);

        if (existingUser is null)
        {
            return InvalidCredentials();
        }

        var appUser = await repository.GetByIdAsync(existingUser.AppUserId);

        if (appUser!.LockedUntil.HasValue && appUser.LockedUntil.Value > DateTime.UtcNow)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = $"Demasiados intentos fallidos. Intenta de nuevo en {LockoutMinutes} minutos." }],
                StatusCode = HttpStatusCode.Unauthorized
            };
        }

        if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, appUser.PasswordHash))
        {
            // Al llegar al máximo se bloquea la cuenta y el contador vuelve a cero, así el
            // siguiente ciclo de intentos empieza limpio cuando vence el bloqueo.
            appUser.FailedLoginAttempts++;
            if (appUser.FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                appUser.FailedLoginAttempts = 0;
                appUser.LockedUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes);
            }

            repository.Update(appUser);
            await _unitOfWork.SaveChangesAsync();

            return InvalidCredentials();
        }

        if (!appUser.IsActive)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Este usuario está inactivo. Contacta al administrador." }],
                StatusCode = HttpStatusCode.Unauthorized
            };
        }

        var roles = await GetRoles(appUser.AppUserId);

        appUser.FailedLoginAttempts = 0;
        appUser.LockedUntil = null;
        appUser.LastLogin = DateTime.UtcNow;
        repository.Update(appUser);

        var authResponse = await IssueTokens(appUser, roles, ipAddress);

        return new ResponseGetObject
        {
            Data = authResponse,
            Messages = [new Message { Type = MessageType.Success, Description = "Inicio de sesión exitoso." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> RefreshToken(RefreshRequestDto refreshRequestDto, string? ipAddress)
    {
        var repository = _unitOfWork.Repository<RefreshToken>();
        var existingToken = await repository.Query()
            .FirstOrDefaultAsync(rt => rt.Token == refreshRequestDto.RefreshToken);

        if (existingToken is null || existingToken.IsRevoked || existingToken.ExpiresAt < DateTime.UtcNow)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "La sesión expiró o no es válida. Vuelve a iniciar sesión." }],
                StatusCode = HttpStatusCode.Unauthorized
            };
        }

        var appUser = await _unitOfWork.Repository<AppUser>().GetByIdAsync(existingToken.AppUserId);
        if (appUser is null || !appUser.IsActive)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Este usuario está inactivo. Contacta al administrador." }],
                StatusCode = HttpStatusCode.Unauthorized
            };
        }

        var trackedToken = await repository.GetByIdAsync(existingToken.RefreshTokenId);
        trackedToken!.IsRevoked = true;
        repository.Update(trackedToken);

        var roles = await GetRoles(appUser.AppUserId);
        var authResponse = await IssueTokens(appUser, roles, ipAddress);

        return new ResponseGetObject
        {
            Data = authResponse,
            Messages = [new Message { Type = MessageType.Success, Description = "Sesión renovada correctamente." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> Logout(LogoutRequestDto logoutRequestDto)
    {
        var repository = _unitOfWork.Repository<RefreshToken>();
        var existingToken = await repository.Query()
            .FirstOrDefaultAsync(rt => rt.Token == logoutRequestDto.RefreshToken);

        if (existingToken is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "No se encontró la sesión a cerrar." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var trackedToken = await repository.GetByIdAsync(existingToken.RefreshTokenId);
        trackedToken!.IsRevoked = true;
        repository.Update(trackedToken);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = existingToken.RefreshTokenId,
            Messages = [new Message { Type = MessageType.Success, Description = "Sesión cerrada correctamente." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static ResponseGetObject InvalidCredentials() => new()
    {
        Data = new(),
        Messages = [new Message { Type = MessageType.Error, Description = "Correo o contraseña incorrectos." }],
        StatusCode = HttpStatusCode.Unauthorized
    };

    private async Task<List<string>> GetRoles(long appUserId)
    {
        return await (
            from ur in _unitOfWork.Repository<AppUserRole>().Query()
            join r in _unitOfWork.Repository<Role>().Query() on ur.RoleId equals r.RoleId
            where ur.AppUserId == appUserId && r.IsActive
            select r.Name
        ).Distinct().ToListAsync();
    }

    private async Task<AuthResponseDto> IssueTokens(AppUser appUser, List<string> roles, string? ipAddress)
    {
        var (accessToken, accessTokenExpiresAt) = _jwtTokenGenerator.GenerateAccessToken(appUser, roles);
        var refreshTokenValue = _jwtTokenGenerator.GenerateRefreshToken();
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

        var refreshToken = new RefreshToken
        {
            AppUserId = appUser.AppUserId,
            Token = refreshTokenValue,
            ExpiresAt = refreshTokenExpiresAt,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            CreatedIp = ipAddress
        };

        await _unitOfWork.Repository<RefreshToken>().AddAsync(refreshToken);
        await _unitOfWork.SaveChangesAsync();

        return new AuthResponseDto
        {
            AppUserId = appUser.AppUserId,
            FullName = appUser.FullName,
            Email = appUser.Email,
            MustChangePassword = appUser.MustChangePassword,
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshToken = refreshTokenValue,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
            Roles = roles
        };
    }
}

using System.Net;
using FundTrack.Application.Dtos.Security;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Configuration;
using FundTrack.core.Entities.Organization;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Entities.Security;
using FundTrack.core.Interfaces;
using FundTrack.core.QueryFilters.Pagination;
using FundTrack.core.QueryFilters.Security;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

public class AppUserService : IAppUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<AppUserDto> _validator;
    private readonly ICurrentUserService _currentUserService;

    public AppUserService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<AppUserDto> validator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetAllAppUsers(PaginationQueryFilter paginationQueryFilter, AppUserQueryFilter appUserQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<AppUser>().Query();

        if (appUserQueryFilter.AppUserId.HasValue)
        {
            query = query.Where(u => u.AppUserId == appUserQueryFilter.AppUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(appUserQueryFilter.FullName))
        {
            query = query.Where(u => u.FullName == appUserQueryFilter.FullName);
        }

        if (!string.IsNullOrWhiteSpace(appUserQueryFilter.Email))
        {
            var email = appUserQueryFilter.Email.Trim().ToLower();
            query = query.Where(u => u.Email == email);
        }

        if (!string.IsNullOrWhiteSpace(appUserQueryFilter.Ci))
        {
            query = query.Where(u => u.Ci == appUserQueryFilter.Ci);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            var searchCriteria = paginationQueryFilter.SearchCriteria;
            query = query.Where(u =>
                u.FullName.Contains(searchCriteria) ||
                u.Email.Contains(searchCriteria) ||
                (u.Ci != null && u.Ci.Contains(searchCriteria)));
        }

        if (appUserQueryFilter.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == appUserQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var appUsers = await query
            .OrderBy(u => u.FullName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<AppUserDto>
            {
                Items = appUsers.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertAppUser(AppUserDto appUserDto)
    {
        var errors = await ValidateAsync(appUserDto, null);

        if (string.IsNullOrWhiteSpace(appUserDto.Password))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "Password is required." });
        }

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        // La contraseña que pone el administrador es temporal: el usuario debe cambiarla al entrar.
        var appUser = new AppUser
        {
            Ci = appUserDto.Ci,
            FullName = appUserDto.FullName,
            Email = NormalizeEmail(appUserDto.Email),
            Phone = appUserDto.Phone,
            PastorId = appUserDto.PastorId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(appUserDto.Password),
            MustChangePassword = true,
            IsActive = appUserDto.IsActive,
            CreatedById = _currentUserService.AppUserId,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<AppUser>().AddAsync(appUser);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = appUser.AppUserId,
            Messages = [new Message { Type = MessageType.Success, Description = "User created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateAppUser(long id, AppUserDto appUserDto)
    {
        var errors = await ValidateAsync(appUserDto, id);

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<AppUser>();
        var appUser = await repository.GetByIdAsync(id);

        if (appUser is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "User not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        appUser.Ci = appUserDto.Ci;
        appUser.FullName = appUserDto.FullName;
        appUser.Email = NormalizeEmail(appUserDto.Email);
        appUser.Phone = appUserDto.Phone;
        appUser.PastorId = appUserDto.PastorId;
        appUser.IsActive = appUserDto.IsActive;

        // Si el administrador pone una contraseña nueva, también desbloquea la cuenta
        // y la contraseña vuelve a ser temporal.
        if (!string.IsNullOrWhiteSpace(appUserDto.Password))
        {
            appUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(appUserDto.Password);
            appUser.MustChangePassword = true;
            appUser.FailedLoginAttempts = 0;
            appUser.LockedUntil = null;
        }

        repository.Update(appUser);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = appUser.AppUserId,
            Messages = [new Message { Type = MessageType.Success, Description = "User updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteAppUser(long id)
    {
        if (id == _currentUserService.AppUserId)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "No puedes eliminar tu propio usuario." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<AppUser>();
        var appUser = await repository.GetByIdAsync(id);

        if (appUser is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "User not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(appUser);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Un usuario que ya registró algo (auditoría, arqueos, historial) no se puede borrar
            // sin perder esa trazabilidad; en ese caso solo se desactiva.
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Este usuario tiene registros asociados y no se puede eliminar. Desactívalo en su lugar." }],
                StatusCode = HttpStatusCode.Conflict
            };
        }

        return new ResponsePost
        {
            Id = appUser.AppUserId,
            Messages = [new Message { Type = MessageType.Success, Description = "User deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task<List<Message>> ValidateAsync(AppUserDto appUserDto, long? id)
    {
        var validationResult = await _validator.ValidateAsync(appUserDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        var email = NormalizeEmail(appUserDto.Email);
        if (await _unitOfWork.Repository<AppUser>().Query().AnyAsync(u => u.Email == email && u.AppUserId != id))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "This email is already in use." });
        }

        if (appUserDto.PastorId.HasValue &&
            !await _unitOfWork.Repository<Pastor>().Query().AnyAsync(p => p.PastorId == appUserDto.PastorId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "PastorId does not reference an existing pastor." });
        }

        return errors;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLower();

    private static AppUserDto ToDto(AppUser appUser) => new()
    {
        AppUserId = appUser.AppUserId,
        Ci = appUser.Ci,
        FullName = appUser.FullName,
        Email = appUser.Email,
        Phone = appUser.Phone,
        PastorId = appUser.PastorId,
        MustChangePassword = appUser.MustChangePassword,
        PasswordChangedAt = appUser.PasswordChangedAt,
        FailedLoginAttempts = appUser.FailedLoginAttempts,
        LockedUntil = appUser.LockedUntil,
        IsActive = appUser.IsActive,
        LastLogin = appUser.LastLogin,
        CreatedById = appUser.CreatedById,
        CreatedAt = appUser.CreatedAt
    };
}

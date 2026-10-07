using System.Net;
using FundTrack.Application.Dtos.Security;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Entities.Security;
using FundTrack.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

public class AppUserRoleService : IAppUserRoleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AppUserRoleService(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetRolesByAppUser(long appUserId)
    {
        var appUser = await _unitOfWork.Repository<AppUser>().GetByIdAsync(appUserId);
        if (appUser is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "User not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var roles = await (
            from ur in _unitOfWork.Repository<AppUserRole>().Query()
            join r in _unitOfWork.Repository<Role>().Query() on ur.RoleId equals r.RoleId
            where ur.AppUserId == appUserId
            orderby r.Name
            select r
        ).ToListAsync();

        return new ResponseGetObject
        {
            Data = roles.Select(r => new RoleDto
            {
                RoleId = r.RoleId,
                Name = r.Name,
                Description = r.Description,
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> AssignRole(long appUserId, long roleId)
    {
        var appUser = await _unitOfWork.Repository<AppUser>().GetByIdAsync(appUserId);
        if (appUser is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "User not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var role = await _unitOfWork.Repository<Role>().GetByIdAsync(roleId);
        if (role is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Role not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var alreadyAssigned = await _unitOfWork.Repository<AppUserRole>().Query()
            .AnyAsync(ur => ur.AppUserId == appUserId && ur.RoleId == roleId);

        if (alreadyAssigned)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "This role is already assigned to the user." }],
                StatusCode = HttpStatusCode.Conflict
            };
        }

        await _unitOfWork.Repository<AppUserRole>().AddAsync(new AppUserRole
        {
            AppUserId = appUserId,
            RoleId = roleId,
            AssignedById = _currentUserService.AppUserId,
            AssignedAt = DateTime.UtcNow
        });
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = roleId,
            Messages = [new Message { Type = MessageType.Success, Description = "Role assigned to user successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> RemoveRole(long appUserId, long roleId)
    {
        var repository = _unitOfWork.Repository<AppUserRole>();
        var appUserRole = await repository.Query()
            .FirstOrDefaultAsync(ur => ur.AppUserId == appUserId && ur.RoleId == roleId);

        if (appUserRole is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "This role is not assigned to the user." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(appUserRole);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = roleId,
            Messages = [new Message { Type = MessageType.Success, Description = "Role removed from user successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }
}

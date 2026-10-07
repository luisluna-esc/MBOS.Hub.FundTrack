using System.Net;
using FundTrack.Application.Dtos.Security;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Configuration;
using FundTrack.core.Constants;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Entities.Security;
using FundTrack.core.Interfaces;
using FundTrack.core.QueryFilters.Pagination;
using FundTrack.core.QueryFilters.Security;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

public class RoleService : IRoleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<RoleDto> _validator;

    public RoleService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<RoleDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllRoles(PaginationQueryFilter paginationQueryFilter, RoleQueryFilter roleQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Role>().Query();

        if (roleQueryFilter.RoleId.HasValue)
        {
            query = query.Where(r => r.RoleId == roleQueryFilter.RoleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(r => r.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (roleQueryFilter.IsActive.HasValue)
        {
            query = query.Where(r => r.IsActive == roleQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var roles = await query
            .OrderBy(r => r.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<RoleDto>
            {
                Items = roles.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertRole(RoleDto roleDto)
    {
        var errors = await ValidateAsync(roleDto, null);
        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var role = new Role
        {
            Name = roleDto.Name,
            Description = roleDto.Description,
            IsActive = roleDto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Role>().AddAsync(role);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = role.RoleId,
            Messages = [new Message { Type = MessageType.Success, Description = "Role created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateRole(long id, RoleDto roleDto)
    {
        var errors = await ValidateAsync(roleDto, id);
        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<Role>();
        var role = await repository.GetByIdAsync(id);

        if (role is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Role not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        // M-BOS es el único rol que administra el sistema; renombrarlo o desactivarlo
        // dejaría a todos sin acceso a la administración.
        if (role.Name == RoleNames.Admin && (roleDto.Name != RoleNames.Admin || !roleDto.IsActive))
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = $"El rol {RoleNames.Admin} no se puede renombrar ni desactivar." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        role.Name = roleDto.Name;
        role.Description = roleDto.Description;
        role.IsActive = roleDto.IsActive;

        repository.Update(role);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = role.RoleId,
            Messages = [new Message { Type = MessageType.Success, Description = "Role updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteRole(long id)
    {
        var repository = _unitOfWork.Repository<Role>();
        var role = await repository.GetByIdAsync(id);

        if (role is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Role not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (role.Name == RoleNames.Admin)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = $"El rol {RoleNames.Admin} no se puede eliminar." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        repository.Remove(role);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = role.RoleId,
            Messages = [new Message { Type = MessageType.Success, Description = "Role deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task<List<Message>> ValidateAsync(RoleDto roleDto, long? id)
    {
        var validationResult = await _validator.ValidateAsync(roleDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (await _unitOfWork.Repository<Role>().Query().AnyAsync(r => r.Name == roleDto.Name && r.RoleId != id))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "A role with this name already exists." });
        }

        return errors;
    }

    private static RoleDto ToDto(Role role) => new()
    {
        RoleId = role.RoleId,
        Name = role.Name,
        Description = role.Description,
        IsActive = role.IsActive,
        CreatedAt = role.CreatedAt
    };
}

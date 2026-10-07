using System.Net;
using FundTrack.Application.Dtos.Security;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Configuration;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Entities.Security;
using FundTrack.core.Interfaces;
using FundTrack.core.QueryFilters.Pagination;
using FundTrack.core.QueryFilters.Security;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

public class MenuService : IMenuService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<MenuDto> _validator;
    private readonly ICurrentUserService _currentUserService;

    public MenuService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<MenuDto> validator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetAllMenus(PaginationQueryFilter paginationQueryFilter, MenuQueryFilter menuQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Menu>().Query();

        if (menuQueryFilter.MenuId.HasValue)
        {
            query = query.Where(m => m.MenuId == menuQueryFilter.MenuId.Value);
        }

        if (menuQueryFilter.ParentMenuId.HasValue)
        {
            query = query.Where(m => m.ParentMenuId == menuQueryFilter.ParentMenuId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(m => m.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (menuQueryFilter.IsActive.HasValue)
        {
            query = query.Where(m => m.IsActive == menuQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var menus = await query
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<MenuDto>
            {
                Items = menus.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertMenu(MenuDto menuDto)
    {
        var errors = await ValidateAsync(menuDto, null);
        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var menu = new Menu
        {
            Name = menuDto.Name,
            TranslationKey = menuDto.TranslationKey,
            Route = menuDto.Route,
            Icon = menuDto.Icon,
            ParentMenuId = menuDto.ParentMenuId,
            DisplayOrder = menuDto.DisplayOrder,
            IsActive = menuDto.IsActive
        };

        await _unitOfWork.Repository<Menu>().AddAsync(menu);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = menu.MenuId,
            Messages = [new Message { Type = MessageType.Success, Description = "Menu created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateMenu(long id, MenuDto menuDto)
    {
        var errors = await ValidateAsync(menuDto, id);
        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<Menu>();
        var menu = await repository.GetByIdAsync(id);

        if (menu is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Menu not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        menu.Name = menuDto.Name;
        menu.TranslationKey = menuDto.TranslationKey;
        menu.Route = menuDto.Route;
        menu.Icon = menuDto.Icon;
        menu.ParentMenuId = menuDto.ParentMenuId;
        menu.DisplayOrder = menuDto.DisplayOrder;
        menu.IsActive = menuDto.IsActive;

        repository.Update(menu);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = menu.MenuId,
            Messages = [new Message { Type = MessageType.Success, Description = "Menu updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteMenu(long id)
    {
        var repository = _unitOfWork.Repository<Menu>();
        var menu = await repository.GetByIdAsync(id);

        if (menu is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Menu not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (await repository.Query().AnyAsync(m => m.ParentMenuId == id))
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "This menu has child menus. Delete or move them first." }],
                StatusCode = HttpStatusCode.Conflict
            };
        }

        repository.Remove(menu);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = menu.MenuId,
            Messages = [new Message { Type = MessageType.Success, Description = "Menu deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetMyMenu()
    {
        var appUserId = _currentUserService.AppUserId;

        var myRoles = await (
            from ur in _unitOfWork.Repository<AppUserRole>().Query()
            join r in _unitOfWork.Repository<Role>().Query() on ur.RoleId equals r.RoleId
            where ur.AppUserId == appUserId && r.IsActive
            select new { r.RoleId, r.Name }
        ).Distinct().ToListAsync();

        var myRoleIds = myRoles.Select(r => r.RoleId).ToList();
        var roleNameById = myRoles.ToDictionary(r => r.RoleId, r => r.Name);

        // Un menu es visible para un rol solo si existe una fila en menu_role para ese
        // par (menu, rol). Un menu nuevo sin ninguna fila no lo ve nadie hasta que se
        // le asignen roles explícitamente.
        var myMenuRoles = await _unitOfWork.Repository<MenuRole>().Query()
            .Where(mr => myRoleIds.Contains(mr.RoleId))
            .ToListAsync();

        var roleNamesByMenu = myMenuRoles
            .GroupBy(mr => mr.MenuId)
            .ToDictionary(g => g.Key, g => g.Select(mr => roleNameById[mr.RoleId]).Distinct().OrderBy(n => n).ToList());

        var allowedMenuIds = roleNamesByMenu.Keys.ToList();

        var visibleByRole = await _unitOfWork.Repository<Menu>().Query()
            .Where(m => m.IsActive && allowedMenuIds.Contains(m.MenuId))
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name)
            .ToListAsync();

        // Un grupo padre (sin ruta propia) no tiene sentido mostrarlo si ninguno
        // de sus hijos quedó visible tras el filtro de rol.
        var visibleChildCountByParent = visibleByRole
            .Where(m => m.ParentMenuId.HasValue)
            .GroupBy(m => m.ParentMenuId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var menus = visibleByRole
            .Where(m => m.Route != null || visibleChildCountByParent.GetValueOrDefault(m.MenuId) > 0)
            .ToList();

        var itemsById = menus.ToDictionary(m => m.MenuId, m => new MenuTreeItemDto
        {
            MenuId = m.MenuId,
            Name = m.Name,
            TranslationKey = m.TranslationKey,
            Route = m.Route,
            Icon = m.Icon,
            DisplayOrder = m.DisplayOrder,
            Roles = roleNamesByMenu[m.MenuId]
        });

        var roots = new List<MenuTreeItemDto>();

        foreach (var menu in menus)
        {
            var item = itemsById[menu.MenuId];

            if (menu.ParentMenuId.HasValue && itemsById.TryGetValue(menu.ParentMenuId.Value, out var parent))
            {
                parent.Children.Add(item);
            }
            else
            {
                roots.Add(item);
            }
        }

        return new ResponseGetObject
        {
            Data = roots,
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task<List<Message>> ValidateAsync(MenuDto menuDto, long? id)
    {
        var validationResult = await _validator.ValidateAsync(menuDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        var repository = _unitOfWork.Repository<Menu>();

        if (menuDto.ParentMenuId.HasValue && menuDto.ParentMenuId.Value == id)
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "A menu cannot be its own parent." });
        }
        else if (menuDto.ParentMenuId.HasValue &&
            !await repository.Query().AnyAsync(m => m.MenuId == menuDto.ParentMenuId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ParentMenuId does not reference an existing menu." });
        }

        if (!string.IsNullOrWhiteSpace(menuDto.Route) &&
            await repository.Query().AnyAsync(m => m.Route == menuDto.Route && m.MenuId != id))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "Another menu already uses this route." });
        }

        return errors;
    }

    private static MenuDto ToDto(Menu menu) => new()
    {
        MenuId = menu.MenuId,
        Name = menu.Name,
        TranslationKey = menu.TranslationKey,
        Route = menu.Route,
        Icon = menu.Icon,
        ParentMenuId = menu.ParentMenuId,
        DisplayOrder = menu.DisplayOrder,
        IsActive = menu.IsActive
    };
}

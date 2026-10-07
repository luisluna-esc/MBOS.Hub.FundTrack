using FundTrack.Application.Dtos.Security;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.QueryFilters.Pagination;
using FundTrack.core.QueryFilters.Security;

namespace FundTrack.Application.Interfaces.Security;

public interface IRoleService
{
    Task<ResponseGetObject> GetAllRoles(PaginationQueryFilter paginationQueryFilter, RoleQueryFilter roleQueryFilter);
    Task<ResponsePost> InsertRole(RoleDto roleDto);
    Task<ResponsePost> UpdateRole(long id, RoleDto roleDto);
    Task<ResponsePost> DeleteRole(long id);
}

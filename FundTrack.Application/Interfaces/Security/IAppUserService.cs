using FundTrack.Application.Dtos.Security;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.QueryFilters.Pagination;
using FundTrack.core.QueryFilters.Security;

namespace FundTrack.Application.Interfaces.Security;

public interface IAppUserService
{
    Task<ResponseGetObject> GetAllAppUsers(PaginationQueryFilter paginationQueryFilter, AppUserQueryFilter appUserQueryFilter);
    Task<ResponsePost> InsertAppUser(AppUserDto appUserDto);
    Task<ResponsePost> UpdateAppUser(long id, AppUserDto appUserDto);
    Task<ResponsePost> DeleteAppUser(long id);
}

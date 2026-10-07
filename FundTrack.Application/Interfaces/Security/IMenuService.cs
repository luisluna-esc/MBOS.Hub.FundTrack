using FundTrack.Application.Dtos.Security;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.QueryFilters.Pagination;
using FundTrack.core.QueryFilters.Security;

namespace FundTrack.Application.Interfaces.Security;

public interface IMenuService
{
    Task<ResponseGetObject> GetAllMenus(PaginationQueryFilter paginationQueryFilter, MenuQueryFilter menuQueryFilter);
    Task<ResponsePost> InsertMenu(MenuDto menuDto);
    Task<ResponsePost> UpdateMenu(long id, MenuDto menuDto);
    Task<ResponsePost> DeleteMenu(long id);
    Task<ResponseGetObject> GetMyMenu();
}

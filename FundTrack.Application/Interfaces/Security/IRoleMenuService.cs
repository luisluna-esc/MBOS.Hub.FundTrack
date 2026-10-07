using FundTrack.core.Entities.ResponseApi.DisplayFormat;

namespace FundTrack.Application.Interfaces.Security;

public interface IRoleMenuService
{
    Task<ResponseGetObject> GetMenusByRole(long roleId);
    Task<ResponsePost> AssignMenu(long roleId, long menuId);
    Task<ResponsePost> RemoveMenu(long roleId, long menuId);
}

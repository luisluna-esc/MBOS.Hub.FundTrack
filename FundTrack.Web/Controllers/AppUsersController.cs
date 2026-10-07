using FundTrack.Application.Dtos.Security;
using FundTrack.Application.Interfaces.Security;
using FundTrack.core.Constants;
using FundTrack.core.QueryFilters.Pagination;
using FundTrack.core.QueryFilters.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundTrack.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleNames.Admin)]
public class AppUsersController : ControllerBase
{
    private readonly IAppUserService _appUserService;
    private readonly IAppUserRoleService _appUserRoleService;

    public AppUsersController(IAppUserService appUserService, IAppUserRoleService appUserRoleService)
    {
        _appUserService = appUserService;
        _appUserRoleService = appUserRoleService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] AppUserQueryFilter appUserQueryFilter)
    {
        var result = await _appUserService.GetAllAppUsers(paginationQueryFilter, appUserQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] AppUserDto appUserDto)
    {
        var result = await _appUserService.InsertAppUser(appUserDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] AppUserDto appUserDto)
    {
        var result = await _appUserService.UpdateAppUser(id, appUserDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _appUserService.DeleteAppUser(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("{id}/roles")]
    public async Task<IActionResult> GetRoles(long id)
    {
        var result = await _appUserRoleService.GetRolesByAppUser(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPost("{id}/roles/{roleId}")]
    public async Task<IActionResult> AssignRole(long id, long roleId)
    {
        var result = await _appUserRoleService.AssignRole(id, roleId);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpDelete("{id}/roles/{roleId}")]
    public async Task<IActionResult> RemoveRole(long id, long roleId)
    {
        var result = await _appUserRoleService.RemoveRole(id, roleId);
        return StatusCode((int)result.StatusCode, result);
    }
}

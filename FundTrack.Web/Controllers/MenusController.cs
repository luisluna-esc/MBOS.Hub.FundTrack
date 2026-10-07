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
public class MenusController : ControllerBase
{
    private readonly IMenuService _menuService;

    public MenusController(IMenuService menuService)
    {
        _menuService = menuService;
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] MenuQueryFilter menuQueryFilter)
    {
        var result = await _menuService.GetAllMenus(paginationQueryFilter, menuQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize]
    [HttpGet("my-menu")]
    public async Task<IActionResult> GetMyMenu()
    {
        var result = await _menuService.GetMyMenu();
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] MenuDto menuDto)
    {
        var result = await _menuService.InsertMenu(menuDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] MenuDto menuDto)
    {
        var result = await _menuService.UpdateMenu(id, menuDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _menuService.DeleteMenu(id);
        return StatusCode((int)result.StatusCode, result);
    }
}

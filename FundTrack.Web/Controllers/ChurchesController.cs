using FundTrack.Application.Dtos.Organization;
using FundTrack.Application.Interfaces.Organization;
using FundTrack.core.Constants;
using FundTrack.core.QueryFilters.Organization;
using FundTrack.core.QueryFilters.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundTrack.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChurchesController : ControllerBase
{
    private readonly IChurchService _churchService;

    public ChurchesController(IChurchService churchService)
    {
        _churchService = churchService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] ChurchQueryFilter churchQueryFilter)
    {
        var result = await _churchService.GetAllChurches(paginationQueryFilter, churchQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] ChurchRequestDto churchRequestDto)
    {
        var result = await _churchService.InsertChurch(churchRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] ChurchRequestDto churchRequestDto)
    {
        var result = await _churchService.UpdateChurch(id, churchRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    // Una iglesia nunca se elimina: se cierra o se fusiona y conserva su historial.
    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost("{id}/close")]
    public async Task<IActionResult> Close(long id, [FromBody] ChurchCloseRequestDto churchCloseRequestDto)
    {
        var result = await _churchService.CloseChurch(id, churchCloseRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetHistory(long id)
    {
        var result = await _churchService.GetHistory(id);
        return StatusCode((int)result.StatusCode, result);
    }
}

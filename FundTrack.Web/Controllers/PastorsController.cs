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
public class PastorsController : ControllerBase
{
    private readonly IPastorService _pastorService;

    public PastorsController(IPastorService pastorService)
    {
        _pastorService = pastorService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] PastorQueryFilter pastorQueryFilter)
    {
        var result = await _pastorService.GetAllPastors(paginationQueryFilter, pastorQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] PastorRequestDto pastorRequestDto)
    {
        var result = await _pastorService.InsertPastor(pastorRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] PastorRequestDto pastorRequestDto)
    {
        var result = await _pastorService.UpdatePastor(id, pastorRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _pastorService.DeletePastor(id);
        return StatusCode((int)result.StatusCode, result);
    }
}

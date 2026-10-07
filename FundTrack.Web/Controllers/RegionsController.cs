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
public class RegionsController : ControllerBase
{
    private readonly IRegionService _regionService;

    public RegionsController(IRegionService regionService)
    {
        _regionService = regionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] RegionQueryFilter regionQueryFilter)
    {
        var result = await _regionService.GetAllRegions(paginationQueryFilter, regionQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] RegionDto regionDto)
    {
        var result = await _regionService.InsertRegion(regionDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] RegionDto regionDto)
    {
        var result = await _regionService.UpdateRegion(id, regionDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _regionService.DeleteRegion(id);
        return StatusCode((int)result.StatusCode, result);
    }
}

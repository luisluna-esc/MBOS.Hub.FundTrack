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
public class DistrictsController : ControllerBase
{
    private readonly IDistrictService _districtService;

    public DistrictsController(IDistrictService districtService)
    {
        _districtService = districtService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] DistrictQueryFilter districtQueryFilter)
    {
        var result = await _districtService.GetAllDistricts(paginationQueryFilter, districtQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] DistrictRequestDto districtRequestDto)
    {
        var result = await _districtService.InsertDistrict(districtRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] DistrictRequestDto districtRequestDto)
    {
        var result = await _districtService.UpdateDistrict(id, districtRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _districtService.DeleteDistrict(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("{id}/pastors")]
    public async Task<IActionResult> GetPastorHistory(long id)
    {
        var result = await _districtService.GetPastorHistory(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost("{id}/pastor")]
    public async Task<IActionResult> AssignPastor(long id, [FromBody] DistrictPastorAssignRequestDto assignRequestDto)
    {
        var result = await _districtService.AssignPastor(id, assignRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }
}

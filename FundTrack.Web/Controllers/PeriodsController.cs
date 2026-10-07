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
public class PeriodsController : ControllerBase
{
    private readonly IPeriodService _periodService;

    public PeriodsController(IPeriodService periodService)
    {
        _periodService = periodService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQueryFilter paginationQueryFilter, [FromQuery] PeriodQueryFilter periodQueryFilter)
    {
        var result = await _periodService.GetAllPeriods(paginationQueryFilter, periodQueryFilter);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] PeriodRequestDto periodRequestDto)
    {
        var result = await _periodService.InsertPeriod(periodRequestDto);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost("generate-year/{year}")]
    public async Task<IActionResult> GenerateYear(int year)
    {
        var result = await _periodService.GenerateYear(year);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpPut("{id}/due-date")]
    public async Task<IActionResult> UpdateDueDate(long id, [FromBody] PeriodDueDateRequestDto dueDateRequestDto)
    {
        var result = await _periodService.UpdateDueDate(id, dueDateRequestDto.DueDate);
        return StatusCode((int)result.StatusCode, result);
    }

    // No hay endpoint para reabrir: un período cerrado queda cerrado.
    [Authorize(Roles = RoleNames.Writers)]
    [HttpPost("{id}/close")]
    public async Task<IActionResult> Close(long id)
    {
        var result = await _periodService.ClosePeriod(id);
        return StatusCode((int)result.StatusCode, result);
    }

    [Authorize(Roles = RoleNames.Writers)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _periodService.DeletePeriod(id);
        return StatusCode((int)result.StatusCode, result);
    }
}

using FundTrack.Application.Interfaces.Charts;
using FundTrack.core.QueryFilters.Charts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundTrack.Web.Controllers;

// Todos los roles ven las gráficas (el pastor solo de su distrito, lo filtra el servicio).
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChartsController : ControllerBase
{
    private readonly IChartService _chartService;

    public ChartsController(IChartService chartService)
    {
        _chartService = chartService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCharts([FromQuery] ChartsQueryFilter filter)
    {
        var result = await _chartService.GetCharts(filter);
        return StatusCode((int)result.StatusCode, result);
    }
}

using FundTrack.Application.Interfaces.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundTrack.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSummary([FromQuery] long? periodId)
    {
        var result = await _dashboardService.GetSummary(periodId);
        return StatusCode((int)result.StatusCode, result);
    }
}

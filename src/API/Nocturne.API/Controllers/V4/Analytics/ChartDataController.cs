using Microsoft.AspNetCore.Mvc;
using OpenApi.Remote.Attributes;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Controllers.V4.Analytics;

/// <summary>
/// Controller for providing pre-computed chart data for the dashboard.
/// Returns all data needed by the glucose chart in a single call:
/// glucose readings, IOB/COB series, basal delivery, treatment markers,
/// state spans, system events, and tracker markers.
/// </summary>
[ApiController]
[Route("api/v4/[controller]")]
[Produces("application/json")]
public class ChartDataController : ControllerBase
{
    private readonly IChartDataService _chartDataService;
    private readonly ILogger<ChartDataController> _logger;

    public ChartDataController(
        IChartDataService chartDataService,
        ILogger<ChartDataController> logger
    )
    {
        _chartDataService = chartDataService;
        _logger = logger;
    }

    /// <summary>
    /// Get complete dashboard chart data in a single call.
    /// Returns pre-calculated IOB, COB, basal series, categorized treatment markers,
    /// state spans, system events, tracker markers, and glucose readings.
    /// </summary>
    [HttpGet("dashboard")]
    [RemoteQuery]
    [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "*" })]
    [ProducesResponseType(typeof(DashboardChartData), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DashboardChartData>> GetDashboardChartData(
        [FromQuery] long startTime,
        [FromQuery] long endTime,
        [FromQuery] int intervalMinutes = 5,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (endTime <= startTime)
                return Problem(detail: "endTime must be greater than startTime", statusCode: 400, title: "Bad Request");

            if (intervalMinutes < 1 || intervalMinutes > 60)
                return Problem(detail: "intervalMinutes must be between 1 and 60", statusCode: 400, title: "Bad Request");

            var result = await _chartDataService.GetDashboardChartDataAsync(
                startTime,
                endTime,
                intervalMinutes,
                cancellationToken
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating dashboard chart data");
            return Problem(detail: "Internal server error", statusCode: 500, title: "Internal Server Error");
        }
    }
}

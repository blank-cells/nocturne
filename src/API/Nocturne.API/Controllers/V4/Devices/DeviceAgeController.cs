using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Controllers.V4.Devices;

/// <summary>
/// Device age endpoints for tracking consumable ages (CAGE, SAGE, IAGE, BAGE).
/// Uses the V4 DeviceEvents system.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v4/deviceage")]
public class DeviceAgeController : ControllerBase
{
    private readonly IDeviceAgeService _deviceAgeService;
    private readonly ILogger<DeviceAgeController> _logger;

    public DeviceAgeController(
        IDeviceAgeService deviceAgeService,
        ILogger<DeviceAgeController> logger)
    {
        _deviceAgeService = deviceAgeService;
        _logger = logger;
    }

    /// <summary>
    /// Get cannula/site age (CAGE)
    /// </summary>
    [HttpGet("cannula")]
    [ProducesResponseType(typeof(DeviceAgeInfo), 200)]
    public async Task<ActionResult<DeviceAgeInfo>> GetCannulaAge(
        [FromQuery] int? info = null,
        [FromQuery] int? warn = null,
        [FromQuery] int? urgent = null,
        [FromQuery] string? display = null,
        [FromQuery] bool? enableAlerts = null)
    {
        var prefs = BuildPreferences(info, warn, urgent, display, enableAlerts);
        var result = await _deviceAgeService.GetCannulaAgeAsync(prefs, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Get sensor age (SAGE)
    /// Returns both Sensor Start and Sensor Change events
    /// </summary>
    [HttpGet("sensor")]
    [ProducesResponseType(typeof(SensorAgeInfo), 200)]
    public async Task<ActionResult<SensorAgeInfo>> GetSensorAge(
        [FromQuery] int? info = null,
        [FromQuery] int? warn = null,
        [FromQuery] int? urgent = null,
        [FromQuery] string? display = null,
        [FromQuery] bool? enableAlerts = null)
    {
        var prefs = BuildPreferences(info, warn, urgent, display, enableAlerts);
        var result = await _deviceAgeService.GetSensorAgeAsync(prefs, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Get insulin reservoir age (IAGE)
    /// </summary>
    [HttpGet("insulin")]
    [ProducesResponseType(typeof(DeviceAgeInfo), 200)]
    public async Task<ActionResult<DeviceAgeInfo>> GetInsulinAge(
        [FromQuery] int? info = null,
        [FromQuery] int? warn = null,
        [FromQuery] int? urgent = null,
        [FromQuery] string? display = null,
        [FromQuery] bool? enableAlerts = null)
    {
        var prefs = BuildPreferences(info, warn, urgent, display, enableAlerts);
        var result = await _deviceAgeService.GetInsulinAgeAsync(prefs, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Get pump battery age (BAGE)
    /// </summary>
    [HttpGet("battery")]
    [ProducesResponseType(typeof(DeviceAgeInfo), 200)]
    public async Task<ActionResult<DeviceAgeInfo>> GetBatteryAge(
        [FromQuery] int? info = null,
        [FromQuery] int? warn = null,
        [FromQuery] int? urgent = null,
        [FromQuery] string? display = null,
        [FromQuery] bool? enableAlerts = null)
    {
        var prefs = BuildPreferences(info, warn, urgent, display, enableAlerts);
        var result = await _deviceAgeService.GetBatteryAgeAsync(prefs, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Get all device ages in a single call
    /// </summary>
    [HttpGet("all")]
    [ProducesResponseType(200)]
    public async Task<ActionResult> GetAllDeviceAges()
    {
        var defaultPrefs = new DeviceAgePreferences();

        var cannula = await _deviceAgeService.GetCannulaAgeAsync(defaultPrefs, HttpContext.RequestAborted);
        var sensor = await _deviceAgeService.GetSensorAgeAsync(defaultPrefs, HttpContext.RequestAborted);
        var insulin = await _deviceAgeService.GetInsulinAgeAsync(defaultPrefs, HttpContext.RequestAborted);
        var battery = await _deviceAgeService.GetBatteryAgeAsync(defaultPrefs, HttpContext.RequestAborted);

        return Ok(new
        {
            cage = cannula,
            sage = sensor,
            iage = insulin,
            bage = battery
        });
    }

    private static DeviceAgePreferences BuildPreferences(
        int? info,
        int? warn,
        int? urgent,
        string? display,
        bool? enableAlerts)
    {
        return new DeviceAgePreferences
        {
            Info = info ?? 0,
            Warn = warn ?? 0,
            Urgent = urgent ?? 0,
            Display = display ?? "hours",
            EnableAlerts = enableAlerts ?? false
        };
    }
}

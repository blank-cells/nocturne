using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nocturne.API.Controllers.V4.Platform;

/// <summary>
/// System-level endpoints for service health and coordination.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v4/system")]
public class SystemController : ControllerBase
{
    /// <summary>
    /// Accept a heartbeat from an external service (e.g. bot adapter).
    /// Returns 200 OK. Actual health tracking will be added later.
    /// </summary>
    [HttpPost("heartbeat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult Heartbeat([FromBody] HeartbeatRequest request)
    {
        return Ok();
    }
}

public class HeartbeatRequest
{
    public string[] Platforms { get; set; } = [];
    public string Service { get; set; } = string.Empty;
}

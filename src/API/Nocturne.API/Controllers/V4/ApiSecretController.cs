using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenApi.Remote.Attributes;
using Nocturne.API.Extensions;
using Nocturne.Core.Contracts.Multitenancy;

namespace Nocturne.API.Controllers.V4;

[ApiController]
[Route("api/v4/me/tenant/api-secret")]
[Produces("application/json")]
[Authorize]
public class ApiSecretController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ITenantAccessor _tenantAccessor;

    public ApiSecretController(ITenantService tenantService, ITenantAccessor tenantAccessor)
    {
        _tenantService = tenantService;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("status")]
    [RemoteQuery]
    [ProducesResponseType(typeof(ApiSecretStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        if (!HttpContext.IsAdmin())
            return Forbid();

        var tenantId = _tenantAccessor.TenantId;
        var hasSecret = await _tenantService.HasApiSecretAsync(tenantId, ct);
        return Ok(new ApiSecretStatusResponse(hasSecret));
    }

    [HttpPost("regenerate")]
    [RemoteCommand(Invalidates = ["GetStatus"])]
    [ProducesResponseType(typeof(ApiSecretRegeneratedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Regenerate(CancellationToken ct)
    {
        if (!HttpContext.IsAdmin())
            return Forbid();

        var tenantId = _tenantAccessor.TenantId;
        var newSecret = await _tenantService.RegenerateApiSecretAsync(tenantId, ct);
        return Ok(new ApiSecretRegeneratedResponse(newSecret));
    }
}

public record ApiSecretStatusResponse(bool HasSecret);
public record ApiSecretRegeneratedResponse(string ApiSecret);

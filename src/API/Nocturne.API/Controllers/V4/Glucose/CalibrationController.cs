using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.API.Models.Requests.V4;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4.Glucose;

/// <summary>
/// Controller for managing CGM calibration records
/// </summary>
[ApiController]
[Route("api/v4/glucose/calibrations")]
[Authorize]
[Produces("application/json")]
public class CalibrationController(ICalibrationRepository repo)
    : V4CrudControllerBase<Calibration, UpsertCalibrationRequest, UpsertCalibrationRequest, ICalibrationRepository>(repo)
{
    [ResponseCache(Duration = 120, VaryByQueryKeys = new[] { "*" })]
    public override Task<ActionResult<PaginatedResponse<Calibration>>> GetAll(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int limit = 100, [FromQuery] int offset = 0,
        [FromQuery] string sort = "timestamp_desc",
        [FromQuery] string? device = null, [FromQuery] string? source = null,
        CancellationToken ct = default)
        => base.GetAll(from, to, limit, offset, sort, device, source, ct);

    protected override Calibration MapCreateToModel(UpsertCalibrationRequest request) => new()
    {
        Timestamp = request.Timestamp.UtcDateTime,
        UtcOffset = request.UtcOffset,
        Device = request.Device,
        App = request.App,
        DataSource = request.DataSource,
        Slope = request.Slope,
        Intercept = request.Intercept,
        Scale = request.Scale,
    };

    protected override Calibration MapUpdateToModel(Guid id, UpsertCalibrationRequest request, Calibration existing) => new()
    {
        Id = id,
        Timestamp = request.Timestamp.UtcDateTime,
        UtcOffset = request.UtcOffset,
        Device = request.Device,
        App = request.App,
        DataSource = request.DataSource,
        Slope = request.Slope,
        Intercept = request.Intercept,
        Scale = request.Scale,
        CorrelationId = existing.CorrelationId,
        LegacyId = existing.LegacyId,
        CreatedAt = existing.CreatedAt,
        AdditionalProperties = existing.AdditionalProperties,
    };
}

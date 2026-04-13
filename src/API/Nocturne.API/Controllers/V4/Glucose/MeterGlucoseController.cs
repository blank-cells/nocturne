using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.API.Models.Requests.V4;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4.Glucose;

/// <summary>
/// Controller for managing blood glucose meter readings
/// </summary>
[ApiController]
[Route("api/v4/glucose/meter")]
[Authorize]
[Produces("application/json")]
public class MeterGlucoseController(IMeterGlucoseRepository repo)
    : V4CrudControllerBase<MeterGlucose, UpsertMeterGlucoseRequest, UpsertMeterGlucoseRequest, IMeterGlucoseRepository>(repo)
{
    [ResponseCache(Duration = 120, VaryByQueryKeys = new[] { "*" })]
    public override Task<ActionResult<PaginatedResponse<MeterGlucose>>> GetAll(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int limit = 100, [FromQuery] int offset = 0,
        [FromQuery] string sort = "timestamp_desc",
        [FromQuery] string? device = null, [FromQuery] string? source = null,
        CancellationToken ct = default)
        => base.GetAll(from, to, limit, offset, sort, device, source, ct);

    protected override MeterGlucose MapCreateToModel(UpsertMeterGlucoseRequest request) => new()
    {
        Timestamp = request.Timestamp.UtcDateTime,
        UtcOffset = request.UtcOffset,
        Device = request.Device,
        App = request.App,
        DataSource = request.DataSource,
        Mgdl = request.Mgdl,
    };

    protected override MeterGlucose MapUpdateToModel(Guid id, UpsertMeterGlucoseRequest request, MeterGlucose existing) => new()
    {
        Id = id,
        Timestamp = request.Timestamp.UtcDateTime,
        UtcOffset = request.UtcOffset,
        Device = request.Device,
        App = request.App,
        DataSource = request.DataSource,
        Mgdl = request.Mgdl,
        CorrelationId = existing.CorrelationId,
        LegacyId = existing.LegacyId,
        CreatedAt = existing.CreatedAt,
        AdditionalProperties = existing.AdditionalProperties,
    };
}

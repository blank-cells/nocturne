using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.API.Models.Requests.V4;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4.Glucose;

/// <summary>
/// Controller for managing blood glucose check observations
/// </summary>
[ApiController]
[Route("api/v4/observations/bg-checks")]
[Authorize]
[Produces("application/json")]
public class BGCheckController(IBGCheckRepository repo)
    : V4CrudControllerBase<BGCheck, UpsertBGCheckRequest, UpsertBGCheckRequest, IBGCheckRepository>(repo)
{
    protected override BGCheck MapCreateToModel(UpsertBGCheckRequest request) => new()
    {
        Timestamp = request.Timestamp.UtcDateTime,
        UtcOffset = request.UtcOffset,
        Device = request.Device,
        App = request.App,
        DataSource = request.DataSource,
        Glucose = request.Glucose,
        Units = request.Units,
        GlucoseType = request.GlucoseType,
        SyncIdentifier = request.SyncIdentifier,
    };

    protected override BGCheck MapUpdateToModel(Guid id, UpsertBGCheckRequest request, BGCheck existing) => new()
    {
        Id = id,
        Timestamp = request.Timestamp.UtcDateTime,
        UtcOffset = request.UtcOffset,
        Device = request.Device,
        App = request.App,
        DataSource = request.DataSource,
        Glucose = request.Glucose,
        Units = request.Units,
        GlucoseType = request.GlucoseType,
        CorrelationId = existing.CorrelationId,
        LegacyId = existing.LegacyId,
        CreatedAt = existing.CreatedAt,
        SyncIdentifier = existing.SyncIdentifier,
        AdditionalProperties = existing.AdditionalProperties,
    };
}

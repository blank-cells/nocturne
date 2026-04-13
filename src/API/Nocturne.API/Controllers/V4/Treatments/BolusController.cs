using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.API.Models.Requests.V4;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4.Treatments;

[ApiController]
[Route("api/v4/insulin/boluses")]
[Authorize]
[Produces("application/json")]
public class BolusController(IBolusRepository repo)
    : V4CrudControllerBase<Bolus, CreateBolusRequest, UpdateBolusRequest, IBolusRepository>(repo)
{
    [ResponseCache(Duration = 90, VaryByQueryKeys = new[] { "*" })]
    public override Task<ActionResult<PaginatedResponse<Bolus>>> GetAll(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int limit = 100, [FromQuery] int offset = 0,
        [FromQuery] string sort = "timestamp_desc",
        [FromQuery] string? device = null, [FromQuery] string? source = null,
        CancellationToken ct = default)
        => base.GetAll(from, to, limit, offset, sort, device, source, ct);

    protected override Bolus MapCreateToModel(CreateBolusRequest request) => new()
    {
        Timestamp = request.Timestamp.UtcDateTime,
        UtcOffset = request.UtcOffset,
        Device = request.Device,
        App = request.App,
        DataSource = request.DataSource,
        Insulin = request.Insulin,
        Programmed = request.Programmed,
        Delivered = request.Delivered,
        BolusType = request.BolusType,
        Kind = request.Kind,
        Automatic = request.Automatic,
        Duration = request.Duration,
        SyncIdentifier = request.SyncIdentifier,
        InsulinType = request.InsulinType,
        Unabsorbed = request.Unabsorbed,
        BolusCalculationId = request.BolusCalculationId,
        ApsSnapshotId = request.ApsSnapshotId,
    };

    protected override Bolus MapUpdateToModel(Guid id, UpdateBolusRequest request, Bolus existing) => new()
    {
        Id = id,
        Timestamp = request.Timestamp.UtcDateTime,
        UtcOffset = request.UtcOffset,
        Device = request.Device,
        App = request.App,
        DataSource = request.DataSource,
        Insulin = request.Insulin,
        Programmed = request.Programmed,
        Delivered = request.Delivered,
        BolusType = existing.BolusType,
        Kind = existing.Kind,
        Automatic = request.Automatic,
        Duration = request.Duration,
        SyncIdentifier = request.SyncIdentifier,
        InsulinType = request.InsulinType,
        Unabsorbed = request.Unabsorbed,
        BolusCalculationId = request.BolusCalculationId,
        ApsSnapshotId = request.ApsSnapshotId,
        CorrelationId = existing.CorrelationId,
        LegacyId = existing.LegacyId,
        CreatedAt = existing.CreatedAt,
        PumpRecordId = existing.PumpRecordId,
        DeviceId = existing.DeviceId,
        AdditionalProperties = existing.AdditionalProperties,
    };
}

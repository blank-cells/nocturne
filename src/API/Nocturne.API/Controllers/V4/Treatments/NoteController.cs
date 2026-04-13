using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.API.Models.Requests.V4;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4.Treatments;

/// <summary>
/// Controller for managing note observations
/// </summary>
[ApiController]
[Route("api/v4/observations/notes")]
[Authorize]
[Produces("application/json")]
public class NoteController(INoteRepository repo)
    : V4CrudControllerBase<Note, UpsertNoteRequest, UpsertNoteRequest, INoteRepository>(repo)
{
    protected override Note MapCreateToModel(UpsertNoteRequest request) => new()
    {
        Timestamp = request.Timestamp.UtcDateTime,
        UtcOffset = request.UtcOffset,
        Device = request.Device,
        App = request.App,
        DataSource = request.DataSource,
        Text = request.Text ?? string.Empty,
        EventType = request.EventType,
        IsAnnouncement = request.IsAnnouncement,
        SyncIdentifier = request.SyncIdentifier,
    };

    protected override Note MapUpdateToModel(Guid id, UpsertNoteRequest request, Note existing) => new()
    {
        Id = id,
        Timestamp = request.Timestamp.UtcDateTime,
        UtcOffset = request.UtcOffset,
        Device = request.Device,
        App = request.App,
        DataSource = request.DataSource,
        Text = request.Text ?? string.Empty,
        EventType = request.EventType,
        IsAnnouncement = request.IsAnnouncement,
        CorrelationId = existing.CorrelationId,
        LegacyId = existing.LegacyId,
        CreatedAt = existing.CreatedAt,
        SyncIdentifier = existing.SyncIdentifier,
        AdditionalProperties = existing.AdditionalProperties,
    };
}

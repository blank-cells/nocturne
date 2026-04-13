using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenApi.Remote.Attributes;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Controllers.V4.Analytics;

/// <summary>
/// Controller for managing time-ranged system states (pump modes, connectivity, overrides)
/// </summary>
[ApiController]
[Route("api/v4/state-spans")]
[Authorize]
public class StateSpansController : ControllerBase
{
    private readonly IStateSpanService _stateSpanService;

    public StateSpansController(IStateSpanService stateSpanService)
    {
        _stateSpanService = stateSpanService;
    }

    /// <summary>
    /// Query all state spans with optional filtering
    /// </summary>
    [HttpGet]
    [RemoteQuery]
    [ResponseCache(Duration = 120, VaryByQueryKeys = new[] { "*" })]
    [ProducesResponseType(typeof(IEnumerable<StateSpan>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<StateSpan>>> GetStateSpans(
        [FromQuery] StateSpanCategory? category = null,
        [FromQuery] string? state = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? source = null,
        [FromQuery] bool? active = null,
        [FromQuery] int count = 100,
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default)
    {
        var spans = await _stateSpanService.GetStateSpansAsync(
            category, state, from, to, source, active, count, skip, cancellationToken);
        return Ok(spans);
    }

    /// <summary>
    /// Get pump mode state spans
    /// </summary>
    [HttpGet("pump-modes")]
    public async Task<ActionResult<IEnumerable<StateSpan>>> GetPumpModes(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var spans = await _stateSpanService.GetStateSpansAsync(StateSpanCategory.PumpMode, from: from, to: to, cancellationToken: cancellationToken);
        return Ok(spans);
    }

    /// <summary>
    /// Get connectivity state spans
    /// </summary>
    [HttpGet("connectivity")]
    public async Task<ActionResult<IEnumerable<StateSpan>>> GetConnectivity(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var spans = await _stateSpanService.GetStateSpansAsync(StateSpanCategory.PumpConnectivity, from: from, to: to, cancellationToken: cancellationToken);
        return Ok(spans);
    }

    /// <summary>
    /// Get override state spans
    /// </summary>
    [HttpGet("overrides")]
    public async Task<ActionResult<IEnumerable<StateSpan>>> GetOverrides(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var spans = await _stateSpanService.GetStateSpansAsync(StateSpanCategory.Override, from: from, to: to, cancellationToken: cancellationToken);
        return Ok(spans);
    }

    /// <summary>
    /// Get temporary target state spans (AAPS temporary glucose targets)
    /// </summary>
    [HttpGet("temporary-targets")]
    public async Task<ActionResult<IEnumerable<StateSpan>>> GetTemporaryTargets(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var spans = await _stateSpanService.GetStateSpansAsync(StateSpanCategory.TemporaryTarget, from: from, to: to, cancellationToken: cancellationToken);
        return Ok(spans);
    }

    /// <summary>
    /// Get profile state spans
    /// </summary>
    [HttpGet("profiles")]
    public async Task<ActionResult<IEnumerable<StateSpan>>> GetProfiles(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var spans = await _stateSpanService.GetStateSpansAsync(StateSpanCategory.Profile, from: from, to: to, cancellationToken: cancellationToken);
        return Ok(spans);
    }

    /// <summary>
    /// Get sleep state spans (user-annotated sleep periods)
    /// </summary>
    [HttpGet("sleep")]
    public async Task<ActionResult<IEnumerable<StateSpan>>> GetSleep(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var spans = await _stateSpanService.GetStateSpansAsync(StateSpanCategory.Sleep, from: from, to: to, cancellationToken: cancellationToken);
        return Ok(spans);
    }

    /// <summary>
    /// Get exercise state spans (user-annotated activity periods)
    /// </summary>
    [HttpGet("exercise")]
    public async Task<ActionResult<IEnumerable<StateSpan>>> GetExercise(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var spans = await _stateSpanService.GetStateSpansAsync(StateSpanCategory.Exercise, from: from, to: to, cancellationToken: cancellationToken);
        return Ok(spans);
    }

    /// <summary>
    /// Get illness state spans (user-annotated illness periods)
    /// </summary>
    [HttpGet("illness")]
    public async Task<ActionResult<IEnumerable<StateSpan>>> GetIllness(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var spans = await _stateSpanService.GetStateSpansAsync(StateSpanCategory.Illness, from: from, to: to, cancellationToken: cancellationToken);
        return Ok(spans);
    }

    /// <summary>
    /// Get travel state spans (user-annotated travel/timezone change periods)
    /// </summary>
    [HttpGet("travel")]
    public async Task<ActionResult<IEnumerable<StateSpan>>> GetTravel(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var spans = await _stateSpanService.GetStateSpansAsync(StateSpanCategory.Travel, from: from, to: to, cancellationToken: cancellationToken);
        return Ok(spans);
    }

    /// <summary>
    /// Get all activity state spans (sleep, exercise, illness, travel)
    /// </summary>
    [HttpGet("activities")]
    public async Task<ActionResult<IEnumerable<StateSpan>>> GetActivities(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var activityCategories = new[] { StateSpanCategory.Sleep, StateSpanCategory.Exercise, StateSpanCategory.Illness, StateSpanCategory.Travel };
        var allSpans = new List<StateSpan>();

        foreach (var category in activityCategories)
        {
            var spans = await _stateSpanService.GetStateSpansAsync(category, from: from, to: to, cancellationToken: cancellationToken);
            allSpans.AddRange(spans);
        }

        return Ok(allSpans.OrderBy(s => s.StartMills));
    }

    /// <summary>
    /// Get a specific state span by ID
    /// </summary>
    [HttpGet("{id}")]
    [RemoteQuery]
    [ProducesResponseType(typeof(StateSpan), StatusCodes.Status200OK)]
    public async Task<ActionResult<StateSpan>> GetStateSpan(string id, CancellationToken cancellationToken = default)
    {
        var span = await _stateSpanService.GetStateSpanByIdAsync(id, cancellationToken);
        if (span == null)
            return NotFound();
        return Ok(span);
    }

    /// <summary>
    /// Create a new state span (manual entry)
    /// </summary>
    [HttpPost]
    [RemoteCommand(Invalidates = ["GetStateSpans"])]
    [ProducesResponseType(typeof(StateSpan), StatusCodes.Status201Created)]
    public async Task<ActionResult<StateSpan>> CreateStateSpan(
        [FromBody] CreateStateSpanRequest request,
        CancellationToken cancellationToken = default)
    {
        var stateSpan = new StateSpan
        {
            Category = request.Category,
            State = request.State,
            StartTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(request.StartMills).UtcDateTime,
            EndTimestamp = request.EndMills.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(request.EndMills.Value).UtcDateTime : null,
            Source = request.Source ?? "manual",
            Metadata = request.Metadata,
            OriginalId = request.OriginalId,
        };

        var created = await _stateSpanService.UpsertStateSpanAsync(stateSpan, cancellationToken);
        return CreatedAtAction(nameof(GetStateSpan), new { id = created.Id }, created);
    }

    /// <summary>
    /// Update an existing state span
    /// </summary>
    [HttpPut("{id}")]
    [RemoteCommand(Invalidates = ["GetStateSpans", "GetStateSpan"])]
    [ProducesResponseType(typeof(StateSpan), StatusCodes.Status200OK)]
    public async Task<ActionResult<StateSpan>> UpdateStateSpan(
        string id,
        [FromBody] UpdateStateSpanRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _stateSpanService.GetStateSpanByIdAsync(id, cancellationToken);
        if (existing == null)
            return NotFound();

        var updated = new StateSpan
        {
            Id = existing.Id,
            Category = request.Category ?? existing.Category,
            State = request.State ?? existing.State,
            StartTimestamp = request.StartMills.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(request.StartMills.Value).UtcDateTime
                : existing.StartTimestamp,
            EndTimestamp = request.EndMills.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(request.EndMills.Value).UtcDateTime
                : existing.EndTimestamp,
            Source = request.Source ?? existing.Source,
            Metadata = request.Metadata ?? existing.Metadata,
            OriginalId = existing.OriginalId,
        };

        var result = await _stateSpanService.UpdateStateSpanAsync(id, updated, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Delete a state span
    /// </summary>
    [HttpDelete("{id}")]
    [RemoteCommand(Invalidates = ["GetStateSpans"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteStateSpan(string id, CancellationToken cancellationToken = default)
    {
        var deleted = await _stateSpanService.DeleteStateSpanAsync(id, cancellationToken);
        if (!deleted)
            return NotFound();
        return NoContent();
    }
}

#region Request Models

public class CreateStateSpanRequest
{
    public StateSpanCategory Category { get; set; }
    public string? State { get; set; }
    public long StartMills { get; set; }
    public long? EndMills { get; set; }
    public string? Source { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public string? OriginalId { get; set; }
}

public class UpdateStateSpanRequest
{
    public StateSpanCategory? Category { get; set; }
    public string? State { get; set; }
    public long? StartMills { get; set; }
    public long? EndMills { get; set; }
    public string? Source { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

#endregion

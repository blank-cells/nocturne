using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenApi.Remote.Attributes;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Controllers.V4.Monitoring;

/// <summary>
/// Controller for active alert state, history, and acknowledgement.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v4/alerts")]
public class AlertsController : ControllerBase
{
    private readonly IDbContextFactory<NocturneDbContext> _contextFactory;
    private readonly IAlertAcknowledgementService _acknowledgementService;
    private readonly IAlertDeliveryService _deliveryService;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        IDbContextFactory<NocturneDbContext> contextFactory,
        IAlertAcknowledgementService acknowledgementService,
        IAlertDeliveryService deliveryService,
        ITenantAccessor tenantAccessor,
        ILogger<AlertsController> logger)
    {
        _contextFactory = contextFactory;
        _acknowledgementService = acknowledgementService;
        _deliveryService = deliveryService;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    /// <summary>
    /// List active (unresolved) excursions for the current tenant.
    /// </summary>
    [HttpGet("active")]
    [RemoteQuery]
    [ProducesResponseType(typeof(List<ActiveExcursionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ActiveExcursionResponse>>> GetActiveAlerts(CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var excursions = await db.AlertExcursions
            .AsNoTracking()
            .Include(e => e.AlertRule)
            .Include(e => e.Instances)
            .Where(e => e.EndedAt == null)
            .OrderByDescending(e => e.StartedAt)
            .ToListAsync(ct);

        var result = excursions.Select(e => new ActiveExcursionResponse
        {
            Id = e.Id,
            AlertRuleId = e.AlertRuleId,
            RuleName = e.AlertRule?.Name ?? string.Empty,
            ConditionType = e.AlertRule?.ConditionType ?? string.Empty,
            StartedAt = e.StartedAt,
            AcknowledgedAt = e.AcknowledgedAt,
            AcknowledgedBy = e.AcknowledgedBy,
            HysteresisStartedAt = e.HysteresisStartedAt,
            ActiveInstances = e.Instances
                .Where(i => i.ResolvedAt == null)
                .Select(i => new ActiveInstanceResponse
                {
                    Id = i.Id,
                    ScheduleId = i.AlertScheduleId,
                    Status = i.Status,
                    CurrentStepOrder = i.CurrentStepOrder,
                    TriggeredAt = i.TriggeredAt,
                    NextEscalationAt = i.NextEscalationAt,
                })
                .ToList(),
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Get paginated history of resolved excursions.
    /// </summary>
    [HttpGet("history")]
    [RemoteQuery]
    [ProducesResponseType(typeof(AlertHistoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AlertHistoryResponse>> GetAlertHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = db.AlertExcursions
            .AsNoTracking()
            .Include(e => e.AlertRule)
            .Where(e => e.EndedAt != null)
            .OrderByDescending(e => e.EndedAt);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var result = new AlertHistoryResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            Items = items.Select(e => new HistoryExcursionResponse
            {
                Id = e.Id,
                AlertRuleId = e.AlertRuleId,
                RuleName = e.AlertRule?.Name ?? string.Empty,
                ConditionType = e.AlertRule?.ConditionType ?? string.Empty,
                StartedAt = e.StartedAt,
                EndedAt = e.EndedAt!.Value,
                AcknowledgedAt = e.AcknowledgedAt,
                AcknowledgedBy = e.AcknowledgedBy,
            }).ToList(),
        };

        return Ok(result);
    }

    /// <summary>
    /// Acknowledge all active alerts for the current tenant.
    /// </summary>
    [HttpPost("acknowledge")]
    [RemoteCommand(Invalidates = ["GetActiveAlerts"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Acknowledge(
        [FromBody] AcknowledgeRequest request, CancellationToken ct)
    {
        var tenantId = _tenantAccessor.TenantId;

        await _acknowledgementService.AcknowledgeAllAsync(
            tenantId,
            request.AcknowledgedBy ?? "unknown",
            ct);

        return NoContent();
    }

    /// <summary>
    /// Get the current quiet hours configuration for the tenant.
    /// </summary>
    [HttpGet("quiet-hours")]
    [RemoteQuery]
    [ProducesResponseType(typeof(QuietHoursResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuietHoursResponse>> GetQuietHours(CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var tenantId = _tenantAccessor.TenantId;

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null)
            return NotFound();

        return Ok(new QuietHoursResponse
        {
            Enabled = tenant.QuietHoursStart is not null && tenant.QuietHoursEnd is not null,
            StartTime = tenant.QuietHoursStart?.ToString("HH:mm"),
            EndTime = tenant.QuietHoursEnd?.ToString("HH:mm"),
            OverrideCritical = tenant.QuietHoursOverrideCritical,
        });
    }

    /// <summary>
    /// Update quiet hours configuration for the tenant.
    /// </summary>
    [HttpPut("quiet-hours")]
    [RemoteCommand(Invalidates = ["GetQuietHours"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateQuietHours(
        [FromBody] UpdateQuietHoursRequest request, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var tenantId = _tenantAccessor.TenantId;

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null)
            return NotFound();

        if (request.Enabled)
        {
            tenant.QuietHoursStart = TimeOnly.Parse(request.StartTime!);
            tenant.QuietHoursEnd = TimeOnly.Parse(request.EndTime!);
        }
        else
        {
            tenant.QuietHoursStart = null;
            tenant.QuietHoursEnd = null;
        }

        tenant.QuietHoursOverrideCritical = request.OverrideCritical;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Snooze an alert instance for the specified duration.
    /// </summary>
    [HttpPost("instances/{instanceId:guid}/snooze")]
    [RemoteCommand(Invalidates = ["GetActiveAlerts"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SnoozeInstance(
        Guid instanceId, [FromBody] SnoozeRequest request, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var instance = await db.AlertInstances
            .Include(i => i.AlertExcursion)
                .ThenInclude(e => e!.AlertRule)
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct);

        if (instance is null)
            return NotFound();

        var rule = instance.AlertExcursion?.AlertRule;

        using var doc = JsonDocument.Parse(rule?.ClientConfiguration ?? "{}");
        var snoozeSection = doc.RootElement.TryGetProperty("snooze", out var snooze) ? snooze : default;

        var maxCount = 5;
        if (snoozeSection.ValueKind != JsonValueKind.Undefined
            && snoozeSection.TryGetProperty("maxCount", out var maxCountElement)
            && maxCountElement.ValueKind == JsonValueKind.Number)
        {
            maxCount = maxCountElement.GetInt32();
        }

        if (instance.SnoozeCount >= maxCount)
            return Problem(detail: "Maximum snooze count reached", statusCode: 409, title: "Conflict");

        instance.SnoozedUntil = DateTime.UtcNow.AddMinutes(request.Minutes);
        instance.SnoozeCount++;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Mark a delivery as successfully sent by the channel adapter.
    /// </summary>
    [HttpPost("deliveries/{deliveryId:guid}/delivered")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> MarkDelivered(
        Guid deliveryId, [FromBody] MarkDeliveredRequest request, CancellationToken ct)
    {
        await _deliveryService.MarkDeliveredAsync(
            deliveryId, request.PlatformMessageId, request.PlatformThreadId, ct);
        return NoContent();
    }

    /// <summary>
    /// Mark a delivery as failed by the channel adapter.
    /// </summary>
    [HttpPost("deliveries/{deliveryId:guid}/failed")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> MarkFailed(
        Guid deliveryId, [FromBody] MarkFailedRequest request, CancellationToken ct)
    {
        await _deliveryService.MarkFailedAsync(deliveryId, request.Error, ct);
        return NoContent();
    }

    /// <summary>
    /// Get pending deliveries for the specified channel types.
    /// Used by bot/adapter services to poll for work.
    /// </summary>
    [HttpGet("deliveries/pending")]
    [RemoteQuery]
    [ProducesResponseType(typeof(List<PendingDeliveryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PendingDeliveryResponse>>> GetPendingDeliveries(
        [FromQuery] string[] channelType, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = db.AlertDeliveries
            .AsNoTracking()
            .Where(d => d.Status == "pending");

        if (channelType.Length > 0)
            query = query.Where(d => channelType.Contains(d.ChannelType));

        var deliveries = await query
            .OrderBy(d => d.CreatedAt)
            .Select(d => new PendingDeliveryResponse
            {
                Id = d.Id,
                AlertInstanceId = d.AlertInstanceId,
                ChannelType = d.ChannelType,
                Destination = d.Destination,
                Payload = d.Payload,
                CreatedAt = d.CreatedAt,
                RetryCount = d.RetryCount,
            })
            .ToListAsync(ct);

        return Ok(deliveries);
    }
}

#region DTOs

public class ActiveExcursionResponse
{
    public Guid Id { get; set; }
    public Guid AlertRuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string ConditionType { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? HysteresisStartedAt { get; set; }
    public List<ActiveInstanceResponse> ActiveInstances { get; set; } = [];
}

public class ActiveInstanceResponse
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int CurrentStepOrder { get; set; }
    public DateTime TriggeredAt { get; set; }
    public DateTime? NextEscalationAt { get; set; }
}

public class AlertHistoryResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<HistoryExcursionResponse> Items { get; set; } = [];
}

public class HistoryExcursionResponse
{
    public Guid Id { get; set; }
    public Guid AlertRuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string ConditionType { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
}

public class AcknowledgeRequest
{
    public string? AcknowledgedBy { get; set; }
}

public class QuietHoursResponse
{
    public bool Enabled { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public bool OverrideCritical { get; set; }
}

public class UpdateQuietHoursRequest
{
    public bool Enabled { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public bool OverrideCritical { get; set; } = true;
}

public class SnoozeRequest
{
    public int Minutes { get; set; }
}

public class MarkDeliveredRequest
{
    public string? PlatformMessageId { get; set; }
    public string? PlatformThreadId { get; set; }
}

public class MarkFailedRequest
{
    public string Error { get; set; } = string.Empty;
}

public class PendingDeliveryResponse
{
    public Guid Id { get; set; }
    public Guid AlertInstanceId { get; set; }
    public string ChannelType { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public int RetryCount { get; set; }
}

#endregion

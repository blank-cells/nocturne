using Microsoft.AspNetCore.Mvc;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Controllers.V4.TenantAdmin;

/// <summary>
/// Controller for async processing status tracking
/// </summary>
[ApiController]
[Route("api/v4/processing")]
public class ProcessingController : ControllerBase
{
    private readonly IProcessingStatusService _processingStatusService;
    private readonly ILogger<ProcessingController> _logger;

    public ProcessingController(
        IProcessingStatusService processingStatusService,
        ILogger<ProcessingController> logger
    )
    {
        _processingStatusService = processingStatusService;
        _logger = logger;
    }

    /// <summary>
    /// Get the processing status for a correlation ID
    /// </summary>
    /// <param name="correlationId">The correlation ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing status or 404 if not found</returns>
    [HttpGet("status/{correlationId}")]
    [ProducesResponseType(typeof(ProcessingStatusResponse), 200)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<ActionResult<ProcessingStatusResponse>> GetProcessingStatus(
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "Processing status requested for correlation ID: {CorrelationId} from {RemoteIpAddress}",
            correlationId,
            HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown"
        );

        try
        {
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        message = "Correlation ID is required",
                        type = "client",
                    }
                );
            }

            var status = await _processingStatusService.GetStatusAsync(
                correlationId,
                cancellationToken
            );

            if (status == null)
            {
                _logger.LogDebug(
                    "No processing record found for correlation ID: {CorrelationId}",
                    correlationId
                );
                return NotFound(
                    new
                    {
                        status = 404,
                        message = $"No processing record found for correlation ID: {correlationId}",
                        type = "client",
                    }
                );
            }

            // Map to response model
            var response = new ProcessingStatusResponse
            {
                CorrelationId = status.CorrelationId,
                Status = status.Status,
                Progress = status.Progress,
                ProcessedCount = status.ProcessedCount,
                TotalCount = status.TotalCount,
                StartedAt = status.StartedAt,
                CompletedAt = status.CompletedAt,
                Errors = status.Errors,
                Results = status.Status == "completed" ? status.Results : null,
            };

            _logger.LogDebug(
                "Returning processing status for correlation ID: {CorrelationId} - Status: {Status}, Progress: {Progress}%",
                correlationId,
                status.Status,
                status.Progress
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving processing status for correlation ID: {CorrelationId}",
                correlationId
            );

            return StatusCode(
                500,
                new
                {
                    status = 500,
                    message = "Internal server error",
                    type = "internal",
                    error = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Wait for processing to complete with long polling
    /// </summary>
    /// <param name="correlationId">The correlation ID to wait for</param>
    /// <param name="timeoutSeconds">Maximum time to wait in seconds (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing status when completed or timeout response</returns>
    [HttpGet("status/{correlationId}/wait")]
    [ProducesResponseType(typeof(ProcessingStatusResponse), 200)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 408)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<ActionResult<ProcessingStatusResponse>> WaitForCompletion(
        string correlationId,
        [FromQuery] int timeoutSeconds = 30,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "Long polling request for correlation ID: {CorrelationId} with timeout: {TimeoutSeconds}s from {RemoteIpAddress}",
            correlationId,
            timeoutSeconds,
            HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown"
        );

        try
        {
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        message = "Correlation ID is required",
                        type = "client",
                    }
                );
            }

            // Validate timeout
            if (timeoutSeconds < 1 || timeoutSeconds > 300) // Max 5 minutes
            {
                return BadRequest(
                    new
                    {
                        status = 400,
                        message = "Timeout must be between 1 and 300 seconds",
                        type = "client",
                    }
                );
            }

            // Check if record exists first
            var initialStatus = await _processingStatusService.GetStatusAsync(
                correlationId,
                cancellationToken
            );
            if (initialStatus == null)
            {
                _logger.LogDebug(
                    "No processing record found for correlation ID: {CorrelationId}",
                    correlationId
                );
                return NotFound(
                    new
                    {
                        status = 404,
                        message = $"No processing record found for correlation ID: {correlationId}",
                        type = "client",
                    }
                );
            }

            // Wait for completion
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            var finalStatus = await _processingStatusService.WaitForCompletionAsync(
                correlationId,
                timeout,
                cancellationToken
            );

            if (finalStatus == null)
            {
                _logger.LogDebug(
                    "Timeout waiting for processing completion for correlation ID: {CorrelationId}",
                    correlationId
                );
                return StatusCode(
                    408,
                    new
                    {
                        status = 408,
                        message = "Timeout waiting for processing completion",
                        type = "timeout",
                        correlationId = correlationId,
                        timeoutSeconds = timeoutSeconds,
                    }
                );
            }

            // Map to response model
            var response = new ProcessingStatusResponse
            {
                CorrelationId = finalStatus.CorrelationId,
                Status = finalStatus.Status,
                Progress = finalStatus.Progress,
                ProcessedCount = finalStatus.ProcessedCount,
                TotalCount = finalStatus.TotalCount,
                StartedAt = finalStatus.StartedAt,
                CompletedAt = finalStatus.CompletedAt,
                Errors = finalStatus.Errors,
                Results = finalStatus.Status == "completed" ? finalStatus.Results : null,
            };

            _logger.LogDebug(
                "Long polling completed for correlation ID: {CorrelationId} - Final Status: {Status}",
                correlationId,
                finalStatus.Status
            );

            return Ok(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Long polling cancelled for correlation ID: {CorrelationId}",
                correlationId
            );
            return StatusCode(
                408,
                new
                {
                    status = 408,
                    message = "Request cancelled",
                    type = "cancelled",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error in long polling for correlation ID: {CorrelationId}",
                correlationId
            );

            return StatusCode(
                500,
                new
                {
                    status = 500,
                    message = "Internal server error",
                    type = "internal",
                    error = ex.Message,
                }
            );
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using OpenApi.Remote.Attributes;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Controllers.V4.Health;

/// <summary>
/// Heart rate controller for xDrip heart rate data
/// </summary>
[ApiController]
[Route("api/v4/[controller]")]
public class HeartRateController : ControllerBase
{
    private readonly IHeartRateService _heartRateService;
    private readonly ILogger<HeartRateController> _logger;

    public HeartRateController(IHeartRateService heartRateService, ILogger<HeartRateController> logger)
    {
        _heartRateService = heartRateService;
        _logger = logger;
    }

    /// <summary>
    /// Get heart rate records with optional pagination
    /// </summary>
    /// <param name="count">Maximum number of records to return (default: 10)</param>
    /// <param name="skip">Number of records to skip for pagination (default: 0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of heart rate records ordered by most recent first</returns>
    [HttpGet]
    [RemoteQuery]
    [ProducesResponseType(typeof(IEnumerable<HeartRate>), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<HeartRate>>> GetHeartRates(
        [FromQuery] int count = 10,
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var records = await _heartRateService.GetHeartRatesAsync(count, skip, cancellationToken);
            return Ok(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving heart rate records");
            return Problem(detail: "Internal server error", statusCode: 500, title: "Internal Server Error");
        }
    }

    /// <summary>
    /// Get a specific heart rate record by ID
    /// </summary>
    /// <param name="id">Record ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("{id}")]
    [RemoteQuery]
    [ProducesResponseType(typeof(HeartRate), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<HeartRate>> GetHeartRate(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var record = await _heartRateService.GetHeartRateByIdAsync(id, cancellationToken);
            if (record == null)
                return Problem(detail: $"Heart rate record with ID {id} not found", statusCode: 404, title: "Not Found");

            return Ok(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving heart rate record with ID {Id}", id);
            return Problem(detail: "Internal server error", statusCode: 500, title: "Internal Server Error");
        }
    }

    /// <summary>
    /// Create one or more heart rate records (single object or array)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(IEnumerable<HeartRate>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<HeartRate>>> CreateHeartRates(
        [FromBody] object heartRates,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (heartRates == null)
                return Problem(detail: "Heart rate data is required", statusCode: 400, title: "Bad Request");

            List<HeartRate> heartRateList;

            if (heartRates is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    heartRateList =
                        System.Text.Json.JsonSerializer.Deserialize<List<HeartRate>>(
                            jsonElement.GetRawText()
                        ) ?? [];
                }
                else
                {
                    var single = System.Text.Json.JsonSerializer.Deserialize<HeartRate>(
                        jsonElement.GetRawText()
                    );
                    heartRateList = single != null ? [single] : [];
                }
            }
            else
            {
                return Problem(detail: "Invalid data format", statusCode: 400, title: "Bad Request");
            }

            if (heartRateList.Count == 0)
                return Problem(detail: "At least one heart rate record is required", statusCode: 400, title: "Bad Request");

            var result = await _heartRateService.CreateHeartRatesAsync(heartRateList, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating heart rate records");
            return Problem(detail: "Internal server error", statusCode: 500, title: "Internal Server Error");
        }
    }

    /// <summary>
    /// Update an existing heart rate record
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(HeartRate), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<HeartRate>> UpdateHeartRate(
        string id,
        [FromBody] HeartRate heartRate,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var updated = await _heartRateService.UpdateHeartRateAsync(id, heartRate, cancellationToken);
            if (updated == null)
                return Problem(detail: $"Heart rate record with ID {id} not found", statusCode: 404, title: "Not Found");

            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating heart rate record with ID {Id}", id);
            return Problem(detail: "Internal server error", statusCode: 500, title: "Internal Server Error");
        }
    }

    /// <summary>
    /// Delete a heart rate record by ID
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult> DeleteHeartRate(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var deleted = await _heartRateService.DeleteHeartRateAsync(id, cancellationToken);
            if (!deleted)
                return Problem(detail: $"Heart rate record with ID {id} not found", statusCode: 404, title: "Not Found");

            return Ok(new { message = "Heart rate record deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting heart rate record with ID {Id}", id);
            return Problem(detail: "Internal server error", statusCode: 500, title: "Internal Server Error");
        }
    }
}

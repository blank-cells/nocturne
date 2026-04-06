using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.HomeAssistant.Models;

namespace Nocturne.Connectors.HomeAssistant.Services;

/// <summary>
///     HTTP client for the Home Assistant REST API, handling state read/write operations.
/// </summary>
public class HomeAssistantApiClient(HttpClient httpClient, ILogger<HomeAssistantApiClient> logger)
    : IHomeAssistantApiClient
{
    // Used for both serialization of outbound payloads (SetState) and deserialization of inbound
    // responses (GetState). The model's [JsonPropertyName] attributes take precedence for
    // property name mapping during deserialization; this policy applies to anonymous/unattributed types.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    ///     Retrieves the current state of a Home Assistant entity, or null if the entity does not exist.
    /// </summary>
    public virtual async Task<HomeAssistantStateResponse?> GetStateAsync(
        string entityId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        var response = await httpClient.GetAsync($"/api/states/{entityId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogDebug("HA entity {EntityId} not found", entityId);
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HomeAssistantStateResponse>(JsonOptions, ct);
    }

    /// <summary>
    ///     Creates or updates the state of a Home Assistant entity, returning true on success.
    /// </summary>
    public virtual async Task<bool> SetStateAsync(
        string entityId, string state, Dictionary<string, object> attributes,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        var payload = new { state, attributes };
        var response = await httpClient.PostAsJsonAsync(
            $"/api/states/{entityId}", payload, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to set HA state for {EntityId}: {StatusCode}",
                entityId, response.StatusCode);
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Checks whether a Home Assistant entity exists by attempting to retrieve its state.
    /// </summary>
    public virtual async Task<bool> ValidateEntityExistsAsync(
        string entityId, CancellationToken ct = default)
    {
        var state = await GetStateAsync(entityId, ct);
        return state != null;
    }
}

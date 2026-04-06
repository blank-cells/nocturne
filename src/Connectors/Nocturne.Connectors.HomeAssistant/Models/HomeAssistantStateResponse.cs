using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nocturne.Connectors.HomeAssistant.Models;

/// <summary>
///     Represents a state object returned by the Home Assistant REST API.
/// </summary>
public class HomeAssistantStateResponse
{
    /// <summary>
    ///     The fully qualified entity identifier (e.g. "sensor.glucose").
    /// </summary>
    [JsonPropertyName("entity_id")]
    public string EntityId { get; init; } = string.Empty;

    /// <summary>
    ///     The current state value of the entity.
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    /// <summary>
    ///     Additional attributes associated with the entity state, deserialized as raw JSON elements.
    /// </summary>
    [JsonPropertyName("attributes")]
    public Dictionary<string, JsonElement> Attributes { get; init; } = new();

    /// <summary>
    ///     Timestamp of the last state change.
    /// </summary>
    [JsonPropertyName("last_changed")]
    public DateTimeOffset LastChanged { get; init; }

    /// <summary>
    ///     Timestamp of the last state or attribute update.
    /// </summary>
    [JsonPropertyName("last_updated")]
    public DateTimeOffset LastUpdated { get; init; }
}

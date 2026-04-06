using Nocturne.Connectors.HomeAssistant.Models;

namespace Nocturne.Connectors.HomeAssistant.Services;

/// <summary>
///     Abstraction over the Home Assistant REST API for state read/write operations.
/// </summary>
public interface IHomeAssistantApiClient
{
    /// <summary>
    ///     Retrieves the current state of a Home Assistant entity, or null if the entity does not exist.
    /// </summary>
    Task<HomeAssistantStateResponse?> GetStateAsync(string entityId, CancellationToken ct = default);

    /// <summary>
    ///     Creates or updates the state of a Home Assistant entity, returning true on success.
    /// </summary>
    Task<bool> SetStateAsync(string entityId, string state, Dictionary<string, object> attributes,
        CancellationToken ct = default);

    /// <summary>
    ///     Checks whether a Home Assistant entity exists by attempting to retrieve its state.
    /// </summary>
    Task<bool> ValidateEntityExistsAsync(string entityId, CancellationToken ct = default);
}

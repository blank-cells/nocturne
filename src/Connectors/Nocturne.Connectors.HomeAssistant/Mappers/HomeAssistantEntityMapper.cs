using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.HomeAssistant.Models;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.HomeAssistant.Mappers;

/// <summary>
///     Maps Home Assistant state responses to Nocturne domain models.
///     Currently supports glucose sensor entities; additional data types
///     (bolus, carbs, activity, manual BG) will be added in a future task.
/// </summary>
public class HomeAssistantEntityMapper
{
    private const double MmolToMgdlFactor = 18.0182;

    private static readonly HashSet<string> InvalidStates = ["unavailable", "unknown"];

    private readonly ILogger<HomeAssistantEntityMapper> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HomeAssistantEntityMapper" /> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public HomeAssistantEntityMapper(ILogger<HomeAssistantEntityMapper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Maps a Home Assistant state response representing a glucose sensor
    ///     to a Nocturne <see cref="Entry" />.
    /// </summary>
    /// <param name="state">The Home Assistant state response to convert.</param>
    /// <returns>
    ///     An <see cref="Entry" /> with the glucose value in mg/dL, or <c>null</c>
    ///     if the state is unavailable, unknown, or non-numeric.
    /// </returns>
    public Entry? MapToEntry(HomeAssistantStateResponse state)
    {
        if (InvalidStates.Contains(state.State))
        {
            return null;
        }

        if (!double.TryParse(state.State, CultureInfo.InvariantCulture, out var value))
        {
            _logger.LogDebug(
                "Non-numeric state value '{State}' for entity {EntityId}, skipping",
                state.State,
                state.EntityId);
            return null;
        }

        var mgdl = ConvertToMgdl(value, state.Attributes);

        return new Entry
        {
            Sgv = mgdl,
            Mills = state.LastChanged.ToUnixTimeMilliseconds(),
            DataSource = DataSources.HomeAssistantConnector,
            Device = DataSources.HomeAssistantConnector,
            Type = "sgv"
        };
    }

    /// <summary>
    ///     Converts a glucose value to mg/dL based on the unit_of_measurement attribute.
    ///     If the unit is mmol/L, the value is multiplied by 18.0182.
    ///     If the unit is mg/dL or missing, the value is returned as-is.
    /// </summary>
    private static double ConvertToMgdl(double value, Dictionary<string, JsonElement> attributes)
    {
        if (attributes.TryGetValue("unit_of_measurement", out var unit)
            && unit.ValueKind == JsonValueKind.String
            && unit.GetString() == "mmol/L")
        {
            return value * MmolToMgdlFactor;
        }

        return value;
    }
}

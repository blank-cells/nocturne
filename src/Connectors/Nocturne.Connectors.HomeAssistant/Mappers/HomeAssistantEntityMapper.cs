using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.HomeAssistant.Models;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.HomeAssistant.Mappers;

/// <summary>
///     Maps Home Assistant state responses to Nocturne domain models.
///     Supports glucose, bolus, carb intake, activity, and manual BG entities.
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
        if (!TryParseNumericState(state, out var value))
            return null;

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
    ///     Maps a Home Assistant state response representing a bolus insulin delivery
    ///     to a Nocturne <see cref="Treatment" /> with <see cref="Treatment.Insulin" /> set.
    /// </summary>
    /// <param name="state">The Home Assistant state response to convert.</param>
    /// <returns>
    ///     A <see cref="Treatment" /> with the insulin value in units, or <c>null</c>
    ///     if the state is unavailable, unknown, or non-numeric.
    /// </returns>
    public Treatment? MapToBolus(HomeAssistantStateResponse state)
    {
        if (!TryParseNumericState(state, out var value))
            return null;

        return new Treatment
        {
            EventType = "Correction Bolus",
            Insulin = value,
            Mills = state.LastChanged.ToUnixTimeMilliseconds(),
            DataSource = DataSources.HomeAssistantConnector,
            EnteredBy = DataSources.HomeAssistantConnector
        };
    }

    /// <summary>
    ///     Maps a Home Assistant state response representing a carbohydrate intake
    ///     to a Nocturne <see cref="Treatment" /> with <see cref="Treatment.Carbs" /> set.
    /// </summary>
    /// <param name="state">The Home Assistant state response to convert.</param>
    /// <returns>
    ///     A <see cref="Treatment" /> with the carbs value in grams, or <c>null</c>
    ///     if the state is unavailable, unknown, or non-numeric.
    /// </returns>
    public Treatment? MapToCarbIntake(HomeAssistantStateResponse state)
    {
        if (!TryParseNumericState(state, out var value))
            return null;

        return new Treatment
        {
            EventType = "Carb Correction",
            Carbs = value,
            Mills = state.LastChanged.ToUnixTimeMilliseconds(),
            DataSource = DataSources.HomeAssistantConnector,
            EnteredBy = DataSources.HomeAssistantConnector
        };
    }

    /// <summary>
    ///     Maps a Home Assistant state response representing an activity sensor
    ///     to a Nocturne <see cref="Activity" />.
    /// </summary>
    /// <param name="state">The Home Assistant state response to convert.</param>
    /// <returns>
    ///     An <see cref="Activity" /> with the duration set, or <c>null</c>
    ///     if the state is unavailable, unknown, or non-numeric.
    /// </returns>
    public Activity? MapToActivity(HomeAssistantStateResponse state)
    {
        if (!TryParseNumericState(state, out var value))
            return null;

        return new Activity
        {
            Mills = state.LastChanged.ToUnixTimeMilliseconds(),
            Duration = value,
            EnteredBy = DataSources.HomeAssistantConnector
        };
    }

    /// <summary>
    ///     Maps a Home Assistant state response representing a manual blood glucose reading
    ///     to a Nocturne <see cref="Treatment" /> with <see cref="Treatment.Glucose" /> set.
    ///     Performs mmol/L to mg/dL conversion when the unit_of_measurement attribute indicates mmol/L.
    /// </summary>
    /// <param name="state">The Home Assistant state response to convert.</param>
    /// <returns>
    ///     A <see cref="Treatment" /> with the glucose value in mg/dL, or <c>null</c>
    ///     if the state is unavailable, unknown, or non-numeric.
    /// </returns>
    public Treatment? MapToManualBg(HomeAssistantStateResponse state)
    {
        if (!TryParseNumericState(state, out var value))
            return null;

        var mgdl = ConvertToMgdl(value, state.Attributes);

        return new Treatment
        {
            EventType = "BG Check",
            Glucose = mgdl,
            GlucoseType = "Finger",
            Units = "mg/dl",
            Mills = state.LastChanged.ToUnixTimeMilliseconds(),
            DataSource = DataSources.HomeAssistantConnector,
            EnteredBy = DataSources.HomeAssistantConnector
        };
    }

    /// <summary>
    ///     Attempts to parse the state value as a numeric double.
    ///     Returns false for unavailable, unknown, or non-numeric states.
    /// </summary>
    private bool TryParseNumericState(HomeAssistantStateResponse state, out double value)
    {
        value = 0;

        if (InvalidStates.Contains(state.State))
            return false;

        if (!double.TryParse(state.State, CultureInfo.InvariantCulture, out value))
        {
            _logger.LogDebug(
                "Non-numeric state value '{State}' for entity {EntityId}, skipping",
                state.State,
                state.EntityId);
            return false;
        }

        return true;
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

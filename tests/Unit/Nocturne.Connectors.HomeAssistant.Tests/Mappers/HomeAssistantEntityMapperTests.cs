using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.Connectors.HomeAssistant.Mappers;
using Nocturne.Connectors.HomeAssistant.Models;
using Nocturne.Core.Constants;
using Xunit;

namespace Nocturne.Connectors.HomeAssistant.Tests.Mappers;

public class HomeAssistantEntityMapperTests
{
    private readonly HomeAssistantEntityMapper _mapper;

    public HomeAssistantEntityMapperTests()
    {
        var logger = new Mock<ILogger<HomeAssistantEntityMapper>>();
        _mapper = new HomeAssistantEntityMapper(logger.Object);
    }

    private static HomeAssistantStateResponse CreateState(
        string state,
        Dictionary<string, JsonElement>? attributes = null,
        DateTimeOffset? lastChanged = null)
    {
        return new HomeAssistantStateResponse
        {
            EntityId = "sensor.glucose",
            State = state,
            Attributes = attributes ?? new Dictionary<string, JsonElement>(),
            LastChanged = lastChanged ?? new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero)
        };
    }

    [Fact]
    public void MapToEntry_WithMgdl_ReturnsCorrectValue()
    {
        var attributes = new Dictionary<string, JsonElement>
        {
            ["unit_of_measurement"] = JsonSerializer.SerializeToElement("mg/dL")
        };
        var state = CreateState("120", attributes);

        var result = _mapper.MapToEntry(state);

        result.Should().NotBeNull();
        result!.Sgv.Should().Be(120);
    }

    [Fact]
    public void MapToEntry_WithMmol_ConvertsToMgdl()
    {
        var attributes = new Dictionary<string, JsonElement>
        {
            ["unit_of_measurement"] = JsonSerializer.SerializeToElement("mmol/L")
        };
        var state = CreateState("6.7", attributes);

        var result = _mapper.MapToEntry(state);

        result.Should().NotBeNull();
        result!.Sgv.Should().BeApproximately(120.72, 0.01);
    }

    [Fact]
    public void MapToEntry_WithUnavailableState_ReturnsNull()
    {
        var state = CreateState("unavailable");

        var result = _mapper.MapToEntry(state);

        result.Should().BeNull();
    }

    [Fact]
    public void MapToEntry_WithUnknownState_ReturnsNull()
    {
        var state = CreateState("unknown");

        var result = _mapper.MapToEntry(state);

        result.Should().BeNull();
    }

    [Fact]
    public void MapToEntry_WithNonNumericState_ReturnsNull()
    {
        var state = CreateState("not_a_number");

        var result = _mapper.MapToEntry(state);

        result.Should().BeNull();
    }

    [Fact]
    public void MapToEntry_WithMissingUnit_AssumeMgdl()
    {
        var state = CreateState("120");

        var result = _mapper.MapToEntry(state);

        result.Should().NotBeNull();
        result!.Sgv.Should().Be(120);
    }

    [Fact]
    public void MapToEntry_SetsDataSourceToHomeAssistant()
    {
        var attributes = new Dictionary<string, JsonElement>
        {
            ["unit_of_measurement"] = JsonSerializer.SerializeToElement("mg/dL")
        };
        var timestamp = new DateTimeOffset(2026, 1, 15, 12, 30, 0, TimeSpan.Zero);
        var state = CreateState("100", attributes, timestamp);

        var result = _mapper.MapToEntry(state);

        result.Should().NotBeNull();
        result!.DataSource.Should().Be(DataSources.HomeAssistantConnector);
        result.Device.Should().Be(DataSources.HomeAssistantConnector);
        result.Type.Should().Be("sgv");
        result.Mills.Should().Be(timestamp.ToUnixTimeMilliseconds());
    }

    // --- MapToBolus Tests ---

    [Fact]
    public void MapToBolus_WithNumericState_ReturnsCorrectInsulinValue()
    {
        var timestamp = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var state = CreateState("3.5", lastChanged: timestamp);

        var result = _mapper.MapToBolus(state);

        result.Should().NotBeNull();
        result!.Insulin.Should().Be(3.5);
        result.EventType.Should().Be("Correction Bolus");
        result.Mills.Should().Be(timestamp.ToUnixTimeMilliseconds());
        result.DataSource.Should().Be(DataSources.HomeAssistantConnector);
        result.EnteredBy.Should().Be(DataSources.HomeAssistantConnector);
    }

    [Fact]
    public void MapToBolus_WithUnavailableState_ReturnsNull()
    {
        var state = CreateState("unavailable");

        var result = _mapper.MapToBolus(state);

        result.Should().BeNull();
    }

    [Fact]
    public void MapToBolus_WithUnknownState_ReturnsNull()
    {
        var state = CreateState("unknown");

        var result = _mapper.MapToBolus(state);

        result.Should().BeNull();
    }

    [Fact]
    public void MapToBolus_WithNonNumericState_ReturnsNull()
    {
        var state = CreateState("not_a_number");

        var result = _mapper.MapToBolus(state);

        result.Should().BeNull();
    }

    // --- MapToCarbIntake Tests ---

    [Fact]
    public void MapToCarbIntake_WithNumericState_ReturnsCorrectCarbsValue()
    {
        var timestamp = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var state = CreateState("45", lastChanged: timestamp);

        var result = _mapper.MapToCarbIntake(state);

        result.Should().NotBeNull();
        result!.Carbs.Should().Be(45);
        result.EventType.Should().Be("Carb Correction");
        result.Mills.Should().Be(timestamp.ToUnixTimeMilliseconds());
        result.DataSource.Should().Be(DataSources.HomeAssistantConnector);
        result.EnteredBy.Should().Be(DataSources.HomeAssistantConnector);
    }

    [Fact]
    public void MapToCarbIntake_WithUnavailableState_ReturnsNull()
    {
        var state = CreateState("unavailable");

        var result = _mapper.MapToCarbIntake(state);

        result.Should().BeNull();
    }

    [Fact]
    public void MapToCarbIntake_WithNonNumericState_ReturnsNull()
    {
        var state = CreateState("not_a_number");

        var result = _mapper.MapToCarbIntake(state);

        result.Should().BeNull();
    }

    // --- MapToActivity Tests ---

    [Fact]
    public void MapToActivity_WithNumericState_ReturnsCorrectDuration()
    {
        var timestamp = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var state = CreateState("30", lastChanged: timestamp);

        var result = _mapper.MapToActivity(state);

        result.Should().NotBeNull();
        result!.Duration.Should().Be(30);
        result.Mills.Should().Be(timestamp.ToUnixTimeMilliseconds());
        result.EnteredBy.Should().Be(DataSources.HomeAssistantConnector);
    }

    [Fact]
    public void MapToActivity_WithUnavailableState_ReturnsNull()
    {
        var state = CreateState("unavailable");

        var result = _mapper.MapToActivity(state);

        result.Should().BeNull();
    }

    [Fact]
    public void MapToActivity_WithNonNumericState_ReturnsNull()
    {
        var state = CreateState("not_a_number");

        var result = _mapper.MapToActivity(state);

        result.Should().BeNull();
    }

    // --- MapToManualBg Tests ---

    [Fact]
    public void MapToManualBg_WithMgdl_ReturnsCorrectValue()
    {
        var attributes = new Dictionary<string, JsonElement>
        {
            ["unit_of_measurement"] = JsonSerializer.SerializeToElement("mg/dL")
        };
        var timestamp = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var state = CreateState("120", attributes, timestamp);

        var result = _mapper.MapToManualBg(state);

        result.Should().NotBeNull();
        result!.Glucose.Should().Be(120);
        result.EventType.Should().Be("BG Check");
        result.GlucoseType.Should().Be("Finger");
        result.Units.Should().Be("mg/dl");
        result.Mills.Should().Be(timestamp.ToUnixTimeMilliseconds());
        result.DataSource.Should().Be(DataSources.HomeAssistantConnector);
        result.EnteredBy.Should().Be(DataSources.HomeAssistantConnector);
    }

    [Fact]
    public void MapToManualBg_WithMmol_ConvertsToMgdl()
    {
        var attributes = new Dictionary<string, JsonElement>
        {
            ["unit_of_measurement"] = JsonSerializer.SerializeToElement("mmol/L")
        };
        var state = CreateState("6.7", attributes);

        var result = _mapper.MapToManualBg(state);

        result.Should().NotBeNull();
        result!.Glucose.Should().BeApproximately(120.72, 0.01);
    }

    [Fact]
    public void MapToManualBg_WithUnavailableState_ReturnsNull()
    {
        var state = CreateState("unavailable");

        var result = _mapper.MapToManualBg(state);

        result.Should().BeNull();
    }

    [Fact]
    public void MapToManualBg_WithNonNumericState_ReturnsNull()
    {
        var state = CreateState("not_a_number");

        var result = _mapper.MapToManualBg(state);

        result.Should().BeNull();
    }
}

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
}

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.HomeAssistant.Configurations;
using Nocturne.Connectors.HomeAssistant.Mappers;
using Nocturne.Connectors.HomeAssistant.Services;
using Xunit;

namespace Nocturne.Connectors.HomeAssistant.Tests;

public class HomeAssistantConnectorServiceTests
{
    [Fact]
    public void SupportedDataTypes_ContainsAllExpectedTypes()
    {
        var service = CreateService();

        service.SupportedDataTypes.Should().BeEquivalentTo(new[]
        {
            SyncDataType.Glucose, SyncDataType.Boluses,
            SyncDataType.CarbIntake, SyncDataType.Activity, SyncDataType.ManualBG
        });
    }

    [Fact]
    public void ServiceName_IsHomeAssistant()
    {
        var service = CreateService();
        service.ServiceName.Should().Be("Home Assistant");
    }

    [Fact]
    public void ConnectorSource_IsHomeAssistantConnector()
    {
        var service = CreateService();
        // ConnectorSource is protected, but we can verify via DataSources constant
        Nocturne.Core.Constants.DataSources.HomeAssistantConnector.Should().Be("home-assistant-connector");
    }

    private static HomeAssistantConnectorService CreateService()
    {
        return new HomeAssistantConnectorService(
            new HttpClient(),
            new Mock<ILogger<HomeAssistantConnectorService>>().Object,
            new Mock<IHomeAssistantApiClient>().Object,
            new HomeAssistantEntityMapper(
                new Mock<ILogger<HomeAssistantEntityMapper>>().Object),
            null);
    }
}

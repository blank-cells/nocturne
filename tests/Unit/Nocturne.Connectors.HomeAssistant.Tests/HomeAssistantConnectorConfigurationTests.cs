using FluentAssertions;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.HomeAssistant.Configurations;
using Xunit;

namespace Nocturne.Connectors.HomeAssistant.Tests;

public class HomeAssistantConnectorConfigurationTests
{
    [Fact]
    public void Validate_WithNoUrl_ShouldFail()
    {
        var config = new HomeAssistantConnectorConfiguration();

        var act = () => config.Validate();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Url*");
    }

    [Fact]
    public void Validate_WithNoAccessToken_ShouldFail()
    {
        var config = new HomeAssistantConnectorConfiguration
        {
            Url = "http://homeassistant.local:8123"
        };

        var act = () => config.Validate();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*AccessToken*");
    }

    [Fact]
    public void Validate_WithUrlAndAccessToken_ShouldSucceed()
    {
        var config = new HomeAssistantConnectorConfiguration
        {
            Url = "http://homeassistant.local:8123",
            AccessToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.test"
        };

        var act = () => config.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void EntityMappings_DefaultsToEmpty()
    {
        var config = new HomeAssistantConnectorConfiguration();
        config.EntityMappings.Should().BeEmpty();
    }

    [Fact]
    public void WriteBackTypes_DefaultsToEmpty()
    {
        var config = new HomeAssistantConnectorConfiguration();
        config.WriteBackTypes.Should().BeEmpty();
    }

    [Fact]
    public void WriteBackEnabled_DefaultsToFalse()
    {
        var config = new HomeAssistantConnectorConfiguration();
        config.WriteBackEnabled.Should().BeFalse();
    }

    [Fact]
    public void ConnectSource_ShouldBeHomeAssistant()
    {
        var config = new HomeAssistantConnectorConfiguration();
        config.ConnectSource.Should().Be(ConnectSource.HomeAssistant);
    }
}

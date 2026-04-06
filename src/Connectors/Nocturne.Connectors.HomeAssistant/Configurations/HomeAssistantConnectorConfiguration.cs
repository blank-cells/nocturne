using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Models;
using Nocturne.Core.Constants;

namespace Nocturne.Connectors.HomeAssistant.Configurations;

[ConnectorRegistration(
    "HomeAssistant",
    ServiceNames.HomeAssistantConnector,
    "HOME_ASSISTANT",
    "ConnectSource.HomeAssistant",
    "home-assistant-connector",
    "home",
    ConnectorCategory.Sync,
    "Bidirectional sync with Home Assistant",
    "Home Assistant",
    SupportsHistoricalSync = false,
    SupportsManualSync = true,
    SupportedDataTypes = [
        SyncDataType.Glucose, SyncDataType.Boluses,
        SyncDataType.CarbIntake, SyncDataType.Activity, SyncDataType.ManualBG
    ]
)]
public class HomeAssistantConnectorConfiguration : BaseConnectorConfiguration
{
    public HomeAssistantConnectorConfiguration()
    {
        ConnectSource = ConnectSource.HomeAssistant;
    }

    [ConnectorProperty(ConnectorPropertyKey.Url, Required = true)]
    public string Url { get; set; } = string.Empty;

    [ConnectorProperty(ConnectorPropertyKey.AccessToken, Required = true, Secret = true)]
    public string AccessToken { get; set; } = string.Empty;

    public Dictionary<SyncDataType, string> EntityMappings { get; set; } = new();

    [ConnectorProperty(ConnectorPropertyKey.WriteBackEnabled)]
    public bool WriteBackEnabled { get; set; } = false;

    public HashSet<WriteBackDataType> WriteBackTypes { get; set; } = [];

    [ConnectorProperty(ConnectorPropertyKey.WebhookEnabled)]
    public bool WebhookEnabled { get; set; } = false;

    [ConnectorProperty(ConnectorPropertyKey.WebhookSecret, Secret = true)]
    public string WebhookSecret { get; set; } = string.Empty;
}

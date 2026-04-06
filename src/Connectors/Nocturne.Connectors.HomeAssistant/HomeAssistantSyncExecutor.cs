using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.HomeAssistant.Configurations;

namespace Nocturne.Connectors.HomeAssistant;

public class HomeAssistantSyncExecutor
    : ConnectorSyncExecutor<HomeAssistantConnectorService, HomeAssistantConnectorConfiguration>
{
    public override string ConnectorId => "home-assistant";
    protected override string ConnectorName => "HomeAssistant";
}

using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Nightscout.Configurations;
using Nocturne.Connectors.Nightscout.Services;

namespace Nocturne.API.Services.BackgroundServices;

public class NightscoutConnectorBackgroundService : ConnectorBackgroundService<NightscoutConnectorConfiguration>
{
    public NightscoutConnectorBackgroundService(
        IServiceProvider serviceProvider,
        NightscoutConnectorConfiguration config,
        ILogger<NightscoutConnectorBackgroundService> logger
    )
        : base(serviceProvider, config, logger) { }

    protected override string ConnectorName => "Nightscout";

    protected override async Task<SyncResult> PerformSyncAsync(IServiceProvider scopeProvider, CancellationToken cancellationToken, ISyncProgressReporter? progressReporter = null)
    {
        var connectorService = scopeProvider.GetRequiredService<NightscoutConnectorService>();
        return await connectorService.SyncDataAsync(Config, cancellationToken, since: null, progressReporter);
    }
}

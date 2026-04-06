using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Dexcom.Configurations;
using Nocturne.Connectors.Dexcom.Services;

namespace Nocturne.API.Services.BackgroundServices;

/// <summary>
/// Background service for Dexcom Share connector
/// </summary>
public class DexcomConnectorBackgroundService : ConnectorBackgroundService<DexcomConnectorConfiguration>
{
    public DexcomConnectorBackgroundService(
        IServiceProvider serviceProvider,
        DexcomConnectorConfiguration config,
        ILogger<DexcomConnectorBackgroundService> logger
    )
        : base(serviceProvider, config, logger) { }

    protected override string ConnectorName => "Dexcom";

    protected override async Task<SyncResult> PerformSyncAsync(IServiceProvider scopeProvider, CancellationToken cancellationToken, ISyncProgressReporter? progressReporter = null)
    {
        var connectorService = scopeProvider.GetRequiredService<DexcomConnectorService>();
        return await connectorService.SyncDataAsync(Config, cancellationToken, since: null, progressReporter);
    }
}

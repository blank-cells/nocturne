using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Tidepool.Configurations;
using Nocturne.Connectors.Tidepool.Services;

namespace Nocturne.API.Services.BackgroundServices;

/// <summary>
/// Background service for Tidepool connector
/// </summary>
public class TidepoolConnectorBackgroundService : ConnectorBackgroundService<TidepoolConnectorConfiguration>
{
    public TidepoolConnectorBackgroundService(
        IServiceProvider serviceProvider,
        TidepoolConnectorConfiguration config,
        ILogger<TidepoolConnectorBackgroundService> logger
    )
        : base(serviceProvider, config, logger) { }

    protected override string ConnectorName => "Tidepool";

    protected override async Task<SyncResult> PerformSyncAsync(IServiceProvider scopeProvider, CancellationToken cancellationToken, ISyncProgressReporter? progressReporter = null)
    {
        var connectorService = scopeProvider.GetRequiredService<TidepoolConnectorService>();
        return await connectorService.SyncDataAsync(Config, cancellationToken, since: null, progressReporter);
    }
}

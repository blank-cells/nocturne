using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.FreeStyle.Configurations;
using Nocturne.Connectors.FreeStyle.Services;

namespace Nocturne.API.Services.BackgroundServices;

/// <summary>
/// Background service for FreeStyle LibreLinkUp connector
/// </summary>
public class FreeStyleConnectorBackgroundService
    : ConnectorBackgroundService<LibreLinkUpConnectorConfiguration>
{
    public FreeStyleConnectorBackgroundService(
        IServiceProvider serviceProvider,
        LibreLinkUpConnectorConfiguration config,
        ILogger<FreeStyleConnectorBackgroundService> logger
    )
        : base(serviceProvider, config, logger) { }

    protected override string ConnectorName => "FreeStyle LibreLinkUp";

    protected override async Task<SyncResult> PerformSyncAsync(IServiceProvider scopeProvider, CancellationToken cancellationToken, ISyncProgressReporter? progressReporter = null)
    {
        var connectorService = scopeProvider.GetRequiredService<LibreConnectorService>();
        return await connectorService.SyncDataAsync(Config, cancellationToken, since: null, progressReporter);
    }
}

using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.MyLife.Configurations;
using Nocturne.Connectors.MyLife.Services;

namespace Nocturne.API.Services.BackgroundServices;

/// <summary>
/// Background service for MyLife connector
/// </summary>
public class MyLifeConnectorBackgroundService : ConnectorBackgroundService<MyLifeConnectorConfiguration>
{
    public MyLifeConnectorBackgroundService(
        IServiceProvider serviceProvider,
        MyLifeConnectorConfiguration config,
        ILogger<MyLifeConnectorBackgroundService> logger
    )
        : base(serviceProvider, config, logger) { }

    protected override string ConnectorName => "MyLife";

    protected override async Task<SyncResult> PerformSyncAsync(IServiceProvider scopeProvider, CancellationToken cancellationToken, ISyncProgressReporter? progressReporter = null)
    {
        var connectorService = scopeProvider.GetRequiredService<MyLifeConnectorService>();
        return await connectorService.SyncDataAsync(Config, cancellationToken, since: null, progressReporter);
    }
}

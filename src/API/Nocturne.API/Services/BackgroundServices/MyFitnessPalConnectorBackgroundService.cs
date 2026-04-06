using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.MyFitnessPal.Configurations;
using Nocturne.Connectors.MyFitnessPal.Services;

namespace Nocturne.API.Services.BackgroundServices;

/// <summary>
/// Background service for MyFitnessPal connector
/// </summary>
public class MyFitnessPalConnectorBackgroundService : ConnectorBackgroundService<MyFitnessPalConnectorConfiguration>
{
    public MyFitnessPalConnectorBackgroundService(
        IServiceProvider serviceProvider,
        MyFitnessPalConnectorConfiguration config,
        ILogger<MyFitnessPalConnectorBackgroundService> logger
    )
        : base(serviceProvider, config, logger) { }

    protected override string ConnectorName => "MyFitnessPal";

    protected override async Task<SyncResult> PerformSyncAsync(IServiceProvider scopeProvider, CancellationToken cancellationToken, ISyncProgressReporter? progressReporter = null)
    {
        var connectorService = scopeProvider.GetRequiredService<MyFitnessPalConnectorService>();
        return await connectorService.SyncDataAsync(Config, cancellationToken, since: null, progressReporter);
    }
}

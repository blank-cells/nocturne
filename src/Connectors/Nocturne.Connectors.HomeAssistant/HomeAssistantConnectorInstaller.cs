using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.HomeAssistant.Configurations;
using Nocturne.Connectors.HomeAssistant.Mappers;
using Nocturne.Connectors.HomeAssistant.Services;
using Nocturne.Connectors.HomeAssistant.WriteBack;

namespace Nocturne.Connectors.HomeAssistant;

public class HomeAssistantConnectorInstaller : IConnectorInstaller
{
    public string ConnectorName => "HomeAssistant";

    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        var config = services.AddConnectorConfiguration<HomeAssistantConnectorConfiguration>(
            configuration, "HomeAssistant");

        if (!config.Enabled)
            return;

        // Connector service
        if (!string.IsNullOrEmpty(config.Url))
            services.AddHttpClient<HomeAssistantConnectorService>()
                .ConfigureConnectorClient(config.Url)
                .AddBearerAuthorization(config.AccessToken);
        else
            services.AddHttpClient<HomeAssistantConnectorService>();

        // API client (typed HttpClient)
        if (!string.IsNullOrEmpty(config.Url))
            services.AddHttpClient<HomeAssistantApiClient>()
                .ConfigureConnectorClient(config.Url)
                .AddBearerAuthorization(config.AccessToken);
        else
            services.AddHttpClient<HomeAssistantApiClient>();

        // Register the interface mapping
        services.AddScoped<IHomeAssistantApiClient>(sp => sp.GetRequiredService<HomeAssistantApiClient>());

        // Mapper
        services.AddScoped<HomeAssistantEntityMapper>();

        // Sync executor
        services.AddScoped<IConnectorSyncExecutor, HomeAssistantSyncExecutor>();

        // Write-back sink (uses IHomeAssistantApiClient which has its own typed HttpClient)
        services.AddScoped<HomeAssistantWriteBackSink>();
    }
}

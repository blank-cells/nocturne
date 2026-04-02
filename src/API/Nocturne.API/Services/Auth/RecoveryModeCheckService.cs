using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nocturne.API.Multitenancy;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Hosted service that runs on startup to determine whether the instance
/// should enter recovery mode. Recovery mode is triggered when any active,
/// non-system subject has neither a passkey credential nor an OIDC binding,
/// or when the NOCTURNE_RECOVERY_MODE environment variable is set to "true".
///
/// In multitenancy mode (BaseDomain configured), the setup-required check is
/// skipped because an empty database is the expected initial state — tenants
/// are created via the external provisioning flow.
/// </summary>
public class RecoveryModeCheckService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RecoveryModeState _state;
    private readonly MultitenancyConfiguration _multitenancyConfig;
    private readonly ILogger<RecoveryModeCheckService> _logger;

    public RecoveryModeCheckService(
        IServiceProvider serviceProvider,
        RecoveryModeState state,
        IOptions<MultitenancyConfiguration> multitenancyConfig,
        ILogger<RecoveryModeCheckService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _state = state;
        _multitenancyConfig = multitenancyConfig.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Check environment variable override first
        var envOverride = Environment.GetEnvironmentVariable("NOCTURNE_RECOVERY_MODE");
        if (string.Equals(envOverride, "true", StringComparison.OrdinalIgnoreCase))
        {
            _state.IsEnabled = true;
            _logger.LogWarning(
                "Recovery mode enabled via NOCTURNE_RECOVERY_MODE environment variable"
            );
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NocturneDbContext>();

            // Use IgnoreQueryFilters to bypass tenant scoping — this is a
            // global startup check that must inspect all subjects across tenants.
            var activeSubjects = await db.Subjects
                .IgnoreQueryFilters()
                .Where(s => s.IsActive && !s.IsSystemSubject)
                .AnyAsync(cancellationToken);

            if (!activeSubjects)
            {
                _state.IsSetupRequired = true;
                _logger.LogWarning(
                    "Setup mode enabled: no user subjects found (fresh database)"
                );
                return;
            }

            var hasOrphaned = await db.Subjects
                .IgnoreQueryFilters()
                .Where(s => s.IsActive && !s.IsSystemSubject)
                .Where(s =>
                    s.OidcSubjectId == null &&
                    !db.PasskeyCredentials
                        .IgnoreQueryFilters()
                        .Any(p => p.SubjectId == s.Id)
                )
                .AnyAsync(cancellationToken);

            if (hasOrphaned)
            {
                _state.IsEnabled = true;
                _logger.LogWarning(
                    "Recovery mode enabled: one or more active subjects have no passkey and no OIDC binding"
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for orphaned subjects during startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

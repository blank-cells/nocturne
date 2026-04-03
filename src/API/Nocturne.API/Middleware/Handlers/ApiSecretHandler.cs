using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Middleware.Handlers;

/// <summary>
/// Authentication handler for per-tenant Nightscout API secret.
/// Validates the SHA1 hash sent in the api-secret header against the tenant's stored hash.
/// Grants full admin (*) permissions on the resolved tenant.
/// </summary>
public class ApiSecretHandler : IAuthHandler
{
    public int Priority => 400;

    public string Name => "ApiSecretHandler";

    private readonly ILogger<ApiSecretHandler> _logger;

    public ApiSecretHandler(ILogger<ApiSecretHandler> logger)
    {
        _logger = logger;
    }

    public async Task<AuthResult> AuthenticateAsync(HttpContext context)
    {
        var apiSecretHeader = context.Request.Headers["api-secret"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiSecretHeader))
            return AuthResult.Skip();

        if (context.Items["TenantContext"] is not TenantContext tenantCtx)
        {
            _logger.LogWarning("api-secret header provided but no tenant context resolved");
            return AuthResult.Failure("api-secret requires a resolved tenant");
        }

        var factory = context.RequestServices.GetRequiredService<IDbContextFactory<NocturneDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantCtx.TenantId);

        if (tenant?.ApiSecretHash == null)
        {
            _logger.LogWarning("api-secret header provided but tenant {Slug} has no API secret configured", tenantCtx.Slug);
            return AuthResult.Failure("API secret not configured for this tenant");
        }

        if (!string.Equals(apiSecretHeader, tenant.ApiSecretHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid API secret for tenant {Slug}", tenantCtx.Slug);
            return AuthResult.Failure("Invalid API secret");
        }

        _logger.LogDebug("Per-tenant API secret authentication successful for tenant {Slug}", tenantCtx.Slug);
        return AuthResult.Success(new AuthContext
        {
            IsAuthenticated = true,
            AuthType = AuthType.ApiSecret,
            SubjectName = "admin",
            Permissions = ["*"],
            Roles = ["admin"],
        });
    }
}

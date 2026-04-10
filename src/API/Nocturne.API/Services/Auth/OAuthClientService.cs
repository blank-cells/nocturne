using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Service for managing OAuth client registrations and the known app directory.
/// </summary>
public class OAuthClientService : IOAuthClientService
{
    private readonly NocturneDbContext _dbContext;
    private readonly RedirectUriValidator _redirectUriValidator;
    private readonly ILogger<OAuthClientService> _logger;

    /// <summary>
    /// Creates a new instance of OAuthClientService
    /// </summary>
    public OAuthClientService(
        NocturneDbContext dbContext,
        RedirectUriValidator redirectUriValidator,
        ILogger<OAuthClientService> logger)
    {
        _dbContext = dbContext;
        _redirectUriValidator = redirectUriValidator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OAuthClientInfo?> GetClientAsync(string clientId, CancellationToken ct = default)
    {
        var entity = await _dbContext.OAuthClients
            .FirstOrDefaultAsync(c => c.ClientId == clientId, ct);

        if (entity == null)
        {
            _logger.LogDebug("OAuth client not found: {ClientId}", clientId);
            return null;
        }

        return MapToInfo(entity);
    }

    /// <inheritdoc />
    public async Task<OAuthClientInfo?> GetClientByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _dbContext.OAuthClients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (entity == null)
        {
            _logger.LogDebug("OAuth client not found by ID {Id}", id);
            return null;
        }

        return MapToInfo(entity);
    }

    /// <inheritdoc />
    public async Task<bool> ValidateRedirectUriAsync(
        string clientId,
        string redirectUri,
        CancellationToken ct = default)
    {
        var entity = await _dbContext.OAuthClients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClientId == clientId, ct);

        if (entity == null)
        {
            _logger.LogWarning(
                "Redirect URI validation failed: client {ClientId} is not registered. " +
                "Apps must call POST /oauth/register before authorize.", clientId);
            return false;
        }

        var registered = DeserializeRedirectUris(entity.RedirectUris);
        if (registered.Count == 0)
        {
            _logger.LogWarning(
                "Client {ClientId} has no registered redirect URIs", clientId);
            return false;
        }

        // RFC 8252 redirect URI matching: byte-exact except loopback allows any port
        return registered.Any(r => _redirectUriValidator.IsValidForAuthorize(r, redirectUri));
    }

    /// <summary>
    /// Deserialize the redirect_uris JSON array from the entity.
    /// </summary>
    private static List<string> DeserializeRedirectUris(string redirectUrisJson)
    {
        if (string.IsNullOrWhiteSpace(redirectUrisJson) || redirectUrisJson == "[]")
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(redirectUrisJson) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    /// <inheritdoc />
    public async Task SeedKnownOAuthClientsAsync(Guid tenantId, CancellationToken ct = default)
    {
        // Read existing software_ids for this tenant so we can skip already-seeded entries.
        var existingSoftwareIds = await _dbContext.OAuthClients
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.SoftwareId != null)
            .Select(c => c.SoftwareId!)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existingSoftwareIds, StringComparer.Ordinal);

        var added = 0;
        foreach (var entry in KnownOAuthClients.Entries)
        {
            if (existingSet.Contains(entry.SoftwareId))
                continue;

            _dbContext.OAuthClients.Add(new OAuthClientEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                ClientId = Guid.CreateVersion7().ToString(),
                SoftwareId = entry.SoftwareId,
                ClientName = entry.DisplayName,
                ClientUri = entry.Homepage,
                LogoUri = entry.LogoUri,
                DisplayName = entry.DisplayName,
                IsKnown = true,
                RedirectUris = JsonSerializer.Serialize(entry.RedirectUris),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            added++;
        }

        if (added > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Seeded {Count} known OAuth clients for tenant {TenantId}",
                added, tenantId);
        }
    }

    /// <inheritdoc />
    public async Task<OAuthClientInfo> RegisterClientAsync(
        string? softwareId,
        string? clientName,
        string? clientUri,
        string? logoUri,
        IReadOnlyList<string> redirectUris,
        string? scope,
        string? createdFromIp,
        CancellationToken ct = default)
    {
        // Idempotent on (tenant, software_id) when software_id is supplied:
        // if a row already exists return it unchanged.
        if (!string.IsNullOrEmpty(softwareId))
        {
            var existing = await _dbContext.OAuthClients
                .FirstOrDefaultAsync(c => c.SoftwareId == softwareId, ct);
            if (existing != null)
            {
                _logger.LogDebug(
                    "DCR: returning existing client for software_id {SoftwareId} (tenant {TenantId})",
                    softwareId, existing.TenantId);
                return MapToInfo(existing);
            }
        }

        // Look up the known directory entry to mark is_known and pull display defaults.
        var known = string.IsNullOrEmpty(softwareId)
            ? null
            : KnownOAuthClients.MatchBySoftwareId(softwareId);

        var entity = new OAuthClientEntity
        {
            Id = Guid.CreateVersion7(),
            // client_id is opaque to clients; use the entity Id as a stable string.
            ClientId = Guid.CreateVersion7().ToString(),
            SoftwareId = softwareId,
            ClientName = clientName ?? known?.DisplayName,
            ClientUri = clientUri ?? known?.Homepage,
            LogoUri = logoUri ?? known?.LogoUri,
            DisplayName = clientName ?? known?.DisplayName,
            IsKnown = known != null,
            RedirectUris = JsonSerializer.Serialize(redirectUris),
            CreatedFromIp = createdFromIp,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _dbContext.OAuthClients.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DCR: registered client {ClientId} software_id={SoftwareId} known={IsKnown}",
            entity.ClientId, softwareId ?? "(none)", entity.IsKnown);

        return MapToInfo(entity);
    }

    /// <summary>
    /// Map an OAuthClientEntity to an OAuthClientInfo DTO.
    /// </summary>
    private static OAuthClientInfo MapToInfo(OAuthClientEntity entity)
    {
        return new OAuthClientInfo
        {
            Id = entity.Id,
            ClientId = entity.ClientId,
            DisplayName = entity.DisplayName,
            ClientUri = entity.ClientUri,
            LogoUri = entity.LogoUri,
            SoftwareId = entity.SoftwareId,
            IsKnown = entity.IsKnown,
            RedirectUris = DeserializeRedirectUris(entity.RedirectUris)
        };
    }
}

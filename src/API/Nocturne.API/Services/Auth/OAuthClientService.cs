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
    private readonly ILogger<OAuthClientService> _logger;

    /// <summary>
    /// Creates a new instance of OAuthClientService
    /// </summary>
    public OAuthClientService(
        NocturneDbContext dbContext,
        ILogger<OAuthClientService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OAuthClientInfo> FindOrCreateClientAsync(string clientId, CancellationToken ct = default)
    {
        var entity = await _dbContext.OAuthClients
            .FirstOrDefaultAsync(c => c.ClientId.ToLower() == clientId.ToLower(), ct);

        if (entity != null)
        {
            _logger.LogDebug("Found existing OAuth client {ClientId}", clientId);
            return MapToInfo(entity);
        }

        entity = new OAuthClientEntity
        {
            Id = Guid.CreateVersion7(),
            ClientId = clientId,
            DisplayName = null,
            IsKnown = false,
            RedirectUris = "[]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.OAuthClients.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created new OAuth client {ClientId} (known: {IsKnown})",
            clientId, entity.IsKnown);

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
            .FirstOrDefaultAsync(c => c.ClientId.ToLower() == clientId.ToLower(), ct);

        // If client not found, it will be created during the authorize flow
        if (entity == null)
        {
            _logger.LogDebug(
                "OAuth client {ClientId} not found during redirect URI validation; will be created during authorize",
                clientId);
            return true;
        }

        var uris = DeserializeRedirectUris(entity.RedirectUris);

        // If no redirect URIs are registered, accept any HTTPS URI (or localhost) and pin it
        if (uris.Count == 0)
        {
            if (!IsAcceptableRedirectUri(redirectUri))
            {
                _logger.LogWarning(
                    "Rejected non-HTTPS redirect URI {RedirectUri} for client {ClientId}",
                    redirectUri, clientId);
                return false;
            }

            // Pin this URI to the client
            uris.Add(redirectUri);
            entity.RedirectUris = JsonSerializer.Serialize(uris);
            entity.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Pinned redirect URI {RedirectUri} to client {ClientId}",
                redirectUri, clientId);
            return true;
        }

        // Check if the redirect URI matches any registered URI
        if (uris.Contains(redirectUri))
        {
            return true;
        }

        _logger.LogWarning(
            "Redirect URI {RedirectUri} does not match any registered URIs for client {ClientId}",
            redirectUri, clientId);
        return false;
    }

    /// <summary>
    /// Check if a redirect URI is acceptable for first-use pinning.
    /// Accepts HTTPS URIs and localhost HTTP URIs (for development/native apps).
    /// </summary>
    private static bool IsAcceptableRedirectUri(string redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
            return false;

        // HTTPS is always acceptable
        if (uri.Scheme == Uri.UriSchemeHttps)
            return true;

        // Allow HTTP for localhost (native app development)
        if (uri.Scheme == Uri.UriSchemeHttp &&
            (uri.Host == "localhost" || uri.Host == "127.0.0.1"))
            return true;

        return false;
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

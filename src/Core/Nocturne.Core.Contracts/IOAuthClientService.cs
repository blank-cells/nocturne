namespace Nocturne.Core.Contracts;

/// <summary>
/// Service for managing OAuth client registrations and the known app directory.
/// </summary>
public interface IOAuthClientService
{
    /// <summary>
    /// Look up a client by its (tenant-scoped) client_id. Returns null if not
    /// found. Clients must be registered via DCR before calling this — there is
    /// no auto-create path.
    /// </summary>
    Task<OAuthClientInfo?> GetClientAsync(string clientId, CancellationToken ct = default);

    /// <summary>
    /// Get client info by internal ID.
    /// </summary>
    Task<OAuthClientInfo?> GetClientByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Check if a redirect URI is valid for the given client.
    /// For unknown clients, any HTTPS URI is accepted on first use.
    /// </summary>
    Task<bool> ValidateRedirectUriAsync(
        string clientId,
        string redirectUri,
        CancellationToken ct = default
    );

    /// <summary>
    /// RFC 7591 Dynamic Client Registration. If the request specifies a known
    /// software_id and a row already exists for the (tenant, software_id) pair,
    /// returns that existing row (idempotent). Otherwise inserts a new row with
    /// a freshly issued client_id.
    /// </summary>
    /// <param name="softwareId">RFC 7591 software_id (reverse-DNS), or null</param>
    /// <param name="clientName">Display name for the consent screen</param>
    /// <param name="clientUri">Homepage URI</param>
    /// <param name="logoUri">Logo URI for the consent screen</param>
    /// <param name="redirectUris">Allowed redirect URIs (already validated)</param>
    /// <param name="scope">Space-delimited scope string</param>
    /// <param name="createdFromIp">IP that performed the registration</param>
    /// <param name="ct">Cancellation token</param>
    /// <summary>
    /// Seed the bundled known-app directory into a tenant's oauth_clients.
    /// Called during tenant provisioning so well-known apps (Trio, xDrip+, etc.)
    /// have pre-verified client rows with is_known=true. Idempotent: existing
    /// rows for the same software_id are left untouched.
    /// </summary>
    Task SeedKnownOAuthClientsAsync(Guid tenantId, CancellationToken ct = default);

    Task<OAuthClientInfo> RegisterClientAsync(
        string? softwareId,
        string? clientName,
        string? clientUri,
        string? logoUri,
        IReadOnlyList<string> redirectUris,
        string? scope,
        string? createdFromIp,
        CancellationToken ct = default
    );
}

/// <summary>
/// OAuth client information returned by the client service.
/// </summary>
public class OAuthClientInfo
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? ClientUri { get; set; }
    public string? LogoUri { get; set; }
    public string? SoftwareId { get; set; }
    public bool IsKnown { get; set; }
    public List<string> RedirectUris { get; set; } = new();
}

using System.Text.Json.Serialization;

namespace Nocturne.API.Models.OAuth;

/// <summary>
/// RFC 7591 Dynamic Client Registration response body.
/// </summary>
public class ClientRegistrationResponse
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = "";

    [JsonPropertyName("client_id_issued_at")]
    public long ClientIdIssuedAt { get; set; }

    [JsonPropertyName("client_name")]
    public string? ClientName { get; set; }

    [JsonPropertyName("redirect_uris")]
    public List<string> RedirectUris { get; set; } = [];

    [JsonPropertyName("grant_types")]
    public List<string> GrantTypes { get; set; } = ["authorization_code", "refresh_token"];

    [JsonPropertyName("response_types")]
    public List<string> ResponseTypes { get; set; } = ["code"];

    [JsonPropertyName("token_endpoint_auth_method")]
    public string TokenEndpointAuthMethod { get; set; } = "none";

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("software_id")]
    public string? SoftwareId { get; set; }
}

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Nocturne.API.Models.OAuth;

/// <summary>
/// OAuth 2.0 error response (RFC 6749 Section 5.2)
/// </summary>
public class OAuthError
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }

    [JsonPropertyName("error_uri")]
    public string? ErrorUri { get; set; }
}

/// <summary>
/// OAuth 2.0 token request (RFC 6749 Section 4.1.3)
/// </summary>
public class OAuthTokenRequest
{
    [FromForm(Name = "grant_type")]
    public string GrantType { get; set; } = string.Empty;

    [FromForm(Name = "code")]
    public string? Code { get; set; }

    [FromForm(Name = "redirect_uri")]
    public string? RedirectUri { get; set; }

    [FromForm(Name = "client_id")]
    public string? ClientId { get; set; }

    [FromForm(Name = "code_verifier")]
    public string? CodeVerifier { get; set; }

    [FromForm(Name = "refresh_token")]
    public string? RefreshToken { get; set; }

    [FromForm(Name = "device_code")]
    public string? DeviceCode { get; set; }

    [FromForm(Name = "scope")]
    public string? Scope { get; set; }
}

/// <summary>
/// OAuth 2.0 token response (RFC 6749 Section 5.1)
/// </summary>
public class OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

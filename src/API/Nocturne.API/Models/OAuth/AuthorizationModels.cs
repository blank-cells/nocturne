using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Nocturne.API.Models.OAuth;

/// <summary>
/// Consent approval request (submitted by the consent page)
/// </summary>
public class ConsentApprovalRequest
{
    [FromForm(Name = "client_id")]
    public string ClientId { get; set; } = string.Empty;

    [FromForm(Name = "redirect_uri")]
    public string RedirectUri { get; set; } = string.Empty;

    [FromForm(Name = "scope")]
    public string? Scope { get; set; }

    [FromForm(Name = "state")]
    public string? State { get; set; }

    [FromForm(Name = "code_challenge")]
    public string CodeChallenge { get; set; } = string.Empty;

    [FromForm(Name = "approved")]
    public bool Approved { get; set; }

    /// <summary>
    /// When true, limits data access to 24 hours from the grant creation time.
    /// </summary>
    [FromForm(Name = "limit_to_24_hours")]
    public bool LimitTo24Hours { get; set; }
}

/// <summary>
/// Client info response for the consent page
/// </summary>
public class OAuthClientInfoResponse
{
    public string ClientId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsKnown { get; set; }
    public string? Homepage { get; set; }
}

/// <summary>
/// Device Authorization Response (RFC 8628 Section 3.2)
/// </summary>
public class OAuthDeviceAuthorizationResponse
{
    [JsonPropertyName("device_code")]
    public string DeviceCode { get; set; } = string.Empty;

    [JsonPropertyName("user_code")]
    public string UserCode { get; set; } = string.Empty;

    [JsonPropertyName("verification_uri")]
    public string VerificationUri { get; set; } = string.Empty;

    [JsonPropertyName("verification_uri_complete")]
    public string? VerificationUriComplete { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("interval")]
    public int Interval { get; set; }
}

/// <summary>
/// Device approval request (submitted by the device approval page)
/// </summary>
public class DeviceApprovalRequest
{
    [FromForm(Name = "user_code")]
    public string UserCode { get; set; } = string.Empty;

    [FromForm(Name = "approved")]
    public bool Approved { get; set; }
}

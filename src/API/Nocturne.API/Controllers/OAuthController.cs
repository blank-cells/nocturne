using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nocturne.API.Extensions;
using Nocturne.API.Models.OAuth;
using Nocturne.API.Services.Auth;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models.Authorization;

namespace Nocturne.API.Controllers;

/// <summary>
/// OAuth 2.0 endpoints for Nocturne.
/// Supports Authorization Code + PKCE and Device Authorization Grant (RFC 8628).
/// All clients are public (no client secrets); PKCE is mandatory.
/// </summary>
[ApiController]
[Route("api/oauth")]
[Tags("OAuth")]
public class OAuthController : ControllerBase
{
    private readonly IOAuthClientService _clientService;
    private readonly IOAuthGrantService _grantService;
    private readonly IOAuthTokenService _tokenService;
    private readonly IOAuthDeviceCodeService _deviceCodeService;
    private readonly ISubjectService _subjectService;
    private readonly IJwtService _jwtService;
    private readonly IOAuthTokenRevocationCache _revocationCache;
    private readonly ILogger<OAuthController> _logger;

    /// <summary>
    /// Creates a new instance of OAuthController
    /// </summary>
    public OAuthController(
        IOAuthClientService clientService,
        IOAuthGrantService grantService,
        IOAuthTokenService tokenService,
        IOAuthDeviceCodeService deviceCodeService,
        ISubjectService subjectService,
        IJwtService jwtService,
        IOAuthTokenRevocationCache revocationCache,
        ILogger<OAuthController> logger
    )
    {
        _clientService = clientService;
        _grantService = grantService;
        _tokenService = tokenService;
        _deviceCodeService = deviceCodeService;
        _subjectService = subjectService;
        _jwtService = jwtService;
        _revocationCache = revocationCache;
        _logger = logger;
    }

    /// <summary>
    /// Authorization endpoint (Authorization Code + PKCE flow).
    /// Redirects to login if not authenticated, then shows consent screen.
    /// </summary>
    [HttpGet("authorize")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Authorize(
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string response_type,
        [FromQuery] string scope,
        [FromQuery] string? state = null,
        [FromQuery] string? code_challenge = null,
        [FromQuery] string? code_challenge_method = null
    )
    {
        // Validate response_type
        if (response_type != "code")
        {
            return BadRequest(new OAuthError
            {
                Error = "unsupported_response_type",
                ErrorDescription = "Only 'code' response type is supported.",
            });
        }

        // Validate PKCE is present (mandatory)
        if (string.IsNullOrEmpty(code_challenge) || code_challenge_method != "S256")
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "PKCE is required. Provide code_challenge with code_challenge_method=S256.",
            });
        }

        // Validate scopes
        var requestedScopes = scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var invalidScopes = requestedScopes.Where(s => !OAuthScopes.IsValid(s)).ToList();
        if (invalidScopes.Count > 0)
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_scope",
                ErrorDescription = $"Invalid scope(s): {string.Join(", ", invalidScopes)}",
            });
        }

        if (requestedScopes.Length == 0)
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_scope",
                ErrorDescription = "At least one scope must be requested.",
            });
        }

        // Look up the registered client; clients must register via DCR before authorize
        var client = await _clientService.GetClientAsync(client_id);
        if (client == null)
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_client",
                ErrorDescription = "Unknown client_id. Register first via POST /oauth/register.",
            });
        }

        // Validate redirect URI
        if (!await _clientService.ValidateRedirectUriAsync(client_id, redirect_uri))
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "Invalid redirect_uri for this client.",
            });
        }

        // Check if user is authenticated
        if (!HttpContext.IsAuthenticated())
        {
            // Redirect to login, preserving the OAuth params to return to after login
            var returnUrl = $"/api/oauth/authorize{Request.QueryString}";
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var subjectId = HttpContext.GetSubjectId();
        if (subjectId == null)
        {
            return Unauthorized(new OAuthError
            {
                Error = "access_denied",
                ErrorDescription = "Could not determine authenticated user.",
            });
        }

        // Normalize the requested scopes
        var normalizedScopes = OAuthScopes.Normalize(requestedScopes);

        // Check if an active grant exists with sufficient scopes
        var existingGrant = await _grantService.GetActiveGrantAsync(client.Id, subjectId.Value);
        if (existingGrant != null)
        {
            var existingSet = new HashSet<string>(existingGrant.Scopes);
            var allSatisfied = normalizedScopes.All(s => OAuthScopes.SatisfiesScope(existingSet, s));

            if (allSatisfied)
            {
                // Silent approval: existing grant covers all requested scopes
                return await IssueAuthorizationCode(
                    client.Id,
                    subjectId.Value,
                    normalizedScopes,
                    redirect_uri,
                    code_challenge,
                    state
                );
            }
        }

        // Redirect to consent page, passing existing scopes for the upgrade UI
        var existingScopeString = existingGrant != null
            ? string.Join(" ", existingGrant.Scopes)
            : null;
        var consentUrl = BuildConsentUrl(client_id, redirect_uri, scope, state, code_challenge, existingScopeString);
        return Redirect(consentUrl);
    }

    /// <summary>
    /// Consent approval endpoint. Called by the consent page when the user approves.
    /// </summary>
    [HttpPost("authorize")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ApproveConsent([FromForm] ConsentApprovalRequest request)
    {
        if (!HttpContext.IsAuthenticated())
        {
            return Unauthorized(new OAuthError
            {
                Error = "access_denied",
                ErrorDescription = "User is not authenticated.",
            });
        }

        var subjectId = HttpContext.GetSubjectId();
        if (subjectId == null)
        {
            return Unauthorized(new OAuthError
            {
                Error = "access_denied",
                ErrorDescription = "Could not determine authenticated user.",
            });
        }

        // If user denied
        if (!request.Approved)
        {
            return RedirectWithError(
                request.RedirectUri,
                "access_denied",
                "The user denied the authorization request.",
                request.State
            );
        }

        // Validate scopes
        var scopes = request.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var normalizedScopes = OAuthScopes.Normalize(scopes);

        if (normalizedScopes.Count == 0)
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_scope",
                ErrorDescription = "No valid scopes were approved.",
            });
        }

        // Find the client
        var client = await _clientService.GetClientAsync(request.ClientId);
        if (client == null)
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_client",
                ErrorDescription = "Unknown client_id.",
            });
        }

        // Re-validate redirect URI to prevent manipulation between GET and POST
        if (!await _clientService.ValidateRedirectUriAsync(request.ClientId, request.RedirectUri))
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "Invalid redirect_uri for this client.",
            });
        }

        // Generate authorization code
        return await IssueAuthorizationCode(
            client.Id,
            subjectId.Value,
            normalizedScopes,
            request.RedirectUri,
            request.CodeChallenge,
            request.State,
            request.LimitTo24Hours
        );
    }

    /// <summary>
    /// Token endpoint. Handles authorization code exchange, refresh token rotation,
    /// and device code polling.
    /// </summary>
    [HttpPost("token")]
    [AllowAnonymous]
    [EnableRateLimiting("oauth-token")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(OAuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OAuthError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Token([FromForm] OAuthTokenRequest request)
    {
        _logger.LogInformation(
            "OAuth token request: grant_type={GrantType}, client_id={ClientId}",
            request.GrantType,
            request.ClientId
        );

        OAuthTokenResult result;

        switch (request.GrantType)
        {
            case "authorization_code":
                if (string.IsNullOrEmpty(request.Code) ||
                    string.IsNullOrEmpty(request.CodeVerifier) ||
                    string.IsNullOrEmpty(request.RedirectUri) ||
                    string.IsNullOrEmpty(request.ClientId))
                {
                    return BadRequest(new OAuthError
                    {
                        Error = "invalid_request",
                        ErrorDescription = "Missing required parameters: code, code_verifier, redirect_uri, client_id.",
                    });
                }

                result = await _tokenService.ExchangeAuthorizationCodeAsync(
                    request.Code,
                    request.CodeVerifier,
                    request.RedirectUri,
                    request.ClientId
                );
                break;

            case "refresh_token":
                if (string.IsNullOrEmpty(request.RefreshToken))
                {
                    return BadRequest(new OAuthError
                    {
                        Error = "invalid_request",
                        ErrorDescription = "Missing required parameter: refresh_token.",
                    });
                }

                result = await _tokenService.RefreshAccessTokenAsync(
                    request.RefreshToken,
                    request.ClientId
                );
                break;

            case "urn:ietf:params:oauth:grant-type:device_code":
                if (string.IsNullOrEmpty(request.DeviceCode) ||
                    string.IsNullOrEmpty(request.ClientId))
                {
                    return BadRequest(new OAuthError
                    {
                        Error = "invalid_request",
                        ErrorDescription = "Missing required parameters: device_code, client_id.",
                    });
                }

                result = await _tokenService.ExchangeDeviceCodeAsync(
                    request.DeviceCode,
                    request.ClientId
                );
                break;

            default:
                return BadRequest(new OAuthError
                {
                    Error = "unsupported_grant_type",
                    ErrorDescription = $"Unsupported grant_type: {request.GrantType}",
                });
        }

        if (!result.Success)
        {
            return BadRequest(new OAuthError
            {
                Error = result.Error!,
                ErrorDescription = result.ErrorDescription,
            });
        }

        return Ok(new OAuthTokenResponse
        {
            AccessToken = result.AccessToken!,
            TokenType = "Bearer",
            ExpiresIn = result.ExpiresIn,
            RefreshToken = result.RefreshToken,
            Scope = result.Scope,
        });
    }

    /// <summary>
    /// Device Authorization endpoint (RFC 8628).
    /// Used by headless clients (CLI tools, scripts, IoT devices, pump rigs).
    /// </summary>
    [HttpPost("device")]
    [AllowAnonymous]
    [EnableRateLimiting("oauth-device")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(OAuthDeviceAuthorizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OAuthError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OAuthDeviceAuthorizationResponse>> DeviceAuthorization(
        [FromForm] string client_id,
        [FromForm] string? scope = null
    )
    {
        var requestedScopes = scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var invalidScopes = requestedScopes.Where(s => !OAuthScopes.IsValid(s)).ToList();
        if (invalidScopes.Count > 0)
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_scope",
                ErrorDescription = $"Invalid scope(s): {string.Join(", ", invalidScopes)}",
            });
        }

        if (requestedScopes.Length == 0)
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_scope",
                ErrorDescription = "At least one scope must be requested.",
            });
        }

        // Validate client
        var deviceClient = await _clientService.GetClientAsync(client_id);
        if (deviceClient == null)
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_client",
                ErrorDescription = "Unknown client_id. Register first via POST /oauth/register.",
            });
        }

        // Normalize scopes
        var normalizedScopes = OAuthScopes.Normalize(requestedScopes);

        // Create device code pair
        var result = await _deviceCodeService.CreateDeviceCodeAsync(client_id, normalizedScopes);

        // Build verification URI - points to frontend device approval page
        // The frontend runs on a different port, so we use the Origin header if available
        // or fall back to the current request with the frontend path
        var baseUrl = Request.Headers.Origin.FirstOrDefault()
            ?? $"{Request.Scheme}://{Request.Host}";
        var verificationUri = $"{baseUrl}/oauth/device";
        var verificationUriComplete = $"{verificationUri}?user_code={Uri.EscapeDataString(result.UserCode)}";

        _logger.LogInformation(
            "Device authorization initiated for client {ClientId}, user_code={UserCode}",
            client_id,
            result.UserCode
        );

        return Ok(new OAuthDeviceAuthorizationResponse
        {
            DeviceCode = result.DeviceCode,
            UserCode = result.UserCode,
            VerificationUri = verificationUri,
            VerificationUriComplete = verificationUriComplete,
            ExpiresIn = result.ExpiresIn,
            Interval = result.Interval,
        });
    }

    /// <summary>
    /// Get device code info for the approval page.
    /// </summary>
    [HttpGet("device-info")]
    [ProducesResponseType(typeof(DeviceCodeInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceCodeInfo>> GetDeviceInfo(
        [FromQuery] string user_code
    )
    {
        if (string.IsNullOrEmpty(user_code))
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "Missing required parameter: user_code.",
            });
        }

        var info = await _deviceCodeService.GetByUserCodeAsync(user_code);
        if (info == null)
        {
            return NotFound(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "Device code not found.",
            });
        }

        if (info.IsExpired)
        {
            return BadRequest(new OAuthError
            {
                Error = "expired_token",
                ErrorDescription = "Device code has expired. Please request a new one.",
            });
        }

        return Ok(info);
    }

    /// <summary>
    /// Approve or deny a device authorization request.
    /// Called by the device approval page.
    /// </summary>
    [HttpPost("device-approve")]
    [EnableRateLimiting("oauth-device-approve")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeviceApprove(
        [FromForm] DeviceApprovalRequest request
    )
    {
        if (!HttpContext.IsAuthenticated())
        {
            return Unauthorized(new OAuthError
            {
                Error = "access_denied",
                ErrorDescription = "User is not authenticated.",
            });
        }

        var subjectId = HttpContext.GetSubjectId();
        if (subjectId == null)
        {
            return Unauthorized(new OAuthError
            {
                Error = "access_denied",
                ErrorDescription = "Could not determine authenticated user.",
            });
        }

        if (string.IsNullOrEmpty(request.UserCode))
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "Missing required parameter: user_code.",
            });
        }

        var success = request.Approved
            ? await _deviceCodeService.ApproveDeviceCodeAsync(request.UserCode, subjectId.Value)
            : await _deviceCodeService.DenyDeviceCodeAsync(request.UserCode);

        if (!success)
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "Device code is invalid, expired, or already processed.",
            });
        }

        return Ok(new { approved = request.Approved });
    }

    /// <summary>
    /// Token revocation endpoint (RFC 7009).
    /// </summary>
    [HttpPost("revoke")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Revoke(
        [FromForm] string token,
        [FromForm] string? token_type_hint = null
    )
    {
        await _tokenService.RevokeTokenAsync(token, token_type_hint);
        return Ok();
    }

    /// <summary>
    /// Get client info for the consent page.
    /// </summary>
    [HttpGet("client-info")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OAuthClientInfoResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OAuthClientInfoResponse>> GetClientInfo(
        [FromQuery] string client_id
    )
    {
        var client = await _clientService.GetClientAsync(client_id);
        if (client == null)
        {
            return NotFound(new OAuthError
            {
                Error = "invalid_client",
                ErrorDescription = "Unknown client_id.",
            });
        }

        return Ok(new OAuthClientInfoResponse
        {
            ClientId = client.ClientId,
            DisplayName = client.DisplayName,
            IsKnown = client.IsKnown,
            Homepage = client.ClientUri,
        });
    }

    private async Task<ActionResult> IssueAuthorizationCode(
        Guid clientEntityId,
        Guid subjectId,
        IReadOnlySet<string> scopes,
        string redirectUri,
        string codeChallenge,
        string? state,
        bool limitTo24Hours = false
    )
    {
        var code = await _tokenService.GenerateAuthorizationCodeAsync(
            clientEntityId,
            subjectId,
            scopes,
            redirectUri,
            codeChallenge,
            limitTo24Hours
        );

        var separator = redirectUri.Contains('?') ? '&' : '?';
        var redirectUrl = $"{redirectUri}{separator}code={Uri.EscapeDataString(code)}";

        if (!string.IsNullOrEmpty(state))
        {
            redirectUrl += $"&state={Uri.EscapeDataString(state)}";
        }

        return Redirect(redirectUrl);
    }

    private ActionResult RedirectWithError(
        string redirectUri,
        string error,
        string errorDescription,
        string? state
    )
    {
        var separator = redirectUri.Contains('?') ? '&' : '?';
        var redirectUrl = $"{redirectUri}{separator}error={Uri.EscapeDataString(error)}&error_description={Uri.EscapeDataString(errorDescription)}";

        if (!string.IsNullOrEmpty(state))
        {
            redirectUrl += $"&state={Uri.EscapeDataString(state)}";
        }

        return Redirect(redirectUrl);
    }

    private static string BuildConsentUrl(
        string clientId,
        string redirectUri,
        string scope,
        string? state,
        string codeChallenge,
        string? existingScopes = null
    )
    {
        var qs = $"client_id={Uri.EscapeDataString(clientId)}" +
                 $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                 $"&scope={Uri.EscapeDataString(scope)}" +
                 $"&code_challenge={Uri.EscapeDataString(codeChallenge)}";

        if (!string.IsNullOrEmpty(state))
        {
            qs += $"&state={Uri.EscapeDataString(state)}";
        }

        if (!string.IsNullOrEmpty(existingScopes))
        {
            qs += $"&existing_scopes={Uri.EscapeDataString(existingScopes)}";
        }

        return $"/oauth/consent?{qs}";
    }

    /// <summary>
    /// List all active grants for the authenticated user.
    /// </summary>
    [HttpGet("grants")]
    [ProducesResponseType(typeof(OAuthGrantListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OAuthGrantListResponse>> GetGrants()
    {
        if (!HttpContext.IsAuthenticated())
        {
            return Unauthorized(new OAuthError
            {
                Error = "access_denied",
                ErrorDescription = "User is not authenticated.",
            });
        }

        var subjectId = HttpContext.GetSubjectId();
        if (subjectId == null)
        {
            return Unauthorized(new OAuthError
            {
                Error = "access_denied",
                ErrorDescription = "Could not determine authenticated user.",
            });
        }

        var grants = await _grantService.GetGrantsForSubjectAsync(subjectId.Value);
        var dtos = grants.Select(MapToDto).ToList();

        return Ok(new OAuthGrantListResponse { Grants = dtos });
    }

    /// <summary>
    /// Revoke (delete) a specific grant owned by the authenticated user.
    /// </summary>
    [HttpDelete("grants/{grantId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteGrant(Guid grantId)
    {
        if (!HttpContext.IsAuthenticated())
        {
            return Unauthorized(new OAuthError
            {
                Error = "access_denied",
                ErrorDescription = "User is not authenticated.",
            });
        }

        var subjectId = HttpContext.GetSubjectId();
        if (subjectId == null)
        {
            return Unauthorized(new OAuthError
            {
                Error = "access_denied",
                ErrorDescription = "Could not determine authenticated user.",
            });
        }

        // Verify ownership: load all grants for the subject and check if grantId is among them
        var grants = await _grantService.GetGrantsForSubjectAsync(subjectId.Value);
        if (grants.All(g => g.Id != grantId))
        {
            return NotFound(new OAuthError
            {
                Error = "not_found",
                ErrorDescription = "Grant not found.",
            });
        }

        await _grantService.RevokeGrantAsync(grantId);
        return NoContent();
    }

    /// <summary>
    /// Update a grant's label and/or scopes.
    /// </summary>
    [HttpPatch("grants/{grantId}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(OAuthGrantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OAuthGrantDto>> UpdateGrant(
        Guid grantId,
        [FromBody] UpdateGrantRequest request
    )
    {
        if (!HttpContext.IsAuthenticated())
        {
            return Unauthorized(new OAuthError
            {
                Error = "access_denied",
                ErrorDescription = "User is not authenticated.",
            });
        }

        var subjectId = HttpContext.GetSubjectId();
        if (subjectId == null)
        {
            return Unauthorized(new OAuthError
            {
                Error = "access_denied",
                ErrorDescription = "Could not determine authenticated user.",
            });
        }

        try
        {
            var updated = await _grantService.UpdateGrantAsync(
                grantId,
                subjectId.Value,
                request.Label,
                request.Scopes
            );

            if (updated == null)
            {
                return NotFound(new OAuthError
                {
                    Error = "not_found",
                    ErrorDescription = "Grant not found.",
                });
            }

            return Ok(MapToDto(updated));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_scope",
                ErrorDescription = ex.Message,
            });
        }
    }

    /// <summary>
    /// Token introspection endpoint (RFC 7662).
    /// Returns metadata about a token including its active status, scopes, and subject.
    /// Per RFC 7662, always returns 200 OK; invalid tokens get active=false.
    /// </summary>
    [HttpPost("introspect")]
    [AllowAnonymous]
    [EnableRateLimiting("oauth-token")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(TokenIntrospectionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TokenIntrospectionResponse>> Introspect(
        [FromForm] string token,
        [FromForm] string? token_type_hint = null)
    {
        if (string.IsNullOrEmpty(token))
        {
            return Ok(new TokenIntrospectionResponse { Active = false });
        }

        // Try as JWT access token
        if (token.Contains('.'))
        {
            var validation = _jwtService.ValidateAccessToken(token);
            if (validation.IsValid && validation.Claims != null)
            {
                var claims = validation.Claims;

                // Check revocation cache
                if (!string.IsNullOrEmpty(claims.JwtId) &&
                    await _revocationCache.IsRevokedAsync(claims.JwtId))
                {
                    return Ok(new TokenIntrospectionResponse { Active = false });
                }

                return Ok(new TokenIntrospectionResponse
                {
                    Active = true,
                    Scope = claims.Scopes.Count > 0 ? string.Join(" ", claims.Scopes) : null,
                    ClientId = claims.ClientId,
                    Sub = claims.SubjectId.ToString(),
                    Exp = claims.ExpiresAt.ToUnixTimeSeconds(),
                    Iat = claims.IssuedAt.ToUnixTimeSeconds(),
                    Jti = claims.JwtId,
                    TokenType = "access_token",
                });
            }
        }

        // Non-JWT tokens (e.g. refresh tokens) are not introspectable in this implementation.
        return Ok(new TokenIntrospectionResponse { Active = false });
    }

    /// <summary>
    /// RFC 7591 Dynamic Client Registration. Allows third-party native apps
    /// (Trio, xDrip+, Loop, AAPS) to obtain a tenant-scoped client_id without
    /// any out-of-band registration step. Idempotent on (tenant, software_id):
    /// re-registering with the same software_id returns the existing client_id.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("oauth-register")]
    [ProducesResponseType(typeof(ClientRegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OAuthError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Register(
        [FromBody] ClientRegistrationRequest request,
        [FromServices] RedirectUriValidator redirectUriValidator,
        CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_client_metadata",
                ErrorDescription = "Request body is required.",
            });
        }

        // RFC 7591 Section 2.0.1: redirect_uris is REQUIRED for native apps
        if (request.RedirectUris is null || request.RedirectUris.Count == 0)
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_redirect_uri",
                ErrorDescription = "At least one redirect_uri is required.",
            });
        }

        // Validate every redirect URI per RFC 8252
        var invalidUris = request.RedirectUris
            .Where(u => !redirectUriValidator.IsValidForRegistration(u))
            .ToList();
        if (invalidUris.Count > 0)
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_redirect_uri",
                ErrorDescription =
                    $"The following redirect_uris are not allowed: {string.Join(", ", invalidUris)}.",
            });
        }

        // Strict scope validation against the canonical registry
        var unknownScopes = RegistrationScopeValidator.ValidateScopes(request.Scope);
        if (unknownScopes is not null)
        {
            return BadRequest(new OAuthError
            {
                Error = "invalid_client_metadata",
                ErrorDescription =
                    $"Unknown scopes: {string.Join(", ", unknownScopes)}.",
            });
        }

        var createdFromIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var client = await _clientService.RegisterClientAsync(
            request.SoftwareId,
            request.ClientName,
            request.ClientUri,
            request.LogoUri,
            request.RedirectUris,
            request.Scope,
            createdFromIp,
            ct);

        var response = new ClientRegistrationResponse
        {
            ClientId = client.ClientId,
            ClientIdIssuedAt = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
            ClientName = client.DisplayName,
            RedirectUris = client.RedirectUris,
            Scope = request.Scope,
            SoftwareId = client.SoftwareId,
        };

        return Ok(response);
    }

    private static OAuthGrantDto MapToDto(OAuthGrantInfo info) => new()
    {
        Id = info.Id,
        GrantType = info.GrantType,
        ClientId = info.ClientId,
        ClientDisplayName = info.ClientDisplayName,
        IsKnownClient = info.IsKnownClient,
        Scopes = info.Scopes,
        Label = info.Label,
        CreatedAt = info.CreatedAt,
        LastUsedAt = info.LastUsedAt,
        LastUsedUserAgent = info.LastUsedUserAgent,
    };
}

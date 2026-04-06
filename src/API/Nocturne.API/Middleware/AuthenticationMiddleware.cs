using Microsoft.Extensions.Options;
using Nocturne.API.Middleware.Handlers;
using Nocturne.API.Services.Auth;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Authorization;
using Nocturne.Core.Models.Configuration;
using OAuthScopes = Nocturne.Core.Models.Authorization.OAuthScopes;
using ScopeTranslator = Nocturne.Core.Models.Authorization.ScopeTranslator;

namespace Nocturne.API.Middleware;

/// <summary>
/// Middleware for handling authentication through a chain of handlers.
/// Handlers are executed in priority order (lowest first).
/// The first handler to return success or failure stops the chain.
/// </summary>
public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationMiddleware> _logger;
    private readonly IAuthHandler[] _handlers;
    private readonly bool _isDevelopment;
    private readonly PublicAccessCacheService _publicAccessCacheService;
    private readonly string _accessTokenCookieName;
    private readonly string _refreshTokenCookieName;

    /// <summary>
    /// Creates a new instance of AuthenticationMiddleware
    /// </summary>
    public AuthenticationMiddleware(
        RequestDelegate next,
        ILogger<AuthenticationMiddleware> logger,
        IEnumerable<IAuthHandler> handlers,
        IHostEnvironment environment,
        PublicAccessCacheService publicAccessCacheService,
        IOptions<OidcOptions> oidcOptions
    )
    {
        _next = next;
        _logger = logger;
        _isDevelopment = environment.IsDevelopment();
        _publicAccessCacheService = publicAccessCacheService;
        _accessTokenCookieName = oidcOptions.Value.Cookie.AccessTokenName;
        _refreshTokenCookieName = oidcOptions.Value.Cookie.RefreshTokenName;

        // Sort handlers by priority (lowest first)
        _handlers = handlers.OrderBy(h => h.Priority).ToArray();
    }

    /// <summary>
    /// Process the HTTP request through the authentication pipeline
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            var authContext = await AuthenticateRequestAsync(context);

            // Set authentication context in HttpContext items
            context.Items["AuthContext"] = authContext;

            // Set tenant ID from the resolved tenant context
            if (context.Items["TenantContext"] is TenantContext tenantCtx)
            {
                authContext.TenantId = tenantCtx.TenantId;
            }

            // Build and set permission trie for fast permission checking
            var permissionTrie = new PermissionTrie();
            if (authContext.IsAuthenticated && authContext.Permissions.Count > 0)
            {
                permissionTrie.Add(authContext.Permissions);
            }
            context.Items["PermissionTrie"] = permissionTrie;

            // Resolve OAuth scopes from either explicit scopes (OAuth tokens) or
            // translated from legacy permissions (api-secret, access tokens, etc.)
            IReadOnlySet<string> grantedScopes;
            if (authContext.IsAuthenticated && authContext.Scopes.Count > 0)
            {
                // OAuth token path: scopes came directly from the token claims
                grantedScopes = OAuthScopes.Normalize(authContext.Scopes);
            }
            else if (authContext.IsAuthenticated && authContext.Permissions.Count > 0)
            {
                // Legacy path: translate Shiro-style permissions to scopes
                grantedScopes = ScopeTranslator.FromPermissions(authContext.Permissions);
            }
            else
            {
                grantedScopes = new HashSet<string>();
            }
            context.Items["GrantedScopes"] = grantedScopes;

            // Also set the legacy AuthenticationContext for backward compatibility
            context.Items["AuthenticationContext"] = MapToLegacyContext(authContext);

            // Set HttpContext.User for [Authorize] attribute to work
            if (authContext.IsAuthenticated)
            {
                var claims = new List<System.Security.Claims.Claim>
                {
                    new(System.Security.Claims.ClaimTypes.NameIdentifier, authContext.SubjectId?.ToString() ?? ""),
                    new(System.Security.Claims.ClaimTypes.Name, authContext.SubjectName ?? ""),
                };

                if (!string.IsNullOrEmpty(authContext.Email))
                {
                    claims.Add(new(System.Security.Claims.ClaimTypes.Email, authContext.Email));
                }

                foreach (var role in authContext.Roles)
                {
                    claims.Add(new(System.Security.Claims.ClaimTypes.Role, role));
                }

                foreach (var permission in authContext.Permissions)
                {
                    claims.Add(new("permission", permission));
                }

                var identity = new System.Security.Claims.ClaimsIdentity(claims, "Nocturne");
                context.User = new System.Security.Claims.ClaimsPrincipal(identity);

            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication");
            SetUnauthenticated(context);
        }

        // Verify authenticated subject is a member of the resolved tenant
        var resolvedAuth = context.Items["AuthContext"] as AuthContext;
        if (resolvedAuth is { IsAuthenticated: true, SubjectId: not null, TenantId: not null })
        {
            // Skip membership check for ApiSecret and InstanceKey auth (grants admin on the resolved tenant)
            if (resolvedAuth.AuthType is not (AuthType.ApiSecret or AuthType.InstanceKey))
            {
                var tenantMemberService = context.RequestServices.GetRequiredService<ITenantMemberService>();
                var isMember = await tenantMemberService.IsMemberAsync(
                    resolvedAuth.SubjectId!.Value,
                    resolvedAuth.TenantId!.Value);

                if (!isMember)
                {
                    _logger.LogWarning(
                        "Subject {SubjectId} is not a member of tenant {TenantId}",
                        resolvedAuth.SubjectId, resolvedAuth.TenantId);
                    SetUnauthenticated(context);
                }
            }
        }

        // For unauthenticated requests with a resolved tenant, try to resolve
        // the Public system subject's permissions for public/read-only access
        resolvedAuth = context.Items["AuthContext"] as AuthContext;
        if (resolvedAuth is { IsAuthenticated: false }
            && context.Items["TenantContext"] is TenantContext publicTenantCtx)
        {
            var publicAccess = await _publicAccessCacheService.GetPublicAccessAsync(publicTenantCtx.TenantId);
            if (publicAccess != null)
            {
                var publicAuthContext = new AuthContext
                {
                    IsAuthenticated = false,
                    AuthType = AuthType.None,
                    SubjectId = publicAccess.SubjectId,
                    TenantId = publicTenantCtx.TenantId,
                    LimitTo24Hours = publicAccess.LimitTo24Hours,
                };
                context.Items["AuthContext"] = publicAuthContext;

                var publicPermissionTrie = new PermissionTrie();
                publicPermissionTrie.Add(publicAccess.EffectivePermissions);
                context.Items["PermissionTrie"] = publicPermissionTrie;

                var publicScopes = ScopeTranslator.FromPermissions(publicAccess.EffectivePermissions);
                context.Items["GrantedScopes"] = publicScopes;

                context.Items["AuthenticationContext"] = MapToLegacyContext(publicAuthContext);

                _logger.LogDebug(
                    "Public access resolved for tenant {TenantId} with {Count} permissions",
                    publicTenantCtx.TenantId, publicAccess.EffectivePermissions.Count);
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Run through the handler chain to authenticate the request
    /// </summary>
    private async Task<AuthContext> AuthenticateRequestAsync(HttpContext context)
    {
        foreach (var handler in _handlers)
        {
            try
            {
                var result = await handler.AuthenticateAsync(context);

                if (result.Succeeded)
                {

                    return result.AuthContext!;
                }

                if (!result.ShouldSkip)
                {
                    // Handler recognized credentials but they were invalid
                    _logger.LogDebug(
                        "Authentication failed by {Handler}: {Error}",
                        handler.Name,
                        result.Error
                    );

                    // Return unauthenticated context but don't try other handlers
                    return AuthContext.Unauthenticated();
                }

                // Handler skipped - try next handler
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Handler {Handler} threw an exception", handler.Name);
                // Continue to next handler
            }
        }

        // In development mode, auto-authenticate as admin when a session cookie is present
        // but no handler succeeded (e.g., expired token without refresh).
        // When no session cookie is present, fall through to public access or unauthenticated.
        if (_isDevelopment)
        {
            var hasSessionCookie =
                context.Request.Cookies.ContainsKey(_accessTokenCookieName) ||
                context.Request.Cookies.ContainsKey(_refreshTokenCookieName);

            if (hasSessionCookie)
            {
                _logger.LogDebug("Development mode: auto-authenticating as admin (session cookie present)");
                return new AuthContext
                {
                    IsAuthenticated = true,
                    AuthType = AuthType.ApiSecret,
                    SubjectName = "dev-admin",
                    Permissions = ["*"],
                    Roles = ["admin"],
                };
            }
        }

        return AuthContext.Unauthenticated();
    }

    /// <summary>
    /// Set unauthenticated context on the HttpContext
    /// </summary>
    private static void SetUnauthenticated(HttpContext context)
    {
        var authContext = AuthContext.Unauthenticated();
        context.Items["AuthContext"] = authContext;
        context.Items["PermissionTrie"] = new PermissionTrie();
        context.Items["GrantedScopes"] = (IReadOnlySet<string>)new HashSet<string>();
        context.Items["AuthenticationContext"] = MapToLegacyContext(authContext);
    }

    /// <summary>
    /// Map new AuthContext to legacy AuthenticationContext for backward compatibility
    /// </summary>
    private static AuthenticationContext MapToLegacyContext(AuthContext authContext)
    {
        return new AuthenticationContext
        {
            IsAuthenticated = authContext.IsAuthenticated,
            AuthenticationType = MapAuthType(authContext.AuthType),
            SubjectId = authContext.SubjectId?.ToString() ?? authContext.SubjectName,
            Permissions = authContext.Permissions,
            Token = authContext.RawToken,
        };
    }

    /// <summary>
    /// Map new AuthType to legacy AuthenticationType enum
    /// </summary>
    private static AuthenticationType MapAuthType(AuthType authType)
    {
        return authType switch
        {
            AuthType.None => AuthenticationType.None,
            AuthType.ApiSecret => AuthenticationType.ApiSecret,
            AuthType.InstanceKey => AuthenticationType.ApiSecret,
            AuthType.LegacyJwt => AuthenticationType.JwtToken,
            AuthType.LegacyAccessToken => AuthenticationType.JwtToken,
            AuthType.OidcToken => AuthenticationType.JwtToken,
            AuthType.SessionCookie => AuthenticationType.JwtToken,
            _ => AuthenticationType.None,
        };
    }
}

/// <summary>
/// Legacy authentication context for backward compatibility.
/// New code should use AuthContext from Core.Models.Authorization.
/// </summary>
public class AuthenticationContext
{
    /// <summary>
    /// Whether the request is authenticated
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Type of authentication used
    /// </summary>
    public AuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Subject identifier (user/device ID)
    /// </summary>
    public string? SubjectId { get; set; }

    /// <summary>
    /// List of permissions for this authentication
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// JWT token if using token authentication
    /// </summary>
    public string? Token { get; set; }
}

/// <summary>
/// Legacy authentication types for backward compatibility
/// </summary>
public enum AuthenticationType
{
    /// <summary>
    /// No authentication
    /// </summary>
    None,

    /// <summary>
    /// API secret authentication
    /// </summary>
    ApiSecret,

    /// <summary>
    /// JWT token authentication
    /// </summary>
    JwtToken,
}

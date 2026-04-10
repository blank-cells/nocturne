namespace Nocturne.API.Services.Auth;

/// <summary>
/// Validates redirect URIs per RFC 8252 (OAuth 2.0 for Native Apps).
/// </summary>
public class RedirectUriValidator
{
    private static readonly HashSet<string> LoopbackHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "127.0.0.1",
        "[::1]",
        "localhost",
    };

    /// <summary>
    /// Validate a redirect URI for use in a DCR registration request.
    /// </summary>
    public bool IsValidForRegistration(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        // Fragments are never allowed (RFC 6749 Section 3.1.2)
        if (uri.Contains('#'))
            return false;

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            return false;

        return Classify(parsed) != UriClass.Invalid;
    }

    /// <summary>
    /// Validate a presented redirect_uri against a registered one during /oauth/authorize.
    /// Byte-exact match, except loopback URIs allow any port on the presented URI.
    /// </summary>
    public bool IsValidForAuthorize(string registered, string presented)
    {
        if (string.Equals(registered, presented, StringComparison.Ordinal))
            return true;

        // For loopback, allow port variation (RFC 8252 Section 7.3)
        if (!Uri.TryCreate(registered, UriKind.Absolute, out var regUri) ||
            !Uri.TryCreate(presented, UriKind.Absolute, out var presUri))
            return false;

        if (Classify(regUri) != UriClass.Loopback)
            return false;

        // Scheme, host, and path must match; port may differ
        return string.Equals(regUri.Scheme, presUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(regUri.Host, presUri.Host, StringComparison.OrdinalIgnoreCase)
            && string.Equals(regUri.AbsolutePath, presUri.AbsolutePath, StringComparison.Ordinal);
    }

    private static UriClass Classify(Uri uri)
    {
        // Custom scheme (not http/https) — must contain a dot to prevent hijacking
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return uri.Scheme.Contains('.') ? UriClass.CustomScheme : UriClass.Invalid;
        }

        var isLoopback = LoopbackHosts.Contains(uri.Host);

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            // HTTPS + loopback is rejected (RFC 8252 Section 8.3: use http for loopback)
            return isLoopback ? UriClass.Invalid : UriClass.ClaimedHttps;
        }

        // HTTP — only loopback is allowed
        return isLoopback ? UriClass.Loopback : UriClass.Invalid;
    }

    private enum UriClass
    {
        Invalid,
        CustomScheme,
        ClaimedHttps,
        Loopback,
    }
}

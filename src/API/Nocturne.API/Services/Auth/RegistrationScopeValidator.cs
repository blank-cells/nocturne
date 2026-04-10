using Nocturne.Core.Models.Authorization;

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Validates requested scopes during DCR against the canonical scope registry.
/// </summary>
public static class RegistrationScopeValidator
{
    /// <summary>
    /// Validate a space-delimited scope string from a DCR request.
    /// Returns null if valid, or a list of unknown scopes if invalid.
    /// </summary>
    public static List<string>? ValidateScopes(string? scopeString)
    {
        if (string.IsNullOrWhiteSpace(scopeString))
            return null; // No scopes requested — valid (will use defaults)

        var requested = scopeString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var unknown = requested.Where(s => !OAuthScopes.ValidRequestScopes.Contains(s)).ToList();

        return unknown.Count > 0 ? unknown : null;
    }
}

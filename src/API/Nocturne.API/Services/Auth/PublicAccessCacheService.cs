using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Cached resolved permissions for the Public system subject on a given tenant.
/// </summary>
public sealed record PublicAccessInfo(
    Guid SubjectId,
    Guid MemberId,
    bool LimitTo24Hours,
    HashSet<string> EffectivePermissions
);

/// <summary>
/// Caches the Public system subject's resolved permissions per tenant.
/// Used by AuthenticationMiddleware to populate auth context for unauthenticated
/// requests when a tenant is resolved.
/// </summary>
public sealed class PublicAccessCacheService
{
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<NocturneDbContext> _dbContextFactory;
    private readonly ILogger<PublicAccessCacheService> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    public PublicAccessCacheService(
        IMemoryCache cache,
        IDbContextFactory<NocturneDbContext> dbContextFactory,
        ILogger<PublicAccessCacheService> logger)
    {
        _cache = cache;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Get the Public subject's effective permissions for the given tenant.
    /// Returns null if the membership doesn't exist, has no roles, or has no permissions.
    /// </summary>
    public async Task<PublicAccessInfo?> GetPublicAccessAsync(Guid tenantId)
    {
        var cacheKey = $"public-permissions:{tenantId}";

        if (_cache.TryGetValue(cacheKey, out PublicAccessInfo? cached))
        {
            return cached;
        }

        var result = await ResolvePublicAccessAsync(tenantId);

        // Cache both hits and misses to avoid repeated DB queries
        _cache.Set(cacheKey, result, CacheTtl);

        return result;
    }

    /// <summary>
    /// Evict the cached permissions for a tenant (e.g., when roles change).
    /// </summary>
    public void Evict(Guid tenantId)
    {
        _cache.Remove($"public-permissions:{tenantId}");
    }

    private async Task<PublicAccessInfo?> ResolvePublicAccessAsync(Guid tenantId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var membership = await dbContext.TenantMembers
            .AsNoTracking()
            .Include(tm => tm.MemberRoles)
                .ThenInclude(mr => mr.TenantRole)
            .Include(tm => tm.Subject)
            .Where(tm => tm.TenantId == tenantId
                         && tm.Subject!.IsSystemSubject
                         && tm.Subject.Name == "Public"
                         && tm.RevokedAt == null)
            .FirstOrDefaultAsync();

        if (membership == null)
        {
            _logger.LogDebug("No Public subject membership found for tenant {TenantId}", tenantId);
            return null;
        }

        if (membership.MemberRoles.Count == 0 && (membership.DirectPermissions == null || membership.DirectPermissions.Count == 0))
        {
            _logger.LogDebug("Public subject membership for tenant {TenantId} has no roles or direct permissions", tenantId);
            return null;
        }

        // Union all role permissions + direct permissions
        var rolePermissions = membership.MemberRoles
            .SelectMany(mr => mr.TenantRole.Permissions);
        var directPermissions = membership.DirectPermissions ?? [];
        var effectivePermissions = rolePermissions.Union(directPermissions).ToHashSet();

        if (effectivePermissions.Count == 0)
        {
            _logger.LogDebug("Public subject membership for tenant {TenantId} resolved to zero effective permissions", tenantId);
            return null;
        }

        _logger.LogDebug(
            "Resolved {Count} effective permissions for Public subject on tenant {TenantId}",
            effectivePermissions.Count, tenantId);

        return new PublicAccessInfo(
            SubjectId: membership.SubjectId,
            MemberId: membership.Id,
            LimitTo24Hours: membership.LimitTo24Hours,
            EffectivePermissions: effectivePermissions
        );
    }
}

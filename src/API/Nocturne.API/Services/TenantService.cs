using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Nocturne.API.Multitenancy;
using Nocturne.Connectors.Core.Utilities;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services;

public partial class TenantService : ITenantService
{
    private readonly IDbContextFactory<NocturneDbContext> _factory;
    private readonly IMemoryCache _cache;
    private readonly MultitenancyConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITenantRoleService _roleService;

    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "api", "www", "default", "app", "mail", "ftp",
        "status", "help", "support"
    };

    [GeneratedRegex(@"^[a-z0-9][a-z0-9\-]{1,62}[a-z0-9]$")]
    private static partial Regex SlugPattern();

    public TenantService(
        IDbContextFactory<NocturneDbContext> factory,
        IMemoryCache cache,
        IOptions<MultitenancyConfiguration> config,
        IHttpClientFactory httpClientFactory,
        ITenantRoleService roleService)
    {
        _factory = factory;
        _cache = cache;
        _config = config.Value;
        _httpClientFactory = httpClientFactory;
        _roleService = roleService;
    }

    public async Task<TenantCreatedDto> CreateAsync(
        string slug, string displayName, Guid creatorSubjectId, string? apiSecret = null, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);

        var plaintextSecret = apiSecret ?? GenerateApiSecret();
        var tenant = new TenantEntity
        {
            Slug = slug.ToLowerInvariant(),
            DisplayName = displayName,
            ApiSecretHash = HashUtils.Sha1Hex(plaintextSecret),
            IsActive = true,
        };

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync(ct);

        // Seed default roles for this tenant
        await _roleService.SeedRolesForTenantAsync(tenant.Id, ct);

        // Assign creator as owner
        var ownerRole = await context.TenantRoles
            .FirstAsync(r => r.TenantId == tenant.Id && r.Slug == "owner", ct);
        await AddMemberAsync(tenant.Id, creatorSubjectId, [ownerRole.Id], ct: ct);

        return ToCreatedDto(tenant, plaintextSecret);
    }

    public async Task<TenantCreatedDto> CreateWithoutOwnerAsync(
        string slug, string displayName, string? apiSecret = null, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);

        var plaintextSecret = apiSecret ?? GenerateApiSecret();
        var tenant = new TenantEntity
        {
            Slug = slug.ToLowerInvariant(),
            DisplayName = displayName,
            ApiSecretHash = HashUtils.Sha1Hex(plaintextSecret),
            IsActive = true,
        };

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync(ct);

        // Seed default roles for this tenant (but don't assign an owner)
        await _roleService.SeedRolesForTenantAsync(tenant.Id, ct);

        return ToCreatedDto(tenant, plaintextSecret);
    }

    public async Task<List<TenantDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);
        return await context.Tenants.AsNoTracking()
            .Select(t => new TenantDto(t.Id, t.Slug, t.DisplayName, t.IsActive, t.IsDefault, t.SysCreatedAt))
            .ToListAsync(ct);
    }

    public async Task<TenantDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);
        var tenant = await context.Tenants.AsNoTracking()
            .Include(t => t.Members)
                .ThenInclude(m => m.Subject)
            .Include(t => t.Members)
                .ThenInclude(m => m.MemberRoles)
                    .ThenInclude(mr => mr.TenantRole)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tenant == null) return null;

        return new TenantDetailDto(
            tenant.Id, tenant.Slug, tenant.DisplayName, tenant.IsActive, tenant.IsDefault, tenant.SysCreatedAt,
            tenant.Members
                .Where(m => m.RevokedAt == null)
                .Select(m => new TenantMemberDto(
                    m.Id,
                    m.SubjectId,
                    m.Subject?.Name,
                    m.MemberRoles.Select(mr => new TenantMemberRoleDto(
                        mr.TenantRoleId, mr.TenantRole.Name, mr.TenantRole.Slug)).ToList(),
                    m.DirectPermissions,
                    m.Label,
                    m.LimitTo24Hours,
                    m.LastUsedAt,
                    m.SysCreatedAt))
                .ToList());
    }

    public async Task<TenantDto> UpdateAsync(
        Guid id, string displayName, bool isActive, bool? allowAccessRequests = null, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);
        var tenant = await context.Tenants.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Tenant {id} not found");

        tenant.DisplayName = displayName;
        tenant.IsActive = isActive;
        if (allowAccessRequests.HasValue)
            tenant.AllowAccessRequests = allowAccessRequests.Value;
        await context.SaveChangesAsync(ct);

        // Invalidate cached tenant context
        _cache.Remove($"tenant:{tenant.Slug}");
        if (tenant.IsDefault)
            _cache.Remove("tenant:__default__");

        return ToDto(tenant);
    }

    public async Task AddMemberAsync(
        Guid tenantId, Guid subjectId, List<Guid> roleIds, List<string>? directPermissions = null,
        string? label = null, bool limitTo24Hours = false, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);

        // Check if already a member
        var exists = await context.TenantMembers
            .AnyAsync(tm => tm.TenantId == tenantId && tm.SubjectId == subjectId, ct);

        if (exists)
            return;

        var member = new TenantMemberEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            SubjectId = subjectId,
            DirectPermissions = directPermissions,
            Label = label,
            LimitTo24Hours = limitTo24Hours,
            SysCreatedAt = DateTime.UtcNow,
            SysUpdatedAt = DateTime.UtcNow,
        };

        context.TenantMembers.Add(member);

        // Create role assignments
        var now = DateTime.UtcNow;
        foreach (var roleId in roleIds)
        {
            context.TenantMemberRoles.Add(new TenantMemberRoleEntity
            {
                Id = Guid.CreateVersion7(),
                TenantMemberId = member.Id,
                TenantRoleId = roleId,
                SysCreatedAt = now,
            });
        }

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Race condition: another request already inserted. This is fine.
        }
    }

    public async Task RemoveMemberAsync(
        Guid tenantId, Guid subjectId, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);
        var member = await context.TenantMembers
            .FirstOrDefaultAsync(tm => tm.TenantId == tenantId && tm.SubjectId == subjectId, ct);

        if (member != null)
        {
            context.TenantMembers.Remove(member);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<List<TenantDto>> GetTenantsForSubjectAsync(
        Guid subjectId, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);
        return await context.TenantMembers.AsNoTracking()
            .Where(tm => tm.SubjectId == subjectId)
            .Include(tm => tm.Tenant)
            .Select(tm => new TenantDto(
                tm.Tenant!.Id, tm.Tenant.Slug, tm.Tenant.DisplayName,
                tm.Tenant.IsActive, tm.Tenant.IsDefault, tm.Tenant.SysCreatedAt))
            .ToListAsync(ct);
    }

    public async Task<SlugValidationResult> ValidateSlugAsync(string slug, CancellationToken ct = default)
    {
        var normalized = slug.ToLowerInvariant().Trim();

        if (!SlugPattern().IsMatch(normalized))
            return new SlugValidationResult(false, "Slug must be 3-64 characters, alphanumeric and hyphens only, no leading/trailing hyphens");

        if (ReservedSlugs.Contains(normalized))
            return new SlugValidationResult(false, "This name is reserved");

        await using var context = await _factory.CreateDbContextAsync(ct);
        var exists = await context.Tenants.AsNoTracking()
            .AnyAsync(t => t.Slug == normalized, ct);

        if (exists)
            return new SlugValidationResult(false, "This name is already taken");

        if (!string.IsNullOrEmpty(_config.SlugValidationWebhookUrl))
        {
            try
            {
                var client = _httpClientFactory.CreateClient("slug-validation");
                var response = await client.PostAsJsonAsync(
                    _config.SlugValidationWebhookUrl,
                    new { slug = normalized },
                    ct);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<SlugValidationResult>(ct);
                    if (result is { IsValid: false })
                        return result;
                }
            }
            catch
            {
                // Webhook failure should not block validation — fall through to success
            }
        }

        return new SlugValidationResult(true);
    }

    public async Task<string> UpdateApiSecretAsync(Guid tenantId, string newApiSecret, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);
        var tenant = await context.Tenants.FindAsync([tenantId], ct)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} not found");

        tenant.ApiSecretHash = HashUtils.Sha1Hex(newApiSecret);
        await context.SaveChangesAsync(ct);

        _cache.Remove($"tenant:{tenant.Slug}");
        if (tenant.IsDefault)
            _cache.Remove("tenant:__default__");

        return newApiSecret;
    }

    public async Task<string> RegenerateApiSecretAsync(Guid tenantId, CancellationToken ct = default)
    {
        var newSecret = GenerateApiSecret();
        await UpdateApiSecretAsync(tenantId, newSecret, ct);
        return newSecret;
    }

    public async Task<bool> HasApiSecretAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);
        return await context.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.ApiSecretHash != null)
            .FirstOrDefaultAsync(ct);
    }

    private static string GenerateApiSecret()
    {
        var bytes = new byte[24];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static TenantDto ToDto(TenantEntity t) =>
        new(t.Id, t.Slug, t.DisplayName, t.IsActive, t.IsDefault, t.SysCreatedAt);

    private static TenantCreatedDto ToCreatedDto(TenantEntity t, string plaintextSecret) =>
        new(t.Id, t.Slug, t.DisplayName, t.IsActive, t.IsDefault, t.SysCreatedAt, plaintextSecret);
}

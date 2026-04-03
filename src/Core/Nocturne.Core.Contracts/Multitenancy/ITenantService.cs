namespace Nocturne.Core.Contracts.Multitenancy;

public interface ITenantService
{
    Task<TenantCreatedDto> CreateAsync(string slug, string displayName, Guid creatorSubjectId, string? apiSecret = null, CancellationToken ct = default);
    Task<TenantCreatedDto> CreateWithoutOwnerAsync(string slug, string displayName, string? apiSecret = null, CancellationToken ct = default);
    Task<List<TenantDto>> GetAllAsync(CancellationToken ct = default);
    Task<TenantDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TenantDto> UpdateAsync(Guid id, string displayName, bool isActive, bool? allowAccessRequests = null, CancellationToken ct = default);
    Task AddMemberAsync(Guid tenantId, Guid subjectId, List<Guid> roleIds, List<string>? directPermissions = null, string? label = null, bool limitTo24Hours = false, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid tenantId, Guid subjectId, CancellationToken ct = default);
    Task<List<TenantDto>> GetTenantsForSubjectAsync(Guid subjectId, CancellationToken ct = default);
    Task<SlugValidationResult> ValidateSlugAsync(string slug, CancellationToken ct = default);
    Task<string> UpdateApiSecretAsync(Guid tenantId, string newApiSecret, CancellationToken ct = default);
    Task<string> RegenerateApiSecretAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> HasApiSecretAsync(Guid tenantId, CancellationToken ct = default);
}

public record TenantDto(Guid Id, string Slug, string DisplayName, bool IsActive, bool IsDefault, DateTime SysCreatedAt);

public record TenantCreatedDto(Guid Id, string Slug, string DisplayName, bool IsActive, bool IsDefault, DateTime SysCreatedAt, string ApiSecret);

public record TenantDetailDto(Guid Id, string Slug, string DisplayName, bool IsActive, bool IsDefault, DateTime SysCreatedAt, List<TenantMemberDto> Members);

public record TenantMemberDto(
    Guid Id,
    Guid SubjectId,
    string? Name,
    List<TenantMemberRoleDto> Roles,
    List<string>? DirectPermissions,
    string? Label,
    bool LimitTo24Hours,
    DateTime? LastUsedAt,
    DateTime SysCreatedAt);

public record TenantMemberRoleDto(Guid RoleId, string Name, string Slug);

public record SlugValidationResult(bool IsValid, string? Message = null);

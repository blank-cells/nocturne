// MIGRATION-IN-PROGRESS: This service is being rewritten as part of the
// Shared Discord Bot Link Flow consolidation (Task 1.9). The body has been
// stubbed so the solution still compiles after the old chat_identity_links
// table was dropped. Do not call any of these methods at runtime.
using Microsoft.EntityFrameworkCore;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Services.Chat;

/// <summary>
/// Manages chat platform identity links for bot-mediated interactions.
/// MIGRATION-IN-PROGRESS: stubbed pending rewrite against ChatIdentityDirectory.
/// </summary>
public sealed class ChatIdentityService(
    IDbContextFactory<NocturneDbContext> contextFactory,
    ILogger<ChatIdentityService> logger)
{
    // Reference fields so DI registration and field-suppression analyzers stay happy.
    private readonly IDbContextFactory<NocturneDbContext> _contextFactory = contextFactory;
    private readonly ILogger<ChatIdentityService> _logger = logger;

    public Task<object?> FindByPlatformAsync(
        Guid tenantId, string platform, string platformUserId, CancellationToken ct) =>
        throw new NotImplementedException("MIGRATION-IN-PROGRESS: ChatIdentityService will be rewritten in Task 1.9");

    public Task<IReadOnlyList<object>> GetByUserAsync(
        Guid tenantId, Guid userId, CancellationToken ct) =>
        throw new NotImplementedException("MIGRATION-IN-PROGRESS: ChatIdentityService will be rewritten in Task 1.9");

    public Task<IReadOnlyList<object>> GetByTenantAsync(
        Guid tenantId, CancellationToken ct) =>
        throw new NotImplementedException("MIGRATION-IN-PROGRESS: ChatIdentityService will be rewritten in Task 1.9");

    public Task<object> CreateLinkAsync(
        Guid tenantId, Guid userId, string platform, string platformUserId,
        string? platformChannelId, CancellationToken ct) =>
        throw new NotImplementedException("MIGRATION-IN-PROGRESS: ChatIdentityService will be rewritten in Task 1.9");

    public Task RevokeLinkAsync(Guid tenantId, Guid linkId, CancellationToken ct) =>
        throw new NotImplementedException("MIGRATION-IN-PROGRESS: ChatIdentityService will be rewritten in Task 1.9");
}

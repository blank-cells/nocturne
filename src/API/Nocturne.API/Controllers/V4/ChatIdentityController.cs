// MIGRATION-IN-PROGRESS: This controller is being rewritten as part of the
// Shared Discord Bot Link Flow consolidation (Task 1.9). All actions throw
// NotImplementedException pending the rewrite against ChatIdentityDirectory.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenApi.Remote.Attributes;
using Nocturne.API.Services.Chat;
using Nocturne.Core.Contracts.Multitenancy;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Manages chat platform identity links for bot-mediated alert delivery and glucose queries.
/// MIGRATION-IN-PROGRESS: stubbed pending rewrite.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v4/chat-identity")]
[Tags("V4 Chat Identity")]
public class ChatIdentityController : ControllerBase
{
    private readonly ChatIdentityService _chatIdentityService;
    private readonly ITenantAccessor _tenantAccessor;

    public ChatIdentityController(
        ChatIdentityService chatIdentityService,
        ITenantAccessor tenantAccessor)
    {
        _chatIdentityService = chatIdentityService;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>
    /// List active chat identity links for the current tenant.
    /// </summary>
    [HttpGet]
    [RemoteQuery]
    [ProducesResponseType(typeof(List<ChatIdentityLinkResponse>), StatusCodes.Status200OK)]
    public Task<ActionResult<List<ChatIdentityLinkResponse>>> GetLinks(CancellationToken ct) =>
        throw new NotImplementedException("MIGRATION-IN-PROGRESS");

    /// <summary>
    /// Create a new chat identity link.
    /// </summary>
    [HttpPost]
    [RemoteCommand(Invalidates = ["GetLinks"])]
    [ProducesResponseType(typeof(ChatIdentityLinkResponse), StatusCodes.Status201Created)]
    public Task<ActionResult<ChatIdentityLinkResponse>> CreateLink(
        [FromBody] CreateChatIdentityLinkRequest request, CancellationToken ct) =>
        throw new NotImplementedException("MIGRATION-IN-PROGRESS");

    /// <summary>
    /// Revoke (soft-delete) a chat identity link.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RemoteCommand(Invalidates = ["GetLinks"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<ActionResult> RevokeLink(Guid id, CancellationToken ct) =>
        throw new NotImplementedException("MIGRATION-IN-PROGRESS");

    /// <summary>
    /// Resolve a platform identity to a Nocturne user.
    /// </summary>
    [HttpGet("resolve")]
    [RemoteQuery]
    [ProducesResponseType(typeof(ChatIdentityLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ChatIdentityLinkResponse>> Resolve(
        [FromQuery] string platform, [FromQuery] string platformUserId, CancellationToken ct) =>
        throw new NotImplementedException("MIGRATION-IN-PROGRESS");
}

#region DTOs

public class ChatIdentityLinkResponse
{
    public Guid Id { get; set; }
    public Guid NocturneUserId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string PlatformUserId { get; set; } = string.Empty;
    public string? PlatformChannelId { get; set; }
    public string DisplayUnit { get; set; } = "mg/dL";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateChatIdentityLinkRequest
{
    public Guid NocturneUserId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string PlatformUserId { get; set; } = string.Empty;
    public string? PlatformChannelId { get; set; }
}

#endregion

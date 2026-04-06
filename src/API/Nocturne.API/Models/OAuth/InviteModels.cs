namespace Nocturne.API.Models.OAuth;

/// <summary>
/// Request to create a follower invite link
/// </summary>
public class CreateInviteRequest
{
    /// <summary>
    /// Scopes to grant when the invite is accepted
    /// </summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>
    /// Optional label for the grant (e.g., "Mom", "Endocrinologist")
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Days until the invite expires (default: 7)
    /// </summary>
    public int? ExpiresInDays { get; set; }

    /// <summary>
    /// Maximum number of times the invite can be used (null = unlimited)
    /// </summary>
    public int? MaxUses { get; set; }

    /// <summary>
    /// When true, grants created from this invite will only allow access to
    /// the last 24 hours of data (rolling window from each request time).
    /// </summary>
    public bool LimitTo24Hours { get; set; }
}

/// <summary>
/// Response after creating an invite
/// </summary>
public class CreateInviteResponse
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string InviteUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// DTO for an invite in list responses
/// </summary>
public class InviteDto
{
    public Guid Id { get; set; }
    public List<string> Scopes { get; set; } = new();
    public string? Label { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int? MaxUses { get; set; }
    public int UseCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsValid { get; set; }
    public bool IsExpired { get; set; }
    public bool IsRevoked { get; set; }
    public bool LimitTo24Hours { get; set; }
    public List<InviteUsageDto> UsedBy { get; set; } = new();
}

/// <summary>
/// DTO for invite usage information
/// </summary>
public class InviteUsageDto
{
    public Guid FollowerSubjectId { get; set; }
    public string? FollowerName { get; set; }
    public string? FollowerEmail { get; set; }
    public DateTime UsedAt { get; set; }
}

/// <summary>
/// Response containing a list of invites
/// </summary>
public class InviteListResponse
{
    public List<InviteDto> Invites { get; set; } = new();
}

/// <summary>
/// Response for invite info (for the accept page)
/// </summary>
public class InviteInfoResponse
{
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public List<string> Scopes { get; set; } = new();
    public string? Label { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsValid { get; set; }
    public bool IsExpired { get; set; }
    public bool IsRevoked { get; set; }
    public bool LimitTo24Hours { get; set; }
}

/// <summary>
/// Response after accepting an invite
/// </summary>
public class AcceptInviteResponse
{
    public bool Success { get; set; }
    public Guid? GrantId { get; set; }
}

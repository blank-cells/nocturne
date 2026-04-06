namespace Nocturne.API.Models.OAuth;

/// <summary>
/// DTO representing an OAuth grant for the management UI
/// </summary>
public class OAuthGrantDto
{
    public Guid Id { get; set; }
    public string GrantType { get; set; } = string.Empty;
    public string? ClientId { get; set; }
    public string? ClientDisplayName { get; set; }
    public bool IsKnownClient { get; set; }
    public Guid? FollowerSubjectId { get; set; }
    public string? FollowerName { get; set; }
    public string? FollowerEmail { get; set; }
    public List<string> Scopes { get; set; } = new();
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? LastUsedUserAgent { get; set; }
    /// <summary>
    /// When true, this grant only allows access to data from the last 24 hours
    /// (rolling window from each request time).
    /// </summary>
    public bool LimitTo24Hours { get; set; }
}

/// <summary>
/// Response containing a list of OAuth grants
/// </summary>
public class OAuthGrantListResponse
{
    public List<OAuthGrantDto> Grants { get; set; } = new();
}

/// <summary>
/// Request to create a follower grant (share data with another user)
/// </summary>
public class CreateFollowerGrantRequest
{
    public string FollowerEmail { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();
    public string? Label { get; set; }
    public string? TemporaryPassword { get; set; }
    public string? FollowerDisplayName { get; set; }
}

/// <summary>
/// Request to update an existing grant's label and/or scopes
/// </summary>
public class UpdateGrantRequest
{
    public string? Label { get; set; }
    public List<string>? Scopes { get; set; }
}

/// <summary>
/// DTO for a data owner that the current user can view as a follower
/// </summary>
public class FollowerTargetDto
{
    public Guid SubjectId { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public List<string> Scopes { get; set; } = new();
    public string? Label { get; set; }
}

/// <summary>
/// Response containing a list of follower targets
/// </summary>
public class FollowerTargetListResponse
{
    public List<FollowerTargetDto> Targets { get; set; } = new();
}

namespace Nocturne.Core.Models.Authorization;

public class SubjectOidcIdentity
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string? ProviderIcon { get; set; }
    public string? ProviderButtonColor { get; set; }
    public string OidcSubjectId { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime LinkedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public enum OidcLinkOutcome
{
    Created,
    AlreadyLinkedToSelf,
    AlreadyLinkedToOther,
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// Join table linking a subject to one or more OIDC provider identities.
/// Replaces the scalar OidcSubjectId/OidcIssuer columns on SubjectEntity.
/// </summary>
[Table("subject_oidc_identities")]
public class SubjectOidcIdentityEntity
{
    [Key, Column("id")]
    public Guid Id { get; set; }

    [Column("subject_id")]
    public Guid SubjectId { get; set; }

    [Column("provider_id")]
    public Guid ProviderId { get; set; }

    [Required, MaxLength(255), Column("oidc_subject_id")]
    public string OidcSubjectId { get; set; } = string.Empty;

    [Required, MaxLength(500), Column("issuer")]
    public string Issuer { get; set; } = string.Empty;

    [MaxLength(255), Column("email")]
    public string? Email { get; set; }

    [Column("linked_at")]
    public DateTime LinkedAt { get; set; }

    [Column("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    // Navigation properties

    public SubjectEntity? Subject { get; set; }

    public OidcProviderEntity? Provider { get; set; }
}

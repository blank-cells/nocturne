namespace Nocturne.Core.Models.Configuration;

/// <summary>
/// Platform-level configuration for managed/SaaS deployments.
/// </summary>
public class PlatformOptions
{
    public const string SectionName = "Platform";

    /// <summary>
    /// Explicit list of subject IDs to grant platform admin on startup.
    /// Takes precedence over the implicit first-tenant-owner bootstrap.
    /// Useful for SaaS operators who control the deployment config.
    /// </summary>
    public List<Guid> AdminSubjectIds { get; set; } = [];
}

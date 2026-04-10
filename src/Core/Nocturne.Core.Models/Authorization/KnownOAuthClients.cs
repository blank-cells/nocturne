namespace Nocturne.Core.Models.Authorization;

/// <summary>
/// Well-known diabetes app directory. Ships bundled with Nocturne and updates
/// with releases. Provides identity metadata for consent screens and for
/// seeding pre-verified OAuth client rows per tenant via DCR.
/// </summary>
public static class KnownOAuthClients
{
    /// <summary>
    /// Bundled known client entries keyed on reverse-DNS software_id.
    /// </summary>
    public static readonly IReadOnlyList<KnownClientEntry> Entries = new List<KnownClientEntry>
    {
        new()
        {
            SoftwareId = "org.trio.diabetes",
            DisplayName = "Trio",
            Homepage = "https://github.com/nightscout/Trio",
            RedirectUris = ["trio://oauth/callback"],
            TypicalScopes =
            [
                OAuthScopes.EntriesReadWrite,
                OAuthScopes.TreatmentsReadWrite,
                OAuthScopes.DeviceStatusReadWrite,
                OAuthScopes.ProfileRead,
            ],
        },
        new()
        {
            SoftwareId = "org.nightscoutfoundation.xdrip",
            DisplayName = "xDrip+",
            Homepage = "https://github.com/NightscoutFoundation/xDrip",
            RedirectUris = ["org.nightscoutfoundation.xdrip://oauth/callback"],
            TypicalScopes =
            [
                OAuthScopes.EntriesReadWrite,
                OAuthScopes.TreatmentsReadWrite,
                OAuthScopes.DeviceStatusReadWrite,
            ],
        },
        new()
        {
            SoftwareId = "org.loopkit.loop",
            DisplayName = "Loop",
            Homepage = "https://loopkit.github.io/loopdocs/",
            RedirectUris = ["org.loopkit.loop://oauth/callback"],
            TypicalScopes =
            [
                OAuthScopes.EntriesReadWrite,
                OAuthScopes.TreatmentsReadWrite,
                OAuthScopes.DeviceStatusReadWrite,
            ],
        },
        new()
        {
            SoftwareId = "org.androidaps.aaps",
            DisplayName = "AAPS",
            Homepage = "https://androidaps.readthedocs.io",
            RedirectUris = ["org.androidaps.aaps://oauth/callback"],
            TypicalScopes =
            [
                OAuthScopes.EntriesReadWrite,
                OAuthScopes.TreatmentsReadWrite,
                OAuthScopes.ProfileRead,
                OAuthScopes.DeviceStatusReadWrite,
            ],
        },
        new()
        {
            SoftwareId = "github.nightscout.nightscout",
            DisplayName = "Nightscout",
            Homepage = "https://nightscout.github.io/",
            RedirectUris = [],
            TypicalScopes =
            [
                OAuthScopes.EntriesRead,
                OAuthScopes.TreatmentsRead,
                OAuthScopes.DeviceStatusRead,
                OAuthScopes.ProfileRead,
            ],
        },
        new()
        {
            SoftwareId = "io.sugarmate",
            DisplayName = "Sugarmate",
            Homepage = "https://sugarmate.io/",
            RedirectUris = [],
            TypicalScopes = [OAuthScopes.EntriesRead],
        },
        new()
        {
            SoftwareId = "com.nickenilsson.nightwatch",
            DisplayName = "Nightwatch",
            Homepage = "https://github.com/nickenilsson/nightwatch",
            RedirectUris = [],
            TypicalScopes = [OAuthScopes.EntriesRead, OAuthScopes.TreatmentsRead],
        },
        new()
        {
            SoftwareId = "com.nocturne.follower",
            DisplayName = "Nocturne Follower",
            RedirectUris = [],
            TypicalScopes =
            [
                OAuthScopes.EntriesRead,
                OAuthScopes.TreatmentsRead,
                OAuthScopes.DeviceStatusRead,
                OAuthScopes.ProfileRead,
            ],
        },
        new()
        {
            SoftwareId = "com.nocturne.widget.windows",
            DisplayName = "Nocturne Windows Widget",
            Homepage = "https://github.com/nightscout/nocturne",
            RedirectUris = [],
            TypicalScopes =
            [
                OAuthScopes.EntriesRead,
                OAuthScopes.TreatmentsRead,
                OAuthScopes.DeviceStatusRead,
                OAuthScopes.ProfileRead,
            ],
        },
        new()
        {
            SoftwareId = "com.nocturne.tray",
            DisplayName = "Nocturne Tray",
            Homepage = "https://github.com/nightscout/nocturne",
            RedirectUris = [],
            TypicalScopes =
            [
                OAuthScopes.EntriesRead,
                OAuthScopes.TreatmentsRead,
                OAuthScopes.DeviceStatusRead,
                OAuthScopes.ProfileRead,
            ],
        },
    };

    /// <summary>
    /// The well-known software_id used for follower (user-to-user sharing) grants.
    /// </summary>
    public const string FollowerSoftwareId = "com.nocturne.follower";

    /// <summary>
    /// Legacy constant kept for backward compatibility with existing follower grant code.
    /// </summary>
    public const string FollowerClientId = "nocturne-follower-internal";

    /// <summary>
    /// Look up a known app entry by its RFC 7591 software_id (reverse-DNS).
    /// </summary>
    public static KnownClientEntry? MatchBySoftwareId(string softwareId) =>
        Entries.FirstOrDefault(e => string.Equals(e.SoftwareId, softwareId, StringComparison.Ordinal));
}

/// <summary>
/// Entry in the known OAuth client directory.
/// </summary>
public class KnownClientEntry
{
    /// <summary>
    /// RFC 7591 software_id — reverse-DNS identifier stable across installs
    /// (e.g., "org.trio.diabetes").
    /// </summary>
    public string SoftwareId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable app name for the consent screen.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// App homepage URL.
    /// </summary>
    public string? Homepage { get; set; }

    /// <summary>
    /// App logo URI for the consent screen.
    /// </summary>
    public string? LogoUri { get; set; }

    /// <summary>
    /// Allowed redirect URIs to seed when the client registers via DCR.
    /// </summary>
    public List<string> RedirectUris { get; set; } = [];

    /// <summary>
    /// Typical scopes this app requests (informational, used for seeding).
    /// </summary>
    public List<string> TypicalScopes { get; set; } = [];
}

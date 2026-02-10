using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace SPTQuestingBots.Server;

/// <summary>
/// Mod metadata for the QuestingBots server-side plugin.
///
/// This record is discovered by the SPT 4.x mod loader at startup. It provides
/// the mod's identity, version compatibility, and license information required
/// by the <see cref="AbstractModMetadata"/> contract.
///
/// <para>
/// The <see cref="SptVersion"/> range constraint (<c>~4.0.0</c>) ensures the
/// mod is only loaded on compatible SPT server versions (4.0.x).
/// </para>
///
/// <para>
/// <b>Original mod:</b> DanW-SPTQuestingBots v0.10.3 (TypeScript / SPT 3.x).
/// This C# port targets SPT 4.x with the server rewritten to use the
/// <c>[Injectable]</c>-based DI system.
/// </para>
/// </summary>
public record QuestingBotsMetadata : AbstractModMetadata
{
    /// <summary>Globally unique identifier using reverse-domain notation.</summary>
    public override string ModGuid { get; init; } = "com.danw.sptquestingbots";

    /// <summary>Human-readable mod name shown in the server console.</summary>
    public override string Name { get; init; } = "SPTQuestingBots";

    /// <summary>Primary author of the original mod.</summary>
    public override string Author { get; init; } = "DanW";

    /// <inheritdoc />
    public override List<string>? Contributors { get; init; } = null;

    /// <summary>
    /// Semantic version of this C# port. Starts at 1.0.0 to distinguish
    /// from the original TypeScript releases (0.x.x).
    /// </summary>
    public override Version Version { get; init; } = new("1.11.0");

    /// <summary>
    /// Compatible SPT server version range. The tilde (<c>~</c>) prefix means
    /// "compatible with 4.0.x" — only patch-level changes are accepted.
    /// </summary>
    public override Range SptVersion { get; init; } = new("~4.0.0");

    /// <inheritdoc />
    public override List<string>? Incompatibilities { get; init; } = null;

    /// <inheritdoc />
    public override Dictionary<string, Range>? ModDependencies { get; init; } = null;

    /// <summary>Hub page for the original QuestingBots mod.</summary>
    public override string? Url { get; init; } = "https://hub.sp-tarkov.com/files/file/1004-spt-questing-bots/";

    /// <inheritdoc />
    public override bool? IsBundleMod { get; init; } = false;

    /// <summary>SPDX license identifier.</summary>
    public override string License { get; init; } = "MIT";
}

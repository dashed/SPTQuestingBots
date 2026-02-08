#pragma warning disable CS0618 // ConfigServer is obsolete (SPT 4.2 migration pending)

using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Servers;
using SPTQuestingBots.Server.Configuration;
using SPTQuestingBots.Server.Services;

namespace SPTQuestingBots.Server;

/// <summary>
/// Main entry point for the QuestingBots server-side plugin.
///
/// <para>
/// Implements <see cref="IOnLoad"/> and runs after the database has loaded
/// (priority 100). This class performs the one-time setup that the original
/// <c>mod.ts</c> handled across its <c>postDBLoad</c> and <c>postSptLoad</c>
/// lifecycle hooks:
/// </para>
///
/// <list type="bullet">
///   <item>Validates the mod configuration (2D array formats for curves/distributions).</item>
///   <item>Removes blacklisted brain types from PMC and Player Scav pools.</item>
///   <item>Stores the original PScav conversion chance, then zeroes it so QuestingBots
///         can manage PScav spawning dynamically.</item>
///   <item>Adjusts bot hostility settings, removes PvE boss waves, disables
///         custom wave generators, and applies EFT bot caps.</item>
///   <item>Removes the artificial Rogue spawn delay on Lighthouse.</item>
/// </list>
///
/// <para>
/// HTTP routes are registered separately via
/// <see cref="Routers.QuestingBotsStaticRouter"/> and
/// <see cref="Routers.QuestingBotsDynamicRouter"/> — the SPT DI system discovers
/// and wires them automatically.
/// </para>
/// </summary>
[Injectable(InjectionType.Singleton, typePriority: 100)]
public class QuestingBotsServerPlugin(
    QuestingBotsConfigLoader configLoader,
    CommonUtils commonUtils,
    BotLocationService botLocationService,
    PMCConversionService pmcConversionService,
    ConfigServer configServer,
    IReadOnlyList<SptMod> loadedMods
) : IOnLoad
{
    /// <summary>
    /// Mod names whose presence should cause the QuestingBots spawning
    /// system to disable itself to avoid conflicts. These are other
    /// community mods that provide their own bot-spawning logic.
    /// </summary>
    private static readonly string[] SpawningModNames =
    [
        "SWAG",
        "DewardianDev-MOAR",
        "PreyToLive-BetterSpawnsPlus",
        "RealPlayerSpawn",
        "acidphantasm-botplacementsystem",
    ];

    /// <summary>
    /// Called once during server startup, after the database is loaded.
    /// Performs all one-time configuration and database mutations.
    /// </summary>
    /// <returns>A completed task (all work is synchronous).</returns>
    public Task OnLoad()
    {
        var config = configLoader.Config;

        // ── Early exit if the mod is disabled ──────────────────────────
        if (!config.Enabled)
        {
            commonUtils.LogInfo("Mod disabled in config.json", alwaysShow: true);
            return Task.CompletedTask;
        }

        // ── Validate 2D arrays in config ───────────────────────────────
        // Curves and distributions must have exactly 2 columns per row,
        // and some require integer values in the left column.
        if (!AreArraysValid())
        {
            config.Enabled = false;
            return Task.CompletedTask;
        }

        // ── Remove blacklisted brain types ─────────────────────────────
        // Prevents bots from receiving boss-like AI when converted to
        // PMCs or Player Scavs (e.g. bossKilla, bossTagilla).
        pmcConversionService.RemoveBlacklistedBrainTypes();

        // ── Store original PScav conversion chance ─────────────────────
        // QuestingBots manages PScav spawning dynamically via the
        // /QuestingBots/AdjustPScavChance/ endpoint, so we zero out
        // SPT's built-in chance and save the original for scaling.
        var botConfig = configServer.GetConfig<BotConfig>();
        config.BasePScavConversionChance = botConfig.ChanceAssaultScavHasPlayerScavName;

        if (config.AdjustPScavChance.Enabled || (config.BotSpawns.Enabled && config.BotSpawns.PlayerScavs.Enabled))
        {
            botConfig.ChanceAssaultScavHasPlayerScavName = 0;
        }

        // ── Disable spawning if a conflicting mod is loaded ─────────────
        if (ShouldDisableSpawningSystem())
        {
            config.BotSpawns.Enabled = false;
        }

        // ── Configure the spawning system ──────────────────────────────
        ConfigureSpawningSystem();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Applies all spawning-system database mutations when the spawning
    /// system is enabled. This includes:
    /// <list type="bullet">
    ///   <item>Overwriting BSG's bot hostility chances.</item>
    ///   <item>Removing PvE-only boss waves.</item>
    ///   <item>Disabling SPT's custom boss, scav, and PMC wave generators.</item>
    ///   <item>Replacing SPT bot caps with EFT's built-in values.</item>
    ///   <item>Removing the artificial Rogue spawn delay on Lighthouse.</item>
    /// </list>
    /// </summary>
    private void ConfigureSpawningSystem()
    {
        var config = configLoader.Config;

        if (!config.BotSpawns.Enabled)
        {
            return;
        }

        commonUtils.LogInfo("Configuring game for bot spawning...");

        // Overwrite BSG's chances of bots being friendly toward each other
        botLocationService.AdjustAllBotHostilityChances();

        // Remove all of BSG's PvE-only boss waves (PMC-named boss spawns)
        botLocationService.DisablePvEBossWaves();

        // Disable SPT's custom boss waves — currently all PMC waves, which
        // are unnecessary because this mod handles PMC spawning
        var locationConfig = configServer.GetConfig<LocationConfig>();
        if (locationConfig.CustomWaves != null)
        {
            botLocationService.DisableBotWaves(locationConfig.CustomWaves.Boss, "boss");

            // Disable the extra Scavs that spawn into Factory
            botLocationService.DisableNormalBotWaves(locationConfig.CustomWaves.Normal, "Scav");
        }

        // Disable SPT's PMC wave generator entirely
        var pmcConfig = configServer.GetConfig<PmcConfig>();
        botLocationService.DisableBotWaves(pmcConfig.CustomPmcWaves, "PMC");

        // Use EFT's bot caps instead of SPT's (optionally only when lower)
        botLocationService.UseEftBotCaps();

        // If Rogues don't spawn immediately on Lighthouse, PMC spawns will
        // be significantly delayed because they wait for initial boss spawns
        if (
            config.BotSpawns.LimitInitialBossSpawns.DisableRogueDelay
            && locationConfig.RogueLighthouseSpawnTimeSettings.WaitTimeSeconds > -1
        )
        {
            locationConfig.RogueLighthouseSpawnTimeSettings.WaitTimeSeconds = -1;
            commonUtils.LogInfo("Removed SPT Rogue spawn delay");
        }

        commonUtils.LogInfo("Configuring game for bot spawning...done.");
    }

    /// <summary>
    /// Checks whether another bot-spawning mod is loaded that would conflict
    /// with QuestingBots' spawning system. If a conflict is detected, the
    /// spawning system is disabled to avoid double-spawning or other issues.
    ///
    /// <para>
    /// <b>Ported from:</b> <c>mod.ts → shouldDisableSpawningSystem()</c>.
    /// </para>
    /// </summary>
    /// <returns>
    /// <c>true</c> if a conflicting spawning mod is loaded; <c>false</c> otherwise.
    /// </returns>
    private bool ShouldDisableSpawningSystem()
    {
        foreach (var mod in loadedMods)
        {
            var modName = mod.ModMetadata?.Name ?? "";
            if (SpawningModNames.Any(name => modName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                commonUtils.LogWarning(
                    $"Detected conflicting spawning mod '{modName}'. " + "QuestingBots spawning system has been disabled."
                );
                return true;
            }
        }

        return false;
    }

    // ────────────────────────────────────────────────────────────────────
    // Configuration validation (ported from mod.ts)
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates all 2D arrays in the config that represent curves or
    /// distributions. Each row must have exactly 2 columns, and some
    /// require integer values in the left column.
    /// </summary>
    /// <returns><c>true</c> if all arrays are valid; <c>false</c> disables the mod.</returns>
    private bool AreArraysValid()
    {
        var config = configLoader.Config;

        if (!IsChanceArrayValid(config.Questing.BotQuests.EftQuests.LevelRange, true))
        {
            commonUtils.LogError("questing.bot_quests.eft_quests.level_range has invalid data. Mod disabled.");
            return false;
        }

        if (!IsChanceArrayValid(config.BotSpawns.Pmcs.FractionOfMaxPlayersVsRaidET, false))
        {
            commonUtils.LogError("bot_spawns.pmcs.fraction_of_max_players_vs_raidET has invalid data. Mod disabled.");
            return false;
        }

        if (!IsChanceArrayValid(config.BotSpawns.Pmcs.BotsPerGroupDistribution, true))
        {
            commonUtils.LogError("bot_spawns.pmcs.bots_per_group_distribution has invalid data. Mod disabled.");
            return false;
        }

        if (!IsChanceArrayValid(config.BotSpawns.Pmcs.BotDifficultyAsOnline, true))
        {
            commonUtils.LogError("bot_spawns.pmcs.bot_difficulty_as_online has invalid data. Mod disabled.");
            return false;
        }

        if (!IsChanceArrayValid(config.BotSpawns.PlayerScavs.BotsPerGroupDistribution, true))
        {
            commonUtils.LogError("bot_spawns.player_scavs.bots_per_group_distribution has invalid data. Mod disabled.");
            return false;
        }

        if (!IsChanceArrayValid(config.BotSpawns.PlayerScavs.BotDifficultyAsOnline, true))
        {
            commonUtils.LogError("bot_spawns.player_scavs.bot_difficulty_as_online has invalid data. Mod disabled.");
            return false;
        }

        if (!IsChanceArrayValid(config.AdjustPScavChance.ChanceVsTimeRemainingFraction, false))
        {
            commonUtils.LogError("adjust_pscav_chance.chance_vs_time_remaining_fraction has invalid data. Mod disabled.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates a single 2D array used as a chance curve or distribution.
    /// </summary>
    /// <param name="array">The 2D array to validate. Must not be null or empty.</param>
    /// <param name="shouldLeftColumnBeIntegers">
    /// When <c>true</c>, the first element of each row must be a whole number.
    /// This is required for distributions where the left column represents
    /// discrete values (group sizes, difficulty levels, etc.).
    /// </param>
    /// <returns><c>true</c> if the array passes all checks.</returns>
    internal bool IsChanceArrayValid(double[][]? array, bool shouldLeftColumnBeIntegers)
    {
        if (array == null || array.Length == 0)
        {
            return false;
        }

        foreach (var row in array)
        {
            // Every row must be a [key, value] pair
            if (row.Length != 2)
            {
                return false;
            }

            // Verify integer constraint when required
            if (shouldLeftColumnBeIntegers && row[0] != Math.Floor(row[0]))
            {
                commonUtils.LogError(
                    "Found a chance array with an invalid value in its left column. "
                        + "Please ensure you are not using an outdated version of config.json."
                );
                return false;
            }
        }

        return true;
    }
}

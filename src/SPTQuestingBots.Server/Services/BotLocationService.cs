using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTQuestingBots.Server.Configuration;

namespace SPTQuestingBots.Server.Services;

/// <summary>
/// Manages bot hostility settings, population caps, and wave removal.
///
/// <para>
/// This service mutates the in-memory database and SPT configuration at
/// startup to enforce QuestingBots' spawning rules. It handles:
/// </para>
///
/// <list type="bullet">
///   <item>Adjusting PMC-vs-Scav and PMC-vs-PMC hostility chances across all maps.</item>
///   <item>Removing PvE-only boss waves (PMC-named entries in <c>BossLocationSpawn</c>).</item>
///   <item>Disabling SPT's custom bot waves (boss, normal, and PMC waves).</item>
///   <item>Replacing SPT's bot population caps with EFT's built-in values.</item>
/// </list>
///
/// <para>
/// <b>Ported from:</b> <c>src/BotLocationUtil.ts</c> (TypeScript / SPT 3.x).
/// </para>
/// </summary>
[Injectable(InjectionType.Singleton)]
public class BotLocationService
{
    /// <summary>The two PMC role identifiers used throughout EFT.</summary>
    private static readonly List<string> PmcRoles = ["pmcBEAR", "pmcUSEC"];

    private readonly CommonUtils _commonUtils;
    private readonly DatabaseService _databaseService;
    private readonly QuestingBotsConfigLoader _configLoader;
    private readonly ConfigServer _configServer;

    private BotConfig? _botConfig;
    private PmcConfig? _pmcConfig;
    private LocationConfig? _locationConfig;

    public BotLocationService(
        CommonUtils commonUtils,
        DatabaseService databaseService,
        QuestingBotsConfigLoader configLoader,
        ConfigServer configServer
    )
    {
        _commonUtils = commonUtils;
        _databaseService = databaseService;
        _configLoader = configLoader;
        _configServer = configServer;
    }

    /// <summary>Lazily resolved SPT bot config.</summary>
    private BotConfig BotConfig => _botConfig ??= _configServer.GetConfig<BotConfig>();

    /// <summary>Lazily resolved SPT PMC config.</summary>
    private PmcConfig PmcConfig => _pmcConfig ??= _configServer.GetConfig<PmcConfig>();

    /// <summary>Lazily resolved SPT location config.</summary>
    private LocationConfig LocationConfig => _locationConfig ??= _configServer.GetConfig<LocationConfig>();

    // ────────────────────────────────────────────────────────────────────
    // Hostility adjustments
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adjusts bot hostility settings across all locations and the SPT PMC config.
    ///
    /// <para>
    /// For each map, PMC <c>AdditionalHostilitySettings</c> are modified so that
    /// the configured enemy roles always have 100% enemy chance, and optionally
    /// forces PMCs to be always hostile against Scavs and other PMCs.
    /// </para>
    ///
    /// <para>
    /// Also sets the <c>ENEMY_BOT_TYPES</c> for assault, assaultgroup, and marksman
    /// bot types when <c>pmcs_always_hostile_against_scavs</c> is enabled.
    /// </para>
    /// </summary>
    public void AdjustAllBotHostilityChances()
    {
        var config = _configLoader.Config;
        if (!config.BotSpawns.PmcHostilityAdjustments.Enabled)
        {
            return;
        }

        _commonUtils.LogInfo("Adjusting bot hostility chances...");

        // Adjust per-location hostility settings
        var locations = _databaseService.GetLocations().GetDictionary();
        foreach (var location in locations.Values)
        {
            AdjustAllBotHostilityChancesForLocation(location);
        }

        // Adjust SPT's PMC config hostility for USEC and BEAR
        if (PmcConfig.HostilitySettings.TryGetValue("pmcusec", out var usecSettings))
        {
            AdjustSptPmcHostilityChances(usecSettings);
        }

        if (PmcConfig.HostilitySettings.TryGetValue("pmcbear", out var bearSettings))
        {
            AdjustSptPmcHostilityChances(bearSettings);
        }

        // Make Scav bot types treat PMCs as enemies
        if (config.BotSpawns.PmcHostilityAdjustments.PmcsAlwaysHostileAgainstScavs)
        {
            SetScavEnemyBotTypes("assault");
            SetScavEnemyBotTypes("assaultgroup");
            SetScavEnemyBotTypes("marksman");
        }

        _commonUtils.LogInfo("Adjusting bot hostility chances...done.");
    }

    /// <summary>
    /// Sets the <c>ENEMY_BOT_TYPES</c> in the Mind difficulty settings for a
    /// given Scav bot type so that PMCs are treated as enemies at all difficulties.
    /// </summary>
    /// <param name="botType">The bot type key (e.g. "assault", "marksman").</param>
    private void SetScavEnemyBotTypes(string botType)
    {
        var bots = _databaseService.GetBots();
        if (!bots.Types.TryGetValue(botType, out var bot) || bot == null)
        {
            return;
        }

        var pmcWildSpawnTypes = PmcRoles
            .Select(r => Enum.TryParse<WildSpawnType>(r, true, out var wst) ? wst : (WildSpawnType?)null)
            .Where(w => w.HasValue)
            .Select(w => w!.Value)
            .ToList();

        // Apply to every difficulty level (easy, normal, hard, impossible)
        foreach (var difficulty in bot.BotDifficulty.Values)
        {
            if (difficulty.Mind?.EnemyBotTypes != null)
            {
                difficulty.Mind.EnemyBotTypes = pmcWildSpawnTypes;
            }
        }
    }

    /// <summary>
    /// Adjusts hostility settings for a single map location.
    /// Only modifies settings for PMC bot roles.
    /// </summary>
    private void AdjustAllBotHostilityChancesForLocation(Location location)
    {
        if (location?.Base?.BotLocationModifier?.AdditionalHostilitySettings == null)
        {
            return;
        }

        foreach (var settings in location.Base.BotLocationModifier.AdditionalHostilitySettings)
        {
            // Only adjust PMC hostility settings, leave bosses etc. alone
            if (!PmcRoles.Contains(settings.BotRole))
            {
                continue;
            }

            AdjustBotHostilityChances(settings);
        }
    }

    /// <summary>
    /// Applies the mod's hostility rules to a single
    /// <see cref="AdditionalHostilitySettings"/> entry.
    /// </summary>
    private void AdjustBotHostilityChances(AdditionalHostilitySettings settings)
    {
        var config = _configLoader.Config;
        var hostilityConfig = config.BotSpawns.PmcHostilityAdjustments;

        // Adjust the global Scav enemy chance
        if (settings.SavageEnemyChance.HasValue)
        {
            settings.SavageEnemyChance = hostilityConfig.GlobalScavEnemyChance;
        }

        // Force PMCs to always be hostile toward Scavs
        if (hostilityConfig.PmcsAlwaysHostileAgainstScavs)
        {
            settings.SavagePlayerBehaviour = "AlwaysEnemies";
        }

        // Set enemy chances for specific roles
        if (settings.ChancedEnemies != null)
        {
            foreach (var chancedEnemy in settings.ChancedEnemies)
            {
                if (hostilityConfig.PmcEnemyRoles.Contains(chancedEnemy.Role))
                {
                    chancedEnemy.EnemyChance = 100;
                    continue;
                }

                // Zero out non-PMC enemy chances so the client plugin
                // can set boss hostilities dynamically when bots spawn
                chancedEnemy.EnemyChance = 0;
            }
        }

        // Force PMC-vs-PMC hostility
        if (hostilityConfig.PmcsAlwaysHostileAgainstPmcs)
        {
            settings.BearEnemyChance = 100;
            settings.UsecEnemyChance = 100;
            AddMissingPmcRolesToChancedEnemies(settings);
        }
    }

    /// <summary>
    /// Ensures both PMC roles (BEAR and USEC) exist in the
    /// <c>ChancedEnemies</c> list with 100% enemy chance.
    /// </summary>
    private void AddMissingPmcRolesToChancedEnemies(AdditionalHostilitySettings settings)
    {
        var hostilityConfig = _configLoader.Config.BotSpawns.PmcHostilityAdjustments;
        settings.ChancedEnemies ??= [];

        foreach (var pmcRole in PmcRoles)
        {
            if (!hostilityConfig.PmcEnemyRoles.Contains(pmcRole))
            {
                continue;
            }

            // Skip if the role is already present
            if (settings.ChancedEnemies.Any(ce => ce.Role == pmcRole))
            {
                continue;
            }

            settings.ChancedEnemies.Add(new ChancedEnemy { EnemyChance = 100, Role = pmcRole });
        }
    }

    /// <summary>
    /// Adjusts the SPT PMC config's hostility settings (separate from the
    /// per-location database entries).
    /// </summary>
    private void AdjustSptPmcHostilityChances(HostilitySettings settings)
    {
        var hostilityConfig = _configLoader.Config.BotSpawns.PmcHostilityAdjustments;

        settings.SavageEnemyChance = hostilityConfig.GlobalScavEnemyChance;

        if (hostilityConfig.PmcsAlwaysHostileAgainstScavs)
        {
            settings.SavagePlayerBehaviour = "AlwaysEnemies";
        }

        if (settings.ChancedEnemies != null)
        {
            foreach (var chancedEnemy in settings.ChancedEnemies)
            {
                if (hostilityConfig.PmcEnemyRoles.Contains(chancedEnemy.Role))
                {
                    chancedEnemy.EnemyChance = 100;
                }
            }
        }

        if (hostilityConfig.PmcsAlwaysHostileAgainstPmcs)
        {
            settings.BearEnemyChance = 100;
            settings.UsecEnemyChance = 100;
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // Wave management
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes all PvE-only boss waves from every location. PvE boss waves
    /// are identified by having a <c>BossName</c> that matches a PMC role
    /// (pmcBEAR or pmcUSEC).
    /// </summary>
    public void DisablePvEBossWaves()
    {
        var removedWaves = 0;
        var locations = _databaseService.GetLocations().GetDictionary();

        foreach (var location in locations.Values)
        {
            removedWaves += RemovePvEBossWavesFromLocation(location);
        }

        if (removedWaves > 0)
        {
            _commonUtils.LogInfo($"Disabled {removedWaves} PvE boss waves");
        }
    }

    /// <summary>
    /// Removes PMC-named boss waves from a single location.
    /// </summary>
    /// <returns>The number of waves removed.</returns>
    private int RemovePvEBossWavesFromLocation(Location location)
    {
        if (location?.Base?.BossLocationSpawn == null)
        {
            return 0;
        }

        var removedWaves = 0;
        var modifiedSpawns = new List<BossLocationSpawn>();

        foreach (var bossSpawn in location.Base.BossLocationSpawn)
        {
            if (PmcRoles.Contains(bossSpawn.BossName))
            {
                removedWaves++;
                continue;
            }

            modifiedSpawns.Add(bossSpawn);
        }

        location.Base.BossLocationSpawn = modifiedSpawns;
        return removedWaves;
    }

    /// <summary>
    /// Disables all custom boss waves by clearing the wave lists for every location.
    /// </summary>
    /// <param name="waves">Boss wave dictionary keyed by location name.</param>
    /// <param name="botType">Label for the log message (e.g. "boss", "PMC").</param>
    public void DisableBotWaves(Dictionary<string, List<BossLocationSpawn>>? waves, string botType)
    {
        if (waves == null)
        {
            return;
        }

        var originalWaves = 0;
        foreach (var location in waves.Keys.ToList())
        {
            originalWaves += waves[location].Count;
            waves[location] = [];
        }

        if (originalWaves > 0)
        {
            _commonUtils.LogInfo($"Disabled {originalWaves} custom {botType} waves");
        }
    }

    /// <summary>
    /// Disables all custom normal (non-boss) waves by clearing the wave lists.
    /// This is used for the extra Scav waves that spawn into Factory.
    /// </summary>
    /// <param name="waves">Normal wave dictionary keyed by location name.</param>
    /// <param name="botType">Label for the log message (e.g. "Scav").</param>
    public void DisableNormalBotWaves(Dictionary<string, List<Wave>>? waves, string botType)
    {
        if (waves == null)
        {
            return;
        }

        var originalWaves = 0;
        foreach (var location in waves.Keys.ToList())
        {
            originalWaves += waves[location].Count;
            waves[location] = [];
        }

        if (originalWaves > 0)
        {
            _commonUtils.LogInfo($"Disabled {originalWaves} custom {botType} waves");
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // Bot cap management
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces SPT's bot population caps with EFT's built-in values, then
    /// applies per-map fixed adjustments from the mod config.
    ///
    /// <para>
    /// When <c>only_decrease_bot_caps</c> is enabled, EFT caps are only
    /// applied when they are lower than SPT's existing caps. The fixed
    /// adjustment is always applied on top.
    /// </para>
    /// </summary>
    public void UseEftBotCaps()
    {
        var config = _configLoader.Config;
        var locations = _databaseService.GetLocations().GetDictionary();

        foreach (var locationKey in BotConfig.MaxBotCap.Keys.ToList())
        {
            if (!locations.TryGetValue(locationKey, out var location) || location?.Base == null)
            {
                continue;
            }

            var originalSptCap = BotConfig.MaxBotCap[locationKey];
            var eftCap = location.Base.BotMax;
            var shouldChangeBotCap = originalSptCap > eftCap || !config.BotSpawns.BotCapAdjustments.OnlyDecreaseBotCaps;

            // Apply EFT's cap if configured and appropriate
            if (config.BotSpawns.BotCapAdjustments.UseEftBotCaps && shouldChangeBotCap)
            {
                BotConfig.MaxBotCap[locationKey] = eftCap;
            }

            // Apply the fixed per-map adjustment (defaults to 0)
            var fixedAdjustment = config.BotSpawns.BotCapAdjustments.MapSpecificAdjustments.GetValueOrDefault(
                locationKey,
                config.BotSpawns.BotCapAdjustments.MapSpecificAdjustments.GetValueOrDefault("default", 0)
            );
            BotConfig.MaxBotCap[locationKey] += fixedAdjustment;

            var newCap = BotConfig.MaxBotCap[locationKey];

            if (newCap != originalSptCap)
            {
                _commonUtils.LogInfo(
                    $"Updated bot cap for {locationKey} to {newCap} "
                        + $"(Original SPT: {originalSptCap}, EFT: {eftCap}, "
                        + $"fixed adjustment: {fixedAdjustment})"
                );
            }
        }
    }
}

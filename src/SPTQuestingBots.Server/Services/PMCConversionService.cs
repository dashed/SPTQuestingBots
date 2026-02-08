using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTQuestingBots.Server.Configuration;

namespace SPTQuestingBots.Server.Services;

/// <summary>
/// Removes blacklisted brain types from PMC and Player Scav brain pools.
///
/// <para>
/// When SPT converts a regular bot into a PMC or Player Scav, it assigns
/// a random brain type from a weighted pool. Some brain types (e.g.
/// <c>bossKilla</c>, <c>bossTagilla</c>) cause undesirable AI behaviour
/// when assigned to PMCs. This service removes those entries at startup.
/// </para>
///
/// <para>
/// The blacklisted brain types are configured in <c>config.json</c> at
/// <c>bot_spawns.blacklisted_pmc_bot_brains</c>.
/// </para>
///
/// <para>
/// <b>Ported from:</b> <c>src/PMCConversionUtil.ts</c> (TypeScript / SPT 3.x).
/// </para>
/// </summary>
[Injectable(InjectionType.Singleton)]
public class PMCConversionService
{
    private readonly CommonUtils _commonUtils;
    private readonly QuestingBotsConfigLoader _configLoader;
    private readonly ConfigServer _configServer;

    private PmcConfig? _pmcConfig;
    private BotConfig? _botConfig;

    public PMCConversionService(CommonUtils commonUtils, QuestingBotsConfigLoader configLoader, ConfigServer configServer)
    {
        _commonUtils = commonUtils;
        _configLoader = configLoader;
        _configServer = configServer;
    }

    /// <summary>Lazily resolved SPT PMC config.</summary>
    private PmcConfig PmcConfig => _pmcConfig ??= _configServer.GetConfig<PmcConfig>();

    /// <summary>Lazily resolved SPT bot config.</summary>
    private BotConfig BotConfig => _botConfig ??= _configServer.GetConfig<BotConfig>();

    /// <summary>
    /// Iterates all PMC type entries (keyed by side, then map, then brain type)
    /// and all Player Scav brain entries (keyed by map, then brain type),
    /// removing any brain types that appear in the blacklist.
    ///
    /// <para>
    /// This mirrors the original TypeScript logic which used <c>delete mapBrains[badBrain]</c>
    /// to remove dictionary entries. In C# we use <see cref="Dictionary{TKey,TValue}.Remove"/>.
    /// </para>
    /// </summary>
    public void RemoveBlacklistedBrainTypes()
    {
        var badBrains = _configLoader.Config.BotSpawns.BlacklistedPmcBotBrains;
        var removedBrains = 0;

        // Remove from PMC brain types
        // Structure: PmcType[side][map][brainType] = weight
        foreach (var pmcType in PmcConfig.PmcType.Values)
        {
            foreach (var mapBrains in pmcType.Values)
            {
                foreach (var badBrain in badBrains)
                {
                    if (mapBrains.Remove(badBrain))
                    {
                        removedBrains++;
                    }
                }
            }
        }

        // Remove from Player Scav brain types
        // Structure: PlayerScavBrainType[map][brainType] = weight
        foreach (var mapBrains in BotConfig.PlayerScavBrainType.Values)
        {
            foreach (var badBrain in badBrains)
            {
                if (mapBrains.Remove(badBrain))
                {
                    removedBrains++;
                }
            }
        }

        _commonUtils.LogInfo($"Removed {removedBrains} blacklisted brain types from being used for PMC's and Player Scav's");
    }
}

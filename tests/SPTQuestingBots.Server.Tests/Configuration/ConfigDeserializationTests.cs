using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Server.Configuration;

namespace SPTQuestingBots.Server.Tests.Configuration;

[TestFixture]
public class ConfigDeserializationTests
{
    [Test]
    public void Deserialize_MinimalConfig_SetsEnabledTrue()
    {
        var json = """{ "enabled": true }""";
        var config = JsonConvert.DeserializeObject<QuestingBotsConfig>(json)!;
        Assert.That(config.Enabled, Is.True);
    }

    [Test]
    public void Deserialize_MinimalConfig_SetsEnabledFalse()
    {
        var json = """{ "enabled": false }""";
        var config = JsonConvert.DeserializeObject<QuestingBotsConfig>(json)!;
        Assert.That(config.Enabled, Is.False);
    }

    [Test]
    public void Deserialize_EmptyJson_DefaultsEnabledFalse()
    {
        var json = "{}";
        var config = JsonConvert.DeserializeObject<QuestingBotsConfig>(json)!;
        Assert.That(config.Enabled, Is.False);
    }

    [Test]
    public void RoundTrip_PreservesNestedValues()
    {
        var original = new QuestingBotsConfig
        {
            Enabled = true,
            MaxCalcTimePerFrameMs = 5,
            Debug = new DebugConfig
            {
                Enabled = true,
                AlwaysSpawnPmcs = true,
                ShowZoneOutlines = false,
            },
            ChanceOfBeingHostileTowardBosses = new BotTypeChanceConfig
            {
                Scav = 10,
                PScav = 20,
                Pmc = 50,
                Boss = 0,
            },
        };

        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<QuestingBotsConfig>(json)!;

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.Enabled, Is.True);
            Assert.That(deserialized.MaxCalcTimePerFrameMs, Is.EqualTo(5));
            Assert.That(deserialized.Debug.Enabled, Is.True);
            Assert.That(deserialized.Debug.AlwaysSpawnPmcs, Is.True);
            Assert.That(deserialized.Debug.ShowZoneOutlines, Is.False);
            Assert.That(deserialized.ChanceOfBeingHostileTowardBosses.Scav, Is.EqualTo(10));
            Assert.That(deserialized.ChanceOfBeingHostileTowardBosses.PScav, Is.EqualTo(20));
            Assert.That(deserialized.ChanceOfBeingHostileTowardBosses.Pmc, Is.EqualTo(50));
            Assert.That(deserialized.ChanceOfBeingHostileTowardBosses.Boss, Is.EqualTo(0));
        });
    }

    [Test]
    public void Deserialize_BotSpawnsSection_ParsesBlacklistedBrains()
    {
        var json = """
        {
            "bot_spawns": {
                "blacklisted_pmc_bot_brains": ["bossKilla", "bossTagilla", "followerGluharSnipe"]
            }
        }
        """;

        var config = JsonConvert.DeserializeObject<QuestingBotsConfig>(json)!;
        Assert.That(config.BotSpawns.BlacklistedPmcBotBrains,
            Is.EqualTo(new[] { "bossKilla", "bossTagilla", "followerGluharSnipe" }));
    }

    [Test]
    public void Deserialize_PmcSpawnConfig_Parses2DArrays()
    {
        var json = """
        {
            "bot_spawns": {
                "pmcs": {
                    "fraction_of_max_players_vs_raidET": [[0.0, 0.75], [0.5, 0.5], [1.0, 0.25]],
                    "bots_per_group_distribution": [[1, 80], [2, 15], [3, 5]],
                    "bot_difficulty_as_online": [[0, 40], [1, 40], [2, 15], [3, 5]]
                }
            }
        }
        """;

        var config = JsonConvert.DeserializeObject<QuestingBotsConfig>(json)!;
        var pmcs = config.BotSpawns.Pmcs;

        Assert.Multiple(() =>
        {
            Assert.That(pmcs.FractionOfMaxPlayersVsRaidET, Has.Length.EqualTo(3));
            Assert.That(pmcs.FractionOfMaxPlayersVsRaidET[0], Is.EqualTo(new[] { 0.0, 0.75 }));
            Assert.That(pmcs.BotsPerGroupDistribution, Has.Length.EqualTo(3));
            Assert.That(pmcs.BotsPerGroupDistribution[0], Is.EqualTo(new double[] { 1, 80 }));
            Assert.That(pmcs.BotDifficultyAsOnline, Has.Length.EqualTo(4));
        });
    }

    [Test]
    public void Deserialize_HostilityAdjustments_ParsesConfig()
    {
        var json = """
        {
            "bot_spawns": {
                "pmc_hostility_adjustments": {
                    "enabled": true,
                    "pmcs_always_hostile_against_pmcs": true,
                    "pmcs_always_hostile_against_scavs": false,
                    "global_scav_enemy_chance": 50,
                    "pmc_enemy_roles": ["pmcBEAR", "pmcUSEC"]
                }
            }
        }
        """;

        var config = JsonConvert.DeserializeObject<QuestingBotsConfig>(json)!;
        var hostility = config.BotSpawns.PmcHostilityAdjustments;

        Assert.Multiple(() =>
        {
            Assert.That(hostility.Enabled, Is.True);
            Assert.That(hostility.PmcsAlwaysHostileAgainstPmcs, Is.True);
            Assert.That(hostility.PmcsAlwaysHostileAgainstScavs, Is.False);
            Assert.That(hostility.GlobalScavEnemyChance, Is.EqualTo(50));
            Assert.That(hostility.PmcEnemyRoles, Is.EqualTo(new[] { "pmcBEAR", "pmcUSEC" }));
        });
    }

    [Test]
    public void Deserialize_AdjustPScavChance_ParsesCurve()
    {
        var json = """
        {
            "adjust_pscav_chance": {
                "enabled": true,
                "chance_vs_time_remaining_fraction": [[0.0, 100], [0.5, 50], [1.0, 0]]
            }
        }
        """;

        var config = JsonConvert.DeserializeObject<QuestingBotsConfig>(json)!;

        Assert.Multiple(() =>
        {
            Assert.That(config.AdjustPScavChance.Enabled, Is.True);
            Assert.That(config.AdjustPScavChance.ChanceVsTimeRemainingFraction, Has.Length.EqualTo(3));
            Assert.That(config.AdjustPScavChance.ChanceVsTimeRemainingFraction[2],
                Is.EqualTo(new double[] { 1.0, 0 }));
        });
    }

    [Test]
    public void BasePScavConversionChance_IsIgnoredDuringSerialization()
    {
        var config = new QuestingBotsConfig { BasePScavConversionChance = 42 };
        var json = JsonConvert.SerializeObject(config);

        Assert.That(json, Does.Not.Contain("BasePScavConversionChance"));
        Assert.That(json, Does.Not.Contain("42"));
    }

    [Test]
    public void Deserialize_BotCapAdjustments_ParsesMapSpecificAdjustments()
    {
        var json = """
        {
            "bot_spawns": {
                "bot_cap_adjustments": {
                    "use_EFT_bot_caps": true,
                    "only_decrease_bot_caps": false,
                    "map_specific_adjustments": {
                        "default": 0,
                        "factory4_day": 3,
                        "bigmap": -2
                    }
                }
            }
        }
        """;

        var config = JsonConvert.DeserializeObject<QuestingBotsConfig>(json)!;
        var caps = config.BotSpawns.BotCapAdjustments;

        Assert.Multiple(() =>
        {
            Assert.That(caps.UseEftBotCaps, Is.True);
            Assert.That(caps.OnlyDecreaseBotCaps, Is.False);
            Assert.That(caps.MapSpecificAdjustments["default"], Is.EqualTo(0));
            Assert.That(caps.MapSpecificAdjustments["factory4_day"], Is.EqualTo(3));
            Assert.That(caps.MapSpecificAdjustments["bigmap"], Is.EqualTo(-2));
        });
    }

    [Test]
    public void Deserialize_QuestingSection_ParsesEftQuestLevelRange()
    {
        var json = """
        {
            "questing": {
                "bot_quests": {
                    "eft_quests": {
                        "desirability": 50,
                        "max_bots_per_quest": 2,
                        "level_range": [[10, 5], [20, 10], [40, 20]]
                    }
                }
            }
        }
        """;

        var config = JsonConvert.DeserializeObject<QuestingBotsConfig>(json)!;
        var eftQuests = config.Questing.BotQuests.EftQuests;

        Assert.Multiple(() =>
        {
            Assert.That(eftQuests.Desirability, Is.EqualTo(50));
            Assert.That(eftQuests.MaxBotsPerQuest, Is.EqualTo(2));
            Assert.That(eftQuests.LevelRange, Has.Length.EqualTo(3));
            Assert.That(eftQuests.LevelRange[1], Is.EqualTo(new double[] { 20, 10 }));
        });
    }

    [Test]
    public void Deserialize_RealConfigJson_DeserializesWithoutErrors()
    {
        // Find the config.json relative to the test assembly location
        var testDir = TestContext.CurrentContext.TestDirectory;
        var configPath = Path.GetFullPath(
            Path.Combine(testDir, "..", "..", "..", "..", "..", "config", "config.json"));

        if (!File.Exists(configPath))
        {
            Assert.Ignore($"config/config.json not found at {configPath}. Skipping real config test.");
            return;
        }

        var json = File.ReadAllText(configPath);
        var config = JsonConvert.DeserializeObject<QuestingBotsConfig>(json);

        Assert.That(config, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(config!.Enabled, Is.True);
            Assert.That(config.Questing.Enabled, Is.True);
            Assert.That(config.BotSpawns.BlacklistedPmcBotBrains, Is.Not.Empty);
            Assert.That(config.BotSpawns.Pmcs.FractionOfMaxPlayersVsRaidET, Is.Not.Empty);
            Assert.That(config.BotSpawns.Pmcs.BotsPerGroupDistribution, Is.Not.Empty);
            Assert.That(config.AdjustPScavChance.ChanceVsTimeRemainingFraction, Is.Not.Empty);
        });
    }
}

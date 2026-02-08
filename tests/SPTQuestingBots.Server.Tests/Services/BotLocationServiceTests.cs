using System.Reflection;
using NSubstitute;
using NUnit.Framework;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTQuestingBots.Server.Configuration;
using SPTQuestingBots.Server.Services;

namespace SPTQuestingBots.Server.Tests.Services;

[TestFixture]
public class BotLocationServiceTests
{
    private CommonUtils _commonUtils = null!;
    private QuestingBotsConfigLoader _configLoader = null!;

    [SetUp]
    public void SetUp()
    {
        var logger = Substitute.For<ISptLogger<CommonUtils>>();
        _configLoader = CreateConfigLoader(new QuestingBotsConfig
        {
            Enabled = true,
            BotSpawns = new BotSpawnsConfig
            {
                Enabled = true,
                PmcHostilityAdjustments = new PmcHostilityAdjustmentsConfig
                {
                    Enabled = true,
                    PmcsAlwaysHostileAgainstPmcs = true,
                    PmcsAlwaysHostileAgainstScavs = true,
                    GlobalScavEnemyChance = 75,
                    PmcEnemyRoles = ["pmcBEAR", "pmcUSEC"],
                },
                BotCapAdjustments = new BotCapAdjustmentsConfig
                {
                    UseEftBotCaps = true,
                    OnlyDecreaseBotCaps = false,
                    MapSpecificAdjustments = new Dictionary<string, int>
                    {
                        ["default"] = 0,
                        ["factory4_day"] = 3,
                    },
                },
            },
        });
        _commonUtils = new CommonUtils(logger, null!, null!, _configLoader);
    }

    // ── DisableBotWaves ──────────────────────────────────────────────

    [Test]
    public void DisableBotWaves_ClearsAllWaveLists()
    {
        var waves = new Dictionary<string, List<BossLocationSpawn>>
        {
            ["factory4_day"] = [new BossLocationSpawn(), new BossLocationSpawn()],
            ["bigmap"] = [new BossLocationSpawn()],
        };

        var service = CreateService();
        service.DisableBotWaves(waves, "boss");

        Assert.Multiple(() =>
        {
            Assert.That(waves["factory4_day"], Is.Empty);
            Assert.That(waves["bigmap"], Is.Empty);
        });
    }

    [Test]
    public void DisableBotWaves_NullWaves_DoesNotThrow()
    {
        var service = CreateService();
        Assert.DoesNotThrow(() => service.DisableBotWaves(null, "boss"));
    }

    [Test]
    public void DisableBotWaves_EmptyDictionary_DoesNotThrow()
    {
        var waves = new Dictionary<string, List<BossLocationSpawn>>();
        var service = CreateService();
        Assert.DoesNotThrow(() => service.DisableBotWaves(waves, "boss"));
    }

    [Test]
    public void DisableBotWaves_AlreadyEmptyLists_StaysEmpty()
    {
        var waves = new Dictionary<string, List<BossLocationSpawn>>
        {
            ["factory4_day"] = [],
        };

        var service = CreateService();
        service.DisableBotWaves(waves, "boss");

        Assert.That(waves["factory4_day"], Is.Empty);
    }

    // ── DisableNormalBotWaves ─────────────────────────────────────────

    [Test]
    public void DisableNormalBotWaves_ClearsAllWaveLists()
    {
        var waves = new Dictionary<string, List<Wave>>
        {
            ["factory4_day"] = [new Wave(), new Wave(), new Wave()],
            ["bigmap"] = [new Wave()],
        };

        var service = CreateService();
        service.DisableNormalBotWaves(waves, "Scav");

        Assert.Multiple(() =>
        {
            Assert.That(waves["factory4_day"], Is.Empty);
            Assert.That(waves["bigmap"], Is.Empty);
        });
    }

    [Test]
    public void DisableNormalBotWaves_NullWaves_DoesNotThrow()
    {
        var service = CreateService();
        Assert.DoesNotThrow(() => service.DisableNormalBotWaves(null, "Scav"));
    }

    // ── Hostility adjustment (private method tested via location data) ──

    [Test]
    public void AdjustBotHostilityChances_SetsPmcEnemyRolesToMaxChance()
    {
        var settings = new AdditionalHostilitySettings
        {
            BotRole = "pmcUSEC",
            SavageEnemyChance = 50,
            ChancedEnemies =
            [
                new ChancedEnemy { Role = "pmcBEAR", EnemyChance = 30 },
                new ChancedEnemy { Role = "pmcUSEC", EnemyChance = 20 },
                new ChancedEnemy { Role = "bossKilla", EnemyChance = 80 },
            ],
        };

        // Use reflection to invoke the private method
        var service = CreateService();
        InvokePrivate(service, "AdjustBotHostilityChances", settings);

        Assert.Multiple(() =>
        {
            // PMC roles should be set to 100
            Assert.That(settings.ChancedEnemies[0].EnemyChance, Is.EqualTo(100),
                "pmcBEAR should be 100% enemy");
            Assert.That(settings.ChancedEnemies[1].EnemyChance, Is.EqualTo(100),
                "pmcUSEC should be 100% enemy");

            // Non-PMC roles should be zeroed out
            Assert.That(settings.ChancedEnemies[2].EnemyChance, Is.EqualTo(0),
                "bossKilla should be zeroed out");

            // SavageEnemyChance updated to config value
            Assert.That(settings.SavageEnemyChance, Is.EqualTo(75));

            // PMC-vs-PMC hostility forced
            Assert.That(settings.BearEnemyChance, Is.EqualTo(100));
            Assert.That(settings.UsecEnemyChance, Is.EqualTo(100));

            // SavagePlayerBehaviour set to AlwaysEnemies
            Assert.That(settings.SavagePlayerBehaviour, Is.EqualTo("AlwaysEnemies"));
        });
    }

    [Test]
    public void AdjustBotHostilityChances_AddsMissingPmcRoles()
    {
        var settings = new AdditionalHostilitySettings
        {
            BotRole = "pmcBEAR",
            SavageEnemyChance = 50,
            ChancedEnemies =
            [
                new ChancedEnemy { Role = "pmcBEAR", EnemyChance = 30 },
                // pmcUSEC is missing
            ],
        };

        var service = CreateService();
        InvokePrivate(service, "AdjustBotHostilityChances", settings);

        Assert.That(settings.ChancedEnemies, Has.Count.EqualTo(2));
        Assert.That(settings.ChancedEnemies.Any(ce => ce.Role == "pmcUSEC" && ce.EnemyChance == 100),
            Is.True, "pmcUSEC should be added with 100% enemy chance");
    }

    [Test]
    public void AdjustBotHostilityChances_NullChancedEnemies_CreatesListAndAddsPmcRoles()
    {
        var settings = new AdditionalHostilitySettings
        {
            BotRole = "pmcUSEC",
            SavageEnemyChance = 50,
            ChancedEnemies = null,
        };

        var service = CreateService();
        InvokePrivate(service, "AdjustBotHostilityChances", settings);

        // Should create the list and add both PMC roles
        Assert.That(settings.ChancedEnemies, Is.Not.Null);
        Assert.That(settings.ChancedEnemies, Has.Count.EqualTo(2));
    }

    [Test]
    public void AdjustBotHostilityChances_NoSavageEnemyChance_DoesNotSetIt()
    {
        // Config has PmcsAlwaysHostileAgainstScavs = true but SavageEnemyChance is null
        var settings = new AdditionalHostilitySettings
        {
            BotRole = "pmcUSEC",
            SavageEnemyChance = null,
            ChancedEnemies = [],
        };

        var service = CreateService();
        InvokePrivate(service, "AdjustBotHostilityChances", settings);

        // SavageEnemyChance should stay null (only set when HasValue)
        Assert.That(settings.SavageEnemyChance, Is.Null);
    }

    [Test]
    public void AdjustBotHostilityChances_HostilityDisabled_NoChanges()
    {
        // Create service with hostility disabled
        var config = _configLoader.Config;
        config.BotSpawns.PmcHostilityAdjustments.PmcsAlwaysHostileAgainstPmcs = false;
        config.BotSpawns.PmcHostilityAdjustments.PmcsAlwaysHostileAgainstScavs = false;
        config.BotSpawns.PmcHostilityAdjustments.PmcEnemyRoles = [];

        var settings = new AdditionalHostilitySettings
        {
            BotRole = "pmcUSEC",
            SavageEnemyChance = 50,
            ChancedEnemies =
            [
                new ChancedEnemy { Role = "bossKilla", EnemyChance = 80 },
            ],
        };

        var service = CreateService();
        InvokePrivate(service, "AdjustBotHostilityChances", settings);

        Assert.Multiple(() =>
        {
            // SavageEnemyChance is updated to GlobalScavEnemyChance even when other settings are disabled
            Assert.That(settings.SavageEnemyChance, Is.EqualTo(75));

            // Non-PMC role zeroed out (since not in PmcEnemyRoles)
            Assert.That(settings.ChancedEnemies[0].EnemyChance, Is.EqualTo(0));

            // Bear/Usec chances not forced to 100
            Assert.That(settings.BearEnemyChance, Is.Null);
            Assert.That(settings.UsecEnemyChance, Is.Null);

            // SavagePlayerBehaviour not changed (PmcsAlwaysHostileAgainstScavs = false)
            Assert.That(settings.SavagePlayerBehaviour, Is.Null);
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private BotLocationService CreateService()
    {
        return new BotLocationService(
            _commonUtils,
            null!, // DatabaseService only needed for methods we test via reflection
            _configLoader,
            null!); // ConfigServer only needed for lazy-loaded configs
    }

    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        method!.Invoke(target, args);
    }

    private static QuestingBotsConfigLoader CreateConfigLoader(QuestingBotsConfig config)
    {
        var loader = new QuestingBotsConfigLoader(
            Substitute.For<ISptLogger<QuestingBotsConfigLoader>>());
        SetPrivateField(loader, "_config", config);
        return loader;
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(target, value);
    }
}

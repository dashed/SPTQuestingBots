using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NSubstitute;
using NUnit.Framework;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTQuestingBots.Server.Configuration;
using SPTQuestingBots.Server.Services;

namespace SPTQuestingBots.Server.Tests.Services;

[TestFixture]
public class PMCConversionServiceTests
{
    private CommonUtils _commonUtils = null!;
    private QuestingBotsConfigLoader _configLoader = null!;

    [SetUp]
    public void SetUp()
    {
        var logger = Substitute.For<ISptLogger<CommonUtils>>();
        _configLoader = CreateConfigLoader(
            new QuestingBotsConfig
            {
                Enabled = true,
                BotSpawns = new BotSpawnsConfig { BlacklistedPmcBotBrains = ["bossKilla", "bossTagilla", "followerGluharSnipe"] },
            }
        );
        _commonUtils = new CommonUtils(logger, null!, null!, _configLoader);
    }

    [Test]
    public void RemoveBlacklistedBrainTypes_RemovesFromPmcType()
    {
        // Arrange: PmcType structure is Dict<side, Dict<map, Dict<brain, weight>>>
        var pmcType = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>
        {
            ["usec"] = new()
            {
                ["factory4_day"] = new()
                {
                    ["bossKilla"] = 1,
                    ["assault"] = 5,
                    ["followerGluharSnipe"] = 2,
                    ["pmcBot"] = 10,
                },
            },
            ["bear"] = new()
            {
                ["bigmap"] = new() { ["bossTagilla"] = 3, ["assault"] = 8 },
            },
        };

        // PlayerScavBrainType: Dict<map, Dict<brain, weight>>
        var playerScavBrains = new Dictionary<string, Dictionary<string, int>>
        {
            ["factory4_day"] = new() { ["bossKilla"] = 1, ["assault"] = 10 },
            ["bigmap"] = new() { ["assault"] = 10, ["bossTagilla"] = 2 },
        };

        var service = CreateServiceWithConfigs(pmcType, playerScavBrains);

        // Act
        service.RemoveBlacklistedBrainTypes();

        // Assert
        Assert.Multiple(() =>
        {
            // PMC brains: bossKilla and followerGluharSnipe removed from usec/factory4_day
            Assert.That(pmcType["usec"]["factory4_day"], Does.Not.ContainKey("bossKilla"));
            Assert.That(pmcType["usec"]["factory4_day"], Does.Not.ContainKey("followerGluharSnipe"));
            Assert.That(pmcType["usec"]["factory4_day"], Does.ContainKey("assault"));
            Assert.That(pmcType["usec"]["factory4_day"], Does.ContainKey("pmcBot"));

            // bossTagilla removed from bear/bigmap
            Assert.That(pmcType["bear"]["bigmap"], Does.Not.ContainKey("bossTagilla"));
            Assert.That(pmcType["bear"]["bigmap"], Does.ContainKey("assault"));

            // Player Scav brains: bossKilla removed from factory4_day
            Assert.That(playerScavBrains["factory4_day"], Does.Not.ContainKey("bossKilla"));
            Assert.That(playerScavBrains["factory4_day"], Does.ContainKey("assault"));

            // bossTagilla removed from bigmap
            Assert.That(playerScavBrains["bigmap"], Does.Not.ContainKey("bossTagilla"));
            Assert.That(playerScavBrains["bigmap"], Does.ContainKey("assault"));
        });
    }

    [Test]
    public void RemoveBlacklistedBrainTypes_NoBrainMatchesBlacklist_NoChanges()
    {
        var pmcType = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>
        {
            ["usec"] = new()
            {
                ["factory4_day"] = new() { ["assault"] = 5, ["pmcBot"] = 10 },
            },
        };

        var playerScavBrains = new Dictionary<string, Dictionary<string, int>> { ["factory4_day"] = new() { ["assault"] = 10 } };

        var service = CreateServiceWithConfigs(pmcType, playerScavBrains);
        service.RemoveBlacklistedBrainTypes();

        Assert.Multiple(() =>
        {
            Assert.That(pmcType["usec"]["factory4_day"].Count, Is.EqualTo(2));
            Assert.That(playerScavBrains["factory4_day"].Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void RemoveBlacklistedBrainTypes_EmptyBlacklist_NoChanges()
    {
        // Override config with empty blacklist
        _configLoader = CreateConfigLoader(
            new QuestingBotsConfig
            {
                Enabled = true,
                BotSpawns = new BotSpawnsConfig { BlacklistedPmcBotBrains = [] },
            }
        );
        _commonUtils = new CommonUtils(Substitute.For<ISptLogger<CommonUtils>>(), null!, null!, _configLoader);

        var pmcType = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>
        {
            ["usec"] = new()
            {
                ["factory4_day"] = new() { ["bossKilla"] = 1, ["assault"] = 5 },
            },
        };

        var playerScavBrains = new Dictionary<string, Dictionary<string, int>> { ["factory4_day"] = new() { ["bossKilla"] = 1 } };

        var service = CreateServiceWithConfigs(pmcType, playerScavBrains);
        service.RemoveBlacklistedBrainTypes();

        Assert.Multiple(() =>
        {
            Assert.That(pmcType["usec"]["factory4_day"], Does.ContainKey("bossKilla"));
            Assert.That(playerScavBrains["factory4_day"], Does.ContainKey("bossKilla"));
        });
    }

    [Test]
    public void RemoveBlacklistedBrainTypes_EmptyBrainPools_DoesNotThrow()
    {
        var pmcType = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();
        var playerScavBrains = new Dictionary<string, Dictionary<string, int>>();

        var service = CreateServiceWithConfigs(pmcType, playerScavBrains);
        Assert.DoesNotThrow(() => service.RemoveBlacklistedBrainTypes());
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private PMCConversionService CreateServiceWithConfigs(
        Dictionary<string, Dictionary<string, Dictionary<string, double>>> pmcType,
        Dictionary<string, Dictionary<string, int>> playerScavBrains
    )
    {
        var service = new PMCConversionService(_commonUtils, _configLoader, null!); // ConfigServer not used when we inject configs via reflection

        // Create uninitialized SPT config records and set only the properties we need.
        // This bypasses 'required' property validation while letting us test our logic.
        var pmcConfig = (PmcConfig)RuntimeHelpers.GetUninitializedObject(typeof(PmcConfig));
        pmcConfig.PmcType = pmcType;

        var botConfig = (BotConfig)RuntimeHelpers.GetUninitializedObject(typeof(BotConfig));
        botConfig.PlayerScavBrainType = playerScavBrains;

        SetPrivateField(service, "_pmcConfig", pmcConfig);
        SetPrivateField(service, "_botConfig", botConfig);

        return service;
    }

    private static QuestingBotsConfigLoader CreateConfigLoader(QuestingBotsConfig config)
    {
        var loader = new QuestingBotsConfigLoader(Substitute.For<ISptLogger<QuestingBotsConfigLoader>>());
        SetPrivateField(loader, "_config", config);
        return loader;
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(target, value);
    }
}

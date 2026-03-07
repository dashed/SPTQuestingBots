using System;
using System.Reflection;
using NSubstitute;
using NUnit.Framework;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTQuestingBots.Server.Configuration;
using SPTQuestingBots.Server.Services;

namespace SPTQuestingBots.Server.Tests.Configuration;

[TestFixture]
public class ConfigSemanticValidationFlowTests
{
    [Test]
    public void AreArraysValid_WithSemanticallyValidConfig_ReturnsTrue()
    {
        Assert.That(InvokeAreArraysValid(CreateValidConfig()), Is.True);
    }

    [Test]
    public void AreArraysValid_WithUnsortedPmcFractionCurve_ReturnsFalse()
    {
        var config = CreateValidConfig();
        config.BotSpawns.Pmcs.FractionOfMaxPlayersVsRaidET =
        [
            [0.75, 0.5],
            [0.50, 0.35],
        ];

        Assert.That(InvokeAreArraysValid(config), Is.False);
    }

    [Test]
    public void AreArraysValid_WithZeroWeightPlayerScavDistribution_ReturnsFalse()
    {
        var config = CreateValidConfig();
        config.BotSpawns.PlayerScavs.BotsPerGroupDistribution =
        [
            [1, 0],
            [2, 0],
        ];

        Assert.That(InvokeAreArraysValid(config), Is.False);
    }

    [Test]
    public void AreArraysValid_WithOutOfRangePScavChanceCurve_ReturnsFalse()
    {
        var config = CreateValidConfig();
        config.AdjustPScavChance.ChanceVsTimeRemainingFraction =
        [
            [0.0, 100],
            [1.0, 125],
        ];

        Assert.That(InvokeAreArraysValid(config), Is.False);
    }

    private static bool InvokeAreArraysValid(QuestingBotsConfig config)
    {
        var configLoader = CreateConfigLoader(config);
        var logger = Substitute.For<ISptLogger<CommonUtils>>();
        var commonUtils = new CommonUtils(logger, null!, null!, configLoader);
        var plugin = new QuestingBotsServerPlugin(configLoader, commonUtils, null!, null!, null!, Array.Empty<SptMod>());

        MethodInfo method = typeof(QuestingBotsServerPlugin).GetMethod("AreArraysValid", BindingFlags.NonPublic | BindingFlags.Instance)!;

        return (bool)method.Invoke(plugin, null)!;
    }

    private static QuestingBotsConfig CreateValidConfig()
    {
        return new QuestingBotsConfig
        {
            Enabled = true,
            Questing =
            {
                BotQuests =
                {
                    EftQuests =
                    {
                        LevelRange =
                        [
                            [0, 1],
                            [15, 10],
                            [30, 15],
                        ],
                    },
                },
            },
            BotSpawns =
            {
                Pmcs =
                {
                    FractionOfMaxPlayersVsRaidET =
                    [
                        [0.0, 0.65],
                        [0.5, 0.45],
                        [1.0, 0.25],
                    ],
                    BotsPerGroupDistribution =
                    [
                        [1, 70],
                        [2, 25],
                        [3, 5],
                    ],
                    BotDifficultyAsOnline =
                    [
                        [0, 40],
                        [1, 40],
                        [2, 15],
                        [3, 5],
                    ],
                },
                PlayerScavs =
                {
                    BotsPerGroupDistribution =
                    [
                        [1, 85],
                        [2, 10],
                        [3, 5],
                    ],
                    BotDifficultyAsOnline =
                    [
                        [0, 60],
                        [1, 40],
                    ],
                },
            },
            AdjustPScavChance =
            {
                ChanceVsTimeRemainingFraction =
                [
                    [0.0, 100],
                    [0.5, 50],
                    [1.0, 0],
                ],
            },
        };
    }

    private static QuestingBotsConfigLoader CreateConfigLoader(QuestingBotsConfig config)
    {
        var loader = new QuestingBotsConfigLoader(Substitute.For<ISptLogger<QuestingBotsConfigLoader>>());
        var field = typeof(QuestingBotsConfigLoader).GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(loader, config);
        return loader;
    }
}

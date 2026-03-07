using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTQuestingBots.Server.Configuration;
using SPTQuestingBots.Server.Services;

namespace SPTQuestingBots.Server.Tests.E2E;

[TestFixture]
public class QuestingBotsServerStartupE2ETests
{
    [Test]
    public async Task OnLoad_WithNullChanceCurveRow_DisablesMod()
    {
        var config = CreateValidConfig();
        config.AdjustPScavChance.ChanceVsTimeRemainingFraction =
        [
            [0.0, 50.0],
            null!,
        ];

        var logger = Substitute.For<ISptLogger<CommonUtils>>();
        var configLoader = CreateConfigLoader(config);
        var commonUtils = new CommonUtils(logger, null!, null!, configLoader);
        var plugin = new QuestingBotsServerPlugin(configLoader, commonUtils, null!, null!, null!, Array.Empty<SptMod>());

        await plugin.OnLoad();

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.False);
            Assert.That(
                logger.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(ISptLogger<CommonUtils>.Error)),
                Is.True,
                "Expected invalid startup config to be logged as an error."
            );
        });
    }

    [Test]
    public async Task OnLoad_WithRealConfig_ZeroesBuiltInPScavChanceAndRemovesBlacklistedBrains()
    {
        var configPath = Path.Combine(GetRepoRoot(), "config", "config.json");
        if (!File.Exists(configPath))
        {
            Assert.Ignore($"config/config.json not found at {configPath}. Skipping startup E2E test.");
            return;
        }

        var config = JsonConvert.DeserializeObject<QuestingBotsConfig>(File.ReadAllText(configPath));
        Assert.That(config, Is.Not.Null, "Real config should deserialize before startup runs.");

        var blacklistedBrain = config!.BotSpawns.BlacklistedPmcBotBrains.FirstOrDefault();
        Assert.That(blacklistedBrain, Is.Not.Null.And.Not.Empty, "Real config should include at least one blacklisted brain type.");

        var botConfig = (BotConfig)RuntimeHelpers.GetUninitializedObject(typeof(BotConfig));
        botConfig.ChanceAssaultScavHasPlayerScavName = 37;
        botConfig.PlayerScavBrainType = new Dictionary<string, Dictionary<string, int>>
        {
            ["factory4_day"] = new() { [blacklistedBrain!] = 1, ["assault"] = 10 },
        };

        var pmcConfig = (PmcConfig)RuntimeHelpers.GetUninitializedObject(typeof(PmcConfig));
        pmcConfig.PmcType = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>
        {
            ["usec"] = new()
            {
                ["factory4_day"] = new() { [blacklistedBrain!] = 1, ["assault"] = 10 },
            },
        };

        var logger = Substitute.For<ISptLogger<CommonUtils>>();
        var configLoader = CreateConfigLoader(config);
        var commonUtils = new CommonUtils(logger, null!, null!, configLoader);
        var configServer = TestConfigServer.Create(botConfig);
        var pmcConversionService = new PMCConversionService(commonUtils, configLoader, configServer);
        SetPrivateField(pmcConversionService, "_pmcConfig", pmcConfig);
        SetPrivateField(pmcConversionService, "_botConfig", botConfig);

        var plugin = new QuestingBotsServerPlugin(
            configLoader,
            commonUtils,
            null!,
            pmcConversionService,
            configServer,
            Array.Empty<SptMod>()
        );

        await plugin.OnLoad();

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.True);
            Assert.That(botConfig.ChanceAssaultScavHasPlayerScavName, Is.Zero);
            Assert.That(pmcConfig.PmcType["usec"]["factory4_day"], Does.Not.ContainKey(blacklistedBrain!));
            Assert.That(botConfig.PlayerScavBrainType["factory4_day"], Does.Not.ContainKey(blacklistedBrain!));
        });
    }

    [Test]
    public async Task OnLoad_WithKnownRouteShadowingMod_DisablesSpawningAndLogsOrderSensitiveWarning()
    {
        var config = CreateValidConfig();
        config.BotSpawns.Enabled = true;

        var botConfig = (BotConfig)RuntimeHelpers.GetUninitializedObject(typeof(BotConfig));
        botConfig.ChanceAssaultScavHasPlayerScavName = 21;
        botConfig.PlayerScavBrainType = new Dictionary<string, Dictionary<string, int>>();

        var pmcConfig = (PmcConfig)RuntimeHelpers.GetUninitializedObject(typeof(PmcConfig));
        pmcConfig.PmcType = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();

        var logger = Substitute.For<ISptLogger<CommonUtils>>();
        var configLoader = CreateConfigLoader(config);
        var commonUtils = new CommonUtils(logger, null!, null!, configLoader);
        var configServer = TestConfigServer.Create(botConfig);
        var pmcConversionService = new PMCConversionService(commonUtils, configLoader, configServer);
        SetPrivateField(pmcConversionService, "_pmcConfig", pmcConfig);
        SetPrivateField(pmcConversionService, "_botConfig", botConfig);

        var plugin = new QuestingBotsServerPlugin(
            configLoader,
            commonUtils,
            null!,
            pmcConversionService,
            configServer,
            [
                new SptMod
                {
                    Directory = "/mods/SWAG+Donuts",
                    Assemblies = Array.Empty<Assembly>(),
                    ModMetadata = new QuestingBotsMetadata { Name = "SWAG+Donuts", ModGuid = "swag-and-donuts" },
                },
            ]
        );

        await plugin.OnLoad();

        var warningMessages = logger
            .ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ISptLogger<CommonUtils>.Warning))
            .Select(call => call.GetArguments()[0]?.ToString() ?? string.Empty)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(config.BotSpawns.Enabled, Is.False, "Known spawning conflicts should disable QuestingBots spawning.");
            Assert.That(
                warningMessages.Any(message => message.Contains("/client/game/bot/generate", StringComparison.Ordinal)),
                Is.True,
                "The startup warning should mention the shared bot-generation route explicitly."
            );
            Assert.That(
                warningMessages.Any(message => message.Contains("order-sensitive", StringComparison.Ordinal)),
                Is.True,
                "The startup warning should explain that route dispatch is order-sensitive."
            );
            Assert.That(
                warningMessages.Any(message =>
                    message.Contains("QuestingBots spawning system has been disabled.", StringComparison.Ordinal)
                ),
                Is.True,
                "The startup warning should make the spawn-system fallback explicit."
            );
        });
    }

    [Test]
    public async Task OnLoad_WithConflictingModAndSpawningAlreadyDisabled_LogsAlreadyDisabledFallback()
    {
        var config = CreateValidConfig();
        config.BotSpawns.Enabled = false;

        var botConfig = (BotConfig)RuntimeHelpers.GetUninitializedObject(typeof(BotConfig));
        botConfig.ChanceAssaultScavHasPlayerScavName = 17;
        botConfig.PlayerScavBrainType = new Dictionary<string, Dictionary<string, int>>();

        var pmcConfig = (PmcConfig)RuntimeHelpers.GetUninitializedObject(typeof(PmcConfig));
        pmcConfig.PmcType = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();

        var logger = Substitute.For<ISptLogger<CommonUtils>>();
        var configLoader = CreateConfigLoader(config);
        var commonUtils = new CommonUtils(logger, null!, null!, configLoader);
        var configServer = TestConfigServer.Create(botConfig);
        var pmcConversionService = new PMCConversionService(commonUtils, configLoader, configServer);
        SetPrivateField(pmcConversionService, "_pmcConfig", pmcConfig);
        SetPrivateField(pmcConversionService, "_botConfig", botConfig);

        var plugin = new QuestingBotsServerPlugin(
            configLoader,
            commonUtils,
            null!,
            pmcConversionService,
            configServer,
            [CreateLoadedMod("SWAG+Donuts", "swag-and-donuts")]
        );

        await plugin.OnLoad();

        var warningMessages = logger
            .ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ISptLogger<CommonUtils>.Warning))
            .Select(call => call.GetArguments()[0]?.ToString() ?? string.Empty)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(config.BotSpawns.Enabled, Is.False, "Spawning should stay disabled when it was already off.");
            Assert.That(
                warningMessages.Any(message => message.Contains("QuestingBots spawning is already disabled.", StringComparison.Ordinal)),
                Is.True,
                "The startup warning should make the already-disabled fallback explicit."
            );
        });
    }

    [Test]
    public async Task OnLoad_WithPartialConflictName_DoesNotTreatItAsKnownConflict()
    {
        var config = CreateValidConfig();
        config.BotSpawns.Enabled = false;

        var botConfig = (BotConfig)RuntimeHelpers.GetUninitializedObject(typeof(BotConfig));
        botConfig.ChanceAssaultScavHasPlayerScavName = 19;
        botConfig.PlayerScavBrainType = new Dictionary<string, Dictionary<string, int>>();

        var pmcConfig = (PmcConfig)RuntimeHelpers.GetUninitializedObject(typeof(PmcConfig));
        pmcConfig.PmcType = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();

        var logger = Substitute.For<ISptLogger<CommonUtils>>();
        var configLoader = CreateConfigLoader(config);
        var commonUtils = new CommonUtils(logger, null!, null!, configLoader);
        var configServer = TestConfigServer.Create(botConfig);
        var pmcConversionService = new PMCConversionService(commonUtils, configLoader, configServer);
        SetPrivateField(pmcConversionService, "_pmcConfig", pmcConfig);
        SetPrivateField(pmcConversionService, "_botConfig", botConfig);

        var plugin = new QuestingBotsServerPlugin(
            configLoader,
            commonUtils,
            null!,
            pmcConversionService,
            configServer,
            [CreateLoadedMod("SWAGCompatBridge", "swag-compat-bridge")]
        );

        await plugin.OnLoad();

        var warningMessages = logger
            .ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ISptLogger<CommonUtils>.Warning))
            .Select(call => call.GetArguments()[0]?.ToString() ?? string.Empty)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(config.BotSpawns.Enabled, Is.False, "The startup path should not change an already-disabled spawning flag.");
            Assert.That(
                warningMessages.Any(message => message.Contains("Detected potentially conflicting server mod", StringComparison.Ordinal)),
                Is.False,
                "Partial alias matches should not be treated as known spawning conflicts."
            );
        });
    }

    [Test]
    public async Task OnLoad_WithConflictingModWhileSpawningAlreadyDisabled_LogsAlreadyDisabledWarning()
    {
        var config = CreateValidConfig();

        var botConfig = (BotConfig)RuntimeHelpers.GetUninitializedObject(typeof(BotConfig));
        botConfig.ChanceAssaultScavHasPlayerScavName = 11;
        botConfig.PlayerScavBrainType = new Dictionary<string, Dictionary<string, int>>();

        var pmcConfig = (PmcConfig)RuntimeHelpers.GetUninitializedObject(typeof(PmcConfig));
        pmcConfig.PmcType = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();

        var logger = Substitute.For<ISptLogger<CommonUtils>>();
        var configLoader = CreateConfigLoader(config);
        var commonUtils = new CommonUtils(logger, null!, null!, configLoader);
        var configServer = TestConfigServer.Create(botConfig);
        var pmcConversionService = new PMCConversionService(commonUtils, configLoader, configServer);
        SetPrivateField(pmcConversionService, "_pmcConfig", pmcConfig);
        SetPrivateField(pmcConversionService, "_botConfig", botConfig);

        var plugin = new QuestingBotsServerPlugin(
            configLoader,
            commonUtils,
            null!,
            pmcConversionService,
            configServer,
            [
                new SptMod
                {
                    Directory = "/mods/SWAG+Donuts",
                    Assemblies = Array.Empty<Assembly>(),
                    ModMetadata = new QuestingBotsMetadata { Name = "SWAG+Donuts", ModGuid = "swag-and-donuts" },
                },
            ]
        );

        await plugin.OnLoad();

        var warningMessages = logger
            .ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ISptLogger<CommonUtils>.Warning))
            .Select(call => call.GetArguments()[0]?.ToString() ?? string.Empty)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(config.BotSpawns.Enabled, Is.False, "Spawning should remain disabled.");
            Assert.That(
                warningMessages.Any(message => message.Contains("QuestingBots spawning is already disabled.", StringComparison.Ordinal)),
                Is.True,
                "The startup warning should explain that no extra spawn toggle was needed."
            );
            Assert.That(
                warningMessages.Any(message => message.Contains("/client/game/bot/generate", StringComparison.Ordinal)),
                Is.True,
                "Route-shadowing warnings should stay explicit even when spawning was already off."
            );
        });
    }

    [Test]
    public async Task OnLoad_WithOnlyPartialConflictNameMatch_DoesNotEmitConflictWarning()
    {
        var config = CreateValidConfig();

        var botConfig = (BotConfig)RuntimeHelpers.GetUninitializedObject(typeof(BotConfig));
        botConfig.ChanceAssaultScavHasPlayerScavName = 13;
        botConfig.PlayerScavBrainType = new Dictionary<string, Dictionary<string, int>>();

        var pmcConfig = (PmcConfig)RuntimeHelpers.GetUninitializedObject(typeof(PmcConfig));
        pmcConfig.PmcType = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();

        var logger = Substitute.For<ISptLogger<CommonUtils>>();
        var configLoader = CreateConfigLoader(config);
        var commonUtils = new CommonUtils(logger, null!, null!, configLoader);
        var configServer = TestConfigServer.Create(botConfig);
        var pmcConversionService = new PMCConversionService(commonUtils, configLoader, configServer);
        SetPrivateField(pmcConversionService, "_pmcConfig", pmcConfig);
        SetPrivateField(pmcConversionService, "_botConfig", botConfig);

        var plugin = new QuestingBotsServerPlugin(
            configLoader,
            commonUtils,
            null!,
            pmcConversionService,
            configServer,
            [
                new SptMod
                {
                    Directory = "/mods/BetterSpawnsPlusCompat",
                    Assemblies = Array.Empty<Assembly>(),
                    ModMetadata = new QuestingBotsMetadata { Name = "BetterSpawnsPlusCompat", ModGuid = "betterspawnsplus-compat" },
                },
            ]
        );

        await plugin.OnLoad();

        var warningMessages = logger
            .ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ISptLogger<CommonUtils>.Warning))
            .Select(call => call.GetArguments()[0]?.ToString() ?? string.Empty)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(config.BotSpawns.Enabled, Is.False, "The baseline config keeps spawning disabled for this startup test.");
            Assert.That(
                warningMessages.Any(message => message.Contains("Detected potentially conflicting server mod", StringComparison.Ordinal)),
                Is.False,
                "Conflict detection should require an exact known alias, not just a substring match."
            );
            Assert.That(
                warningMessages.Any(message => message.Contains("/client/game/bot/generate", StringComparison.Ordinal)),
                Is.False,
                "A non-matching mod name should not trigger route-shadowing diagnostics."
            );
        });
    }

    private static QuestingBotsConfig CreateValidConfig()
    {
        return new QuestingBotsConfig
        {
            Enabled = true,
            Questing = new QuestingConfig
            {
                BotQuests = new BotQuestsConfig
                {
                    EftQuests = new EftQuestsConfig
                    {
                        LevelRange =
                        [
                            [0.0, 1.0],
                            [20.0, 10.0],
                        ],
                    },
                },
            },
            BotSpawns = new BotSpawnsConfig
            {
                Enabled = false,
                Pmcs = new PmcSpawnConfig
                {
                    FractionOfMaxPlayersVsRaidET =
                    [
                        [0.0, 1.0],
                        [1.0, 0.5],
                    ],
                    BotsPerGroupDistribution =
                    [
                        [1.0, 100.0],
                    ],
                    BotDifficultyAsOnline =
                    [
                        [0.0, 100.0],
                    ],
                },
                PlayerScavs = new PlayerScavSpawnConfig
                {
                    BotsPerGroupDistribution =
                    [
                        [1.0, 100.0],
                    ],
                    BotDifficultyAsOnline =
                    [
                        [0.0, 100.0],
                    ],
                },
            },
            AdjustPScavChance = new AdjustPScavChanceConfig
            {
                Enabled = true,
                ChanceVsTimeRemainingFraction =
                [
                    [0.0, 50.0],
                    [1.0, 0.0],
                ],
            },
        };
    }

    private static QuestingBotsConfigLoader CreateConfigLoader(QuestingBotsConfig config)
    {
        var loader = new QuestingBotsConfigLoader(Substitute.For<ISptLogger<QuestingBotsConfigLoader>>());
        SetPrivateField(loader, "_config", config);
        return loader;
    }

    private static string GetRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(target, value);
    }

    private static SptMod CreateLoadedMod(string name, string guid)
    {
        return new SptMod
        {
            Directory = "/mods/" + name,
            Assemblies = Array.Empty<Assembly>(),
            ModMetadata = new QuestingBotsMetadata { Name = name, ModGuid = guid },
        };
    }

#pragma warning disable CS0618 // ConfigServer is obsolete (SPT 4.2 migration pending)
    private sealed class TestConfigServer : ConfigServer
    {
        private BotConfig _botConfig = null!;

        private TestConfigServer()
            : base(Substitute.For<ISptLogger<ConfigServer>>(), null!, null!) { }

        public static TestConfigServer Create(BotConfig botConfig)
        {
            var configServer = (TestConfigServer)RuntimeHelpers.GetUninitializedObject(typeof(TestConfigServer));
            configServer._botConfig = botConfig;
            return configServer;
        }

        [Obsolete("Test shim for ConfigServer.GetConfig<T>().")]
        public override T GetConfig<T>()
        {
            if (typeof(T) == typeof(BotConfig))
            {
                return (T)(object)_botConfig;
            }

            throw new InvalidOperationException($"No config stub registered for {typeof(T).Name}.");
        }
    }
#pragma warning restore CS0618
}

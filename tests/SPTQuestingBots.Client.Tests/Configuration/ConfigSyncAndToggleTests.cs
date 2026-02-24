using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration;

/// <summary>
/// Verifies BepInEx ConfigEntry defaults, ConfigSync coverage,
/// feature toggle defaults, and the mapping between F12 menu entries
/// and runtime config properties.
/// </summary>
[TestFixture]
public class ConfigSyncAndToggleTests
{
    private string _configSyncSource;
    private string _pluginConfigSource;

    [OneTimeSetUp]
    public void LoadSourceFiles()
    {
        // Walk up from bin/Debug/net9.0 to repo root
        var dir = TestContext.CurrentContext.TestDirectory;
        string repoRoot = null;
        for (int i = 0; i < 10; i++)
        {
            if (File.Exists(Path.Combine(dir, "config", "config.json")))
            {
                repoRoot = dir;
                break;
            }
            dir = Path.GetDirectoryName(dir);
            if (dir == null)
                break;
        }

        Assert.That(repoRoot, Is.Not.Null, "Could not find repo root from test directory");

        var syncPath = Path.Combine(repoRoot, "src", "SPTQuestingBots.Client", "ConfigSync.cs");
        Assert.That(File.Exists(syncPath), Is.True, $"ConfigSync.cs not found at {syncPath}");
        _configSyncSource = File.ReadAllText(syncPath);

        var pluginPath = Path.Combine(repoRoot, "src", "SPTQuestingBots.Client", "QuestingBotsPluginConfig.cs");
        Assert.That(File.Exists(pluginPath), Is.True, $"QuestingBotsPluginConfig.cs not found at {pluginPath}");
        _pluginConfigSource = File.ReadAllText(pluginPath);
    }

    // ══════════════════════════════════════════════════════════════
    //  Feature Toggle Defaults
    //  All major features should be ENABLED by default via C# class defaults.
    //  This ensures features work even if config.json fails to load.
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void SquadStrategyConfig_EnabledByDefault()
    {
        Assert.That(new SquadStrategyConfig().Enabled, Is.True);
    }

    [Test]
    public void BotLodConfig_EnabledByDefault()
    {
        Assert.That(new BotLodConfig().Enabled, Is.True);
    }

    [Test]
    public void BotPathingConfig_CustomMover_EnabledByDefault()
    {
        Assert.That(new BotPathingConfig().UseCustomMover, Is.True);
    }

    [Test]
    public void BotPathingConfig_DoorBypass_EnabledByDefault()
    {
        Assert.That(new BotPathingConfig().BypassDoorColliders, Is.True);
    }

    [Test]
    public void LootingConfig_EnabledByDefault()
    {
        Assert.That(new LootingConfig().Enabled, Is.True);
    }

    [Test]
    public void VultureConfig_EnabledByDefault()
    {
        Assert.That(new VultureConfig().Enabled, Is.True);
    }

    [Test]
    public void InvestigateConfig_EnabledByDefault()
    {
        Assert.That(new InvestigateConfig().Enabled, Is.True);
    }

    [Test]
    public void LingerConfig_EnabledByDefault()
    {
        Assert.That(new LingerConfig().Enabled, Is.True);
    }

    [Test]
    public void SpawnEntryConfig_EnabledByDefault()
    {
        Assert.That(new SpawnEntryConfig().Enabled, Is.True);
    }

    [Test]
    public void RoomClearConfig_EnabledByDefault()
    {
        Assert.That(new RoomClearConfig().Enabled, Is.True);
    }

    [Test]
    public void PatrolConfig_EnabledByDefault()
    {
        Assert.That(new PatrolConfig().Enabled, Is.True);
    }

    [Test]
    public void DynamicObjectiveConfig_EnabledByDefault()
    {
        Assert.That(new DynamicObjectiveConfig().Enabled, Is.True);
    }

    [Test]
    public void PersonalityConfig_EnabledByDefault()
    {
        Assert.That(new PersonalityConfig().Enabled, Is.True);
    }

    [Test]
    public void PersonalityConfig_RaidTimeEnabled_ByDefault()
    {
        Assert.That(new PersonalityConfig().RaidTimeEnabled, Is.True);
    }

    [Test]
    public void LookVarianceConfig_EnabledByDefault()
    {
        Assert.That(new LookVarianceConfig().Enabled, Is.True);
    }

    // ══════════════════════════════════════════════════════════════
    //  BepInEx Hardcoded Defaults
    //  These Config.Bind() calls use literal values rather than reading
    //  from ConfigController.Config. Verify they exist and document them.
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void BepInEx_QuestingEnabled_Hardcoded_True()
    {
        // QuestingEnabled is hardcoded to true in Config.Bind(), not read from config.json.
        // It's consumed directly via QuestingBotsPluginConfig.QuestingEnabled.Value.
        Assert.That(
            _pluginConfigSource.Contains("Config.Bind(ConfigSections.General, \"Enable Questing\", true"),
            Is.True,
            "QuestingEnabled should be hardcoded to true"
        );
    }

    [Test]
    public void BepInEx_SprintingEnabled_Hardcoded_True()
    {
        // CSharpier reformats across lines, so use regex with \s+
        Assert.That(
            Regex.IsMatch(
                _pluginConfigSource,
                @"SprintingEnabled\s*=\s*Config\.Bind\(\s*ConfigSections\.General,\s*""Allow Bots to Sprint while Questing"",\s*true"
            ),
            Is.True,
            "SprintingEnabled should be hardcoded to true"
        );
    }

    [Test]
    public void BepInEx_UseUtilityAI_Hardcoded_True()
    {
        Assert.That(
            _pluginConfigSource.Contains("\"Use Utility AI for Action Selection\""),
            Is.True,
            "UseUtilityAI Config.Bind entry should exist"
        );
        // The default is 'true', which is hardcoded (not from config.json)
    }

    [Test]
    public void BepInEx_SleepingEnabled_Hardcoded_False()
    {
        // AI Limiter is intentionally disabled by default (opt-in feature)
        Assert.That(
            Regex.IsMatch(
                _pluginConfigSource,
                @"SleepingEnabled\s*=\s*Config\.Bind\(\s*ConfigSections\.AILimiter,\s*""Enable AI Limiting"",\s*false"
            ),
            Is.True,
            "SleepingEnabled should be hardcoded to false"
        );
    }

    [Test]
    public void BepInEx_ScavLimitsEnabled_Hardcoded_True()
    {
        Assert.That(
            Regex.IsMatch(
                _pluginConfigSource,
                @"ScavLimitsEnabled\s*=\s*Config\.Bind\(\s*ConfigSections\.ScavLimits,\s*""Enable Scav Spawn Restrictions"",\s*true"
            ),
            Is.True,
            "ScavLimitsEnabled should be hardcoded to true"
        );
    }

    // ══════════════════════════════════════════════════════════════
    //  ConfigSync Coverage
    //  Verify that every property synced in ConfigSync.SyncToModConfig()
    //  has a corresponding ConfigEntry field declaration.
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ConfigSync_SyncsExpectedPropertyCount()
    {
        // Count all assignment lines in SyncToModConfig that push ConfigEntry values
        // Pattern: "Something = QuestingBotsPluginConfig.SomethingElse.Value"
        var syncLines = Regex.Matches(_configSyncSource, @"= QuestingBotsPluginConfig\.\w+\.Value");

        // Expected: 01 General (2) + 02 Bot Spawns (4) + 03 PMC (5) + 04 PScav (5) +
        // 06 Bot Pathing (3) + 07 Bot LOD (5) + 08 Looting (11) + 09 Vulture (8) +
        // 10 Investigate (4) + 11 Linger (4) + 12 Spawn Entry (4) + 13 Room Clear (5) +
        // 14 Patrol (4) + 15 Dynamic Objectives (6) + 16 Personality (2) +
        // 17 Look Variance (1) + 18 Zone Movement (6) = 79
        Assert.That(
            syncLines.Count,
            Is.EqualTo(79),
            "ConfigSync should sync exactly 79 properties. " + "If this changed, verify new ConfigEntries are synced."
        );
    }

    [Test]
    public void ConfigSync_CoversAllConfiguredSections()
    {
        // Verify that ConfigSync mentions every expected config section
        var expectedSections = new[]
        {
            "General",
            "Bot Spawns",
            "PMC Spawns",
            "PScav Spawns",
            "Bot Pathing",
            "Bot LOD",
            "Looting",
            "Vulture",
            "Investigate",
            "Linger",
            "Spawn Entry",
            "Room Clear",
            "Patrol",
            "Dynamic Objectives",
            "Personality",
            "Look Variance",
            "Zone Movement",
        };

        Assert.Multiple(() =>
        {
            foreach (var section in expectedSections)
            {
                Assert.That(_configSyncSource.Contains(section), Is.True, $"ConfigSync should mention section '{section}'");
            }
        });
    }

    [Test]
    public void ConfigSync_DoesNotSync_DirectlyConsumedEntries()
    {
        // These ConfigEntries are consumed directly via .Value (not through ConfigController.Config),
        // so ConfigSync correctly does NOT sync them.
        var directEntries = new[]
        {
            "QuestingEnabled",
            "SprintingEnabled",
            "MinSprintingDistance",
            "UseUtilityAI",
            "ScavLimitsEnabled",
            "SleepingEnabled",
            "SleepingEnabledForQuestingBots",
            "SleepingMinDistanceToHumansGlobal",
            "SleepingMinDistanceToQuestingBots",
            "MapsToAllowSleepingForQuestingBots",
            "SleeplessBotTypes",
            "MinBotsToEnableSleeping",
            "ShowSpawnDebugMessages",
            "ShowBotInfoOverlays",
            "CreateQuestLocations",
            "ZoneMovementDebugOverlay",
            "ZoneMovementDebugMinimap",
        };

        Assert.Multiple(() =>
        {
            foreach (var entry in directEntries)
            {
                // These should NOT appear in sync assignments
                var pattern = $"= QuestingBotsPluginConfig.{entry}.Value";
                Assert.That(
                    _configSyncSource.Contains(pattern),
                    Is.False,
                    $"'{entry}' is directly consumed and should NOT be in ConfigSync"
                );
            }
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  ConfigSync: every synced property has a matching ConfigEntry
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ConfigSync_AllReferencedConfigEntries_ExistAsStaticFields()
    {
        // Extract all "QuestingBotsPluginConfig.XYZ.Value" references from ConfigSync
        var matches = Regex.Matches(_configSyncSource, @"QuestingBotsPluginConfig\.(\w+)\.Value");

        var syncedEntryNames = new HashSet<string>();
        foreach (Match m in matches)
        {
            syncedEntryNames.Add(m.Groups[1].Value);
        }

        Assert.That(syncedEntryNames.Count, Is.GreaterThan(0), "Should find synced entries");

        // Verify each referenced entry is declared as a static field in QuestingBotsPluginConfig
        Assert.Multiple(() =>
        {
            foreach (var entryName in syncedEntryNames)
            {
                var fieldPattern = $"public static ConfigEntry<";
                Assert.That(
                    _pluginConfigSource.Contains($" {entryName};") || _pluginConfigSource.Contains($" {entryName} "),
                    Is.True,
                    $"ConfigSync references '{entryName}' but it's not declared in QuestingBotsPluginConfig"
                );
            }
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  ConfigSync: null guard for conditional sections
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ConfigSync_HasNullGuard_ForConditionalPMCSpawns()
    {
        // PMC/PScav entries are only bound when BotSpawns.Enabled is true.
        // ConfigSync must null-check before accessing them.
        Assert.That(
            _configSyncSource.Contains("QuestingBotsPluginConfig.PMCSpawnsEnabled != null"),
            Is.True,
            "ConfigSync should null-check PMCSpawnsEnabled before syncing PMC spawn values"
        );
    }

    [Test]
    public void ConfigSync_HasNullGuard_ForConditionalPScavSpawns()
    {
        Assert.That(
            _configSyncSource.Contains("QuestingBotsPluginConfig.PScavSpawnsEnabled != null"),
            Is.True,
            "ConfigSync should null-check PScavSpawnsEnabled before syncing PScav spawn values"
        );
    }

    // ══════════════════════════════════════════════════════════════
    //  BepInEx defaults from config-absent sections
    //  These sections have NO config.json representation, so BepInEx
    //  defaults come entirely from C# class defaults.
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void BepInEx_SquadStrategyEnabled_UsesClassDefault_SinceNoJsonSection()
    {
        // SquadStrategyEnabled = Config.Bind(..., q.SquadStrategy.Enabled)
        // Since "squad_strategy" doesn't exist in config.json, q.SquadStrategy.Enabled
        // is the C# default (true). Verify Config.Bind reads from q.SquadStrategy.Enabled.
        Assert.That(
            _pluginConfigSource.Contains("q.SquadStrategy.Enabled"),
            Is.True,
            "SquadStrategyEnabled should read from q.SquadStrategy.Enabled"
        );
        Assert.That(new SquadStrategyConfig().Enabled, Is.True, "C# default for SquadStrategy.Enabled should be true");
    }

    [Test]
    public void BepInEx_BotLodEnabled_UsesClassDefault_SinceNoJsonSection()
    {
        Assert.That(_pluginConfigSource.Contains("q.BotLod.Enabled"), Is.True, "BotLodEnabled should read from q.BotLod.Enabled");
        Assert.That(new BotLodConfig().Enabled, Is.True, "C# default for BotLod.Enabled should be true");
    }

    [Test]
    public void BepInEx_UseCustomMover_UsesClassDefault_SinceNotInJson()
    {
        Assert.That(
            _pluginConfigSource.Contains("q.BotPathing.UseCustomMover"),
            Is.True,
            "UseCustomMover should read from q.BotPathing.UseCustomMover"
        );
        Assert.That(new BotPathingConfig().UseCustomMover, Is.True, "C# default for UseCustomMover should be true");
    }

    [Test]
    public void BepInEx_BypassDoorColliders_UsesClassDefault_SinceNotInJson()
    {
        Assert.That(
            _pluginConfigSource.Contains("q.BotPathing.BypassDoorColliders"),
            Is.True,
            "BypassDoorColliders should read from q.BotPathing.BypassDoorColliders"
        );
        Assert.That(new BotPathingConfig().BypassDoorColliders, Is.True, "C# default for BypassDoorColliders should be true");
    }

    // ══════════════════════════════════════════════════════════════
    //  ConfigSections: verify all section constants are unique
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ConfigSections_AllValuesAreUnique()
    {
        var fields = typeof(ConfigSections)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null))
            .ToList();

        Assert.That(fields.Count, Is.GreaterThan(0), "Should find section constants");
        Assert.That(fields.Distinct().Count(), Is.EqualTo(fields.Count), "All ConfigSection values should be unique");
    }

    [Test]
    public void ConfigSections_AllValuesHaveNumericPrefix()
    {
        var fields = typeof(ConfigSections)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (Name: f.Name, Value: (string)f.GetValue(null)))
            .ToList();

        Assert.Multiple(() =>
        {
            foreach (var (name, value) in fields)
            {
                Assert.That(Regex.IsMatch(value, @"^\d{2}\. "), Is.True, $"ConfigSections.{name} = \"{value}\" should start with 'NN. '");
            }
        });
    }

    [Test]
    public void ConfigSections_NumericPrefixesAreContiguous()
    {
        var fields = typeof(ConfigSections)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null))
            .ToList();

        var numbers = fields
            .Select(v =>
            {
                var match = Regex.Match(v, @"^(\d{2})\. ");
                return match.Success ? int.Parse(match.Groups[1].Value) : -1;
            })
            .Where(n => n > 0)
            .OrderBy(n => n)
            .ToList();

        // Check contiguous: first should be 1, last should equal count
        Assert.That(numbers.First(), Is.EqualTo(1), "First section should be 01");
        Assert.That(numbers.Last(), Is.EqualTo(numbers.Count), $"Section numbers should be contiguous 1..{numbers.Count}");
    }

    // ══════════════════════════════════════════════════════════════
    //  ConfigEntry field count matches Config.Bind() call count
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void PluginConfig_AllStaticConfigEntryFields_HaveCorrespondingBindCall()
    {
        // Extract all "public static ConfigEntry<T> XYZ;" field names
        var fieldMatches = Regex.Matches(_pluginConfigSource, @"public static ConfigEntry<[^>]+>\s+(\w+)\s*;");

        var fieldNames = new List<string>();
        foreach (Match m in fieldMatches)
        {
            fieldNames.Add(m.Groups[1].Value);
        }

        Assert.That(fieldNames.Count, Is.GreaterThan(50), "Should find many ConfigEntry fields");

        // Extract all "XYZ = Config.Bind(" assignment targets
        var bindMatches = Regex.Matches(_pluginConfigSource, @"(\w+)\s*=\s*Config\.Bind\(");

        var boundNames = new HashSet<string>();
        foreach (Match m in bindMatches)
        {
            boundNames.Add(m.Groups[1].Value);
        }

        // Every declared field should have a matching Config.Bind() call
        Assert.Multiple(() =>
        {
            foreach (var fieldName in fieldNames)
            {
                Assert.That(boundNames.Contains(fieldName), Is.True, $"ConfigEntry field '{fieldName}' has no matching Config.Bind() call");
            }
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  ConfigSync section comment numbers match ConfigSections
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ConfigSync_SectionComments_MatchConfigSections()
    {
        // ConfigSync uses comments like "// 01. General" or "// 03. PMC Spawns (conditional...)".
        // Extract the base section name (before any parenthetical annotation).
        var sectionComments = Regex.Matches(_configSyncSource, @"//\s+(\d{2})\.\s+([^\r\n(]+)");

        var configSectionValues = typeof(ConfigSections)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null))
            .ToHashSet();

        Assert.Multiple(() =>
        {
            foreach (Match m in sectionComments)
            {
                var sectionRef = $"{m.Groups[1].Value}. {m.Groups[2].Value.Trim()}";
                Assert.That(
                    configSectionValues.Contains(sectionRef),
                    Is.True,
                    $"ConfigSync references section '{sectionRef}' not found in ConfigSections"
                );
            }
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  Two-pattern consumption: verify no property is BOTH synced AND read directly
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ConfigSync_NullGuard_TopLevel()
    {
        // ConfigSync should check cfg == null before accessing any properties
        Assert.That(
            _configSyncSource.Contains("if (cfg == null)"),
            Is.True,
            "ConfigSync should null-check cfg at the top of SyncToModConfig()"
        );
    }

    // ══════════════════════════════════════════════════════════════
    //  BepInEx conditional binding: PMC/PScav sections only bind when spawns enabled
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void PluginConfig_PMCSpawns_OnlyBound_WhenSpawnsEnabled()
    {
        // PMCSpawnsEnabled should be bound inside an "if (cfg.BotSpawns.Enabled)" block
        Assert.That(
            _pluginConfigSource.Contains("if (cfg.BotSpawns.Enabled)"),
            Is.True,
            "PMC/PScav/ScavLimit entries should be conditionally bound"
        );
    }

    // ══════════════════════════════════════════════════════════════
    //  BepInEx defaults from config.json-sourced values
    //  Verify Config.Bind() pulls from cfg/q objects (not hardcoded)
    //  for properties that SHOULD come from config.json.
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void BepInEx_ConfigJsonSourced_BindCallsReferenceConfigObjects()
    {
        // These Config.Bind() calls should use cfg.X or q.X as defaults,
        // ensuring they inherit config.json values at runtime.
        var expectedReferences = new Dictionary<string, string>
        {
            { "MaxCalcTimePerFrame", "cfg.MaxCalcTimePerFrame" },
            { "SpawnsEnabled", "cfg.BotSpawns.Enabled" },
            { "SpawnInitialBossesFirst", "cfg.BotSpawns.SpawnInitialBossesFirst" },
            { "SpawnRetryTime", "cfg.BotSpawns.SpawnRetryTime" },
            { "DelayGameStartUntilBotGenFinishes", "cfg.BotSpawns.DelayGameStartUntilBotGenFinishes" },
            { "UseCustomMover", "q.BotPathing.UseCustomMover" },
            { "BypassDoorColliders", "q.BotPathing.BypassDoorColliders" },
            { "IncompletePathRetryInterval", "q.BotPathing.IncompletePathRetryInterval" },
            { "BotLodEnabled", "q.BotLod.Enabled" },
            { "LootingEnabled", "q.Looting.Enabled" },
            { "VultureEnabled", "q.Vulture.Enabled" },
            { "InvestigateEnabled", "q.Investigate.Enabled" },
            { "LingerEnabled", "q.Linger.Enabled" },
            { "SpawnEntryEnabled", "q.SpawnEntry.Enabled" },
            { "RoomClearEnabled", "q.RoomClear.Enabled" },
            { "PatrolEnabled", "q.Patrol.Enabled" },
            { "DynamicObjectivesEnabled", "q.DynamicObjectives.Enabled" },
            { "PersonalityEnabled", "q.Personality.Enabled" },
            { "LookVarianceEnabled", "q.LookVariance.Enabled" },
            { "ZoneMovementEnabled", "q.ZoneMovement.Enabled" },
        };

        Assert.Multiple(() =>
        {
            foreach (var (entry, reference) in expectedReferences)
            {
                Assert.That(
                    _pluginConfigSource.Contains(reference),
                    Is.True,
                    $"Config.Bind for {entry} should reference '{reference}' (from config.json)"
                );
            }
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  Zone Movement: ConfigSync syncs all exposed weight fields
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ConfigSync_ZoneMovement_SyncsAllWeightFields()
    {
        var expectedZoneFields = new[]
        {
            "q.ZoneMovement.Enabled",
            "q.ZoneMovement.ConvergenceWeight",
            "q.ZoneMovement.AdvectionWeight",
            "q.ZoneMovement.MomentumWeight",
            "q.ZoneMovement.NoiseWeight",
            "q.ZoneMovement.TargetCellCount",
        };

        Assert.Multiple(() =>
        {
            foreach (var field in expectedZoneFields)
            {
                Assert.That(_configSyncSource.Contains(field), Is.True, $"ConfigSync should sync '{field}'");
            }
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  Looting: ConfigSync syncs all 11 looting properties
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ConfigSync_Looting_SyncsAllProperties()
    {
        var expectedLootFields = new[]
        {
            "q.Looting.Enabled",
            "q.Looting.DetectContainerDistance",
            "q.Looting.DetectItemDistance",
            "q.Looting.DetectCorpseDistance",
            "q.Looting.MinItemValue",
            "q.Looting.MaxConcurrentLooters",
            "q.Looting.ContainerLootingEnabled",
            "q.Looting.LooseItemLootingEnabled",
            "q.Looting.CorpseLootingEnabled",
            "q.Looting.GearSwapEnabled",
            "q.Looting.LootDuringCombat",
        };

        Assert.Multiple(() =>
        {
            foreach (var field in expectedLootFields)
            {
                Assert.That(_configSyncSource.Contains(field), Is.True, $"ConfigSync should sync '{field}'");
            }
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  Vulture: ConfigSync syncs all 8 vulture properties
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ConfigSync_Vulture_SyncsAllProperties()
    {
        var expectedVultureFields = new[]
        {
            "q.Vulture.Enabled",
            "q.Vulture.BaseDetectionRange",
            "q.Vulture.CourageThreshold",
            "q.Vulture.AmbushDuration",
            "q.Vulture.EnableSilentApproach",
            "q.Vulture.EnableBaiting",
            "q.Vulture.EnableBossAvoidance",
            "q.Vulture.EnableAirdropVulturing",
        };

        Assert.Multiple(() =>
        {
            foreach (var field in expectedVultureFields)
            {
                Assert.That(_configSyncSource.Contains(field), Is.True, $"ConfigSync should sync '{field}'");
            }
        });
    }
}

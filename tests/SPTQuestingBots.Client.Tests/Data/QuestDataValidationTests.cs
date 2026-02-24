using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Data;

/// <summary>
/// Validates the committed quest JSON data files that ship with the mod.
/// A bug in quest data means bots go to wrong positions on specific maps.
/// </summary>
[TestFixture]
public class QuestDataValidationTests
{
    // All known SPT map IDs that should have quest files
    private static readonly string[] ExpectedMaps = new[]
    {
        "bigmap",
        "factory4_day",
        "factory4_night",
        "interchange",
        "laboratory",
        "lighthouse",
        "rezervbase",
        "sandbox",
        "sandbox_high",
        "shoreline",
        "tarkovstreets",
        "woods",
    };

    // Valid QuestAction enum values from QuestObjectiveStep.cs
    private static readonly HashSet<string> ValidStepTypes = new(StringComparer.Ordinal)
    {
        "Undefined",
        "MoveToPosition",
        "HoldAtPosition",
        "Ambush",
        "Snipe",
        "PlantItem",
        "ToggleSwitch",
        "RequestExtract",
        "CloseNearbyDoors",
    };

    // Valid LootAfterCompleting enum values from QuestObjective.cs
    private static readonly HashSet<string> ValidLootSettings = new(StringComparer.Ordinal) { "Default", "Force", "Inhibit" };

    private static string _repoRoot;
    private static string _questDir;
    private static string _configDir;

    [OneTimeSetUp]
    public void FindRepoRoot()
    {
        // Walk up from test output directory to find repo root (contains quests/ and config/)
        string dir = TestContext.CurrentContext.TestDirectory;
        for (int i = 0; i < 10; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "quests")) && Directory.Exists(Path.Combine(dir, "config")))
            {
                _repoRoot = dir;
                _questDir = Path.Combine(dir, "quests", "standard");
                _configDir = Path.Combine(dir, "config");
                return;
            }
            dir = Directory.GetParent(dir)?.FullName;
            if (dir == null)
                break;
        }
        Assert.Fail("Could not find repo root (quests/ + config/) walking up from " + TestContext.CurrentContext.TestDirectory);
    }

    #region Map Coverage

    [Test]
    public void AllExpectedMaps_HaveQuestFiles()
    {
        foreach (string map in ExpectedMaps)
        {
            string path = Path.Combine(_questDir, map + ".json");
            Assert.That(File.Exists(path), Is.True, $"Missing quest file for map: {map}");
        }
    }

    [Test]
    public void NoUnexpectedQuestFiles_Exist()
    {
        var expected = new HashSet<string>(ExpectedMaps.Select(m => m + ".json"));
        var actual = Directory.GetFiles(_questDir, "*.json").Select(Path.GetFileName).ToList();

        foreach (string file in actual)
        {
            Assert.That(expected.Contains(file), Is.True, $"Unexpected quest file: {file}");
        }
    }

    [Test]
    public void FactoryDayAndNight_AreIdentical()
    {
        // Factory Day and Night share the same map layout; quest data should match
        string day = File.ReadAllText(Path.Combine(_questDir, "factory4_day.json"));
        string night = File.ReadAllText(Path.Combine(_questDir, "factory4_night.json"));
        Assert.That(day, Is.EqualTo(night), "factory4_day.json and factory4_night.json differ");
    }

    [Test]
    public void SandboxAndSandboxHigh_AreIdentical()
    {
        // Ground Zero and Ground Zero (High) share the same map layout
        string standard = File.ReadAllText(Path.Combine(_questDir, "sandbox.json"));
        string high = File.ReadAllText(Path.Combine(_questDir, "sandbox_high.json"));
        Assert.That(standard, Is.EqualTo(high), "sandbox.json and sandbox_high.json differ");
    }

    #endregion

    #region JSON Structure

    [TestCaseSource(nameof(ExpectedMaps))]
    public void QuestFile_ParsesAsValidJsonArray(string map)
    {
        string json = File.ReadAllText(Path.Combine(_questDir, map + ".json"));
        JArray quests = JArray.Parse(json);
        Assert.That(quests.Count, Is.GreaterThan(0), $"{map}.json has no quests");
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllQuests_HaveNonEmptyName(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name");
            Assert.That(string.IsNullOrWhiteSpace(name), Is.False, $"{map}: quest at index {quests.IndexOf(quest)} has empty/null name");
        }
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllQuests_HaveAtLeastOneObjective(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name") ?? "unnamed";
            JArray objectives = quest.Value<JArray>("objectives");
            Assert.That(objectives, Is.Not.Null, $"{map}: quest \"{name}\" has null objectives");
            Assert.That(objectives.Count, Is.GreaterThan(0), $"{map}: quest \"{name}\" has no objectives");
        }
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllObjectives_HaveAtLeastOneStep(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name") ?? "unnamed";
            JArray objectives = quest.Value<JArray>("objectives");
            if (objectives == null)
                continue;

            for (int i = 0; i < objectives.Count; i++)
            {
                JObject obj = (JObject)objectives[i];
                JArray steps = obj.Value<JArray>("steps");
                Assert.That(steps, Is.Not.Null, $"{map}: quest \"{name}\" objective[{i}] has null steps");
                Assert.That(steps.Count, Is.GreaterThan(0), $"{map}: quest \"{name}\" objective[{i}] has no steps");
            }
        }
    }

    #endregion

    #region Position Validation

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllStepPositions_AreFiniteNumbers(string map)
    {
        foreach (var (questName, stepIdx, pos) in EnumerateStepPositions(map))
        {
            float x = pos.Value<float>("x");
            float y = pos.Value<float>("y");
            float z = pos.Value<float>("z");

            Assert.That(float.IsFinite(x), Is.True, $"{map}: quest \"{questName}\" step[{stepIdx}] x={x} is not finite");
            Assert.That(float.IsFinite(y), Is.True, $"{map}: quest \"{questName}\" step[{stepIdx}] y={y} is not finite");
            Assert.That(float.IsFinite(z), Is.True, $"{map}: quest \"{questName}\" step[{stepIdx}] z={z} is not finite");
        }
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllStepPositions_AreNotExactOrigin(string map)
    {
        foreach (var (questName, stepIdx, pos) in EnumerateStepPositions(map))
        {
            float x = pos.Value<float>("x");
            float y = pos.Value<float>("y");
            float z = pos.Value<float>("z");

            bool isOrigin = x == 0f && y == 0f && z == 0f;
            Assert.That(isOrigin, Is.False, $"{map}: quest \"{questName}\" step[{stepIdx}] position is exact (0,0,0)");
        }
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllStepPositions_HaveReasonableYCoordinate(string map)
    {
        // Tarkov maps: Y typically ranges from -60 (deep underground) to +100 (rooftops)
        // Use generous bounds to catch clearly wrong data
        const float minY = -100f;
        const float maxY = 500f;

        foreach (var (questName, stepIdx, pos) in EnumerateStepPositions(map))
        {
            float y = pos.Value<float>("y");
            Assert.That(
                y,
                Is.GreaterThanOrEqualTo(minY).And.LessThanOrEqualTo(maxY),
                $"{map}: quest \"{questName}\" step[{stepIdx}] Y={y} out of range [{minY}, {maxY}]"
            );
        }
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllStepPositions_HaveReasonableXZCoordinates(string map)
    {
        // Tarkov maps: X/Z typically within [-1000, 1000] for the largest maps
        const float maxCoord = 2000f;

        foreach (var (questName, stepIdx, pos) in EnumerateStepPositions(map))
        {
            float x = pos.Value<float>("x");
            float z = pos.Value<float>("z");

            Assert.That(
                Math.Abs(x),
                Is.LessThanOrEqualTo(maxCoord),
                $"{map}: quest \"{questName}\" step[{stepIdx}] X={x} exceeds {maxCoord}"
            );
            Assert.That(
                Math.Abs(z),
                Is.LessThanOrEqualTo(maxCoord),
                $"{map}: quest \"{questName}\" step[{stepIdx}] Z={z} exceeds {maxCoord}"
            );
        }
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllWaypointPositions_AreFiniteAndNonOrigin(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name") ?? "unnamed";
            JArray waypoints = quest.Value<JArray>("waypoints");
            if (waypoints == null)
                continue;

            for (int i = 0; i < waypoints.Count; i++)
            {
                JObject wp = (JObject)waypoints[i];
                float x = wp.Value<float>("x");
                float y = wp.Value<float>("y");
                float z = wp.Value<float>("z");

                Assert.That(
                    float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z),
                    Is.True,
                    $"{map}: quest \"{name}\" waypoint[{i}] has non-finite coordinate"
                );

                bool isOrigin = x == 0f && y == 0f && z == 0f;
                Assert.That(isOrigin, Is.False, $"{map}: quest \"{name}\" waypoint[{i}] is exact (0,0,0)");
            }
        }
    }

    #endregion

    #region Step Types and Enum Values

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllStepTypes_AreValidQuestActions(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name") ?? "unnamed";
            foreach (JObject obj in quest.Value<JArray>("objectives") ?? new JArray())
            {
                foreach (JObject step in obj.Value<JArray>("steps") ?? new JArray())
                {
                    string stepType = step.Value<string>("stepType");
                    if (stepType == null)
                        continue; // default is MoveToPosition

                    Assert.That(
                        ValidStepTypes.Contains(stepType),
                        Is.True,
                        $"{map}: quest \"{name}\" has invalid stepType: \"{stepType}\""
                    );
                }
            }
        }
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllLootAfterCompleting_AreValidEnum(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name") ?? "unnamed";
            foreach (JObject obj in quest.Value<JArray>("objectives") ?? new JArray())
            {
                string lootSetting = obj.Value<string>("lootAfterCompleting");
                if (lootSetting == null)
                    continue; // default is Default

                Assert.That(
                    ValidLootSettings.Contains(lootSetting),
                    Is.True,
                    $"{map}: quest \"{name}\" has invalid lootAfterCompleting: \"{lootSetting}\""
                );
            }
        }
    }

    [Test]
    public void ToggleSwitchSteps_HaveNonEmptySwitchID()
    {
        foreach (string map in ExpectedMaps)
        {
            JArray quests = LoadQuestFile(map);
            foreach (JObject quest in quests)
            {
                string name = quest.Value<string>("name") ?? "unnamed";
                foreach (JObject obj in quest.Value<JArray>("objectives") ?? new JArray())
                {
                    foreach (JObject step in obj.Value<JArray>("steps") ?? new JArray())
                    {
                        string stepType = step.Value<string>("stepType");
                        if (stepType != "ToggleSwitch")
                            continue;

                        string switchId = step.Value<string>("switchID");
                        Assert.That(
                            string.IsNullOrEmpty(switchId),
                            Is.False,
                            $"{map}: quest \"{name}\" ToggleSwitch step has empty switchID"
                        );
                    }
                }
            }
        }
    }

    #endregion

    #region Numeric Range Validation

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllQuests_HaveLevelRangeInOrder(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name") ?? "unnamed";
            int minLevel = quest.Value<int?>("minLevel") ?? 0;
            int maxLevel = quest.Value<int?>("maxLevel") ?? 99;

            Assert.That(minLevel, Is.LessThanOrEqualTo(maxLevel), $"{map}: quest \"{name}\" minLevel({minLevel}) > maxLevel({maxLevel})");
            Assert.That(minLevel, Is.GreaterThanOrEqualTo(0), $"{map}: quest \"{name}\" minLevel({minLevel}) is negative");
        }
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllQuests_HaveNonNegativeDesirability(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name") ?? "unnamed";
            float desirability = quest.Value<float?>("desirability") ?? 0f;

            Assert.That(desirability, Is.GreaterThanOrEqualTo(0f), $"{map}: quest \"{name}\" has negative desirability: {desirability}");
        }
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllQuests_HavePositiveMaxBots(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name") ?? "unnamed";
            int maxBots = quest.Value<int?>("maxBots") ?? 2;

            Assert.That(maxBots, Is.GreaterThan(0), $"{map}: quest \"{name}\" has maxBots <= 0: {maxBots}");
        }
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllObjectives_HaveDistanceRangeInOrder(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name") ?? "unnamed";
            JArray objectives = quest.Value<JArray>("objectives");
            if (objectives == null)
                continue;

            for (int i = 0; i < objectives.Count; i++)
            {
                JObject obj = (JObject)objectives[i];
                float minDist = obj.Value<float?>("minDistanceFromBot") ?? 10f;
                float maxDist = obj.Value<float?>("maxDistanceFromBot") ?? 9999f;

                Assert.That(
                    minDist,
                    Is.LessThanOrEqualTo(maxDist),
                    $"{map}: quest \"{name}\" obj[{i}] minDist({minDist}) > maxDist({maxDist})"
                );
                Assert.That(minDist, Is.GreaterThanOrEqualTo(0f), $"{map}: quest \"{name}\" obj[{i}] minDist is negative");
            }
        }
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllSteps_HaveNonNegativeMaxDistance(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name") ?? "unnamed";
            foreach (JObject obj in quest.Value<JArray>("objectives") ?? new JArray())
            {
                JArray steps = obj.Value<JArray>("steps");
                if (steps == null)
                    continue;

                for (int i = 0; i < steps.Count; i++)
                {
                    JObject step = (JObject)steps[i];
                    float? maxDist = step.Value<float?>("maxDistance");
                    if (!maxDist.HasValue)
                        continue;

                    Assert.That(maxDist.Value, Is.GreaterThan(0f), $"{map}: quest \"{name}\" step[{i}] maxDistance <= 0: {maxDist.Value}");
                }
            }
        }
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllSteps_MinElapsedTime_HasValidRange(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name") ?? "unnamed";
            foreach (JObject obj in quest.Value<JArray>("objectives") ?? new JArray())
            {
                JArray steps = obj.Value<JArray>("steps");
                if (steps == null)
                    continue;

                for (int i = 0; i < steps.Count; i++)
                {
                    JObject step = (JObject)steps[i];
                    JToken met = step["minElapsedTime"];
                    if (met == null)
                        continue;

                    if (met.Type == JTokenType.Object)
                    {
                        float min = met.Value<float>("min");
                        float max = met.Value<float>("max");

                        Assert.That(min, Is.GreaterThanOrEqualTo(0f), $"{map}: quest \"{name}\" step[{i}] minElapsedTime.min is negative");
                        Assert.That(
                            max,
                            Is.GreaterThanOrEqualTo(min),
                            $"{map}: quest \"{name}\" step[{i}] minElapsedTime.max({max}) < min({min})"
                        );
                    }
                    else if (met.Type == JTokenType.Float || met.Type == JTokenType.Integer)
                    {
                        float val = met.Value<float>();
                        Assert.That(
                            val,
                            Is.GreaterThanOrEqualTo(0f),
                            $"{map}: quest \"{name}\" step[{i}] minElapsedTime is negative: {val}"
                        );
                    }
                }
            }
        }
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllQuests_RaidETRange_IsValid(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name") ?? "unnamed";
            float? minRaidET = quest.Value<float?>("minRaidET");
            float? maxRaidET = quest.Value<float?>("maxRaidET");

            if (minRaidET.HasValue)
            {
                Assert.That(minRaidET.Value, Is.GreaterThanOrEqualTo(0f), $"{map}: quest \"{name}\" minRaidET is negative");
            }

            if (minRaidET.HasValue && maxRaidET.HasValue)
            {
                Assert.That(
                    maxRaidET.Value,
                    Is.GreaterThanOrEqualTo(minRaidET.Value),
                    $"{map}: quest \"{name}\" maxRaidET({maxRaidET}) < minRaidET({minRaidET})"
                );
            }
        }
    }

    #endregion

    #region EFT Quest Settings

    [Test]
    public void EftQuestSettings_ParsesAsValidJson()
    {
        string json = File.ReadAllText(Path.Combine(_configDir, "eftQuestSettings.json"));
        JObject settings = JObject.Parse(json);
        Assert.That(settings.Count, Is.GreaterThan(0));
    }

    [Test]
    public void EftQuestSettings_AllWaypoints_AreFiniteAndNonOrigin()
    {
        JObject settings = LoadEftQuestSettings();
        foreach (var prop in settings.Properties())
        {
            JArray waypoints = prop.Value.Value<JArray>("waypoints");
            if (waypoints == null)
                continue;

            for (int i = 0; i < waypoints.Count; i++)
            {
                JObject wp = (JObject)waypoints[i];
                float x = wp.Value<float>("x");
                float y = wp.Value<float>("y");
                float z = wp.Value<float>("z");

                Assert.That(
                    float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z),
                    Is.True,
                    $"eftQuestSettings: quest {prop.Name} waypoint[{i}] has non-finite coordinate"
                );

                bool isOrigin = x == 0f && y == 0f && z == 0f;
                Assert.That(isOrigin, Is.False, $"eftQuestSettings: quest {prop.Name} waypoint[{i}] is exact (0,0,0)");
            }
        }
    }

    [Test]
    public void EftQuestSettings_RequiredSwitches_HaveNonEmptyIds()
    {
        JObject settings = LoadEftQuestSettings();
        foreach (var prop in settings.Properties())
        {
            JObject switches = prop.Value.Value<JObject>("requiredSwitches");
            if (switches == null)
                continue;

            foreach (var sw in switches.Properties())
            {
                Assert.That(string.IsNullOrEmpty(sw.Name), Is.False, $"eftQuestSettings: quest {prop.Name} has empty switch ID");
            }
        }
    }

    #endregion

    #region Zone and Item Quest Positions

    [Test]
    public void ZonePositions_ParsesAsValidJson()
    {
        string json = File.ReadAllText(Path.Combine(_configDir, "zoneAndItemQuestPositions.json"));
        JObject zones = JObject.Parse(json);
        Assert.That(zones.Count, Is.GreaterThan(0));
    }

    [Test]
    public void ZonePositions_AllPositions_AreFiniteAndNonOrigin()
    {
        JObject zones = LoadZonePositions();
        foreach (var prop in zones.Properties())
        {
            JObject pos = prop.Value.Value<JObject>("position");
            Assert.That(pos, Is.Not.Null, $"Zone {prop.Name} has no position");

            float x = pos.Value<float>("x");
            float y = pos.Value<float>("y");
            float z = pos.Value<float>("z");

            Assert.That(
                float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z),
                Is.True,
                $"Zone {prop.Name} has non-finite coordinate"
            );

            bool isOrigin = x == 0f && y == 0f && z == 0f;
            Assert.That(isOrigin, Is.False, $"Zone {prop.Name} position is exact (0,0,0)");
        }
    }

    [Test]
    public void ZonePositions_AllPositions_HaveReasonableCoordinates()
    {
        JObject zones = LoadZonePositions();
        foreach (var prop in zones.Properties())
        {
            JObject pos = prop.Value.Value<JObject>("position");
            if (pos == null)
                continue;

            float x = pos.Value<float>("x");
            float y = pos.Value<float>("y");
            float z = pos.Value<float>("z");

            Assert.That(y, Is.GreaterThanOrEqualTo(-100f).And.LessThanOrEqualTo(500f), $"Zone {prop.Name} Y={y} out of range");
            Assert.That(Math.Abs(x), Is.LessThanOrEqualTo(2000f), $"Zone {prop.Name} X={x} is extreme");
            Assert.That(Math.Abs(z), Is.LessThanOrEqualTo(2000f), $"Zone {prop.Name} Z={z} is extreme");
        }
    }

    [Test]
    public void ZonePositions_DoorUnlockEntries_HaveInteractionPosition()
    {
        JObject zones = LoadZonePositions();
        foreach (var prop in zones.Properties())
        {
            bool mustUnlock = prop.Value.Value<bool?>("mustUnlockNearbyDoor") ?? false;
            if (!mustUnlock)
                continue;

            JObject interactionPos = prop.Value.Value<JObject>("nearbyDoorInteractionPosition");
            Assert.That(interactionPos, Is.Not.Null, $"Zone {prop.Name} requires door unlock but has no nearbyDoorInteractionPosition");

            float searchRadius = prop.Value.Value<float?>("nearbyDoorSearchRadius") ?? 0f;
            Assert.That(searchRadius, Is.GreaterThan(0f), $"Zone {prop.Name} requires door unlock but nearbyDoorSearchRadius <= 0");
        }
    }

    #endregion

    #region Config.json Per-Map Completeness

    [Test]
    public void Config_PerMapDictionaries_CoverAllMaps()
    {
        string json = File.ReadAllText(Path.Combine(_configDir, "config.json"));
        JObject config = JObject.Parse(json);

        var mapSet = new HashSet<string>(ExpectedMaps);
        var issues = new List<string>();

        CheckPerMapDict(
            config.SelectToken("questing.bot_questing_requirements.hearing_sensor.max_suspicious_time"),
            "hearing_sensor.max_suspicious_time",
            mapSet,
            issues
        );
        CheckPerMapDict(config.SelectToken("questing.bot_quests.exfil_direction_weighting"), "exfil_direction_weighting", mapSet, issues);
        CheckPerMapDict(config.SelectToken("questing.zone_movement.convergence_per_map"), "convergence_per_map", mapSet, issues);
        CheckPerMapDict(
            config.SelectToken("bot_spawns.bot_cap_adjustments.map_specific_adjustments"),
            "map_specific_adjustments",
            mapSet,
            issues
        );
        CheckPerMapDict(config.SelectToken("bot_spawns.max_alive_bots"), "max_alive_bots", mapSet, issues);

        if (issues.Count > 0)
        {
            Assert.Fail("Per-map dictionary issues:\n" + string.Join("\n", issues.Select(i => "  - " + i)));
        }
    }

    #endregion

    #region Cross-File Consistency

    [Test]
    public void QuestCounts_AreReasonable()
    {
        // Verify quest counts haven't dramatically changed (regression guard)
        var counts = new Dictionary<string, int>();
        foreach (string map in ExpectedMaps)
        {
            JArray quests = LoadQuestFile(map);
            counts[map] = quests.Count;
        }

        // Every map should have at least 10 quests
        foreach (var kvp in counts)
        {
            Assert.That(kvp.Value, Is.GreaterThanOrEqualTo(10), $"{kvp.Key} has suspiciously few quests: {kvp.Value}");
        }

        // Total should be at least 500 across all maps
        int total = counts.Values.Sum();
        Assert.That(total, Is.GreaterThanOrEqualTo(500), $"Total quest count ({total}) seems too low");
    }

    [Test]
    public void AllPositionFields_HaveAllThreeComponents()
    {
        // Ensure no position objects are missing x, y, or z
        foreach (string map in ExpectedMaps)
        {
            JArray quests = LoadQuestFile(map);
            foreach (JObject quest in quests)
            {
                string name = quest.Value<string>("name") ?? "unnamed";
                foreach (JObject obj in quest.Value<JArray>("objectives") ?? new JArray())
                {
                    JArray steps = obj.Value<JArray>("steps");
                    if (steps == null)
                        continue;

                    for (int i = 0; i < steps.Count; i++)
                    {
                        JObject step = (JObject)steps[i];
                        JObject pos = step.Value<JObject>("position");
                        if (pos == null)
                            continue;

                        Assert.That(pos["x"], Is.Not.Null, $"{map}: quest \"{name}\" step[{i}] position missing x");
                        Assert.That(pos["y"], Is.Not.Null, $"{map}: quest \"{name}\" step[{i}] position missing y");
                        Assert.That(pos["z"], Is.Not.Null, $"{map}: quest \"{name}\" step[{i}] position missing z");
                    }
                }
            }
        }
    }

    [TestCaseSource(nameof(ExpectedMaps))]
    public void AllQuests_HaveAtLeastOneValidPosition(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name") ?? "unnamed";
            bool hasValidPosition = false;

            foreach (JObject obj in quest.Value<JArray>("objectives") ?? new JArray())
            {
                JArray steps = obj.Value<JArray>("steps");
                if (steps == null || steps.Count == 0)
                    continue;

                JObject firstStep = (JObject)steps[0];
                JObject pos = firstStep.Value<JObject>("position");
                if (pos != null)
                {
                    float x = pos.Value<float>("x");
                    float y = pos.Value<float>("y");
                    float z = pos.Value<float>("z");

                    if (float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z))
                    {
                        hasValidPosition = true;
                        break;
                    }
                }
            }

            Assert.That(hasValidPosition, Is.True, $"{map}: quest \"{name}\" has no objective with a valid first-step position");
        }
    }

    #endregion

    #region Helpers

    private JArray LoadQuestFile(string map)
    {
        string path = Path.Combine(_questDir, map + ".json");
        return JArray.Parse(File.ReadAllText(path));
    }

    private JObject LoadEftQuestSettings()
    {
        string path = Path.Combine(_configDir, "eftQuestSettings.json");
        return JObject.Parse(File.ReadAllText(path));
    }

    private JObject LoadZonePositions()
    {
        string path = Path.Combine(_configDir, "zoneAndItemQuestPositions.json");
        return JObject.Parse(File.ReadAllText(path));
    }

    private IEnumerable<(string questName, int stepIndex, JObject position)> EnumerateStepPositions(string map)
    {
        JArray quests = LoadQuestFile(map);
        foreach (JObject quest in quests)
        {
            string name = quest.Value<string>("name") ?? "unnamed";
            foreach (JObject obj in quest.Value<JArray>("objectives") ?? new JArray())
            {
                JArray steps = obj.Value<JArray>("steps");
                if (steps == null)
                    continue;

                for (int i = 0; i < steps.Count; i++)
                {
                    JObject step = (JObject)steps[i];
                    JObject pos = step.Value<JObject>("position");
                    if (pos != null)
                    {
                        yield return (name, i, pos);
                    }
                }
            }
        }
    }

    private static void CheckPerMapDict(JToken token, string name, HashSet<string> expectedMaps, List<string> issues)
    {
        if (token == null || token.Type != JTokenType.Object)
        {
            issues.Add($"{name}: not found or not an object");
            return;
        }

        JObject dict = (JObject)token;
        var keys = new HashSet<string>(dict.Properties().Select(p => p.Name));
        keys.Remove("default"); // "default" is optional fallback, not a map

        foreach (string map in expectedMaps)
        {
            if (!keys.Contains(map))
            {
                issues.Add($"{name}: missing map entry \"{map}\"");
            }
        }
    }

    #endregion
}

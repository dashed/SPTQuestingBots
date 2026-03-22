using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration;

/// <summary>
/// Round 15 config pipeline audit tests.
/// Verifies JSON-to-C# consistency, dead config detection, round-trip fidelity,
/// and that utility task scorers read config values at scoring time.
/// </summary>
[TestFixture]
public class ConfigPipelineAuditTests
{
    private JObject _configJson;
    private string _configText;

    [OneTimeSetUp]
    public void LoadConfigJson()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        string configPath = null;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "config", "config.json");
            if (File.Exists(candidate))
            {
                configPath = candidate;
                break;
            }
            dir = Path.GetDirectoryName(dir);
            if (dir == null)
                break;
        }

        Assert.That(configPath, Is.Not.Null, "Could not find config/config.json from test directory");
        _configText = File.ReadAllText(configPath);
        _configJson = JObject.Parse(_configText);
    }

    // ──────────────────────────────────────────────────────────────
    //  Area 1: JSON ↔ C# Key Consistency (per-section)
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void VultureConfig_AllJsonKeysMatchCSharpProperties()
    {
        AssertJsonKeysMatchType<VultureConfig>(_configJson["questing"]?["vulture"] as JObject, "questing.vulture");
    }

    [Test]
    public void LingerConfig_AllJsonKeysMatchCSharpProperties()
    {
        AssertJsonKeysMatchType<LingerConfig>(_configJson["questing"]?["linger"] as JObject, "questing.linger");
    }

    [Test]
    public void InvestigateConfig_AllJsonKeysMatchCSharpProperties()
    {
        AssertJsonKeysMatchType<InvestigateConfig>(_configJson["questing"]?["investigate"] as JObject, "questing.investigate");
    }

    [Test]
    public void PatrolConfig_AllJsonKeysMatchCSharpProperties()
    {
        AssertJsonKeysMatchType<PatrolConfig>(_configJson["questing"]?["patrol"] as JObject, "questing.patrol");
    }

    [Test]
    public void LootingConfig_AllJsonKeysMatchCSharpProperties()
    {
        AssertJsonKeysMatchType<LootingConfig>(_configJson["questing"]?["looting"] as JObject, "questing.looting");
    }

    [Test]
    public void SpawnEntryConfig_AllJsonKeysMatchCSharpProperties()
    {
        AssertJsonKeysMatchType<SpawnEntryConfig>(_configJson["questing"]?["spawn_entry"] as JObject, "questing.spawn_entry");
    }

    [Test]
    public void RoomClearConfig_AllJsonKeysMatchCSharpProperties()
    {
        AssertJsonKeysMatchType<RoomClearConfig>(_configJson["questing"]?["room_clear"] as JObject, "questing.room_clear");
    }

    [Test]
    public void LookVarianceConfig_AllJsonKeysMatchCSharpProperties()
    {
        AssertJsonKeysMatchType<LookVarianceConfig>(_configJson["questing"]?["look_variance"] as JObject, "questing.look_variance");
    }

    [Test]
    public void DynamicObjectiveConfig_AllJsonKeysMatchCSharpProperties()
    {
        AssertJsonKeysMatchType<DynamicObjectiveConfig>(
            _configJson["questing"]?["dynamic_objectives"] as JObject,
            "questing.dynamic_objectives"
        );
    }

    [Test]
    public void PersonalityConfig_AllJsonKeysMatchCSharpProperties()
    {
        AssertJsonKeysMatchType<PersonalityConfig>(_configJson["questing"]?["personality"] as JObject, "questing.personality");
    }

    // ──────────────────────────────────────────────────────────────
    //  Area 2: JSON Round-Trip Test (per-section)
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void VultureConfig_JsonRoundTrip_PreservesAllKeys()
    {
        AssertRoundTrip<VultureConfig>(_configJson["questing"]?["vulture"] as JObject, "vulture");
    }

    [Test]
    public void LingerConfig_JsonRoundTrip_PreservesAllKeys()
    {
        AssertRoundTrip<LingerConfig>(_configJson["questing"]?["linger"] as JObject, "linger");
    }

    [Test]
    public void InvestigateConfig_JsonRoundTrip_PreservesAllKeys()
    {
        AssertRoundTrip<InvestigateConfig>(_configJson["questing"]?["investigate"] as JObject, "investigate");
    }

    // ──────────────────────────────────────────────────────────────
    //  Area 3: Nested Config Null Safety
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void ConfigClasses_DefaultToNonNull()
    {
        Assert.That(new VultureConfig(), Is.Not.Null);
        Assert.That(new LingerConfig(), Is.Not.Null);
        Assert.That(new InvestigateConfig(), Is.Not.Null);
        Assert.That(new PatrolConfig(), Is.Not.Null);
        Assert.That(new SpawnEntryConfig(), Is.Not.Null);
        Assert.That(new RoomClearConfig(), Is.Not.Null);
        Assert.That(new LookVarianceConfig(), Is.Not.Null);
        Assert.That(new DynamicObjectiveConfig(), Is.Not.Null);
        Assert.That(new PersonalityConfig(), Is.Not.Null);
        Assert.That(new LootingConfig(), Is.Not.Null);
        Assert.That(new BotLodConfig(), Is.Not.Null);
        Assert.That(new SquadStrategyConfig(), Is.Not.Null);
        Assert.That(new ZoneMovementConfig(), Is.Not.Null);
        Assert.That(new SprintingLimitationsConfig(), Is.Not.Null);
        Assert.That(new DebugConfig(), Is.Not.Null);
    }

    [Test]
    public void EmptyJsonObject_DeserializesToDefaultVultureConfig()
    {
        var deserialized = JsonConvert.DeserializeObject<VultureConfig>("{}");
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized.CourageThreshold, Is.EqualTo(15));
        Assert.That(deserialized.BaseDetectionRange, Is.EqualTo(150.0f));
    }

    [Test]
    public void EmptyJsonObject_DeserializesToDefaultLingerConfig()
    {
        var deserialized = JsonConvert.DeserializeObject<LingerConfig>("{}");
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized.BaseScore, Is.EqualTo(0.45f));
        Assert.That(deserialized.DurationMin, Is.EqualTo(10.0f));
    }

    [Test]
    public void EmptyJsonObject_DeserializesToDefaultInvestigateConfig()
    {
        var deserialized = JsonConvert.DeserializeObject<InvestigateConfig>("{}");
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized.IntensityThreshold, Is.EqualTo(5));
        Assert.That(deserialized.DetectionRange, Is.EqualTo(120.0f));
    }

    // ──────────────────────────────────────────────────────────────
    //  Area 4: VultureTask reads config at scoring time (BUG 1 fix)
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void VultureTask_Score_UsesPassedCourageThreshold()
    {
        var entity = CreateEntity();
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 20;
        entity.NearbyEventX = entity.CurrentPositionX + 50;
        entity.NearbyEventZ = entity.CurrentPositionZ + 50;

        // With default threshold of 15, intensity 20 should score > 0
        float score15 = VultureTask.Score(entity, 15, 150f);
        Assert.That(score15, Is.GreaterThan(0f), "Score with threshold=15 and intensity=20 should be > 0");

        // With threshold of 25, intensity 20 should score 0
        float score25 = VultureTask.Score(entity, 25, 150f);
        Assert.That(score25, Is.EqualTo(0f), "Score with threshold=25 and intensity=20 should be 0");
    }

    [Test]
    public void VultureTask_Score_UsesPassedDetectionRange()
    {
        var entity = CreateEntity();
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 30;
        // Place event far away
        entity.NearbyEventX = entity.CurrentPositionX + 200;
        entity.NearbyEventZ = entity.CurrentPositionZ + 200;
        entity.VisibleDist = 500f; // Extend vision range so event is within sight

        // With range=300, the event is in range
        float scoreWide = VultureTask.Score(entity, 15, 300f);
        // With range=50, the event is out of range (proximity=0, intensity still contributes)
        float scoreNarrow = VultureTask.Score(entity, 15, 50f);

        // Wide range should give higher score due to proximity contribution
        Assert.That(scoreWide, Is.GreaterThan(scoreNarrow));
    }

    [Test]
    public void InvestigateTask_Score_UsesPassedIntensityThreshold()
    {
        var entity = CreateEntity();
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 8;
        entity.NearbyEventX = entity.CurrentPositionX + 30;
        entity.NearbyEventZ = entity.CurrentPositionZ + 30;

        // With default threshold of 5, intensity 8 should score > 0
        float score5 = InvestigateTask.Score(entity, 5, 120f);
        Assert.That(score5, Is.GreaterThan(0f), "Score with threshold=5 and intensity=8 should be > 0");

        // With threshold of 10, intensity 8 should score 0
        float score10 = InvestigateTask.Score(entity, 10, 120f);
        Assert.That(score10, Is.EqualTo(0f), "Score with threshold=10 and intensity=8 should be 0");
    }

    [Test]
    public void InvestigateTask_Score_UsesPassedDetectionRange()
    {
        var entity = CreateEntity();
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 15;
        entity.NearbyEventX = entity.CurrentPositionX + 100;
        entity.NearbyEventZ = entity.CurrentPositionZ + 100;

        float scoreWide = InvestigateTask.Score(entity, 5, 250f);
        float scoreNarrow = InvestigateTask.Score(entity, 5, 50f);

        Assert.That(scoreWide, Is.GreaterThan(scoreNarrow));
    }

    [Test]
    public void LingerTask_Score_UsesPassedBaseScore()
    {
        var entity = CreateEntity();
        entity.ObjectiveCompletedTime = 10f;
        entity.CurrentGameTime = 10.5f;
        entity.LingerDuration = 20f;

        float scoreHigh = LingerTask.Score(entity, 0.80f);
        float scoreLow = LingerTask.Score(entity, 0.20f);

        Assert.That(scoreHigh, Is.GreaterThan(scoreLow), "Higher base score should produce higher score");
        Assert.That(scoreHigh, Is.GreaterThan(0f));
        Assert.That(scoreLow, Is.GreaterThan(0f));
    }

    // ──────────────────────────────────────────────────────────────
    //  Area 5: Deserialization Accuracy
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void Deserialized_VultureConfig_MatchesJsonValues()
    {
        var json = _configJson["questing"]?["vulture"]?.ToString();
        Assert.That(json, Is.Not.Null);
        var v = JsonConvert.DeserializeObject<VultureConfig>(json);
        Assert.That(v.Enabled, Is.True);
        Assert.That(v.BaseDetectionRange, Is.EqualTo(150.0f));
        Assert.That(v.CourageThreshold, Is.EqualTo(15));
        Assert.That(v.AmbushDuration, Is.EqualTo(90.0f));
        Assert.That(v.AmbushDistanceMin, Is.EqualTo(25.0f));
        Assert.That(v.AmbushDistanceMax, Is.EqualTo(30.0f));
        Assert.That(v.SilenceTriggerDuration, Is.EqualTo(45.0f));
        Assert.That(v.EnableSilentApproach, Is.True);
        Assert.That(v.SilentApproachDistance, Is.EqualTo(35.0f));
        Assert.That(v.EnableFlashlightDiscipline, Is.True);
        Assert.That(v.EnableParanoia, Is.True);
        Assert.That(v.ParanoiaIntervalMin, Is.EqualTo(3.0f));
        Assert.That(v.ParanoiaIntervalMax, Is.EqualTo(6.0f));
        Assert.That(v.ParanoiaAngleRange, Is.EqualTo(45.0f));
        Assert.That(v.EnableBaiting, Is.True);
        Assert.That(v.BaitingChance, Is.EqualTo(25));
        Assert.That(v.EnableBossAvoidance, Is.True);
        Assert.That(v.BossAvoidanceRadius, Is.EqualTo(75.0f));
        Assert.That(v.BossZoneDecay, Is.EqualTo(120.0f));
        Assert.That(v.MovementTimeout, Is.EqualTo(90.0f));
    }

    [Test]
    public void Deserialized_LingerConfig_MatchesJsonValues()
    {
        var json = _configJson["questing"]?["linger"]?.ToString();
        Assert.That(json, Is.Not.Null);
        var l = JsonConvert.DeserializeObject<LingerConfig>(json);
        Assert.That(l.Enabled, Is.True);
        Assert.That(l.BaseScore, Is.EqualTo(0.45f));
        Assert.That(l.DurationMin, Is.EqualTo(10.0f));
        Assert.That(l.DurationMax, Is.EqualTo(30.0f));
        Assert.That(l.HeadScanIntervalMin, Is.EqualTo(3.0f));
        Assert.That(l.HeadScanIntervalMax, Is.EqualTo(8.0f));
        Assert.That(l.Pose, Is.EqualTo(0.7f));
    }

    [Test]
    public void Deserialized_InvestigateConfig_MatchesJsonValues()
    {
        var json = _configJson["questing"]?["investigate"]?.ToString();
        Assert.That(json, Is.Not.Null);
        var i = JsonConvert.DeserializeObject<InvestigateConfig>(json);
        Assert.That(i.Enabled, Is.True);
        Assert.That(i.BaseScore, Is.EqualTo(0.40f));
        Assert.That(i.IntensityThreshold, Is.EqualTo(5));
        Assert.That(i.DetectionRange, Is.EqualTo(120.0f));
        Assert.That(i.MovementTimeout, Is.EqualTo(45.0f));
        Assert.That(i.ApproachSpeed, Is.EqualTo(0.5f));
        Assert.That(i.ApproachPose, Is.EqualTo(0.6f));
        Assert.That(i.ArrivalDistance, Is.EqualTo(15.0f));
        Assert.That(i.LookAroundDuration, Is.EqualTo(8.0f));
    }

    [Test]
    public void Deserialized_PatrolConfig_MatchesJsonValues()
    {
        var json = _configJson["questing"]?["patrol"]?.ToString();
        Assert.That(json, Is.Not.Null);
        var p = JsonConvert.DeserializeObject<PatrolConfig>(json);
        Assert.That(p.Enabled, Is.True);
        Assert.That(p.BaseScore, Is.EqualTo(0.50f));
        Assert.That(p.CooldownSec, Is.EqualTo(120f));
        Assert.That(p.WaypointArrivalRadius, Is.EqualTo(3f));
        Assert.That(p.Pose, Is.EqualTo(0.85f));
    }

    [Test]
    public void Deserialized_SpawnEntryConfig_MatchesJsonValues()
    {
        var json = _configJson["questing"]?["spawn_entry"]?.ToString();
        Assert.That(json, Is.Not.Null);
        var s = JsonConvert.DeserializeObject<SpawnEntryConfig>(json);
        Assert.That(s.Enabled, Is.True);
        Assert.That(s.BaseDurationMin, Is.EqualTo(3.0f));
        Assert.That(s.BaseDurationMax, Is.EqualTo(5.0f));
        Assert.That(s.SquadStaggerPerMember, Is.EqualTo(1.5f));
        Assert.That(s.DirectionBiasDuration, Is.EqualTo(30.0f));
        Assert.That(s.DirectionBiasStrength, Is.EqualTo(0.05f));
        Assert.That(s.Pose, Is.EqualTo(0.85f));
    }

    [Test]
    public void Deserialized_RoomClearConfig_MatchesJsonValues()
    {
        var json = _configJson["questing"]?["room_clear"]?.ToString();
        Assert.That(json, Is.Not.Null);
        var r = JsonConvert.DeserializeObject<RoomClearConfig>(json);
        Assert.That(r.Enabled, Is.True);
        Assert.That(r.DurationMin, Is.EqualTo(15.0f));
        Assert.That(r.DurationMax, Is.EqualTo(30.0f));
        Assert.That(r.CornerPauseDuration, Is.EqualTo(1.2f));
        Assert.That(r.CornerAngleThreshold, Is.EqualTo(60.0f));
        Assert.That(r.Pose, Is.EqualTo(0.7f));
        Assert.That(r.WalkThroughDistance, Is.EqualTo(0.75f));
        Assert.That(r.LookRaycastDistance, Is.EqualTo(30.0f));
        Assert.That(r.LookDuration, Is.EqualTo(1.2f));
    }

    [Test]
    public void Deserialized_LookVarianceConfig_MatchesJsonValues()
    {
        var json = _configJson["questing"]?["look_variance"]?.ToString();
        Assert.That(json, Is.Not.Null);
        var lv = JsonConvert.DeserializeObject<LookVarianceConfig>(json);
        Assert.That(lv.Enabled, Is.True);
        Assert.That(lv.FlankCheckIntervalMin, Is.EqualTo(5.0f));
        Assert.That(lv.FlankCheckIntervalMax, Is.EqualTo(15.0f));
        Assert.That(lv.PoiGlanceIntervalMin, Is.EqualTo(8.0f));
        Assert.That(lv.PoiGlanceIntervalMax, Is.EqualTo(20.0f));
        Assert.That(lv.PoiDetectionRange, Is.EqualTo(20.0f));
        Assert.That(lv.SquadGlanceRange, Is.EqualTo(15.0f));
        Assert.That(lv.CombatEventLookChance, Is.EqualTo(0.7f));
    }

    [Test]
    public void Deserialized_LootingConfig_MatchesJsonValues()
    {
        var json = _configJson["questing"]?["looting"]?.ToString();
        Assert.That(json, Is.Not.Null);
        var l = JsonConvert.DeserializeObject<LootingConfig>(json);
        Assert.That(l.Enabled, Is.True);
        Assert.That(l.DetectContainerDistance, Is.EqualTo(60f));
        Assert.That(l.DetectItemDistance, Is.EqualTo(40f));
        Assert.That(l.DetectCorpseDistance, Is.EqualTo(50f));
        Assert.That(l.ScanIntervalSeconds, Is.EqualTo(5.0f));
        Assert.That(l.MinItemValue, Is.EqualTo(5000));
        Assert.That(l.MaxConcurrentLooters, Is.EqualTo(5));
    }

    [Test]
    public void Deserialized_DynamicObjectiveConfig_MatchesJsonValues()
    {
        var json = _configJson["questing"]?["dynamic_objectives"]?.ToString();
        Assert.That(json, Is.Not.Null);
        var d = JsonConvert.DeserializeObject<DynamicObjectiveConfig>(json);
        Assert.That(d.Enabled, Is.True);
        Assert.That(d.ScanIntervalSec, Is.EqualTo(30f));
        Assert.That(d.MaxActiveQuests, Is.EqualTo(10));
        Assert.That(d.FirefightEnabled, Is.True);
        Assert.That(d.FirefightMinIntensity, Is.EqualTo(3));
    }

    // ──────────────────────────────────────────────────────────────
    //  Area 6: Config Validation Gaps
    // ──────────────────────────────────────────────────────────────

    [Test]
    public void LookVariance_SampleInterval_ClampsToMinimum()
    {
        // Zero or negative intervals should be clamped
        float result = SPTQuestingBots.BotLogic.ECS.Systems.LookVarianceController.SampleInterval(0f, 0f);
        Assert.That(result, Is.GreaterThanOrEqualTo(0.5f), "Zero interval should be clamped to minimum 0.5s");
    }

    [Test]
    public void MinMaxConfig_DivisionByZero_ReturnsZero()
    {
        var a = new MinMaxConfig(10, 20);
        var zero = new MinMaxConfig(0, 0);
        var result = a / zero;
        Assert.That(result.Min, Is.EqualTo(0));
        Assert.That(result.Max, Is.EqualTo(0));
    }

    [Test]
    public void MinMaxConfig_ScalarDivisionByZero_ReturnsZero()
    {
        var a = new MinMaxConfig(10, 20);
        var result = a / 0.0;
        Assert.That(result.Min, Is.EqualTo(0));
        Assert.That(result.Max, Is.EqualTo(0));
    }

    // ──────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────

    private static BotEntity CreateEntity()
    {
        var entity = new BotEntity(999);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        return entity;
    }

    /// <summary>
    /// Verifies that every key in the JSON object has a matching [JsonProperty] in the C# type,
    /// and every [JsonProperty] in the C# type has a matching key in the JSON object.
    /// </summary>
    private static void AssertJsonKeysMatchType<T>(JObject json, string sectionName)
    {
        Assert.That(json, Is.Not.Null, "JSON section '" + sectionName + "' not found in config.json");

        var props = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonPropertyAttribute>() != null)
            .ToDictionary(p => p.GetCustomAttribute<JsonPropertyAttribute>().PropertyName, p => p);

        var missingInCSharp = new List<string>();
        var missingInJson = new List<string>();

        foreach (var property in json.Properties())
        {
            if (!props.ContainsKey(property.Name))
            {
                missingInCSharp.Add(property.Name);
            }
        }

        foreach (var kvp in props)
        {
            if (json[kvp.Key] == null)
            {
                missingInJson.Add(kvp.Key);
            }
        }

        if (missingInCSharp.Count > 0)
        {
            Assert.Fail(sectionName + ": JSON keys without matching C# [JsonProperty]: " + string.Join(", ", missingInCSharp));
        }

        if (missingInJson.Count > 0)
        {
            Assert.Fail(sectionName + ": C# [JsonProperty] without matching JSON key: " + string.Join(", ", missingInJson));
        }
    }

    /// <summary>
    /// Verifies that deserializing and re-serializing a JSON section preserves all keys.
    /// </summary>
    private static void AssertRoundTrip<T>(JObject json, string sectionName)
    {
        Assert.That(json, Is.Not.Null, "JSON section '" + sectionName + "' not found");

        var original = json.ToString();
        var deserialized = JsonConvert.DeserializeObject<T>(original);
        var reserialized = JsonConvert.SerializeObject(deserialized, Formatting.Indented);
        var reparsed = JObject.Parse(reserialized);

        var originalKeys = GetAllLeafPaths(json).OrderBy(k => k).ToList();
        var roundTripKeys = GetAllLeafPaths(reparsed).OrderBy(k => k).ToList();

        var lostKeys = originalKeys.Except(roundTripKeys).ToList();
        if (lostKeys.Count > 0)
        {
            Assert.Fail(sectionName + ": Keys lost during round-trip: " + string.Join(", ", lostKeys));
        }
    }

    private static List<string> GetAllLeafPaths(JToken token, string prefix = "")
    {
        var paths = new List<string>();
        if (token is JObject obj)
        {
            foreach (var prop in obj.Properties())
            {
                string path = string.IsNullOrEmpty(prefix) ? prop.Name : prefix + "." + prop.Name;
                paths.AddRange(GetAllLeafPaths(prop.Value, path));
            }
        }
        else if (token is JArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                paths.AddRange(GetAllLeafPaths(arr[i], prefix + "[" + i + "]"));
            }
        }
        else
        {
            paths.Add(prefix);
        }
        return paths;
    }
}

using System;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Models.Questing;

/// <summary>
/// Tests for improved NavMesh validation tolerance (Task #6).
///
/// Bug: Quest trigger points near NavMesh edges would fail to snap because the
/// default search distances were too tight (item=1.5, zone=1.5, spawn=2, doors=0.75).
/// Also, TrySnapToNavMesh had no fallback — a single failed attempt meant the
/// objective was permanently unreachable.
///
/// Fix 1: Increased default search distances (item=2.5, zone=5, spawn=5, doors=1.5).
/// Fix 2: Added fallback retry in TrySnapToNavMesh with Max(3 * maxDistance, 10f).
/// Fix 3: Updated config.json to match new defaults.
/// </summary>
[TestFixture]
public class NavMeshToleranceTests
{
    // ── QuestGenerationConfig default values ─────────────────────────

    [Test]
    public void NavMeshSearchDistanceItem_Default_Is2_5()
    {
        var config = new QuestGenerationConfig();
        Assert.That(config.NavMeshSearchDistanceItem, Is.EqualTo(2.5f));
    }

    [Test]
    public void NavMeshSearchDistanceZone_Default_Is5()
    {
        var config = new QuestGenerationConfig();
        Assert.That(config.NavMeshSearchDistanceZone, Is.EqualTo(5f));
    }

    [Test]
    public void NavMeshSearchDistanceSpawn_Default_Is5()
    {
        var config = new QuestGenerationConfig();
        Assert.That(config.NavMeshSearchDistanceSpawn, Is.EqualTo(5f));
    }

    [Test]
    public void NavMeshSearchDistanceDoors_Default_Is1_5()
    {
        var config = new QuestGenerationConfig();
        Assert.That(config.NavMeshSearchDistanceDoors, Is.EqualTo(1.5f));
    }

    // ── Defaults are greater than old values ─────────────────────────

    [Test]
    public void NavMeshSearchDistanceItem_IncreasedFromOldDefault()
    {
        var config = new QuestGenerationConfig();
        float oldDefault = 1.5f;
        Assert.That(config.NavMeshSearchDistanceItem, Is.GreaterThan(oldDefault));
    }

    [Test]
    public void NavMeshSearchDistanceZone_IncreasedFromOldDefault()
    {
        var config = new QuestGenerationConfig();
        float oldDefault = 1.5f;
        Assert.That(config.NavMeshSearchDistanceZone, Is.GreaterThan(oldDefault));
    }

    [Test]
    public void NavMeshSearchDistanceSpawn_IncreasedFromOldDefault()
    {
        var config = new QuestGenerationConfig();
        float oldDefault = 2f;
        Assert.That(config.NavMeshSearchDistanceSpawn, Is.GreaterThan(oldDefault));
    }

    [Test]
    public void NavMeshSearchDistanceDoors_IncreasedFromOldDefault()
    {
        var config = new QuestGenerationConfig();
        float oldDefault = 0.75f;
        Assert.That(config.NavMeshSearchDistanceDoors, Is.GreaterThan(oldDefault));
    }

    // ── Fallback distance calculation: Math.Max(3 * maxDistance, 10f) ─

    [Test]
    public void FallbackDistance_SmallMaxDistance_UsesMinimumOf10()
    {
        float maxDistance = 1.5f;
        float fallback = Math.Max(3 * maxDistance, 10f);
        Assert.That(fallback, Is.EqualTo(10f));
    }

    [Test]
    public void FallbackDistance_MediumMaxDistance_UsesTriple()
    {
        float maxDistance = 5f;
        float fallback = Math.Max(3 * maxDistance, 10f);
        Assert.That(fallback, Is.EqualTo(15f));
    }

    [Test]
    public void FallbackDistance_LargeMaxDistance_UsesTriple()
    {
        float maxDistance = 10f;
        float fallback = Math.Max(3 * maxDistance, 10f);
        Assert.That(fallback, Is.EqualTo(30f));
    }

    [Test]
    public void FallbackDistance_ExactlyAtThreshold_Uses10()
    {
        // 3 * 3.333... = 10, threshold boundary
        float maxDistance = 10f / 3f;
        float fallback = Math.Max(3 * maxDistance, 10f);
        Assert.That(fallback, Is.EqualTo(10f).Within(0.001f));
    }

    [Test]
    public void FallbackDistance_AlwaysGreaterThanOriginal()
    {
        float[] distances = { 0.5f, 1f, 1.5f, 2f, 2.5f, 5f, 10f };
        foreach (float maxDistance in distances)
        {
            float fallback = Math.Max(3 * maxDistance, 10f);
            Assert.That(fallback, Is.GreaterThan(maxDistance), $"Fallback {fallback} should be greater than original {maxDistance}");
        }
    }

    [Test]
    public void FallbackDistance_ForItemDefault_Is10()
    {
        var config = new QuestGenerationConfig();
        float fallback = Math.Max(3 * config.NavMeshSearchDistanceItem, 10f);
        Assert.That(fallback, Is.EqualTo(10f).Within(0.001f));
    }

    [Test]
    public void FallbackDistance_ForZoneDefault_Is15()
    {
        var config = new QuestGenerationConfig();
        float fallback = Math.Max(3 * config.NavMeshSearchDistanceZone, 10f);
        Assert.That(fallback, Is.EqualTo(15f));
    }

    [Test]
    public void FallbackDistance_ForSpawnDefault_Is15()
    {
        var config = new QuestGenerationConfig();
        float fallback = Math.Max(3 * config.NavMeshSearchDistanceSpawn, 10f);
        Assert.That(fallback, Is.EqualTo(15f));
    }

    [Test]
    public void FallbackDistance_ForDoorsDefault_Is10()
    {
        var config = new QuestGenerationConfig();
        float fallback = Math.Max(3 * config.NavMeshSearchDistanceDoors, 10f);
        Assert.That(fallback, Is.EqualTo(10f).Within(0.001f));
    }

    // ── Config deserialization ───────────────────────────────────────

    [Test]
    public void QuestGenerationConfig_DeserializesFromJson()
    {
        string json =
            @"{
            ""navmesh_search_distance_item"": 3.0,
            ""navmesh_search_distance_zone"": 6.0,
            ""navmesh_search_distance_spawn"": 7.0,
            ""navmesh_search_distance_doors"": 2.0
        }";

        var config = Newtonsoft.Json.JsonConvert.DeserializeObject<QuestGenerationConfig>(json);

        Assert.That(config.NavMeshSearchDistanceItem, Is.EqualTo(3f));
        Assert.That(config.NavMeshSearchDistanceZone, Is.EqualTo(6f));
        Assert.That(config.NavMeshSearchDistanceSpawn, Is.EqualTo(7f));
        Assert.That(config.NavMeshSearchDistanceDoors, Is.EqualTo(2f));
    }

    [Test]
    public void QuestGenerationConfig_MissingFields_UsesDefaults()
    {
        string json = "{}";
        var config = Newtonsoft.Json.JsonConvert.DeserializeObject<QuestGenerationConfig>(json);

        Assert.That(config.NavMeshSearchDistanceItem, Is.EqualTo(2.5f));
        Assert.That(config.NavMeshSearchDistanceZone, Is.EqualTo(5f));
        Assert.That(config.NavMeshSearchDistanceSpawn, Is.EqualTo(5f));
        Assert.That(config.NavMeshSearchDistanceDoors, Is.EqualTo(1.5f));
    }
}

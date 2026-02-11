using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration;

[TestFixture]
public class PatrolConfigTests
{
    [Test]
    public void DefaultConstructor_SetsCorrectDefaults()
    {
        var config = new PatrolConfig();

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.True);
            Assert.That(config.BaseScore, Is.EqualTo(0.50f));
            Assert.That(config.CooldownSec, Is.EqualTo(120f));
            Assert.That(config.WaypointArrivalRadius, Is.EqualTo(3f));
            Assert.That(config.Pose, Is.EqualTo(0.85f));
            Assert.That(config.EnableForPmcs, Is.True);
            Assert.That(config.EnableForScavs, Is.True);
            Assert.That(config.EnableForPscavs, Is.True);
            Assert.That(config.RoutesPerMap, Is.Not.Null);
            Assert.That(config.RoutesPerMap.Count, Is.EqualTo(0));
        });
    }

    [Test]
    public void Deserialize_EmptyJson_UsesDefaults()
    {
        var config = JsonConvert.DeserializeObject<PatrolConfig>("{}");

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.True);
            Assert.That(config.BaseScore, Is.EqualTo(0.50f));
            Assert.That(config.CooldownSec, Is.EqualTo(120f));
            Assert.That(config.WaypointArrivalRadius, Is.EqualTo(3f));
            Assert.That(config.Pose, Is.EqualTo(0.85f));
            Assert.That(config.EnableForPmcs, Is.True);
            Assert.That(config.EnableForScavs, Is.True);
            Assert.That(config.EnableForPscavs, Is.True);
        });
    }

    [Test]
    public void Deserialize_AllProperties_FromJson()
    {
        var json =
            @"{
                    ""enabled"": false,
                    ""base_score"": 0.60,
                    ""cooldown_sec"": 180,
                    ""waypoint_arrival_radius"": 5,
                    ""pose"": 0.9,
                    ""enable_for_pmcs"": false,
                    ""enable_for_scavs"": false,
                    ""enable_for_pscavs"": false,
                    ""routes_per_map"": {}
                }";
        var config = JsonConvert.DeserializeObject<PatrolConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.False);
            Assert.That(config.BaseScore, Is.EqualTo(0.60f).Within(0.01f));
            Assert.That(config.CooldownSec, Is.EqualTo(180f));
            Assert.That(config.WaypointArrivalRadius, Is.EqualTo(5f));
            Assert.That(config.Pose, Is.EqualTo(0.9f));
            Assert.That(config.EnableForPmcs, Is.False);
            Assert.That(config.EnableForScavs, Is.False);
            Assert.That(config.EnableForPscavs, Is.False);
        });
    }

    [Test]
    public void Deserialize_PartialOverride_OtherDefaultsIntact()
    {
        var json = @"{ ""enabled"": false, ""base_score"": 0.70 }";
        var config = JsonConvert.DeserializeObject<PatrolConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.False);
            Assert.That(config.BaseScore, Is.EqualTo(0.70f).Within(0.01f));
            Assert.That(config.CooldownSec, Is.EqualTo(120f));
            Assert.That(config.WaypointArrivalRadius, Is.EqualTo(3f));
            Assert.That(config.Pose, Is.EqualTo(0.85f));
            Assert.That(config.EnableForPmcs, Is.True);
        });
    }

    [Test]
    public void Deserialize_WithRouteOverrides()
    {
        var json =
            @"{
                    ""routes_per_map"": {
                        ""bigmap"": [
                            {
                                ""name"": ""Custom Route"",
                                ""type"": ""Interior"",
                                ""waypoints"": [
                                    { ""x"": 1, ""y"": 2, ""z"": 3, ""pause_min"": 1, ""pause_max"": 2 }
                                ],
                                ""min_aggression"": 0.2,
                                ""max_aggression"": 0.8,
                                ""is_loop"": false
                            }
                        ]
                    }
                }";
        var config = JsonConvert.DeserializeObject<PatrolConfig>(json);

        Assert.That(config.RoutesPerMap, Has.Count.EqualTo(1));
        Assert.That(config.RoutesPerMap.ContainsKey("bigmap"), Is.True);

        var routes = config.RoutesPerMap["bigmap"];
        Assert.That(routes.Length, Is.EqualTo(1));
        Assert.That(routes[0].Name, Is.EqualTo("Custom Route"));
        Assert.That(routes[0].Type, Is.EqualTo("Interior"));
        Assert.That(routes[0].Waypoints.Length, Is.EqualTo(1));
        Assert.That(routes[0].Waypoints[0].X, Is.EqualTo(1f));
        Assert.That(routes[0].Waypoints[0].Y, Is.EqualTo(2f));
        Assert.That(routes[0].Waypoints[0].Z, Is.EqualTo(3f));
        Assert.That(routes[0].Waypoints[0].PauseMin, Is.EqualTo(1f));
        Assert.That(routes[0].Waypoints[0].PauseMax, Is.EqualTo(2f));
        Assert.That(routes[0].MinAggression, Is.EqualTo(0.2f));
        Assert.That(routes[0].MaxAggression, Is.EqualTo(0.8f));
        Assert.That(routes[0].IsLoop, Is.False);
    }

    [Test]
    public void RoundTrip_PreservesAllValues()
    {
        var original = new PatrolConfig
        {
            Enabled = false,
            BaseScore = 0.60f,
            CooldownSec = 180f,
            WaypointArrivalRadius = 5f,
            Pose = 0.9f,
            EnableForPmcs = false,
            EnableForScavs = false,
            EnableForPscavs = false,
            RoutesPerMap = new Dictionary<string, PatrolRouteEntry[]>
            {
                ["bigmap"] = new[]
                {
                    new PatrolRouteEntry
                    {
                        Name = "Test",
                        Type = "Perimeter",
                        Waypoints = new[]
                        {
                            new PatrolWaypointEntry
                            {
                                X = 10f,
                                Y = 5f,
                                Z = 20f,
                            },
                        },
                    },
                },
            },
        };

        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<PatrolConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.Enabled, Is.EqualTo(original.Enabled));
            Assert.That(deserialized.BaseScore, Is.EqualTo(original.BaseScore));
            Assert.That(deserialized.CooldownSec, Is.EqualTo(original.CooldownSec));
            Assert.That(deserialized.WaypointArrivalRadius, Is.EqualTo(original.WaypointArrivalRadius));
            Assert.That(deserialized.Pose, Is.EqualTo(original.Pose));
            Assert.That(deserialized.EnableForPmcs, Is.EqualTo(original.EnableForPmcs));
            Assert.That(deserialized.EnableForScavs, Is.EqualTo(original.EnableForScavs));
            Assert.That(deserialized.EnableForPscavs, Is.EqualTo(original.EnableForPscavs));
            Assert.That(deserialized.RoutesPerMap.Count, Is.EqualTo(1));
            Assert.That(deserialized.RoutesPerMap.ContainsKey("bigmap"), Is.True);
        });
    }

    [Test]
    public void Serialize_IncludesJsonPropertyNames()
    {
        var config = new PatrolConfig();
        var json = JsonConvert.SerializeObject(config);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("enabled"));
            Assert.That(json, Does.Contain("base_score"));
            Assert.That(json, Does.Contain("cooldown_sec"));
            Assert.That(json, Does.Contain("waypoint_arrival_radius"));
            Assert.That(json, Does.Contain("pose"));
            Assert.That(json, Does.Contain("enable_for_pmcs"));
            Assert.That(json, Does.Contain("enable_for_scavs"));
            Assert.That(json, Does.Contain("enable_for_pscavs"));
            Assert.That(json, Does.Contain("routes_per_map"));
        });
    }
}

[TestFixture]
public class PatrolRouteEntryTests
{
    [Test]
    public void DefaultValues()
    {
        var entry = new PatrolRouteEntry();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Name, Is.EqualTo(""));
            Assert.That(entry.Type, Is.EqualTo("Perimeter"));
            Assert.That(entry.Waypoints, Is.Not.Null);
            Assert.That(entry.Waypoints.Length, Is.EqualTo(0));
            Assert.That(entry.MinAggression, Is.EqualTo(0f));
            Assert.That(entry.MaxAggression, Is.EqualTo(1f));
            Assert.That(entry.MinRaidTime, Is.EqualTo(0f));
            Assert.That(entry.MaxRaidTime, Is.EqualTo(1f));
            Assert.That(entry.IsLoop, Is.True);
        });
    }
}

[TestFixture]
public class PatrolWaypointEntryTests
{
    [Test]
    public void DefaultValues()
    {
        var entry = new PatrolWaypointEntry();

        Assert.Multiple(() =>
        {
            Assert.That(entry.X, Is.EqualTo(0f));
            Assert.That(entry.Y, Is.EqualTo(0f));
            Assert.That(entry.Z, Is.EqualTo(0f));
            Assert.That(entry.PauseMin, Is.EqualTo(2f));
            Assert.That(entry.PauseMax, Is.EqualTo(5f));
        });
    }

    [Test]
    public void Deserialize_FromJson()
    {
        var json = @"{ ""x"": 10, ""y"": 5, ""z"": 20, ""pause_min"": 3, ""pause_max"": 7 }";
        var entry = JsonConvert.DeserializeObject<PatrolWaypointEntry>(json);

        Assert.Multiple(() =>
        {
            Assert.That(entry.X, Is.EqualTo(10f));
            Assert.That(entry.Y, Is.EqualTo(5f));
            Assert.That(entry.Z, Is.EqualTo(20f));
            Assert.That(entry.PauseMin, Is.EqualTo(3f));
            Assert.That(entry.PauseMax, Is.EqualTo(7f));
        });
    }
}

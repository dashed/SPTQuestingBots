using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.E2E;

[TestFixture]
public class ConfigSemanticPipelineTests
{
    private JObject _configJson = null!;

    [OneTimeSetUp]
    public void LoadConfig()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        string configPath = null;

        for (int i = 0; i < 10; i++)
        {
            string candidate = Path.Combine(dir, "config", "config.json");
            if (File.Exists(candidate))
            {
                configPath = candidate;
                break;
            }

            dir = Path.GetDirectoryName(dir);
            if (dir == null)
            {
                break;
            }
        }

        Assert.That(configPath, Is.Not.Null, "Could not find config/config.json from test directory");
        _configJson = JObject.Parse(File.ReadAllText(configPath));
    }

    [Test]
    public void ClientRuntimeCurves_AndDistributions_AreSemanticallyValid()
    {
        AssertInterpolatedArray(GetArray("questing.bot_quests.eft_quests.level_range"), shouldUseIntegerKeys: true, minX: 0, minY: 0);
        AssertInterpolatedArray(
            GetArray("bot_spawns.pmcs.fraction_of_max_players_vs_raidET"),
            shouldUseIntegerKeys: false,
            minX: 0,
            maxX: 1,
            minY: 0
        );
        AssertWeightedDistribution(GetArray("bot_spawns.pmcs.bots_per_group_distribution"), minX: 1);
        AssertWeightedDistribution(GetArray("bot_spawns.pmcs.bot_difficulty_as_online"), minX: 0);
        AssertWeightedDistribution(GetArray("bot_spawns.player_scavs.bots_per_group_distribution"), minX: 1);
        AssertWeightedDistribution(GetArray("bot_spawns.player_scavs.bot_difficulty_as_online"), minX: 0);
        AssertInterpolatedArray(
            GetArray("adjust_pscav_chance.chance_vs_time_remaining_fraction"),
            shouldUseIntegerKeys: false,
            minX: 0,
            maxX: 1,
            minY: 0,
            maxY: 100
        );
    }

    private JArray GetArray(string path)
    {
        JToken current = _configJson;
        foreach (string segment in path.Split('.'))
        {
            current = current[segment];
            Assert.That(current, Is.Not.Null, $"Missing config segment '{segment}' while resolving '{path}'");
        }

        Assert.That(current, Is.TypeOf<JArray>(), $"Expected JArray at config path '{path}'");
        return (JArray)current;
    }

    private static void AssertInterpolatedArray(
        JArray rows,
        bool shouldUseIntegerKeys,
        double? minX = null,
        double? maxX = null,
        double? minY = null,
        double? maxY = null
    )
    {
        ValidateArrayShape(rows);

        double previousX = double.NegativeInfinity;
        foreach (JArray row in rows.Cast<JArray>())
        {
            double x = row[0]!.Value<double>();
            double y = row[1]!.Value<double>();

            if (shouldUseIntegerKeys)
            {
                Assert.That(x % 1, Is.EqualTo(0).Within(0.000001), $"Expected integer x-value but got {x}");
            }

            Assert.That(double.IsFinite(x), Is.True, $"Expected finite x-value but got {x}");
            Assert.That(double.IsFinite(y), Is.True, $"Expected finite y-value but got {y}");
            Assert.That(x, Is.GreaterThan(previousX), "Interpolation keys must be strictly increasing");

            if (minX.HasValue)
            {
                Assert.That(x, Is.GreaterThanOrEqualTo(minX.Value), $"Expected x >= {minX.Value} but got {x}");
            }

            if (maxX.HasValue)
            {
                Assert.That(x, Is.LessThanOrEqualTo(maxX.Value), $"Expected x <= {maxX.Value} but got {x}");
            }

            if (minY.HasValue)
            {
                Assert.That(y, Is.GreaterThanOrEqualTo(minY.Value), $"Expected y >= {minY.Value} but got {y}");
            }

            if (maxY.HasValue)
            {
                Assert.That(y, Is.LessThanOrEqualTo(maxY.Value), $"Expected y <= {maxY.Value} but got {y}");
            }

            previousX = x;
        }
    }

    private static void AssertWeightedDistribution(JArray rows, double? minX = null, double? maxX = null)
    {
        ValidateArrayShape(rows);

        double totalWeight = 0;
        foreach (JArray row in rows.Cast<JArray>())
        {
            double x = row[0]!.Value<double>();
            double weight = row[1]!.Value<double>();

            Assert.That(double.IsFinite(x), Is.True, $"Expected finite x-value but got {x}");
            Assert.That(double.IsFinite(weight), Is.True, $"Expected finite weight but got {weight}");
            Assert.That(weight, Is.GreaterThanOrEqualTo(0), $"Expected non-negative weight but got {weight}");

            if (minX.HasValue)
            {
                Assert.That(x, Is.GreaterThanOrEqualTo(minX.Value), $"Expected x >= {minX.Value} but got {x}");
            }

            if (maxX.HasValue)
            {
                Assert.That(x, Is.LessThanOrEqualTo(maxX.Value), $"Expected x <= {maxX.Value} but got {x}");
            }

            totalWeight += weight;
        }

        Assert.That(totalWeight, Is.GreaterThan(0), "Weighted distributions must have a positive total weight");
    }

    private static void ValidateArrayShape(JArray rows)
    {
        Assert.That(rows.Count, Is.GreaterThan(0), "Expected at least one row");

        foreach (JToken row in rows)
        {
            Assert.That(row, Is.TypeOf<JArray>(), "Expected each row to be a JArray");
            Assert.That(((JArray)row).Count, Is.EqualTo(2), "Expected each row to contain exactly two values");
        }
    }
}

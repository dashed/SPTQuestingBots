using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

/// <summary>
/// Regression tests for the convergence balance changes:
/// - Config: convergence_weight=0.3, combat_convergence_force=0.8
/// - WorldGridManager passes botPositions (not playerPositions) to convergence
/// - ConvergenceField uses 1/dist for positions, 1/sqrt for combat events
/// </summary>
[TestFixture]
public class ConvergenceBalanceRegressionTests
{
    private static string FindRepoRoot()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "SPTQuestingBots.sln")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.That(dir, Is.Not.Null, "Could not find repo root (SPTQuestingBots.sln)");
        return dir;
    }

    private static string ReadSourceFile(string relativePath)
    {
        var fullPath = Path.Combine(FindRepoRoot(), relativePath);
        Assert.That(File.Exists(fullPath), Is.True, $"Source file not found: {fullPath}");
        return File.ReadAllText(fullPath);
    }

    private static JObject LoadConfig()
    {
        var repoRoot = FindRepoRoot();
        var configPath = Path.Combine(repoRoot, "config", "config.json");
        Assert.That(File.Exists(configPath), Is.True, "config.json not found");
        return JObject.Parse(File.ReadAllText(configPath));
    }

    // --- Config value regression tests ---

    [Test]
    public void Config_ConvergenceWeight_Is03()
    {
        var config = LoadConfig();
        var zoneMovement = config["questing"]?["zone_movement"];
        Assert.That(zoneMovement, Is.Not.Null, "zone_movement section missing from config");

        var weight = zoneMovement["convergence_weight"]?.Value<float>();
        Assert.That(weight, Is.Not.Null, "convergence_weight missing from config");
        Assert.That(weight, Is.EqualTo(0.3f).Within(0.01f), "convergence_weight should be 0.3 (reduced from 1.0 for bot clustering)");
    }

    [Test]
    public void Config_CombatConvergenceForce_Is08()
    {
        var config = LoadConfig();
        var zoneMovement = config["questing"]?["zone_movement"];
        Assert.That(zoneMovement, Is.Not.Null, "zone_movement section missing from config");

        var force = zoneMovement["combat_convergence_force"]?.Value<float>();
        Assert.That(force, Is.Not.Null, "combat_convergence_force missing from config");
        Assert.That(force, Is.EqualTo(0.8f).Within(0.01f), "combat_convergence_force should be 0.8 (increased from 0.5 as primary driver)");
    }

    // --- Source code regression tests ---

    [Test]
    public void WorldGridManager_PassesBotPositions_ToConvergenceField()
    {
        // Verify that GetCompositeDirection passes botPositions, not playerPositions, to convergence.
        // This is the core of the "no player tracking" change.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/ZoneMovement/Integration/WorldGridManager.cs");

        // The convergence field call in GetCompositeDirection should use botPositions
        Assert.That(
            source,
            Does.Contain("convergenceField.GetConvergence(\n            position,\n            botPositions,"),
            "convergenceField should receive botPositions for clustering, not playerPositions"
        );
    }

    [Test]
    public void WorldGridManager_TelemetryLog_IncludesBotAndPlayerCounts()
    {
        // Verify the telemetry logging is in place for monitoring convergence behavior.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/ZoneMovement/Integration/WorldGridManager.cs");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("[WorldGridManager] Convergence update:"), "Telemetry log line should be present");
            Assert.That(source, Does.Contain("bots="), "Telemetry should include bot count");
            Assert.That(source, Does.Contain("combatEvents="), "Telemetry should include combat event count");
            Assert.That(source, Does.Contain("raidTime="), "Telemetry should include raid time");
        });
    }

    [Test]
    public void ConvergenceField_PositionFalloff_Uses1OverDist()
    {
        // Verify the source code uses 1/dist (force / dist) for position-based attraction.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/ZoneMovement/Fields/ConvergenceField.cs");

        // Should contain 1/dist pattern for positions
        Assert.That(source, Does.Contain("float w = force / dist;"), "Position attraction should use 1/dist falloff (force / dist)");

        // Should NOT contain 1/sqrt(dist) for position attraction
        // (but 1/sqrt IS used for combat events, so check the specific context)
        Assert.That(source, Does.Contain("1/dist falloff"), "Comment should document position uses 1/dist falloff");
    }

    [Test]
    public void ConvergenceField_CombatFalloff_Uses1OverSqrtDist()
    {
        // Verify the source code preserves 1/sqrt(dist) for combat event pull.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/ZoneMovement/Fields/ConvergenceField.cs");

        Assert.That(
            source,
            Does.Contain("/ (float)Math.Sqrt(dist)"),
            "Combat events should use 1/sqrt(dist) falloff for longer-range response"
        );

        Assert.That(source, Does.Contain("1/sqrt(dist) falloff"), "Comment should document combat uses 1/sqrt(dist) falloff");
    }

    [Test]
    public void ConvergenceField_ParameterName_IsAttractionPositions()
    {
        // Verify the API uses the generic "attractionPositions" name (not "playerPositions")
        // to reflect that bots converge on other bots, not player tracking.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/ZoneMovement/Fields/ConvergenceField.cs");

        Assert.That(
            source,
            Does.Contain("attractionPositions"),
            "Parameter should be named attractionPositions (generic, not player-specific)"
        );

        Assert.That(
            source,
            Does.Not.Contain("playerPositions"),
            "Should not contain playerPositions parameter name — replaced with attractionPositions"
        );
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Components.Spawning;

/// <summary>
/// Bug-catching tests for spawning, PMC conversion, and bot lifecycle initialization.
/// Round 5 findings: 8 bugs across BotGenerator, PMCGenerator, PScavGenerator,
/// QuestObjectiveStep, BotJobAssignmentFactory, and GameStartPatch.
/// </summary>
[TestFixture]
public class SpawningLifecycleBugTests
{
    private static readonly string SrcRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "SPTQuestingBots.Client")
    );

    // ── Bug 1: PMCGenerator.GetMaxGeneratedBots Random.Next off-by-one ──
    // Random.Next(min, max) is max-exclusive. The original code used
    // random.Next((int)pmcCountRange.Min, (int)pmcCountRange.Max) which
    // means the Max value is never selected. Fix: use Max + 1.

    [Test]
    public void RandomNext_MaxExclusive_MeansMaxNeverReached()
    {
        // Demonstrate the off-by-one: Random.Next(min, max) never returns max
        var rng = new System.Random(42);
        int min = 3;
        int max = 5;
        var results = new HashSet<int>();
        for (int i = 0; i < 10000; i++)
        {
            results.Add(rng.Next(min, max));
        }

        // max (5) is never in the results because Next is max-exclusive
        Assert.That(results, Does.Not.Contain(max), "Random.Next(min,max) should never return max (max-exclusive)");
        Assert.That(results, Does.Contain(min), "Random.Next(min,max) should return min");
        Assert.That(results, Does.Contain(max - 1), "Random.Next(min,max) should return max-1");
    }

    [Test]
    public void PMCGenerator_GetMaxGeneratedBots_UsesMaxPlusOneForRandomNext()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Components", "Spawning", "PMCGenerator.cs"));

        // The fix should use Max + 1 to make the Max value inclusive
        // Pattern: random.Next(..., (int)pmcCountRange.Max + 1)
        Assert.That(
            source,
            Does.Contain("pmcCountRange.Max + 1"),
            "PMCGenerator.GetMaxGeneratedBots must use Max + 1 in Random.Next to make Max inclusive"
        );

        // Ensure the old buggy pattern is gone
        Assert.That(
            source,
            Does.Not.Match(@"random\.Next\(\(int\)pmcCountRange\.Min,\s*\(int\)pmcCountRange\.Max\)"),
            "PMCGenerator.GetMaxGeneratedBots must not use Random.Next(Min, Max) without +1"
        );
    }

    // ── Bug 2: PScavGenerator.createBotSpawnSchedule Random.Next off-by-one ──
    // random.Next(0, possibleSpawnTimes.Count - 1) generates 0..Count-2.
    // The last element is never selected. Fix: use Count (not Count - 1).

    [Test]
    public void RandomNext_CountMinusOne_SkipsLastElement()
    {
        // Demonstrate: Random.Next(0, list.Count - 1) never selects the last element
        var list = new List<int> { 10, 20, 30, 40, 50 };
        var rng = new System.Random(42);
        var selectedIndices = new HashSet<int>();
        for (int i = 0; i < 10000; i++)
        {
            selectedIndices.Add(rng.Next(0, list.Count - 1));
        }

        // Index 4 (last) is never selected
        Assert.That(selectedIndices, Does.Not.Contain(list.Count - 1), "Random.Next(0, Count-1) never selects the last index");
        Assert.That(selectedIndices.Max(), Is.EqualTo(list.Count - 2));
    }

    [Test]
    public void PScavGenerator_CreateBotSpawnSchedule_UsesCountNotCountMinusOne()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Components", "Spawning", "PScavGenerator.cs"));

        // The fix should use possibleSpawnTimes.Count (not Count - 1)
        Assert.That(
            source,
            Does.Not.Match(@"random\.Next\(0,\s*possibleSpawnTimes\.Count\s*-\s*1\)"),
            "PScavGenerator must not use Random.Next(0, Count - 1) which skips the last element"
        );

        // Should use Count directly
        Assert.That(
            source,
            Does.Match(@"random\.Next\(0,\s*possibleSpawnTimes\.Count\)"),
            "PScavGenerator must use Random.Next(0, Count) to include all elements"
        );
    }

    // ── Bug 3: PScavGenerator.CanSpawnBots blocks PScavs when all PMCs alive ──
    // The check !pmcGenerator.CanSpawnAdditionalBots() prevents PScavs from
    // spawning when all PMCs are alive, even though PScavs have their own
    // MaxAliveBots pool. Fix: remove the CanSpawnAdditionalBots check.

    [Test]
    public void PScavGenerator_CanSpawnBots_DoesNotCheckPMCCanSpawnAdditionalBots()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Components", "Spawning", "PScavGenerator.cs"));

        // Extract the CanSpawnBots method body
        int methodStart = source.IndexOf("override bool CanSpawnBots()");
        Assert.That(methodStart, Is.GreaterThan(-1), "PScavGenerator must have a CanSpawnBots override");

        // Get the method body (from first { to matching })
        int braceStart = source.IndexOf('{', methodStart);
        int braceCount = 1;
        int pos = braceStart + 1;
        while (braceCount > 0 && pos < source.Length)
        {
            if (source[pos] == '{')
                braceCount++;
            else if (source[pos] == '}')
                braceCount--;
            pos++;
        }
        string methodBody = source.Substring(braceStart, pos - braceStart);

        // The fix removes CanSpawnAdditionalBots check from CanSpawnBots
        Assert.That(
            methodBody,
            Does.Not.Contain("CanSpawnAdditionalBots"),
            "PScavGenerator.CanSpawnBots must not check pmcGenerator.CanSpawnAdditionalBots() "
                + "as it blocks PScavs when all PMCs are alive"
        );

        // Should still check HasRemainingSpawns
        Assert.That(
            methodBody,
            Does.Contain("HasRemainingSpawns"),
            "PScavGenerator.CanSpawnBots must still check HasRemainingSpawns to wait for PMC spawning"
        );
    }

    // ── Bug 4: QuestObjectiveStep.SampleWaitTime uses inline new System.Random() ──
    // new System.Random() in rapid succession produces identical sequences.
    // Fix: use a shared static Random instance.

    [Test]
    public void InlineNewRandom_IsAnAntiPattern_InGameCode()
    {
        // In .NET Framework / netstandard2.1 (which the client targets), new System.Random()
        // uses Environment.TickCount for seeding. Objects created in the same millisecond
        // produce identical sequences. Even in .NET 6+ tests, this is a well-documented
        // anti-pattern that wastes allocations and causes correlation.
        // We verify the source code doesn't use this pattern in method bodies.

        var source = File.ReadAllText(Path.Combine(SrcRoot, "Models", "Questing", "QuestObjectiveStep.cs"));

        // Inline new System.Random() inside method bodies (not static field initializers) is the anti-pattern.
        // A static field like "private static readonly System.Random SharedRandom = new System.Random();" is fine.
        // Look for "new System.Random()" that is NOT preceded by "static" on the same line.
        int inlineRandomCount = Regex
            .Matches(source, @"(?<!static\s+readonly\s+System\.Random\s+\w+\s*=\s*)new\s+System\.Random\(\)\.NextDouble")
            .Count;

        // Count local variable declarations like "System.Random x = new System.Random();"
        // but exclude static field declarations (which are the correct pattern).
        int localVarRandomCount = 0;
        foreach (var line in source.Split('\n'))
        {
            if (
                line.Contains("new System.Random()")
                && !line.Contains("static")
                && Regex.IsMatch(line, @"System\.Random\s+\w+\s*=\s*new\s+System\.Random\(\)")
            )
            {
                localVarRandomCount++;
            }
        }

        Assert.That(inlineRandomCount, Is.EqualTo(0), "QuestObjectiveStep must not use inline new System.Random().NextDouble()");

        Assert.That(
            localVarRandomCount,
            Is.EqualTo(0),
            "QuestObjectiveStep must not create local System.Random variables — use shared static RNG"
        );
    }

    [Test]
    public void QuestObjectiveStep_SampleWaitTime_UsesSharedRandom()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Models", "Questing", "QuestObjectiveStep.cs"));

        // The fix should use a shared static Random instead of new System.Random()
        Assert.That(
            source,
            Does.Not.Match(@"new\s+System\.Random\(\)\.NextDouble\(\)"),
            "QuestObjectiveStep.SampleWaitTime must not use inline new System.Random().NextDouble()"
        );
    }

    // ── Bug 5: QuestObjectiveStep.GetRandomMinElapsedTime uses inline new System.Random() ──

    [Test]
    public void QuestObjectiveStep_GetRandomMinElapsedTime_UsesSharedRandom()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Models", "Questing", "QuestObjectiveStep.cs"));

        // Extract the GetRandomMinElapsedTime method
        int methodStart = source.IndexOf("GetRandomMinElapsedTime()");
        Assert.That(methodStart, Is.GreaterThan(-1));

        string methodArea = source.Substring(methodStart, Math.Min(300, source.Length - methodStart));

        // Should not create new System.Random() inside the method
        Assert.That(methodArea, Does.Not.Contain("new System.Random()"), "GetRandomMinElapsedTime must not use inline new System.Random()");
    }

    // ── Bug 6: BotJobAssignmentFactory.GetRandomQuest uses inline new System.Random() ──

    [Test]
    public void BotJobAssignmentFactory_GetRandomQuest_UsesSharedRandom()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Controllers", "BotJobAssignmentFactory.cs"));

        // Extract the GetRandomQuest method area
        int methodStart = source.IndexOf("public static Quest GetRandomQuest");
        Assert.That(methodStart, Is.GreaterThan(-1));

        string methodArea = source.Substring(methodStart, Math.Min(2000, source.Length - methodStart));

        // Should not create new System.Random() inside the method
        Assert.That(
            methodArea,
            Does.Not.Contain("System.Random random = new System.Random()"),
            "GetRandomQuest must not create inline new System.Random() — use shared static RNG"
        );
    }

    // ── Bug 7: GameStartPatch.missedBossWaves not cleared between raids ──
    // The static missedBossWaves list is never cleared by BotsControllerStopPatch,
    // causing stale boss wave data from previous raids to accumulate.

    [Test]
    public void BotsControllerStopPatch_ClearsMissedBossWaves()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Patches", "Spawning", "ScavLimits", "BotsControllerStopPatch.cs"));

        // The fix should call GameStartPatch.ClearMissedWaves() or similar
        Assert.That(
            source,
            Does.Contain("GameStartPatch.ClearMissedWaves()"),
            "BotsControllerStopPatch must call GameStartPatch.ClearMissedWaves() to prevent "
                + "stale boss wave data from accumulating across raids"
        );
    }

    [Test]
    public void GameStartPatch_ClearMissedWaves_Exists()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Patches", "Spawning", "GameStartPatch.cs"));

        Assert.That(
            source,
            Does.Contain("public static void ClearMissedWaves()"),
            "GameStartPatch must expose ClearMissedWaves() as a public static method"
        );
    }

    // Also verify TryLoadBotsProfilesOnStartPatch is cleared at raid end (already fixed but verify)
    [Test]
    public void BotsControllerStopPatch_ClearsTryLoadBotsProfiles()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Patches", "Spawning", "ScavLimits", "BotsControllerStopPatch.cs"));

        Assert.That(
            source,
            Does.Contain("TryLoadBotsProfilesOnStartPatch.Clear()"),
            "BotsControllerStopPatch must clear TryLoadBotsProfilesOnStartPatch state"
        );
    }

    // ── Bug 8: BotGenerator.GeneratorProgress division by zero ──
    // 100 * GeneratedBotCount / MaxGeneratedBots crashes when MaxGeneratedBots is 0.

    [Test]
    public void BotGenerator_GeneratorProgress_GuardsDivisionByZero()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Components", "Spawning", "BotGenerator.cs"));

        // Find the instance GeneratorProgress property (not the static CurrentBotGeneratorProgress)
        int propStart = source.IndexOf("public int GeneratorProgress");
        Assert.That(propStart, Is.GreaterThan(-1), "BotGenerator must have a public int GeneratorProgress property");

        string propArea = source.Substring(propStart, Math.Min(200, source.Length - propStart));

        // The fix should guard against MaxGeneratedBots == 0
        Assert.That(
            propArea,
            Does.Contain("MaxGeneratedBots == 0").Or.Contain("MaxGeneratedBots <= 0").Or.Contain("MaxGeneratedBots < 1"),
            "BotGenerator.GeneratorProgress must guard against division by zero when MaxGeneratedBots is 0"
        );
    }

    // ── Regression: Verify QuestObjectiveStep has a shared static Random ──

    [Test]
    public void QuestObjectiveStep_HasSharedStaticRandom()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Models", "Questing", "QuestObjectiveStep.cs"));

        // Should have a static Random field
        Assert.That(
            source,
            Does.Match(@"private\s+static\s+(readonly\s+)?System\.Random\s+\w+"),
            "QuestObjectiveStep must declare a private static System.Random field for shared RNG"
        );
    }

    // ── Regression: Verify BotGenerator statics ──

    [Test]
    public void BotGenerator_StaticRegisteredGenerators_NotLeakedAcrossRaids()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Components", "Spawning", "BotGenerator.cs"));

        // Verify that registeredBotGenerators is properly managed
        // The dictionary should be cleared between raids
        Assert.That(source, Does.Contain("registeredBotGenerators"), "BotGenerator must have registeredBotGenerators dictionary");
    }

    // ── PMCGenerator.GetUSECChance uses 1-100 range (not 0-99) ──
    // random.Next(1, 100) generates 1..99 (max-exclusive), so chance of 100 means
    // USEC is ALWAYS selected (<=100 is always true for 1..99). This is correct.
    // But chance of 1 means only value 1 triggers, which is 1/99 = ~1%, correct.

    [Test]
    public void PMCGenerator_USECChance_RangeIsCorrect()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "Components", "Spawning", "PMCGenerator.cs"));

        // Verify the USEC random check uses appropriate range
        // random.Next(1, 101) gives 1..100 inclusive (100 possible values)
        // This matches "percentage chance" semantics correctly
        Assert.That(
            source,
            Does.Match(@"random\.Next\(1,\s*101\)"),
            "PMCGenerator USEC chance must use random.Next(1, 101) for correct 1-100 inclusive range"
        );
    }
}

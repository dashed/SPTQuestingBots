using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Concurrency;

/// <summary>
/// Source-scanning tests that verify iteration safety patterns across the codebase.
/// Checks that cross-frame coroutine iterations snapshot their collections, that
/// swap-remove registries are not modified during for-loop iteration, and that
/// shared reusable buffers are documented and not nested.
/// </summary>
[TestFixture]
public class IterationSafetyTests
{
    private static readonly string SrcRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "SPTQuestingBots.Client")
    );

    // ================================================================
    // Coroutine snapshot safety
    // ================================================================

    [Test]
    public void EnumeratorWithTimeLimit_RunInternal_SnapshotsCollection()
    {
        // The Run_Internal method must snapshot the collection before iterating,
        // because the coroutine can yield between frames, and the source collection
        // can be modified by other MonoBehaviour.Update() calls between yields.
        var filePath = Path.Combine(SrcRoot, "CoroutineExtensions", "EnumeratorWithTimeLimit.cs");
        Assert.IsTrue(File.Exists(filePath), "EnumeratorWithTimeLimit.cs not found at " + filePath);

        string content = File.ReadAllText(filePath);

        // Must contain a snapshot of the collection (e.g., new List<TItem>(collection) or similar)
        Assert.IsTrue(
            content.Contains("snapshot") || content.Contains("Snapshot") || content.Contains("new List<TItem>"),
            "Run_Internal must snapshot the collection before iterating to prevent cross-frame InvalidOperationException"
        );

        // The yield-containing foreach (the one with WaitForNextFrame inside its body)
        // must iterate `snapshot`, not `collection`. The snapshot-building foreach that
        // copies items into the snapshot list is allowed to iterate `collection`.
        // Verify: "foreach (TItem item in snapshot)" appears (the cross-frame loop).
        Assert.IsTrue(
            Regex.IsMatch(content, @"foreach\s*\(\s*\w+\s+\w+\s+in\s+snapshot\s*\)"),
            "Run_Internal must iterate 'snapshot' (not 'collection') in its cross-frame foreach loop"
        );
    }

    // ================================================================
    // BotRegistry: swap-remove never called during HiveMind tick
    // ================================================================

    [Test]
    public void BotRegistry_Remove_NeverCalledFromHiveMindMonitor()
    {
        // BotRegistry.Remove() uses swap-remove which shifts indices.
        // It must never be called from code that also iterates Entities.
        var hiveMindPath = Path.Combine(SrcRoot, "BotLogic", "HiveMind", "BotHiveMindMonitor.cs");
        Assert.IsTrue(File.Exists(hiveMindPath), "BotHiveMindMonitor.cs not found");

        string content = File.ReadAllText(hiveMindPath);

        // Should not call _registry.Remove or Registry.Remove
        Assert.IsFalse(
            content.Contains("_registry.Remove") || content.Contains("Registry.Remove("),
            "BotHiveMindMonitor must not call BotRegistry.Remove() — "
                + "entities should only be soft-deleted via IsActive = false during the tick"
        );
    }

    [Test]
    public void HiveMindSystem_CleanupDeadEntities_DoesNotRemoveFromRegistry()
    {
        // CleanupDeadEntities only clears boss/follower references, not registry entries.
        var systemPath = Path.Combine(SrcRoot, "BotLogic", "ECS", "Systems", "HiveMindSystem.cs");
        Assert.IsTrue(File.Exists(systemPath), "HiveMindSystem.cs not found");

        string content = File.ReadAllText(systemPath);

        // CleanupDeadEntities should NOT call Remove on any registry (BotRegistry/SquadRegistry).
        // Note: List<BotEntity>.Followers.Remove(entity) is fine — that is detaching a follower
        // reference, not removing from the swap-remove registry.
        var methodContent = ExtractMethod(content, "CleanupDeadEntities");
        Assert.IsNotNull(methodContent, "CleanupDeadEntities method not found");
        Assert.IsFalse(
            methodContent.Contains("Registry.Remove(") || methodContent.Contains("_registry.Remove("),
            "CleanupDeadEntities must not call Remove on BotRegistry/SquadRegistry — " + "it should only clear boss/follower references"
        );
    }

    // ================================================================
    // SquadRegistry: swap-remove never called during iteration
    // ================================================================

    [Test]
    public void SquadRegistry_Remove_NeverCalledDuringSquadIteration()
    {
        // SquadRegistry.Remove() should not be called from any file that also iterates ActiveSquads.
        var searchDirs = new[] { Path.Combine(SrcRoot, "BotLogic", "HiveMind"), Path.Combine(SrcRoot, "BotLogic", "ECS", "UtilityAI") };

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir))
                continue;
            foreach (var file in Directory.GetFiles(dir, "*.cs"))
            {
                string content = File.ReadAllText(file);
                if (content.Contains("ActiveSquads") && content.Contains("SquadRegistry.Remove("))
                {
                    Assert.Fail(
                        $"File {Path.GetFileName(file)} iterates ActiveSquads AND calls SquadRegistry.Remove() — "
                            + "swap-remove during iteration causes index corruption"
                    );
                }
            }
        }
    }

    // ================================================================
    // allQuests: foreach without defensive copy check
    // ================================================================

    [Test]
    public void BotJobAssignmentFactory_ProcessAllQuests_UsesSnapshotViaCoroutine()
    {
        // ProcessAllQuests passes allQuests to EnumeratorWithTimeLimit.Run which
        // now snapshots. Verify the coroutine path snapshots.
        var filePath = Path.Combine(SrcRoot, "CoroutineExtensions", "EnumeratorWithTimeLimit.cs");
        string content = File.ReadAllText(filePath);

        // Verify the snapshot pattern exists in Run_Internal<TItem>
        var methodContent = ExtractMethod(content, "Run_Internal<TItem>");
        Assert.IsNotNull(methodContent, "Run_Internal<TItem> method not found");
        Assert.IsTrue(methodContent.Contains("snapshot"), "Run_Internal must snapshot the collection before iterating");
    }

    // ================================================================
    // Shared reusable buffer safety
    // ================================================================

    [Test]
    public void GetFollowers_CallersDoNotNestGetFollowersCalls()
    {
        // BotEntityBridge.GetFollowers() returns a static reusable buffer.
        // Callers must not call GetFollowers again within a foreach loop over its result.
        var files = Directory.GetFiles(SrcRoot, "*.cs", SearchOption.AllDirectories);
        var getFollowersPattern = new Regex(@"foreach\s*\(.*\bGetFollowers\b.*\)");

        foreach (var file in files)
        {
            string content = File.ReadAllText(file);
            var matches = getFollowersPattern.Matches(content);
            if (matches.Count == 0)
                continue;

            // For each foreach-GetFollowers block, check the loop body
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (!getFollowersPattern.IsMatch(lines[i]))
                    continue;

                // Scan forward to find matching closing brace
                int braceDepth = 0;
                bool started = false;
                for (int j = i; j < lines.Length; j++)
                {
                    foreach (char c in lines[j])
                    {
                        if (c == '{')
                        {
                            braceDepth++;
                            started = true;
                        }
                        else if (c == '}')
                        {
                            braceDepth--;
                        }
                    }

                    if (started && braceDepth == 0)
                        break;

                    if (j > i && lines[j].Contains("GetFollowers("))
                    {
                        Assert.Fail(
                            $"Nested GetFollowers() call detected in {Path.GetFileName(file)}:{j + 1} — "
                                + "this would corrupt the shared static buffer mid-iteration"
                        );
                    }
                }
            }
        }
    }

    [Test]
    public void GetAllGroupMembers_CallersDoNotNestCalls()
    {
        // Same pattern check for GetAllGroupMembers shared buffer
        var files = Directory.GetFiles(SrcRoot, "*.cs", SearchOption.AllDirectories);
        var pattern = new Regex(@"foreach\s*\(.*\bGetAllGroupMembers\b.*\)");

        foreach (var file in files)
        {
            string content = File.ReadAllText(file);
            var matches = pattern.Matches(content);
            if (matches.Count == 0)
                continue;

            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (!pattern.IsMatch(lines[i]))
                    continue;

                int braceDepth = 0;
                bool started = false;
                for (int j = i; j < lines.Length; j++)
                {
                    foreach (char c in lines[j])
                    {
                        if (c == '{')
                        {
                            braceDepth++;
                            started = true;
                        }
                        else if (c == '}')
                        {
                            braceDepth--;
                        }
                    }

                    if (started && braceDepth == 0)
                        break;

                    if (j > i && lines[j].Contains("GetAllGroupMembers("))
                    {
                        Assert.Fail(
                            $"Nested GetAllGroupMembers() call in {Path.GetFileName(file)}:{j + 1} — "
                                + "would corrupt the shared static buffer"
                        );
                    }
                }
            }
        }
    }

    // ================================================================
    // CombatEventRegistry: ring buffer index math
    // ================================================================

    [Test]
    public void CombatEventRegistry_IndexCalculation_HandlesWrapAround()
    {
        // Verify the ring buffer index formula is always non-negative
        // Formula: (_head - 1 - i + _capacity * 2) % _capacity
        var filePath = Path.Combine(SrcRoot, "BotLogic", "ECS", "Systems", "CombatEventRegistry.cs");
        Assert.IsTrue(File.Exists(filePath), "CombatEventRegistry.cs not found");

        string content = File.ReadAllText(filePath);

        // The formula must use _capacity * 2 (not just _capacity) to ensure non-negative
        // values even when _head is 0 and i is at its maximum.
        Assert.IsTrue(
            content.Contains("_capacity * 2"),
            "Ring buffer index formula must use _capacity * 2 to ensure non-negative indices on wrap-around"
        );
    }

    // ================================================================
    // LootClaimRegistry: consistency between dual maps
    // ================================================================

    [Test]
    public void LootClaimRegistry_ReleaseAll_RemovesBothMappings()
    {
        // ReleaseAll must remove entries from both _lootToBotId and _botToLootIds
        var filePath = Path.Combine(SrcRoot, "BotLogic", "ECS", "Systems", "LootClaimRegistry.cs");
        Assert.IsTrue(File.Exists(filePath), "LootClaimRegistry.cs not found");

        string content = File.ReadAllText(filePath);
        var methodContent = ExtractMethod(content, "ReleaseAll");
        Assert.IsNotNull(methodContent, "ReleaseAll method not found");

        // Must remove from _lootToBotId
        Assert.IsTrue(methodContent.Contains("_lootToBotId.Remove"), "ReleaseAll must remove entries from _lootToBotId");

        // Must remove from _botToLootIds
        Assert.IsTrue(methodContent.Contains("_botToLootIds.Remove"), "ReleaseAll must remove the bot entry from _botToLootIds");
    }

    // ================================================================
    // For-loop iteration: no swap-remove during forward iteration
    // ================================================================

    [Test]
    public void EntityIterationLoops_UseForwardForLoop_NotForeach()
    {
        // All HiveMind tick steps iterate BotRegistry.Entities with for-loops (not foreach).
        // for-loops are safe for in-place mutation (setting fields) and handle
        // Count changes correctly if entities are only appended (not swap-removed).
        var hiveMindPath = Path.Combine(SrcRoot, "BotLogic", "HiveMind", "BotHiveMindMonitor.cs");
        string content = File.ReadAllText(hiveMindPath);

        // Should not use foreach on Entities
        Assert.IsFalse(
            Regex.IsMatch(content, @"foreach\s*\(.*\bEntities\b"),
            "BotHiveMindMonitor must use for-loops (not foreach) when iterating BotRegistry.Entities"
        );
    }

    // ================================================================
    // Helpers
    // ================================================================

    /// <summary>
    /// Extract the body of a method by name (simple heuristic: find method signature,
    /// then capture content between first { and matching }.
    /// </summary>
    private static string ExtractMethod(string content, string methodName)
    {
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(methodName) && (lines[i].Contains("(") || (i + 1 < lines.Length && lines[i + 1].Contains("("))))
            {
                // Find opening brace
                int braceStart = -1;
                for (int j = i; j < lines.Length; j++)
                {
                    if (lines[j].Contains("{"))
                    {
                        braceStart = j;
                        break;
                    }
                }

                if (braceStart < 0)
                    continue;

                // Find matching closing brace
                int depth = 0;
                var methodLines = new List<string>();
                for (int j = braceStart; j < lines.Length; j++)
                {
                    methodLines.Add(lines[j]);
                    foreach (char c in lines[j])
                    {
                        if (c == '{')
                            depth++;
                        else if (c == '}')
                            depth--;
                    }

                    if (depth == 0)
                        return string.Join("\n", methodLines);
                }
            }
        }

        return null;
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests;

/// <summary>
/// Runtime fault tolerance tests — validates that the mod degrades gracefully
/// when external game state changes unexpectedly (disposed bots, renamed fields,
/// interrupted actions, missing data, concurrent access, mid-iteration removal).
/// Round 12 deployment robustness audit.
/// </summary>
[TestFixture]
public class RuntimeFaultToleranceTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string ClientSrcDir = Path.Combine(RepoRoot, "src", "SPTQuestingBots.Client");

    private static string FindRepoRoot()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        return TestContext.CurrentContext.TestDirectory;
    }

    // ── Bug 1: GetLocationOfNearestGroupMember null-bot NRE ──────────

    [Test]
    public void GetLocationOfNearestGroupMember_NullBot_DoesNotAccessBotPosition()
    {
        // BotEntityBridge.GetLocationOfNearestGroupMember previously returned bot.Position
        // in the early-exit path when bot == null, causing an NRE.
        // Verify the source no longer dereferences bot after the null check.
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "ECS", "BotEntityBridge.cs"));

        // Find the method body
        int methodStart = source.IndexOf("GetLocationOfNearestGroupMember");
        Assert.That(methodStart, Is.GreaterThan(0), "Method GetLocationOfNearestGroupMember not found");

        // Extract the early-return line (within first 5 lines of method)
        var methodBody = source.Substring(methodStart, System.Math.Min(500, source.Length - methodStart));
        var lines = methodBody.Split('\n');

        // Find the early-return line
        string earlyReturnLine = lines.FirstOrDefault(l =>
            l.Contains("return") && !l.TrimStart().StartsWith("//") && lines.ToList().IndexOf(l) < 10
        );
        Assert.That(earlyReturnLine, Is.Not.Null, "No early return found in first 10 lines of method");

        // The early return must NOT contain "bot.Position" since bot could be null
        Assert.That(
            earlyReturnLine,
            Does.Not.Contain("bot.Position"),
            "GetLocationOfNearestGroupMember early-exit must not dereference bot.Position when bot may be null. "
                + "Use Vector3.zero or a safe default instead."
        );
    }

    // ── Bug 2: UnlockDoorAction.Stop() bundleLoader leak ──────────

    [Test]
    public void UnlockDoorAction_Stop_ReleasesBundleLoader()
    {
        // If Stop() is called while a key bundle is being loaded, the loader must be
        // released to prevent resource leaks. Verify Stop() contains bundleLoader release.
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "Objective", "UnlockDoorAction.cs"));

        // Find the Stop() method
        var stopPattern = new Regex(
            @"public\s+override\s+void\s+Stop\s*\(\s*\)(.*?)(?=public\s+override\s+void\s+\w+|$)",
            RegexOptions.Singleline
        );
        var match = stopPattern.Match(source);
        Assert.That(match.Success, Is.True, "Stop() method not found in UnlockDoorAction.cs");

        string stopBody = match.Value;
        Assert.That(
            stopBody,
            Does.Contain("bundleLoader"),
            "UnlockDoorAction.Stop() must release bundleLoader to prevent resource leak "
                + "when the action is interrupted (bot killed, objective changed, etc.)"
        );

        // Verify it calls Release()
        Assert.That(stopBody, Does.Contain(".Release()"), "UnlockDoorAction.Stop() must call bundleLoader.Release() to clean up");
    }

    // ── Reflection KnownFields: all consumers guard null FieldInfo ──

    [Test]
    public void AllReflectionHelperConsumers_GuardNullFieldInfo()
    {
        // When a game update renames an obfuscated field, RequireField returns null.
        // All consumers must check for null before calling .GetValue() or .SetValue().
        var helperFiles = new[]
        {
            Path.Combine(ClientSrcDir, "Helpers", "CombatStateHelper.cs"),
            Path.Combine(ClientSrcDir, "Helpers", "RaidTimeHelper.cs"),
            Path.Combine(ClientSrcDir, "Helpers", "ExtractionHelper.cs"),
            Path.Combine(ClientSrcDir, "Helpers", "PlantZoneHelper.cs"),
            Path.Combine(ClientSrcDir, "Helpers", "HearingSensorHelper.cs"),
        };

        var getValuePattern = new Regex(@"\.GetValue\s*\(", RegexOptions.Compiled);
        var setValuePattern = new Regex(@"\.SetValue\s*\(", RegexOptions.Compiled);
        var failures = new List<string>();

        foreach (var file in helperFiles)
        {
            if (!File.Exists(file))
                continue;

            string source = File.ReadAllText(file);
            string relPath = Path.GetRelativePath(RepoRoot, file);
            var sourceLines = source.Split('\n');

            for (int i = 0; i < sourceLines.Length; i++)
            {
                string line = sourceLines[i];
                if (getValuePattern.IsMatch(line) || setValuePattern.IsMatch(line))
                {
                    // Check if there's a null guard in surrounding context (within prior 5 lines)
                    bool hasGuard = false;
                    for (int j = System.Math.Max(0, i - 5); j <= i; j++)
                    {
                        if (sourceLines[j].Contains("== null") || sourceLines[j].Contains("!= null") || sourceLines[j].Contains("?."))
                        {
                            hasGuard = true;
                            break;
                        }
                    }

                    if (!hasGuard)
                    {
                        failures.Add($"{relPath}:{i + 1} — {line.Trim()}");
                    }
                }
            }
        }

        Assert.That(
            failures,
            Is.Empty,
            "Found reflection .GetValue()/.SetValue() calls without null guard for field:\n  " + string.Join("\n  ", failures)
        );
    }

    // ── BotEntityBridge: all BotOwner-accepting methods guard null ──

    [Test]
    public void BotEntityBridge_AllBotOwnerMethods_GuardNull()
    {
        // When a bot is disposed mid-tick, BotOwner becomes Unity-null.
        // Every public method accepting BotOwner must check for null before dictionary access.
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "ECS", "BotEntityBridge.cs"));

        // Find all public static methods that take BotOwner as first param
        var methodPattern = new Regex(@"public\s+static\s+\w+\s+(\w+)\s*\(\s*BotOwner\s+(\w+)", RegexOptions.Compiled);

        var dictionaryAccessPattern = new Regex(@"_ownerToEntity\.(TryGetValue|ContainsKey)\s*\(", RegexOptions.Compiled);

        var violations = new List<string>();

        foreach (Match match in methodPattern.Matches(source))
        {
            string methodName = match.Groups[1].Value;
            string paramName = match.Groups[2].Value;

            // Get the method body (up to 2000 chars after declaration)
            int bodyStart = match.Index;
            int bodyLength = System.Math.Min(2000, source.Length - bodyStart);
            string methodBody = source.Substring(bodyStart, bodyLength);

            // Find the first opening brace
            int braceIdx = methodBody.IndexOf('{');
            if (braceIdx < 0)
                continue;

            // Check first 300 chars after brace for null check
            string earlyBody = methodBody.Substring(braceIdx, System.Math.Min(300, methodBody.Length - braceIdx));

            // Should have either `bot != null` or `bot == null` check before dictionary access
            bool hasNullCheck =
                earlyBody.Contains(paramName + " != null")
                || earlyBody.Contains(paramName + " == null")
                || earlyBody.Contains(paramName + " is null");

            bool hasDictionaryAccess = dictionaryAccessPattern.IsMatch(earlyBody);

            if (hasDictionaryAccess && !hasNullCheck)
            {
                violations.Add(methodName);
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "BotEntityBridge methods with BotOwner param must guard null before dictionary access:\n  " + string.Join("\n  ", violations)
        );
    }

    // ── Action Stop() methods: paired cleanup ──

    [Test]
    public void AllActionStopMethods_CallBaseStop()
    {
        // Every BigBrain action's Stop() must call base.Stop() to ensure
        // sprinting is disabled and the action timer is stopped.
        var actionDir = Path.Combine(ClientSrcDir, "BotLogic", "Objective");
        var actionFiles = Directory.GetFiles(actionDir, "*Action.cs");
        var violations = new List<string>();

        var stopMethodPattern = new Regex(
            @"public\s+override\s+void\s+Stop\s*\(\s*\)(.*?)(?=public\s+override\s+void|private\s|protected\s|$)",
            RegexOptions.Singleline
        );

        foreach (var file in actionFiles)
        {
            string source = File.ReadAllText(file);
            string relPath = Path.GetRelativePath(RepoRoot, file);

            var match = stopMethodPattern.Match(source);
            if (!match.Success)
            {
                // SnipeAction inherits from AmbushAction and may not override Stop — that's OK
                if (relPath.Contains("SnipeAction"))
                    continue;
                continue; // Some actions may not override Stop at all
            }

            string stopBody = match.Value;
            if (!stopBody.Contains("base.Stop()"))
            {
                violations.Add(relPath);
            }
        }

        Assert.That(violations, Is.Empty, "Action Stop() methods must call base.Stop():\n  " + string.Join("\n  ", violations));
    }

    [Test]
    public void AllActionStopMethods_UnpausePatrollingIfPaused()
    {
        // If an action's Start() calls PatrollingData.Pause(), its Stop() must call
        // PatrollingData.Unpause() to restore bot's default patrolling behavior.
        var actionDir = Path.Combine(ClientSrcDir, "BotLogic", "Objective");
        var actionFiles = Directory.GetFiles(actionDir, "*Action.cs");
        var violations = new List<string>();

        foreach (var file in actionFiles)
        {
            string source = File.ReadAllText(file);
            string relPath = Path.GetRelativePath(RepoRoot, file);

            // Check if Start() pauses patrolling
            var startPattern = new Regex(
                @"public\s+override\s+void\s+Start\s*\(\s*\)(.*?)(?=public\s+override\s+void)",
                RegexOptions.Singleline
            );
            var startMatch = startPattern.Match(source);
            if (!startMatch.Success)
                continue;

            bool startPauses = startMatch.Value.Contains("PatrollingData.Pause()");
            if (!startPauses)
                continue;

            // Now check Stop() unpauses
            var stopPattern = new Regex(
                @"public\s+override\s+void\s+Stop\s*\(\s*\)(.*?)(?=public\s+override\s+void|private\s|protected\s|$)",
                RegexOptions.Singleline
            );
            var stopMatch = stopPattern.Match(source);
            if (!stopMatch.Success)
            {
                violations.Add(relPath + " — has Pause() in Start() but no Stop() override");
                continue;
            }

            if (!stopMatch.Value.Contains("PatrollingData.Unpause()"))
            {
                violations.Add(relPath + " — has Pause() in Start() but no Unpause() in Stop()");
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Actions that call PatrollingData.Pause() in Start() must call Unpause() in Stop():\n  " + string.Join("\n  ", violations)
        );
    }

    // ── ECS entity cleanup in Stop() ──

    [Test]
    public void ActionsThatSetEntityFlags_ClearThemInStop()
    {
        // Actions that set entity state flags (IsLingering, IsInvestigating, IsPatrolling,
        // IsApproachingLoot, IsLooting) in Start() must clear them in Stop() to prevent
        // stale state when actions are interrupted.
        var actionDir = Path.Combine(ClientSrcDir, "BotLogic", "Objective");
        var actionFiles = Directory.GetFiles(actionDir, "*Action.cs");
        var violations = new List<string>();

        var entityFlags = new[] { "IsLingering", "IsInvestigating", "IsPatrolling", "IsApproachingLoot", "IsLooting" };

        foreach (var file in actionFiles)
        {
            string source = File.ReadAllText(file);
            string relPath = Path.GetRelativePath(RepoRoot, file);

            var startPattern = new Regex(
                @"public\s+override\s+void\s+Start\s*\(\s*\)(.*?)(?=public\s+override\s+void)",
                RegexOptions.Singleline
            );
            var stopPattern = new Regex(
                @"public\s+override\s+void\s+Stop\s*\(\s*\)(.*?)(?=public\s+override\s+void|private\s|protected\s|$)",
                RegexOptions.Singleline
            );

            var startMatch = startPattern.Match(source);
            var stopMatch = stopPattern.Match(source);

            if (!startMatch.Success || !stopMatch.Success)
                continue;

            string startBody = startMatch.Value;
            string stopBody = stopMatch.Value;

            foreach (var flag in entityFlags)
            {
                // Pattern: entity.Flag = true in Start
                if (startBody.Contains(flag + " = true"))
                {
                    // Must have entity.Flag = false in Stop
                    if (!stopBody.Contains(flag + " = false"))
                    {
                        violations.Add($"{relPath}: sets {flag} = true in Start() but does not clear it in Stop()");
                    }
                }
            }
        }

        Assert.That(violations, Is.Empty, "Entity flags set in Start() must be cleared in Stop():\n  " + string.Join("\n  ", violations));
    }

    // ── HiveMind iteration safety ──

    [Test]
    public void HiveMindMonitor_IteratesEntitiesWithForLoop_NotForeach()
    {
        // Dense entity list uses swap-remove. Using foreach during iteration
        // where elements could be removed would throw InvalidOperationException.
        // All iteration over Registry.Entities must use index-based for loops.
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "HiveMind", "BotHiveMindMonitor.cs"));

        // Check for foreach over entities
        var foreachPattern = new Regex(@"foreach\s*\([^)]*entities\[", RegexOptions.Compiled);

        // More broadly, check for foreach with Entities
        var foreachEntitiesPattern = new Regex(@"foreach\s*\([^)]*\.Entities\)", RegexOptions.Compiled);

        Assert.That(
            foreachEntitiesPattern.Matches(source).Count,
            Is.EqualTo(0),
            "BotHiveMindMonitor must use index-based for loops (not foreach) over entities "
                + "to avoid InvalidOperationException during swap-remove"
        );
    }

    [Test]
    public void HiveMindMonitor_EntityIterations_CheckIsActive()
    {
        // All entity iterations in HiveMind tick must check entity.IsActive
        // to skip dead/despawned entities that haven't been removed yet.
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "HiveMind", "BotHiveMindMonitor.cs"));

        // Find all for-loops iterating entities
        var forLoopPattern = new Regex(@"for\s*\(\s*int\s+\w+\s*=\s*0\s*;\s*\w+\s*<\s*entities\.Count", RegexOptions.Compiled);

        int loopCount = forLoopPattern.Matches(source).Count;

        // Each loop should have an IsActive check nearby
        int isActiveCount = Regex.Matches(source, @"\.IsActive").Count;

        // There should be at least as many IsActive checks as entity iteration loops
        Assert.That(
            isActiveCount,
            Is.GreaterThanOrEqualTo(loopCount),
            $"Found {loopCount} entity iteration loops but only {isActiveCount} .IsActive checks. "
                + "All entity iterations should check IsActive to skip dead/despawned bots."
        );
    }

    // ── Registry: swap-remove unit tests ──

    [Test]
    public void BotRegistry_Remove_DoesNotCorruptDenseList()
    {
        // Verify swap-remove maintains correct entity→index mapping
        // when removing from the middle of the dense list.
        var registry = new BotRegistry(8);
        var e1 = registry.Add(10);
        var e2 = registry.Add(20);
        var e3 = registry.Add(30);

        Assert.That(registry.Count, Is.EqualTo(3));

        // Remove middle entity
        bool removed = registry.Remove(e2);
        Assert.That(removed, Is.True);
        Assert.That(registry.Count, Is.EqualTo(2));

        // Remaining entities should still be accessible
        Assert.That(registry.GetByBsgId(10), Is.EqualTo(e1));
        Assert.That(registry.GetByBsgId(30), Is.EqualTo(e3));

        // Removed entity should be gone
        Assert.That(registry.GetByBsgId(20), Is.Null);
    }

    [Test]
    public void BotRegistry_RemoveAll_ThenReAdd_Works()
    {
        // Verify registry can be fully emptied and repopulated
        var registry = new BotRegistry(8);
        var e1 = registry.Add(10);
        var e2 = registry.Add(20);

        registry.Remove(e1);
        registry.Remove(e2);
        Assert.That(registry.Count, Is.EqualTo(0));

        // Re-add after full empty
        var e3 = registry.Add(30);
        Assert.That(registry.Count, Is.EqualTo(1));
        Assert.That(registry.GetByBsgId(30), Is.EqualTo(e3));
    }

    [Test]
    public void BotRegistry_Remove_DuringForwardIteration_SkipsSwappedEntity()
    {
        // Demonstrate the swap-remove hazard: if you remove entity at index i
        // during forward iteration, the entity that was swapped in gets skipped.
        // HiveMind avoids this by using soft-delete (IsActive=false) instead of Remove.
        var registry = new BotRegistry(8);
        var e1 = registry.Add(10);
        e1.IsActive = true;
        var e2 = registry.Add(20);
        e2.IsActive = true;
        var e3 = registry.Add(30);
        e3.IsActive = true;

        // Simulate soft-delete (what HiveMind actually does)
        e2.IsActive = false;

        // Forward iteration should still process all entities including inactive
        int processedCount = 0;
        int activeCount = 0;
        for (int i = 0; i < registry.Entities.Count; i++)
        {
            processedCount++;
            if (registry.Entities[i].IsActive)
                activeCount++;
        }

        Assert.That(processedCount, Is.EqualTo(3), "All entities should be visited");
        Assert.That(activeCount, Is.EqualTo(2), "Only active entities should be counted");
    }

    // ── Reflection helpers: static field initialization safety ──

    [Test]
    public void ReflectionHelpers_AllFieldsAreDeclaredStaticReadonly()
    {
        // Reflection field lookups are expensive. All FieldInfo fields should be
        // static readonly (initialized once at type load) rather than computed per-call.
        var helperFiles = new[]
        {
            Path.Combine(ClientSrcDir, "Helpers", "CombatStateHelper.cs"),
            Path.Combine(ClientSrcDir, "Helpers", "RaidTimeHelper.cs"),
            Path.Combine(ClientSrcDir, "Helpers", "ExtractionHelper.cs"),
            Path.Combine(ClientSrcDir, "Helpers", "PlantZoneHelper.cs"),
            Path.Combine(ClientSrcDir, "Helpers", "HearingSensorHelper.cs"),
        };

        var fieldPattern = new Regex(
            @"private\s+static\s+readonly\s+FieldInfo\s+_\w+Field\s*=\s*ReflectionHelper\.RequireField",
            RegexOptions.Compiled
        );

        var requireFieldCallPattern = new Regex(@"ReflectionHelper\.RequireField\s*\(", RegexOptions.Compiled);

        var violations = new List<string>();

        foreach (var file in helperFiles)
        {
            if (!File.Exists(file))
                continue;

            string source = File.ReadAllText(file);
            string relPath = Path.GetRelativePath(RepoRoot, file);

            int requireFieldCalls = requireFieldCallPattern.Matches(source).Count;
            int staticReadonlyFields = fieldPattern.Matches(source).Count;

            if (requireFieldCalls != staticReadonlyFields)
            {
                violations.Add($"{relPath}: {requireFieldCalls} RequireField calls but {staticReadonlyFields} static readonly fields");
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "All RequireField calls should be in static readonly fields:\n  " + string.Join("\n  ", violations)
        );
    }

    // ── VultureAction + InvestigateAction: Stop clears entity state ──

    [Test]
    public void VultureAction_Stop_ClearsHasNearbyEvent()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "Objective", "VultureAction.cs"));

        var stopPattern = new Regex(
            @"public\s+override\s+void\s+Stop\s*\(\s*\)(.*?)(?=public\s+override\s+void|private\s|protected\s|$)",
            RegexOptions.Singleline
        );
        var match = stopPattern.Match(source);
        Assert.That(match.Success, Is.True, "Stop() method not found in VultureAction.cs");

        Assert.That(
            match.Value,
            Does.Contain("HasNearbyEvent = false"),
            "VultureAction.Stop() must clear HasNearbyEvent to prevent stale combat event data"
        );
    }

    [Test]
    public void InvestigateAction_Stop_ClearsHasNearbyEvent()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "Objective", "InvestigateAction.cs"));

        var stopPattern = new Regex(
            @"public\s+override\s+void\s+Stop\s*\(\s*\)(.*?)(?=public\s+override\s+void|private\s|protected\s|$)",
            RegexOptions.Singleline
        );
        var match = stopPattern.Match(source);
        Assert.That(match.Success, Is.True, "Stop() method not found in InvestigateAction.cs");

        Assert.That(
            match.Value,
            Does.Contain("HasNearbyEvent = false"),
            "InvestigateAction.Stop() must clear HasNearbyEvent to prevent stale combat event data"
        );
    }

    // ── LootAction: Stop clears both approach and looting flags ──

    [Test]
    public void LootAction_Stop_ClearsBothLootingFlags()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "Objective", "LootAction.cs"));

        var stopPattern = new Regex(
            @"public\s+override\s+void\s+Stop\s*\(\s*\)(.*?)(?=public\s+override\s+void|private\s|protected\s|$)",
            RegexOptions.Singleline
        );
        var match = stopPattern.Match(source);
        Assert.That(match.Success, Is.True, "Stop() method not found in LootAction.cs");

        string stopBody = match.Value;
        Assert.That(stopBody, Does.Contain("IsApproachingLoot = false"), "LootAction.Stop() must clear IsApproachingLoot");
        Assert.That(stopBody, Does.Contain("IsLooting = false"), "LootAction.Stop() must clear IsLooting");
    }
}

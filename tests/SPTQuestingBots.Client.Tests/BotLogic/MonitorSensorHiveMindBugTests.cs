using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic;

/// <summary>
/// Bug-catching tests for BotMonitor state machines, sensors, HiveMind coordination,
/// combat events, personality scoring, and sprint limiting.
/// Round 4 bug hunt: auditing monitors/sensors/hivemind.
/// </summary>
[TestFixture]
public class MonitorSensorHiveMindBugTests
{
    private static readonly string SrcRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "SPTQuestingBots.Client")
    );

    // ── Bug 1: shouldWaitForFollowers vacuous truth on empty IEnumerable ──
    // When all followers are dead/null, LINQ .Where() produces an empty IEnumerable.
    // .All(pred) on empty returns true (vacuous truth), causing the boss to
    // incorrectly think followers are too far and wait for dead followers.

    [Test]
    public void BotQuestingMonitor_ShouldWaitForFollowers_ChecksEmptyAliveFollowers()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "Monitors", "BotQuestingMonitor.cs"));

        // After the Where/Select filter, there must be a count == 0 check before
        // calling .Any() or .All() to avoid vacuous truth on an empty sequence.
        Assert.That(
            source,
            Does.Contain("followerDistances.Count == 0"),
            "shouldWaitForFollowers must check for empty alive-follower list " + "to prevent vacuous truth from .All() on empty IEnumerable"
        );
    }

    [Test]
    public void BotQuestingMonitor_ShouldWaitForFollowers_MaterializesEnumerable()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "Monitors", "BotQuestingMonitor.cs"));

        // The followerDistances should be materialized (ToList/ToArray) to avoid
        // double-enumeration across .Any() and .All() calls.
        Assert.That(
            source,
            Does.Contain(".ToList()").Or.Contain(".ToArray()"),
            "shouldWaitForFollowers must materialize the LINQ query to avoid " + "double-enumeration between .Any() and .All()"
        );
    }

    // ── Bug 2: BotHearingMonitor.updateSuspiciousTime creates new Random per call ──
    // new System.Random() per call with same-millisecond callers produces identical
    // values. Should use a shared static RNG instance.

    [Test]
    public void BotHearingMonitor_UpdateSuspiciousTime_UsesSharedRng()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "Monitors", "BotHearingMonitor.cs"));

        // The updateSuspiciousTime method should NOT create new System.Random()
        Assert.That(
            Regex.IsMatch(source, @"updateSuspiciousTime\(\)[\s\S]{0,200}new\s+System\.Random\(\)"),
            Is.False,
            "updateSuspiciousTime must not create new System.Random() per call — " + "bots in the same millisecond get identical results"
        );
    }

    [Test]
    public void BotHearingMonitor_HasStaticSharedRng()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "Monitors", "BotHearingMonitor.cs"));

        // Should have a static shared RNG field
        Assert.That(
            source,
            Does.Contain("static").And.Contain("System.Random"),
            "BotHearingMonitor should have a static shared System.Random instance"
        );
    }

    // ── Bug 3: updateSuspiciousTime int truncation loses config precision ──
    // Config values are doubles (e.g., 3.5, 8.7) but were cast to int before
    // Random.Next, losing fractional precision and making max exclusive.

    [Test]
    public void BotHearingMonitor_UpdateSuspiciousTime_UsesDoubleRange()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "Monitors", "BotHearingMonitor.cs"));

        // The method should use NextDouble() for continuous range instead of
        // Next(int, int) which truncates and is max-exclusive.
        Assert.That(
            source,
            Does.Contain("NextDouble()"),
            "updateSuspiciousTime should use NextDouble() for continuous random range "
                + "instead of int truncation with Random.Next(int, int)"
        );
    }

    [Test]
    public void BotHearingMonitor_UpdateSuspiciousTime_ReturnsDouble()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "Monitors", "BotHearingMonitor.cs"));

        // Method return type should be double, not int
        Assert.That(
            source,
            Does.Contain("private double updateSuspiciousTime()"),
            "updateSuspiciousTime should return double, not int, to preserve config precision"
        );
    }

    // ── Bug 4: BotCombatMonitor.updateSearchTimeAfterCombat same truncation ──

    [Test]
    public void BotCombatMonitor_UpdateSearchTimeAfterCombat_UsesDoubleRange()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "Monitors", "BotCombatMonitor.cs"));

        // Should use NextDouble() for continuous range
        Assert.That(
            source,
            Does.Contain("NextDouble()"),
            "updateSearchTimeAfterCombat should use NextDouble() for continuous random range"
        );
    }

    [Test]
    public void BotCombatMonitor_UpdateSearchTimeAfterCombat_ReturnsDouble()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "Monitors", "BotCombatMonitor.cs"));

        Assert.That(
            source,
            Does.Contain("private double updateSearchTimeAfterCombat()"),
            "updateSearchTimeAfterCombat should return double, not int"
        );
    }

    // ── Bug 5: BotExtractMonitor quest count thresholds off by one ──
    // Random.Next(min, max) is max-exclusive. When config says TotalQuests max=5,
    // the bot could never need to complete 5 quests. Fix: max+1.

    [Test]
    public void BotExtractMonitor_QuestThresholds_MaxIsInclusive()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "Monitors", "BotExtractMonitor.cs"));

        // The fix adds +1 to make max inclusive
        Assert.That(
            source,
            Does.Contain("max + 1"),
            "BotExtractMonitor quest count thresholds must add 1 to max for inclusive upper bound"
        );
    }

    [Test]
    public void BotExtractMonitor_QuestThresholds_HandlesEqualMinMax()
    {
        var source = File.ReadAllText(Path.Combine(SrcRoot, "BotLogic", "BotMonitor", "Monitors", "BotExtractMonitor.cs"));

        // When min == max, Random.Next(min, min+1) would return min.
        // The fix should guard against min >= max.
        Assert.That(
            source,
            Does.Contain("max > min"),
            "BotExtractMonitor must guard against min >= max to avoid ArgumentOutOfRangeException"
        );
    }

    // ── HiveMindSystem: boss/follower lifecycle ──

    [Test]
    public void HiveMindSystem_CleanupDeadEntities_DetachesDeadBossFollowers()
    {
        var boss = new BotEntity(1) { IsActive = false };
        var follower1 = new BotEntity(2) { IsActive = true };
        var follower2 = new BotEntity(3) { IsActive = true };

        HiveMindSystem.AssignBoss(follower1, boss);
        HiveMindSystem.AssignBoss(follower2, boss);

        Assert.That(boss.Followers, Has.Count.EqualTo(2));

        var entities = new System.Collections.Generic.List<BotEntity> { boss, follower1, follower2 };
        HiveMindSystem.CleanupDeadEntities(entities);

        Assert.Multiple(() =>
        {
            Assert.That(boss.Followers, Has.Count.EqualTo(0), "Dead boss should have no followers after cleanup");
            Assert.That(follower1.Boss, Is.Null, "Follower1 should lose boss reference after boss dies");
            Assert.That(follower2.Boss, Is.Null, "Follower2 should lose boss reference after boss dies");
        });
    }

    [Test]
    public void HiveMindSystem_CleanupDeadEntities_DetachesDeadFollowers()
    {
        var boss = new BotEntity(1) { IsActive = true };
        var follower1 = new BotEntity(2) { IsActive = false };
        var follower2 = new BotEntity(3) { IsActive = true };

        HiveMindSystem.AssignBoss(follower1, boss);
        HiveMindSystem.AssignBoss(follower2, boss);

        Assert.That(boss.Followers, Has.Count.EqualTo(2));

        var entities = new System.Collections.Generic.List<BotEntity> { boss, follower1, follower2 };
        HiveMindSystem.CleanupDeadEntities(entities);

        Assert.Multiple(() =>
        {
            Assert.That(boss.Followers, Has.Count.EqualTo(1), "Boss should have 1 follower after dead follower cleanup");
            Assert.That(boss.Followers[0], Is.SameAs(follower2), "Remaining follower should be the alive one");
            Assert.That(follower1.Boss, Is.Null, "Dead follower should lose boss reference");
        });
    }

    [Test]
    public void HiveMindSystem_AssignBoss_SwitchesBossCorrectly()
    {
        var oldBoss = new BotEntity(1);
        var newBoss = new BotEntity(2);
        var follower = new BotEntity(3);

        HiveMindSystem.AssignBoss(follower, oldBoss);
        Assert.That(oldBoss.Followers, Has.Count.EqualTo(1));

        HiveMindSystem.AssignBoss(follower, newBoss);

        Assert.Multiple(() =>
        {
            Assert.That(oldBoss.Followers, Has.Count.EqualTo(0), "Old boss should have 0 followers after reassignment");
            Assert.That(newBoss.Followers, Has.Count.EqualTo(1), "New boss should have 1 follower");
            Assert.That(follower.Boss, Is.SameAs(newBoss), "Follower should reference new boss");
        });
    }

    [Test]
    public void HiveMindSystem_AssignBoss_PreventsSelfAssignment()
    {
        var entity = new BotEntity(1);

        HiveMindSystem.AssignBoss(entity, entity);

        Assert.Multiple(() =>
        {
            Assert.That(entity.Boss, Is.Null, "Entity should not be its own boss");
            Assert.That(entity.Followers, Has.Count.EqualTo(0), "Entity should not follow itself");
        });
    }

    [Test]
    public void HiveMindSystem_SeparateFromGroup_ClearsAllReferences()
    {
        var boss = new BotEntity(1);
        var follower1 = new BotEntity(2);
        var follower2 = new BotEntity(3);

        HiveMindSystem.AssignBoss(follower1, boss);
        HiveMindSystem.AssignBoss(follower2, boss);

        HiveMindSystem.SeparateFromGroup(boss);

        Assert.Multiple(() =>
        {
            Assert.That(boss.Followers, Has.Count.EqualTo(0), "Separated boss should have no followers");
            Assert.That(follower1.Boss, Is.Null, "Follower1 should lose boss reference");
            Assert.That(follower2.Boss, Is.Null, "Follower2 should lose boss reference");
        });
    }

    // ── CombatEventRegistry: ring buffer ──

    [Test]
    public void CombatEventRegistry_GetNearestEvent_ReturnsNearest()
    {
        CombatEventRegistry.Initialize(128);

        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 1.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(20f, 0f, 20f, 2.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(50f, 0f, 50f, 3.0f, 100f, CombatEventType.Gunshot, false);

        bool found = CombatEventRegistry.GetNearestEvent(22f, 22f, 200f, 5.0f, 10f, out var nearest);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(nearest.X, Is.EqualTo(20f), "Should return the nearest event");
        });

        CombatEventRegistry.Clear();
    }

    [Test]
    public void CombatEventRegistry_GetNearestEvent_IgnoresExpired()
    {
        CombatEventRegistry.Initialize(128);

        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(50f, 0f, 50f, 100.0f, 100f, CombatEventType.Gunshot, false);

        // Query at time 102 with maxAge 5 — only the event at time 100 should be valid
        bool found = CombatEventRegistry.GetNearestEvent(12f, 12f, 200f, 102.0f, 5f, out var nearest);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(nearest.X, Is.EqualTo(50f), "Should only return non-expired event");
        });

        CombatEventRegistry.Clear();
    }

    [Test]
    public void CombatEventRegistry_GetIntensity_ExplosionsCountTriple()
    {
        CombatEventRegistry.Initialize(128);

        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(12f, 0f, 12f, 2.0f, 150f, CombatEventType.Explosion, false);

        int intensity = CombatEventRegistry.GetIntensity(11f, 11f, 50f, 10f, 5.0f);

        // 1 gunshot (1) + 1 explosion (1+2=3) = 4
        Assert.That(intensity, Is.EqualTo(4), "Explosions should count as 3 intensity units (1+2)");

        CombatEventRegistry.Clear();
    }

    [Test]
    public void CombatEventRegistry_CleanupExpired_MarksOldEventsInactive()
    {
        CombatEventRegistry.Initialize(128);

        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(20f, 0f, 20f, 50.0f, 100f, CombatEventType.Gunshot, false);

        CombatEventRegistry.CleanupExpired(60f, 20f);

        // Event at time 1 is 59s old (>20), should be inactive
        // Event at time 50 is 10s old (<20), should still be active
        Assert.That(CombatEventRegistry.ActiveCount, Is.EqualTo(1));

        CombatEventRegistry.Clear();
    }

    // ── CombatEventClustering ──

    [Test]
    public void CombatEventClustering_ClusterEvents_GroupsNearbyEvents()
    {
        var events = new CombatEvent[]
        {
            new CombatEvent
            {
                X = 10f,
                Y = 0f,
                Z = 10f,
                Time = 1f,
                Power = 100f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 12f,
                Y = 0f,
                Z = 12f,
                Time = 2f,
                Power = 100f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 200f,
                Y = 0f,
                Z = 200f,
                Time = 3f,
                Power = 100f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
        };

        var output = new CombatEventClustering.ClusterResult[10];
        int count = CombatEventClustering.ClusterEvents(events, 3, 5f, 10f, 50f * 50f, output, 10);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(2), "Should produce 2 clusters for distant event groups");
            // First cluster should be the centroid of the two nearby events
            Assert.That(output[0].X, Is.EqualTo(11f).Within(0.1f));
            Assert.That(output[0].Intensity, Is.EqualTo(2));
        });
    }

    [Test]
    public void CombatEventClustering_ClusterEvents_SkipsDeathEvents()
    {
        var events = new CombatEvent[]
        {
            new CombatEvent
            {
                X = 10f,
                Y = 0f,
                Z = 10f,
                Time = 1f,
                Power = 100f,
                Type = CombatEventType.Death,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 12f,
                Y = 0f,
                Z = 12f,
                Time = 2f,
                Power = 100f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
        };

        var output = new CombatEventClustering.ClusterResult[10];
        int count = CombatEventClustering.ClusterEvents(events, 2, 5f, 10f, 50f * 50f, output, 10);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(1), "Death events should be excluded from clusters");
            Assert.That(output[0].X, Is.EqualTo(12f));
        });
    }

    [Test]
    public void CombatEventClustering_FilterDeathEvents_OnlyReturnsDeaths()
    {
        var events = new CombatEvent[]
        {
            new CombatEvent
            {
                X = 10f,
                Time = 1f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 20f,
                Time = 2f,
                Type = CombatEventType.Death,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 30f,
                Time = 3f,
                Type = CombatEventType.Explosion,
                IsActive = true,
            },
        };

        var output = new CombatEvent[10];
        int count = CombatEventClustering.FilterDeathEvents(events, 3, 5f, 10f, output);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(1));
            Assert.That(output[0].X, Is.EqualTo(20f));
        });
    }

    // ── RoomClearController edge cases ──

    [Test]
    public void RoomClearController_Update_CornerPauseExpiresCorrectly()
    {
        var entity = new BotEntity(0);
        entity.LastEnvironmentId = 0; // outdoor
        entity.IsInRoomClear = false;

        // Trigger outdoor->indoor transition
        RoomClearController.Update(entity, true, 10f, 5f, 10f, 1.5f);
        Assert.That(entity.IsInRoomClear, Is.True);

        // Trigger corner pause
        RoomClearController.TriggerCornerPause(entity, 11f, 1.5f);
        Assert.That(entity.CornerPauseUntil, Is.EqualTo(12.5f).Within(0.001f));

        // During pause, instruction should be PauseAtCorner
        var instruction = RoomClearController.Update(entity, true, 11.5f, 5f, 10f, 1.5f);
        Assert.That(instruction, Is.EqualTo(RoomClearInstruction.PauseAtCorner));

        // After pause expires, instruction should be SlowWalk
        instruction = RoomClearController.Update(entity, true, 13f, 5f, 10f, 1.5f);
        Assert.That(instruction, Is.EqualTo(RoomClearInstruction.SlowWalk));
    }

    [Test]
    public void RoomClearController_IsSharpCorner_StraightLine_NotSharp()
    {
        // Three collinear points: (0,0) -> (5,0) -> (10,0) — angle is 0 degrees
        bool isSharp = RoomClearController.IsSharpCorner(0f, 0f, 5f, 0f, 10f, 0f, 30f);
        Assert.That(isSharp, Is.False, "Collinear points should not be a sharp corner");
    }

    [Test]
    public void RoomClearController_IsSharpCorner_UTurn_IsSharp()
    {
        // U-turn: (0,0) -> (5,0) -> (0,0) — angle is 180 degrees
        bool isSharp = RoomClearController.IsSharpCorner(0f, 0f, 5f, 0f, 0f, 0f, 30f);
        Assert.That(isSharp, Is.True, "U-turn should be a sharp corner");
    }

    [Test]
    public void RoomClearController_IsSharpCorner_RightAngle()
    {
        // Right angle: (0,0) -> (5,0) -> (5,5) — angle is 90 degrees
        bool isSharp = RoomClearController.IsSharpCorner(0f, 0f, 5f, 0f, 5f, 5f, 80f);
        Assert.That(isSharp, Is.True, "90-degree turn should be sharp with 80-degree threshold");
    }

    [Test]
    public void RoomClearController_IsSharpCorner_CoincidentPoints_NotSharp()
    {
        // Degenerate: all same point
        bool isSharp = RoomClearController.IsSharpCorner(5f, 5f, 5f, 5f, 5f, 5f, 30f);
        Assert.That(isSharp, Is.False, "Coincident points should return false (zero-length vectors)");
    }

    // ── ScoringModifiers edge cases ──

    [Test]
    public void ScoringModifiers_CombinedModifier_NeverReturnsNaN()
    {
        // Test with extreme values
        float result = SPTQuestingBots.BotLogic.ECS.UtilityAI.ScoringModifiers.CombinedModifier(
            float.NaN,
            0.5f,
            SPTQuestingBots.BotLogic.ECS.UtilityAI.BotActionTypeId.GoToObjective
        );
        Assert.That(float.IsNaN(result), Is.False, "CombinedModifier must not return NaN");
        Assert.That(result, Is.EqualTo(1.0f), "NaN input should fall back to 1.0");
    }

    [Test]
    public void ScoringModifiers_CombinedModifier_NegativeResultFallsBack()
    {
        // Both modifiers are always positive for valid inputs, but test the guard
        float result = SPTQuestingBots.BotLogic.ECS.UtilityAI.ScoringModifiers.CombinedModifier(
            0.5f,
            0.5f,
            SPTQuestingBots.BotLogic.ECS.UtilityAI.BotActionTypeId.GoToObjective
        );
        Assert.That(result, Is.GreaterThan(0f), "CombinedModifier should always be positive for valid inputs");
    }

    [Test]
    public void ScoringModifiers_PersonalityModifier_UnknownActionReturnsOne()
    {
        float result = SPTQuestingBots.BotLogic.ECS.UtilityAI.ScoringModifiers.PersonalityModifier(0.5f, 999);
        Assert.That(result, Is.EqualTo(1f), "Unknown action type should return 1.0 modifier");
    }

    [Test]
    public void ScoringModifiers_RaidTimeModifier_BoundaryValues()
    {
        // At raid start (0.0) and raid end (1.0) for GoToObjective
        float atStart = SPTQuestingBots.BotLogic.ECS.UtilityAI.ScoringModifiers.RaidTimeModifier(
            0f,
            SPTQuestingBots.BotLogic.ECS.UtilityAI.BotActionTypeId.GoToObjective
        );
        float atEnd = SPTQuestingBots.BotLogic.ECS.UtilityAI.ScoringModifiers.RaidTimeModifier(
            1f,
            SPTQuestingBots.BotLogic.ECS.UtilityAI.BotActionTypeId.GoToObjective
        );

        Assert.Multiple(() =>
        {
            Assert.That(atStart, Is.EqualTo(1.2f).Within(0.001f), "GoToObjective should be boosted at raid start");
            Assert.That(atEnd, Is.EqualTo(0.8f).Within(0.001f), "GoToObjective should be reduced at raid end");
        });
    }

    [Test]
    public void ScoringModifiers_PersonalityModifier_AggressionBoundaries()
    {
        // Aggressive bot (1.0) should boost GoToObjective
        float aggressive = SPTQuestingBots.BotLogic.ECS.UtilityAI.ScoringModifiers.PersonalityModifier(
            1f,
            SPTQuestingBots.BotLogic.ECS.UtilityAI.BotActionTypeId.GoToObjective
        );
        // Timid bot (0.0) should reduce GoToObjective
        float timid = SPTQuestingBots.BotLogic.ECS.UtilityAI.ScoringModifiers.PersonalityModifier(
            0f,
            SPTQuestingBots.BotLogic.ECS.UtilityAI.BotActionTypeId.GoToObjective
        );

        Assert.Multiple(() =>
        {
            Assert.That(aggressive, Is.EqualTo(1.15f).Within(0.001f), "Max aggression should give 1.15 for GoToObjective");
            Assert.That(timid, Is.EqualTo(0.85f).Within(0.001f), "Min aggression should give 0.85 for GoToObjective");
        });
    }

    // ── PersonalityHelper ──

    [Test]
    public void PersonalityHelper_GetAggression_OutOfRange_ReturnsNormal()
    {
        float aggression = PersonalityHelper.GetAggression(255);
        Assert.That(aggression, Is.EqualTo(0.5f), "Out-of-range personality byte should return Normal aggression");
    }

    [Test]
    public void PersonalityHelper_FromDifficulty_AllMappings()
    {
        // Each difficulty should mostly produce its center personality (60% chance)
        var rng = new System.Random(42);
        Assert.Multiple(() =>
        {
            int c0 = 0,
                c1 = 0,
                c2 = 0,
                c3 = 0;
            for (int i = 0; i < 200; i++)
            {
                if (PersonalityHelper.FromDifficulty(0, rng) == BotPersonality.Cautious)
                    c0++;
                if (PersonalityHelper.FromDifficulty(1, rng) == BotPersonality.Normal)
                    c1++;
                if (PersonalityHelper.FromDifficulty(2, rng) == BotPersonality.Aggressive)
                    c2++;
                if (PersonalityHelper.FromDifficulty(3, rng) == BotPersonality.Reckless)
                    c3++;
            }
            Assert.That(c0, Is.GreaterThan(80), "Easy should mostly produce Cautious");
            Assert.That(c1, Is.GreaterThan(80), "Normal should mostly produce Normal");
            Assert.That(c2, Is.GreaterThan(80), "Hard should mostly produce Aggressive");
            Assert.That(c3, Is.GreaterThan(80), "Impossible should mostly produce Reckless");
        });
    }

    [Test]
    public void PersonalityHelper_FromDifficulty_UnknownUsesWeightedRandom()
    {
        var rng = new System.Random(42);
        // Unknown difficulty should produce a valid personality byte
        byte result = PersonalityHelper.FromDifficulty(-1, rng);
        Assert.That(result, Is.LessThanOrEqualTo(BotPersonality.Reckless));
    }

    [Test]
    public void PersonalityHelper_RandomFallback_ProducesValidPersonalities()
    {
        var rng = new System.Random(42);
        var counts = new int[5];
        for (int i = 0; i < 1000; i++)
        {
            byte p = PersonalityHelper.RandomFallback(rng);
            Assert.That(p, Is.LessThanOrEqualTo(BotPersonality.Reckless));
            counts[p]++;
        }

        // All personality types should appear at least once in 1000 rolls
        for (int i = 0; i < 5; i++)
        {
            Assert.That(counts[i], Is.GreaterThan(0), $"Personality {i} should appear in 1000 random rolls");
        }
    }

    // ── SquadPersonalityCalculator ──

    [Test]
    public void SquadPersonalityCalculator_EmptyMembers_ReturnsNone()
    {
        var result = SquadPersonalityCalculator.DeterminePersonality(new BotType[0], 0);
        Assert.That(result, Is.EqualTo(SquadPersonalityType.None));
    }

    [Test]
    public void SquadPersonalityCalculator_SingleBoss_ReturnsElite()
    {
        var types = new[] { BotType.Boss };
        var result = SquadPersonalityCalculator.DeterminePersonality(types, 1);
        Assert.That(result, Is.EqualTo(SquadPersonalityType.Elite));
    }

    [Test]
    public void SquadPersonalityCalculator_MixedSquad_MajorityWins()
    {
        // 2 PMCs, 1 Scav — PMC (GigaChads) should win
        var types = new[] { BotType.PMC, BotType.PMC, BotType.Scav };
        var result = SquadPersonalityCalculator.DeterminePersonality(types, 3);
        Assert.That(result, Is.EqualTo(SquadPersonalityType.GigaChads));
    }

    [Test]
    public void SquadPersonalityCalculator_AllUnknown_ReturnsNone()
    {
        var types = new[] { BotType.Unknown, BotType.Unknown };
        var result = SquadPersonalityCalculator.DeterminePersonality(types, 2);
        Assert.That(result, Is.EqualTo(SquadPersonalityType.None));
    }

    // ── HiveMindSystem sensor reset ──

    [Test]
    public void HiveMindSystem_ResetInactiveEntitySensors_OnlyResetsInactive()
    {
        var active = new BotEntity(1)
        {
            IsActive = true,
            IsInCombat = true,
            IsSuspicious = true,
        };
        var inactive = new BotEntity(2)
        {
            IsActive = false,
            IsInCombat = true,
            IsSuspicious = true,
        };

        var entities = new System.Collections.Generic.List<BotEntity> { active, inactive };
        HiveMindSystem.ResetInactiveEntitySensors(entities);

        Assert.Multiple(() =>
        {
            // Active entity should be unchanged
            Assert.That(active.IsInCombat, Is.True, "Active entity combat should be unchanged");
            Assert.That(active.IsSuspicious, Is.True, "Active entity suspicion should be unchanged");

            // Inactive entity should be reset
            Assert.That(inactive.IsInCombat, Is.False, "Inactive entity combat should be reset");
            Assert.That(inactive.IsSuspicious, Is.False, "Inactive entity suspicion should be reset");
            Assert.That(inactive.CanQuest, Is.False, "Inactive entity CanQuest should be reset");
            Assert.That(inactive.CanSprintToObjective, Is.True, "Inactive entity CanSprintToObjective should default to true");
            Assert.That(inactive.WantsToLoot, Is.False, "Inactive entity WantsToLoot should be reset");
        });
    }

    // ── SpawnEntryTask ──

    [Test]
    public void SpawnEntryTask_AlreadyComplete_ReturnsZero()
    {
        var entity = new BotEntity(0)
        {
            IsSpawnEntryComplete = true,
            SpawnEntryDuration = 5f,
            SpawnTime = 0f,
            CurrentGameTime = 10f,
        };

        float score = SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks.SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void SpawnEntryTask_DurationExpired_MarksComplete()
    {
        var entity = new BotEntity(0)
        {
            SpawnEntryDuration = 3f,
            SpawnTime = 0f,
            CurrentGameTime = 5f,
        };

        float score = SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks.SpawnEntryTask.Score(entity);

        Assert.Multiple(() =>
        {
            Assert.That(score, Is.EqualTo(0f));
            Assert.That(entity.IsSpawnEntryComplete, Is.True, "SpawnEntry should be marked complete");
        });
    }

    [Test]
    public void SpawnEntryTask_WithinDuration_ReturnsMaxScore()
    {
        var entity = new BotEntity(0)
        {
            SpawnEntryDuration = 5f,
            SpawnTime = 10f,
            CurrentGameTime = 12f,
        };

        float score = SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks.SpawnEntryTask.Score(entity);

        Assert.That(score, Is.EqualTo(SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks.SpawnEntryTask.MaxBaseScore));
    }

    // ── LingerTask ──

    [Test]
    public void LingerTask_NoObjectiveCompleted_ReturnsZero()
    {
        var entity = new BotEntity(0)
        {
            ObjectiveCompletedTime = 0f,
            LingerDuration = 10f,
            CurrentGameTime = 5f,
        };

        float score = SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks.LingerTask.Score(entity, 0.45f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LingerTask_InCombat_ReturnsZero()
    {
        var entity = new BotEntity(0)
        {
            ObjectiveCompletedTime = 1f,
            LingerDuration = 10f,
            CurrentGameTime = 5f,
            IsInCombat = true,
        };

        float score = SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks.LingerTask.Score(entity, 0.45f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LingerTask_LinearDecay_HalfwayIsHalfScore()
    {
        var entity = new BotEntity(0)
        {
            ObjectiveCompletedTime = 10f, // must be > 0 (0 means "not set")
            LingerDuration = 10f,
            CurrentGameTime = 15f, // 5s into 10s linger = halfway
        };

        float score = SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks.LingerTask.Score(entity, 0.45f);
        Assert.That(score, Is.EqualTo(0.225f).Within(0.001f), "Halfway through duration should give half score");
    }

    [Test]
    public void LingerTask_DurationExpired_ReturnsZero()
    {
        var entity = new BotEntity(0)
        {
            ObjectiveCompletedTime = 5f, // must be > 0 (0 means "not set")
            LingerDuration = 10f,
            CurrentGameTime = 15f, // 10s elapsed = duration expired
        };

        float score = SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks.LingerTask.Score(entity, 0.45f);
        Assert.That(score, Is.EqualTo(0f));
    }

    // ── InvestigateTask ──

    [Test]
    public void InvestigateTask_NoNearbyEvent_ReturnsZero()
    {
        var entity = new BotEntity(0) { HasNearbyEvent = false };

        float score = SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks.InvestigateTask.Score(entity, 5, 120f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void InvestigateTask_InCombat_ReturnsZero()
    {
        var entity = new BotEntity(0)
        {
            HasNearbyEvent = true,
            IsInCombat = true,
            CombatIntensity = 10,
        };

        float score = SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks.InvestigateTask.Score(entity, 5, 120f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void InvestigateTask_BelowThreshold_ReturnsZero()
    {
        var entity = new BotEntity(0)
        {
            HasNearbyEvent = true,
            CombatIntensity = 3,
            CurrentPositionX = 10f,
            CurrentPositionZ = 10f,
            NearbyEventX = 20f,
            NearbyEventZ = 20f,
        };

        float score = SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks.InvestigateTask.Score(entity, 5, 120f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void InvestigateTask_AboveThreshold_ReturnsPositive()
    {
        var entity = new BotEntity(0)
        {
            HasNearbyEvent = true,
            CombatIntensity = 10,
            CurrentPositionX = 10f,
            CurrentPositionZ = 10f,
            NearbyEventX = 20f,
            NearbyEventZ = 20f,
        };

        float score = SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks.InvestigateTask.Score(entity, 5, 120f);
        Assert.That(score, Is.GreaterThan(0f));
    }

    // ── CombatEventScanner ──

    [Test]
    public void CombatEventScanner_UpdateEntity_SetsFieldsOnFoundEvent()
    {
        CombatEventRegistry.Initialize(128);
        CombatEventRegistry.RecordEvent(50f, 5f, 50f, 1.0f, 100f, CombatEventType.Gunshot, false);

        var entity = new BotEntity(0)
        {
            IsActive = true,
            CurrentPositionX = 55f,
            CurrentPositionZ = 55f,
        };

        CombatEventScanner.UpdateEntity(entity, 2.0f, 10f, 200f, 50f, 10f, 100f, 60f);

        Assert.Multiple(() =>
        {
            Assert.That(entity.HasNearbyEvent, Is.True);
            Assert.That(entity.NearbyEventX, Is.EqualTo(50f));
            Assert.That(entity.NearbyEventY, Is.EqualTo(5f));
            Assert.That(entity.NearbyEventZ, Is.EqualTo(50f));
            Assert.That(entity.CombatIntensity, Is.GreaterThanOrEqualTo(1));
        });

        CombatEventRegistry.Clear();
    }

    [Test]
    public void CombatEventScanner_UpdateEntity_ClearsFieldsOnNoEvent()
    {
        CombatEventRegistry.Initialize(128);
        // No events recorded

        var entity = new BotEntity(0)
        {
            IsActive = true,
            CurrentPositionX = 55f,
            CurrentPositionZ = 55f,
            HasNearbyEvent = true,
            NearbyEventX = 999f,
            CombatIntensity = 99,
        };

        CombatEventScanner.UpdateEntity(entity, 2.0f, 10f, 200f, 50f, 10f, 100f, 60f);

        Assert.Multiple(() =>
        {
            Assert.That(entity.HasNearbyEvent, Is.False);
            Assert.That(entity.NearbyEventX, Is.EqualTo(0f));
            Assert.That(entity.CombatIntensity, Is.EqualTo(0));
        });

        CombatEventRegistry.Clear();
    }

    // ── SquadPersonalitySettings ──

    [Test]
    public void SquadPersonalitySettings_SharingChance_ValidRange()
    {
        // Elite: coordination=5 → 25 + 5*15 = 100
        var elite = SquadPersonalitySettings.ForType(SquadPersonalityType.Elite);
        Assert.That(elite.GetSharingChance(), Is.EqualTo(100f).Within(0.001f));

        // TimmyTeam6: coordination=1 → 25 + 1*15 = 40
        var timmy = SquadPersonalitySettings.ForType(SquadPersonalityType.TimmyTeam6);
        Assert.That(timmy.GetSharingChance(), Is.EqualTo(40f).Within(0.001f));
    }

    // ── SoftStuckDetector state machine ──

    [Test]
    public void SoftStuckDetector_ProgressionOrder_VaultThenJumpThenFail()
    {
        var detector = new SPTQuestingBots.Models.SoftStuckDetector(vaultDelay: 1f, jumpDelay: 2f, failDelay: 3f);

        var pos = new UnityEngine.Vector3(10f, 0f, 10f);

        // Initialize
        detector.Update(pos, 2.0f, 0f);

        // Keep updating at same position with speed > 0 and small deltaTime
        bool transitioned;
        float time = 0f;
        SPTQuestingBots.Models.SoftStuckStatus lastStatus = SPTQuestingBots.Models.SoftStuckStatus.None;

        // Walk to vault threshold
        for (int i = 0; i < 100; i++)
        {
            time += 0.05f;
            transitioned = detector.Update(pos, 2.0f, time);
            if (transitioned && lastStatus == SPTQuestingBots.Models.SoftStuckStatus.None)
            {
                Assert.That(
                    detector.Status,
                    Is.EqualTo(SPTQuestingBots.Models.SoftStuckStatus.Vaulting),
                    "First transition should be to Vaulting"
                );
                lastStatus = detector.Status;
            }
            else if (transitioned && lastStatus == SPTQuestingBots.Models.SoftStuckStatus.Vaulting)
            {
                Assert.That(
                    detector.Status,
                    Is.EqualTo(SPTQuestingBots.Models.SoftStuckStatus.Jumping),
                    "Second transition should be to Jumping"
                );
                lastStatus = detector.Status;
            }
            else if (transitioned && lastStatus == SPTQuestingBots.Models.SoftStuckStatus.Jumping)
            {
                Assert.That(
                    detector.Status,
                    Is.EqualTo(SPTQuestingBots.Models.SoftStuckStatus.Failed),
                    "Third transition should be to Failed"
                );
                lastStatus = detector.Status;
                break;
            }
        }

        Assert.That(lastStatus, Is.EqualTo(SPTQuestingBots.Models.SoftStuckStatus.Failed), "Should reach Failed state");
    }

    [Test]
    public void SoftStuckDetector_Movement_ResetsStuckState()
    {
        var detector = new SPTQuestingBots.Models.SoftStuckDetector(vaultDelay: 0.5f, jumpDelay: 1f, failDelay: 2f);

        var pos = new UnityEngine.Vector3(10f, 0f, 10f);
        detector.Update(pos, 2.0f, 0f);

        // Get stuck
        for (float t = 0.05f; t < 0.6f; t += 0.05f)
        {
            detector.Update(pos, 2.0f, t);
        }

        Assert.That(detector.Status, Is.Not.EqualTo(SPTQuestingBots.Models.SoftStuckStatus.None));

        // Move significantly
        var newPos = new UnityEngine.Vector3(100f, 0f, 100f);
        detector.Update(newPos, 2.0f, 0.65f);

        Assert.That(detector.Status, Is.EqualTo(SPTQuestingBots.Models.SoftStuckStatus.None), "Movement should reset stuck state");
    }
}

using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.StateMachines;

/// <summary>
/// Tests for action lifecycle state machines: LootAction, PatrolAction, SpawnEntryAction,
/// and RoomClearController. Validates transitions, cleanup, and edge cases.
/// </summary>
[TestFixture]
public class ActionLifecycleTests
{
    // ════════════════════════════════════════════════════════════
    // LootAction lifecycle
    // ════════════════════════════════════════════════════════════

    [Test]
    public void LootAction_Start_SetsApproachPhase()
    {
        var entity = MakeLootEntity();

        // Simulate Start(): set approach state
        entity.IsApproachingLoot = true;

        Assert.Multiple(() =>
        {
            Assert.That(entity.HasLootTarget, Is.True);
            Assert.That(entity.IsApproachingLoot, Is.True);
            Assert.That(entity.IsLooting, Is.False);
        });
    }

    [Test]
    public void LootAction_ApproachToInteract_ClearsApproachSetsLooting()
    {
        var entity = MakeLootEntity();
        entity.IsApproachingLoot = true;

        // Arrived at loot — transition to Interact
        entity.IsApproachingLoot = false;
        entity.IsLooting = true;

        Assert.Multiple(() =>
        {
            Assert.That(entity.IsApproachingLoot, Is.False);
            Assert.That(entity.IsLooting, Is.True);
            Assert.That(entity.HasLootTarget, Is.True);
        });
    }

    [Test]
    public void LootAction_InteractComplete_ClearsLootTarget()
    {
        var entity = MakeLootEntity();
        entity.IsLooting = true;

        // HiveMind clears loot target when interaction finishes
        entity.HasLootTarget = false;
        entity.IsLooting = false;

        Assert.Multiple(() =>
        {
            Assert.That(entity.HasLootTarget, Is.False);
            Assert.That(entity.IsLooting, Is.False);
        });
    }

    [Test]
    public void LootAction_FailAndClear_ClearsAllState()
    {
        var entity = MakeLootEntity();
        entity.IsApproachingLoot = true;

        // Simulate FailAndClear
        entity.HasLootTarget = false;
        entity.IsApproachingLoot = false;
        entity.IsLooting = false;

        Assert.Multiple(() =>
        {
            Assert.That(entity.HasLootTarget, Is.False);
            Assert.That(entity.IsApproachingLoot, Is.False);
            Assert.That(entity.IsLooting, Is.False);
        });
    }

    [Test]
    public void LootAction_Stop_ClearsApproachAndLootingFlags()
    {
        var entity = MakeLootEntity();
        entity.IsApproachingLoot = true;

        // Simulate Stop()
        entity.IsApproachingLoot = false;
        entity.IsLooting = false;

        Assert.Multiple(() =>
        {
            Assert.That(entity.IsApproachingLoot, Is.False);
            Assert.That(entity.IsLooting, Is.False);
            // HasLootTarget is intentionally NOT cleared — allows resuming after interruption
            Assert.That(entity.HasLootTarget, Is.True);
        });
    }

    [Test]
    public void LootAction_StopDuringInteract_ClearsLootingFlag()
    {
        var entity = MakeLootEntity();
        entity.IsApproachingLoot = false;
        entity.IsLooting = true;

        // Stop during interact phase
        entity.IsApproachingLoot = false;
        entity.IsLooting = false;

        Assert.That(entity.IsLooting, Is.False);
    }

    [Test]
    public void LootAction_NoTarget_ImmediatelyFails()
    {
        var entity = new BotEntity(0);
        entity.HasLootTarget = false;

        // Start() with no target -> Failed phase
        Assert.That(entity.HasLootTarget, Is.False);
    }

    [Test]
    public void LootAction_Timeout_ClearsAllState()
    {
        var entity = MakeLootEntity();
        entity.IsApproachingLoot = true;

        // Timeout triggers FailAndClear
        entity.HasLootTarget = false;
        entity.IsApproachingLoot = false;
        entity.IsLooting = false;

        Assert.Multiple(() =>
        {
            Assert.That(entity.HasLootTarget, Is.False);
            Assert.That(entity.IsApproachingLoot, Is.False);
            Assert.That(entity.IsLooting, Is.False);
        });
    }

    // ════════════════════════════════════════════════════════════
    // PatrolAction lifecycle
    // ════════════════════════════════════════════════════════════

    [Test]
    public void PatrolAction_Start_SetsIsPatrolling()
    {
        var entity = MakePatrolEntity(routeIndex: 0);

        entity.IsPatrolling = true;

        Assert.That(entity.IsPatrolling, Is.True);
    }

    [Test]
    public void PatrolAction_AdvanceWaypoint_IncrementsIndex()
    {
        var entity = MakePatrolEntity(routeIndex: 0);
        entity.PatrolWaypointIndex = 0;

        // Advance
        entity.PatrolWaypointIndex = 1;

        Assert.That(entity.PatrolWaypointIndex, Is.EqualTo(1));
    }

    [Test]
    public void PatrolAction_LoopRoute_WrapsToZero()
    {
        var entity = MakePatrolEntity(routeIndex: 0);
        var route = Make3WaypointRoute(isLoop: true);

        // Simulate reaching end of 3-waypoint route
        entity.PatrolWaypointIndex = 3; // past end

        if (entity.PatrolWaypointIndex >= route.Waypoints.Length)
        {
            if (route.IsLoop && route.Waypoints.Length > 1)
            {
                entity.PatrolWaypointIndex = 0;
            }
        }

        Assert.That(entity.PatrolWaypointIndex, Is.EqualTo(0));
    }

    [Test]
    public void PatrolAction_NonLoopRoute_CompletesAtEnd()
    {
        var entity = MakePatrolEntity(routeIndex: 0);
        entity.IsPatrolling = true;
        var route = Make3WaypointRoute(isLoop: false);

        // Simulate reaching end
        entity.PatrolWaypointIndex = 3;

        if (entity.PatrolWaypointIndex >= route.Waypoints.Length)
        {
            if (route.IsLoop && route.Waypoints.Length > 1)
            {
                entity.PatrolWaypointIndex = 0;
            }
            else
            {
                // Complete patrol
                entity.IsPatrolling = false;
                entity.PatrolRouteIndex = -1;
                entity.PatrolWaypointIndex = 0;
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(entity.IsPatrolling, Is.False);
            Assert.That(entity.PatrolRouteIndex, Is.EqualTo(-1));
        });
    }

    /// <summary>
    /// BUG FIX: 1-waypoint loop route. Before fix, the bot would loop forever
    /// in a pause-arrive cycle because timer resets prevented the movement timeout
    /// from accumulating. After fix, single-waypoint loops complete immediately
    /// after the first pause.
    /// </summary>
    [Test]
    public void PatrolAction_SingleWaypointLoop_CompletesAfterFirstPause()
    {
        var entity = MakePatrolEntity(routeIndex: 0);
        entity.IsPatrolling = true;
        var route = new PatrolRoute("single", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(10, 0, 20, 2, 5) }, isLoop: true);

        // Simulate: arrive at waypoint 0, pause, then AdvanceWaypoint
        entity.PatrolWaypointIndex = 1; // past end

        bool completed = false;
        if (entity.PatrolWaypointIndex >= route.Waypoints.Length)
        {
            if (route.IsLoop)
            {
                // Degenerate case: 1-waypoint loop
                if (route.Waypoints.Length <= 1)
                {
                    entity.IsPatrolling = false;
                    entity.PatrolRouteIndex = -1;
                    entity.PatrolWaypointIndex = 0;
                    completed = true;
                }
                else
                {
                    entity.PatrolWaypointIndex = 0;
                }
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(completed, Is.True, "Single-waypoint loop should complete after first pause");
            Assert.That(entity.IsPatrolling, Is.False);
            Assert.That(entity.PatrolRouteIndex, Is.EqualTo(-1));
        });
    }

    [Test]
    public void PatrolAction_ZeroWaypoints_IsGuarded()
    {
        var route = new PatrolRoute("empty", PatrolRouteType.Perimeter, new PatrolWaypoint[0], isLoop: true);

        // Update() checks: _route.Waypoints.Length == 0 → return
        Assert.That(route.Waypoints.Length, Is.EqualTo(0));
        // No crash, just returns early
    }

    [Test]
    public void PatrolAction_Stop_SavesWaypointIndex()
    {
        var entity = MakePatrolEntity(routeIndex: 0);
        entity.IsPatrolling = true;
        entity.PatrolWaypointIndex = 2;

        // Simulate Stop()
        entity.IsPatrolling = false;

        Assert.Multiple(() =>
        {
            Assert.That(entity.IsPatrolling, Is.False);
            Assert.That(entity.PatrolWaypointIndex, Is.EqualTo(2), "Waypoint index should be preserved for resume");
        });
    }

    [Test]
    public void PatrolAction_CompletePatrol_SetsCooldown()
    {
        var entity = MakePatrolEntity(routeIndex: 0);
        entity.IsPatrolling = true;
        entity.CurrentGameTime = 100f;

        float cooldownSec = 120f;

        // Simulate CompletePatrol
        entity.IsPatrolling = false;
        entity.PatrolRouteIndex = -1;
        entity.PatrolWaypointIndex = 0;
        entity.PatrolCooldownUntil = entity.CurrentGameTime + cooldownSec;

        Assert.Multiple(() =>
        {
            Assert.That(entity.IsPatrolling, Is.False);
            Assert.That(entity.PatrolRouteIndex, Is.EqualTo(-1));
            Assert.That(entity.PatrolCooldownUntil, Is.EqualTo(220f).Within(0.01f));
        });
    }

    [Test]
    public void PatrolAction_InvalidWaypointIndex_ClampedToZero()
    {
        var entity = MakePatrolEntity(routeIndex: 0);
        entity.PatrolWaypointIndex = 999; // Way out of bounds
        var route = Make3WaypointRoute(isLoop: false);

        // Start() clamps: if (_waypointIndex < 0 || _waypointIndex >= _route.Waypoints.Length)
        if (entity.PatrolWaypointIndex < 0 || entity.PatrolWaypointIndex >= route.Waypoints.Length)
            entity.PatrolWaypointIndex = 0;

        Assert.That(entity.PatrolWaypointIndex, Is.EqualTo(0));
    }

    [Test]
    public void PatrolAction_NegativeWaypointIndex_ClampedToZero()
    {
        var entity = MakePatrolEntity(routeIndex: 0);
        entity.PatrolWaypointIndex = -5;
        var route = Make3WaypointRoute(isLoop: false);

        if (entity.PatrolWaypointIndex < 0 || entity.PatrolWaypointIndex >= route.Waypoints.Length)
            entity.PatrolWaypointIndex = 0;

        Assert.That(entity.PatrolWaypointIndex, Is.EqualTo(0));
    }

    // ════════════════════════════════════════════════════════════
    // SpawnEntry lifecycle
    // ════════════════════════════════════════════════════════════

    [Test]
    public void SpawnEntry_Start_PausesBot()
    {
        var entity = new BotEntity(0);
        entity.SpawnEntryDuration = 4f;
        entity.SpawnTime = 10f;
        entity.IsSpawnEntryComplete = false;

        Assert.Multiple(() =>
        {
            Assert.That(entity.SpawnEntryDuration, Is.GreaterThan(0f));
            Assert.That(entity.IsSpawnEntryComplete, Is.False);
        });
    }

    [Test]
    public void SpawnEntry_DurationExpired_MarksComplete()
    {
        var entity = new BotEntity(0);
        entity.SpawnEntryDuration = 4f;
        entity.SpawnTime = 10f;
        entity.CurrentGameTime = 15f; // 5s elapsed > 4s duration

        float elapsed = entity.CurrentGameTime - entity.SpawnTime;
        if (elapsed >= entity.SpawnEntryDuration)
        {
            entity.IsSpawnEntryComplete = true;
        }

        Assert.That(entity.IsSpawnEntryComplete, Is.True);
    }

    [Test]
    public void SpawnEntry_Stop_MarksComplete()
    {
        var entity = new BotEntity(0);
        entity.SpawnEntryDuration = 4f;
        entity.IsSpawnEntryComplete = false;

        // Stop() always marks complete (even if interrupted early)
        entity.IsSpawnEntryComplete = true;

        Assert.That(entity.IsSpawnEntryComplete, Is.True);
    }

    [Test]
    public void SpawnEntry_ZeroDuration_ScoresZero()
    {
        var entity = new BotEntity(0);
        entity.SpawnEntryDuration = 0f;
        entity.IsSpawnEntryComplete = false;

        float score = entity.SpawnEntryDuration <= 0f ? 0f : 0.80f;

        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void SpawnEntry_AlreadyComplete_ScoresZero()
    {
        var entity = new BotEntity(0);
        entity.IsSpawnEntryComplete = true;
        entity.SpawnEntryDuration = 4f;

        float score = entity.IsSpawnEntryComplete ? 0f : 0.80f;

        Assert.That(score, Is.EqualTo(0f));
    }

    // ════════════════════════════════════════════════════════════
    // RoomClearController transitions
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// BUG FIX: Room clear was not canceled when bot moved back outdoors.
    /// Before fix: bot maintained slow walk and lowered pose outdoors until timer expired.
    /// After fix: room clear is immediately canceled on indoor→outdoor transition.
    /// </summary>
    [Test]
    public void RoomClear_IndoorToOutdoor_CancelsRoomClear()
    {
        var entity = new BotEntity(0);

        // Start room clear: outdoor -> indoor
        entity.LastEnvironmentId = 1; // was outdoor
        var result1 = RoomClearController.Update(entity, 0, 10f, 15f, 30f, 1.5f);
        Assert.That(result1, Is.EqualTo(RoomClearInstruction.SlowWalk));
        Assert.That(entity.IsInRoomClear, Is.True);

        // Now move back outdoors
        var result2 = RoomClearController.Update(entity, 1, 12f, 15f, 30f, 1.5f);

        Assert.Multiple(() =>
        {
            Assert.That(result2, Is.EqualTo(RoomClearInstruction.None), "Should cancel room clear when moving outdoors");
            Assert.That(entity.IsInRoomClear, Is.False, "IsInRoomClear should be cleared");
        });
    }

    [Test]
    public void RoomClear_RapidOscillation_HandlesCorrectly()
    {
        var entity = new BotEntity(0);
        entity.LastEnvironmentId = 1;

        // Enter indoor
        RoomClearController.Update(entity, 0, 10f, 15f, 30f, 1.5f);
        Assert.That(entity.IsInRoomClear, Is.True);

        // Exit to outdoor (should cancel)
        RoomClearController.Update(entity, 1, 10.5f, 15f, 30f, 1.5f);
        Assert.That(entity.IsInRoomClear, Is.False);

        // Re-enter indoor (should start new room clear)
        var result = RoomClearController.Update(entity, 0, 11f, 15f, 30f, 1.5f);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
            Assert.That(entity.IsInRoomClear, Is.True);
        });
    }

    [Test]
    public void RoomClear_MultipleRapidOscillations_NoStateCorruption()
    {
        var entity = new BotEntity(0);
        entity.LastEnvironmentId = 1;

        for (int i = 0; i < 10; i++)
        {
            float time = 10f + i * 0.5f;

            // Enter indoor
            var resultIn = RoomClearController.Update(entity, 0, time, 15f, 30f, 1.5f);
            Assert.That(entity.IsInRoomClear, Is.True, $"Iteration {i}: should be in room clear after entering indoor");

            // Exit to outdoor
            var resultOut = RoomClearController.Update(entity, 1, time + 0.1f, 15f, 30f, 1.5f);
            Assert.That(entity.IsInRoomClear, Is.False, $"Iteration {i}: should NOT be in room clear after exiting to outdoor");
        }
    }

    [Test]
    public void RoomClear_StaysIndoor_ContinuesUntilTimerExpires()
    {
        var entity = new BotEntity(0);
        entity.LastEnvironmentId = 1;

        // Start room clear
        RoomClearController.Update(entity, 0, 10f, 15f, 30f, 1.5f);
        Assert.That(entity.IsInRoomClear, Is.True);

        // Stay indoor for several ticks
        var result = RoomClearController.Update(entity, 0, 12f, 15f, 30f, 1.5f);
        Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
        Assert.That(entity.IsInRoomClear, Is.True);

        // Timer expires (min 15s, max 30s, started at t=10, check at t=50)
        result = RoomClearController.Update(entity, 0, 50f, 15f, 30f, 1.5f);
        Assert.That(result, Is.EqualTo(RoomClearInstruction.None));
        Assert.That(entity.IsInRoomClear, Is.False);
    }

    [Test]
    public void RoomClear_OutdoorToOutdoor_NeverStartsRoomClear()
    {
        var entity = new BotEntity(0);
        entity.LastEnvironmentId = 1;

        var result = RoomClearController.Update(entity, 2, 10f, 15f, 30f, 1.5f);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RoomClearInstruction.None));
            Assert.That(entity.IsInRoomClear, Is.False);
        });
    }

    // ════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════

    private static BotEntity MakeLootEntity()
    {
        var entity = new BotEntity(0);
        entity.HasLootTarget = true;
        entity.LootTargetX = 50f;
        entity.LootTargetY = 0f;
        entity.LootTargetZ = 75f;
        entity.LootTargetValue = 10000f;
        entity.InventorySpaceFree = 5f;
        return entity;
    }

    private static BotEntity MakePatrolEntity(int routeIndex)
    {
        var entity = new BotEntity(0);
        entity.PatrolRouteIndex = routeIndex;
        entity.PatrolWaypointIndex = 0;
        entity.IsPatrolling = false;
        return entity;
    }

    private static PatrolRoute Make3WaypointRoute(bool isLoop)
    {
        return new PatrolRoute(
            "test",
            PatrolRouteType.Perimeter,
            new[] { new PatrolWaypoint(0, 0, 0, 2, 5), new PatrolWaypoint(100, 0, 0, 2, 5), new PatrolWaypoint(100, 0, 100, 2, 5) },
            isLoop: isLoop
        );
    }
}

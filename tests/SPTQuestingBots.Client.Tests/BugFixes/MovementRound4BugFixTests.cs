using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Helpers;
using SPTQuestingBots.Models;
using SPTQuestingBots.Models.Pathing;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.BugFixes;

/// <summary>
/// Regression tests for bugs found during the Round 4 movement/pathing audit.
/// Each test reproduces the bug condition and validates the fix.
/// </summary>
[TestFixture]
public class MovementRound4BugFixTests
{
    // ── BUG 1: FormationSpeedController Walk zone ignores boss sprint ──

    [Test]
    public void Formation_BossSprintingFollowerInWalkZone_ShouldMatchBossSprint()
    {
        // BUG: When boss is sprinting and follower is 15-30m away (Walk zone),
        // the decision was always Walk (never sprint), causing oscillating
        // rubber-band behavior where follower falls behind then catches up.
        var config = FormationConfig.Default; // catchUp=30, match=15, slow=5

        // 20m from boss (squared = 400) — in the Walk zone (15-30m)
        float distToBossSqr = 20f * 20f;
        float distToTacticalSqr = 50f * 50f; // far from tactical position

        var decision = FormationSpeedController.ComputeSpeedDecision(bossIsSprinting: true, distToBossSqr, distToTacticalSqr, config);

        // With the fix, decision should be MatchBoss (not Walk) when boss sprints
        Assert.That(decision, Is.EqualTo(FormationSpeedDecision.MatchBoss));
        Assert.That(
            FormationSpeedController.ShouldSprint(decision, bossIsSprinting: true),
            Is.True,
            "Follower should sprint to match sprinting boss at 20m"
        );
    }

    [Test]
    public void Formation_BossWalkingFollowerInWalkZone_ShouldWalk()
    {
        // When boss is NOT sprinting, the Walk zone should still produce Walk
        var config = FormationConfig.Default;
        float distToBossSqr = 20f * 20f;
        float distToTacticalSqr = 50f * 50f;

        var decision = FormationSpeedController.ComputeSpeedDecision(bossIsSprinting: false, distToBossSqr, distToTacticalSqr, config);

        Assert.That(decision, Is.EqualTo(FormationSpeedDecision.Walk));
        Assert.That(
            FormationSpeedController.ShouldSprint(decision, bossIsSprinting: false),
            Is.False,
            "Follower should walk when boss is walking"
        );
    }

    [Test]
    public void Formation_BossSprintingFollowerJustOutsideMatch_ShouldSprint()
    {
        // Edge case: follower at 15.1m (just entered Walk zone) while boss sprints
        var config = FormationConfig.Default;
        float distToBossSqr = 15.1f * 15.1f; // just above MatchSpeedDistanceSqr (225)
        float distToTacticalSqr = 50f * 50f;

        var decision = FormationSpeedController.ComputeSpeedDecision(bossIsSprinting: true, distToBossSqr, distToTacticalSqr, config);

        Assert.That(decision, Is.EqualTo(FormationSpeedDecision.MatchBoss));
        Assert.That(FormationSpeedController.ShouldSprint(decision, bossIsSprinting: true), Is.True);
    }

    [Test]
    public void Formation_BossSprintingFollowerJustBeforeCatchUp_ShouldSprint()
    {
        // Edge case: follower at 29m (near CatchUp threshold) while boss sprints
        var config = FormationConfig.Default;
        float distToBossSqr = 29f * 29f;
        float distToTacticalSqr = 50f * 50f;

        var decision = FormationSpeedController.ComputeSpeedDecision(bossIsSprinting: true, distToBossSqr, distToTacticalSqr, config);

        // Should still be MatchBoss (sprinting), not the forced Sprint
        Assert.That(decision, Is.EqualTo(FormationSpeedDecision.MatchBoss));
        Assert.That(FormationSpeedController.ShouldSprint(decision, bossIsSprinting: true), Is.True);
    }

    [Test]
    public void Formation_NoOscillation_BossSprintPullsAhead()
    {
        // Simulate the oscillation scenario:
        // Boss sprints away, follower should keep sprinting across the Walk zone
        var config = FormationConfig.Default;
        float distToTacticalSqr = 100f * 100f;

        // Boss is at 14m → MatchBoss zone → sprint
        for (float dist = 14f; dist <= 31f; dist += 1f)
        {
            float distSqr = dist * dist;
            var decision = FormationSpeedController.ComputeSpeedDecision(bossIsSprinting: true, distSqr, distToTacticalSqr, config);

            bool shouldSprint = FormationSpeedController.ShouldSprint(decision, bossIsSprinting: true);
            Assert.That(shouldSprint, Is.True, $"Follower should sprint at {dist}m when boss is sprinting");
        }
    }

    // ── BUG 2: PositionHistory.GetDistanceSqr includes Y axis ─────────

    [Test]
    public void PositionHistory_VerticalMovementOnly_ReturnsZeroDistance()
    {
        // BUG: A bot stuck against a wall that keeps jumping would show vertical
        // displacement in sqrMagnitude, defeating hard stuck detection.
        // Fix: Use XZ-plane distance only (ignore Y).
        var history = new PositionHistory(5);

        // Bot stays at same XZ position but jumps up and down (Y changes)
        history.Update(new Vector3(10, 0, 20));
        history.Update(new Vector3(10, 3, 20)); // jumped up 3m
        history.Update(new Vector3(10, 0, 20)); // landed
        history.Update(new Vector3(10, 2, 20)); // jumped again
        history.Update(new Vector3(10, 0, 20)); // landed
        history.Update(new Vector3(10, 5, 20)); // big jump (vault attempt)

        // XZ position never changed → distance should be 0
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(0f), "Vertical movement should not count as XZ displacement");
    }

    [Test]
    public void PositionHistory_XZMovementWithVerticalNoise_OnlyCountsXZ()
    {
        // Bot moves 4m east and 3m north, but also changes Y by 10m
        var history = new PositionHistory(1); // buffer size 2
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(4, 10, 3));

        // XZ distance squared = 16 + 9 = 25 (Y=10 is ignored)
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(25f), "Only XZ displacement should be measured");
    }

    [Test]
    public void PositionHistory_WarmupProjection_XZOnly()
    {
        // During warmup, the projection scaling should also be XZ-only
        var history = new PositionHistory(4); // buffer size 5
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(2, 100, 0)); // massive Y change, small X change

        // XZ observedDistSqr = 4 (only X=2), scaleFactor = 4/1 = 4
        // result = 4 * 16 = 64
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(64f), "Warmup projection should use XZ-only distance");
    }

    [Test]
    public void HardStuck_BotJumpingInPlace_DetectedAsStuck()
    {
        // End-to-end test: a bot stuck against a wall that jumps repeatedly
        // should still be detected as hard stuck (Y changes don't help)
        var detector = new HardStuckDetector(10, 3f, 8f, 12f);
        float time = 0f;
        float dt = 0.1f;
        float speed = 2.0f;

        // Initialize
        detector.Update(new Vector3(5, 0, 5), speed, time);

        // Bot jumps up and down but stays at same XZ position
        bool transitioned = false;
        for (int i = 0; i < 50; i++)
        {
            time += dt;
            float y = (i % 2 == 0) ? 0f : 3f; // alternating jump
            if (detector.Update(new Vector3(5, y, 5), speed, time))
            {
                if (detector.Status == HardStuckStatus.Retrying)
                {
                    transitioned = true;
                    break;
                }
            }
        }

        Assert.That(transitioned, Is.True, "Bot jumping in place should be detected as hard stuck");
    }

    // ── BUG 3: CustomPathFollower.Tick per-frame retry exhaustion ──────

    [Test]
    public void PathFollower_PartialPath_DoesNotExhaustRetriesInOneFrame()
    {
        // BUG: IncrementRetry was called every tick on a partial path.
        // With MaxRetries=10 at 60fps, path failed in ~167ms.
        // Fix: Only increment retry ONCE per partial path detection.
        var config = CustomMoverConfig.CreateDefault(); // MaxRetries=10
        var follower = new CustomPathFollower(config);

        // Path that doesn't reach the target (partial path)
        var target = new Vector3(100, 0, 0);
        var corners = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0), // last corner is 90m from target
        };
        follower.SetPath(corners, target);
        follower.AdvanceCorner(); // move to last corner

        // Tick 60 times (simulating 1 second at 60fps)
        int failedCount = 0;
        for (int i = 0; i < 60; i++)
        {
            var status = follower.Tick(new Vector3(5, 0, 0), isSprinting: false);
            if (status == PathFollowerStatus.Failed)
                failedCount++;
        }

        // With the fix, retry count should be exactly 1 (not 10+)
        Assert.That(follower.RetryCount, Is.EqualTo(1), "Retry count should only increment once per partial path, not every tick");
        Assert.That(failedCount, Is.EqualTo(0), "Path should NOT fail after only 1 retry (max is 10)");
    }

    [Test]
    public void PathFollower_PartialPath_RetriesOnlyAfterNewPath()
    {
        // Each SetPath should allow one more retry
        var config = CustomMoverConfig.CreateDefault(); // MaxRetries=10
        var follower = new CustomPathFollower(config);
        var target = new Vector3(100, 0, 0);
        var partialCorners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };

        for (int attempt = 0; attempt < 9; attempt++)
        {
            follower.SetPath(partialCorners, target);
            follower.AdvanceCorner();

            // Tick several times — retry should only fire once
            for (int tick = 0; tick < 10; tick++)
            {
                follower.Tick(new Vector3(5, 0, 0), false);
            }

            Assert.That(follower.RetryCount, Is.EqualTo(attempt + 1), $"After attempt {attempt + 1}, retry count should be {attempt + 1}");
        }

        // One more attempt should exhaust retries
        follower.SetPath(partialCorners, target);
        follower.AdvanceCorner();
        var status = follower.Tick(new Vector3(5, 0, 0), false);
        Assert.That(status, Is.EqualTo(PathFollowerStatus.Failed));
        Assert.That(follower.RetryCount, Is.EqualTo(10));
    }

    [Test]
    public void PathFollower_PartialPath_StillReportsFollowing()
    {
        // On a partial path, the follower should stay in Following status
        // (not immediately fail) so the bot can walk the available segment
        var config = CustomMoverConfig.CreateDefault();
        var follower = new CustomPathFollower(config);
        var target = new Vector3(100, 0, 0);
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };

        follower.SetPath(corners, target);
        follower.AdvanceCorner();

        var status = follower.Tick(new Vector3(5, 0, 0), false);
        Assert.That(status, Is.EqualTo(PathFollowerStatus.Following), "Partial path should stay Following after first retry increment");
    }

    [Test]
    public void PathFollower_ResetPath_ClearsPartialPathFlag()
    {
        // After ResetPath, a new partial path should get fresh retries
        var config = CustomMoverConfig.CreateDefault();
        var follower = new CustomPathFollower(config);
        var target = new Vector3(100, 0, 0);
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };

        // First partial path
        follower.SetPath(corners, target);
        follower.AdvanceCorner();
        follower.Tick(new Vector3(5, 0, 0), false);
        Assert.That(follower.RetryCount, Is.EqualTo(1));

        // Full reset
        follower.ResetPath();
        Assert.That(follower.RetryCount, Is.EqualTo(0));

        // New partial path should work fresh
        follower.SetPath(corners, target);
        follower.AdvanceCorner();
        follower.Tick(new Vector3(5, 0, 0), false);
        Assert.That(follower.RetryCount, Is.EqualTo(1));
    }

    [Test]
    public void PathFollower_CompletePath_NoRetryIncrement()
    {
        // A path that DOES reach the target should not trigger retries
        var config = CustomMoverConfig.CreateDefault(); // DestinationEpsilon=1.5
        var follower = new CustomPathFollower(config);
        var target = new Vector3(10, 0, 0);
        var corners = new[] { new Vector3(0, 0, 0), target };

        follower.SetPath(corners, target);
        follower.AdvanceCorner(); // on last corner

        // Tick near last corner but not at destination
        for (int i = 0; i < 20; i++)
        {
            follower.Tick(new Vector3(7, 0, 0), false); // 3m from target
        }

        Assert.That(follower.RetryCount, Is.EqualTo(0), "Complete path should never increment retries");
    }

    // ── BUG 4: NavMeshHelpers bounds copy-paste ───────────────────────
    // (Cannot unit test directly — uses Unity Bounds type not available in test shim.
    //  Bug was trivial copy-paste: size.x used 3 times instead of x, y, z.
    //  Fix verified by code review.)

    // ── BUG 5: BotPathData &= dead code path ─────────────────────────
    // (Cannot unit test directly — uses BotOwner, GameWorld, NavMesh.
    //  Bug: `requiresUpdate &= expr` when requiresUpdate is already false
    //  always yields false. Fix changes to explicit if-block with assignment.
    //  Fix verified by code review.)
}

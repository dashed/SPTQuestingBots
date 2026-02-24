using NUnit.Framework;
using SPTQuestingBots.Models.Pathing;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ErrorRecovery;

/// <summary>
/// Tests for NavMesh path failure handling — verifies that the path following
/// system degrades gracefully when paths are null, empty, or partial.
/// </summary>
[TestFixture]
public class NavMeshFailureTests
{
    private CustomPathFollower _follower;

    [SetUp]
    public void SetUp()
    {
        _follower = new CustomPathFollower(CustomMoverConfig.CreateDefault());
    }

    // ── SetPath edge cases ──

    [Test]
    public void SetPath_NullCorners_FailsSafely()
    {
        _follower.SetPath(null, new Vector3(10, 0, 0));

        Assert.AreEqual(PathFollowerStatus.Failed, _follower.Status);
        Assert.IsFalse(_follower.HasPath);
        Assert.AreEqual(0, _follower.TotalCorners);
    }

    [Test]
    public void SetPath_EmptyCorners_FailsSafely()
    {
        _follower.SetPath(new Vector3[0], new Vector3(10, 0, 0));

        Assert.AreEqual(PathFollowerStatus.Failed, _follower.Status);
        Assert.IsFalse(_follower.HasPath);
    }

    [Test]
    public void SetPath_SingleCorner_FollowingStatus()
    {
        var corners = new[] { new Vector3(5, 0, 5) };
        _follower.SetPath(corners, new Vector3(5, 0, 5));

        Assert.AreEqual(PathFollowerStatus.Following, _follower.Status);
        Assert.AreEqual(1, _follower.TotalCorners);
    }

    // ── Tick with no path ──

    [Test]
    public void Tick_NoPathSet_ReturnsIdleSafely()
    {
        var status = _follower.Tick(Vector3.zero, false);
        Assert.AreEqual(PathFollowerStatus.Idle, status);
    }

    [Test]
    public void Tick_AfterReset_ReturnsIdleSafely()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));
        _follower.ResetPath();

        var status = _follower.Tick(Vector3.zero, false);
        Assert.AreEqual(PathFollowerStatus.Idle, status);
    }

    [Test]
    public void Tick_AfterFailed_StaysFailedSafely()
    {
        _follower.SetPath(null, Vector3.zero);
        var status = _follower.Tick(Vector3.zero, false);
        Assert.AreEqual(PathFollowerStatus.Failed, status);
    }

    // ── Partial path handling ──

    [Test]
    public void Tick_PartialPath_DetectsAndRetries()
    {
        // Path ends at (5,0,5) but target is at (100,0,100) — partial path
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 5) };
        _follower.SetPath(corners, new Vector3(100, 0, 100));

        // Walk to the last corner
        _follower.Tick(new Vector3(0, 0, 0), false);
        _follower.AdvanceCorner(); // move to last corner

        // Now tick at a position near the last corner — triggers partial detection
        var status = _follower.Tick(new Vector3(5, 0, 5), false);
        Assert.AreEqual(PathFollowerStatus.Following, status);
        Assert.AreEqual(1, _follower.RetryCount);
    }

    [Test]
    public void Tick_PartialPath_RetriesExhausted_FailsGracefully()
    {
        var config = CustomMoverConfig.CreateDefault();
        var follower = new CustomPathFollower(config);

        // Path doesn't reach target
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 5) };
        follower.SetPath(corners, new Vector3(100, 0, 100));

        // Walk to last corner
        follower.AdvanceCorner();

        // Exhaust retries
        PathFollowerStatus lastStatus = PathFollowerStatus.Following;
        for (int i = 0; i < config.MaxRetries + 1; i++)
        {
            // Reset path each time to clear _partialPathDetected flag
            follower.SetPath(corners, new Vector3(100, 0, 100));
            follower.AdvanceCorner();
            lastStatus = follower.Tick(new Vector3(5, 0, 5), false);
            if (lastStatus == PathFollowerStatus.Failed)
                break;
        }

        Assert.AreEqual(PathFollowerStatus.Failed, lastStatus);
    }

    // ── ComputeMoveDirection safety ──

    [Test]
    public void ComputeMoveDirection_NoPath_ReturnsZero()
    {
        var dir = _follower.ComputeMoveDirection(Vector3.zero);
        Assert.AreEqual(Vector3.zero, dir);
    }

    [Test]
    public void ComputeMoveDirection_AfterFail_ReturnsZero()
    {
        _follower.SetPath(null, Vector3.zero);
        var dir = _follower.ComputeMoveDirection(Vector3.zero);
        Assert.AreEqual(Vector3.zero, dir);
    }

    [Test]
    public void ComputeRawDirection_NoPath_ReturnsZero()
    {
        var dir = _follower.ComputeRawDirection(Vector3.zero);
        Assert.AreEqual(Vector3.zero, dir);
    }

    // ── HasReachedCorner safety ──

    [Test]
    public void HasReachedCorner_NoPath_ReturnsFalse()
    {
        Assert.IsFalse(_follower.HasReachedCorner(Vector3.zero, false));
    }

    [Test]
    public void IsCloseEnoughForCornerCut_NoPath_ReturnsFalse()
    {
        Assert.IsFalse(_follower.IsCloseEnoughForCornerCut(Vector3.zero));
    }

    [Test]
    public void TryCornerCut_NoPath_ReturnsFalse()
    {
        Assert.IsFalse(_follower.TryCornerCut(Vector3.zero, true));
    }

    [Test]
    public void TryCornerCut_OnLastCorner_ReturnsFalse()
    {
        var corners = new[] { new Vector3(0, 0, 0) };
        _follower.SetPath(corners, Vector3.zero);
        Assert.IsFalse(_follower.TryCornerCut(Vector3.zero, true));
    }

    // ── AdvanceCorner beyond bounds ──

    [Test]
    public void AdvanceCorner_PastEnd_ReturnsFalse()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 0) };
        _follower.SetPath(corners, new Vector3(5, 0, 0));

        Assert.IsTrue(_follower.AdvanceCorner()); // 0 -> 1
        Assert.IsFalse(_follower.AdvanceCorner()); // past end
    }

    [Test]
    public void AdvanceCorner_NullPath_ReturnsFalse()
    {
        Assert.IsFalse(_follower.AdvanceCorner());
    }

    // ── DoesPathReachTarget with empty/null ──

    [Test]
    public void DoesPathReachTarget_NoPath_ReturnsFalse()
    {
        Assert.IsFalse(_follower.DoesPathReachTarget());
    }

    [Test]
    public void HasReachedDestination_NoPath_ReturnsCorrectly()
    {
        // Target defaults to zero — position at zero should be "reached"
        // but the semantics don't matter here; we just verify no crash
        _follower.HasReachedDestination(Vector3.zero);
    }

    // ── Sprint decisions with no path ──

    [Test]
    public void CanSprint_NoPath_DoesNotThrow()
    {
        // SprintAngleJitter.ComputeAngleJitter handles null/empty corners
        Assert.DoesNotThrow(() => _follower.CanSprint(SprintUrgency.Medium));
    }

    [Test]
    public void ComputeSprintAngleJitter_NoPath_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _follower.ComputeSprintAngleJitter());
    }
}

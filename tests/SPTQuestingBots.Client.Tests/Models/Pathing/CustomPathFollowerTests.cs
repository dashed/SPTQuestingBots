using System;
using NUnit.Framework;
using SPTQuestingBots.Models.Pathing;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.Models.Pathing;

[TestFixture]
public class CustomPathFollowerTests
{
    private CustomPathFollower _follower;

    [SetUp]
    public void SetUp()
    {
        _follower = new CustomPathFollower(CustomMoverConfig.CreateDefault());
    }

    // --- Lifecycle ---

    [Test]
    public void NewFollower_IsIdle()
    {
        Assert.AreEqual(PathFollowerStatus.Idle, _follower.Status);
        Assert.IsFalse(_follower.HasPath);
        Assert.AreEqual(0, _follower.TotalCorners);
    }

    [Test]
    public void SetPath_ValidCorners_StatusFollowing()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        Assert.AreEqual(PathFollowerStatus.Following, _follower.Status);
        Assert.IsTrue(_follower.HasPath);
        Assert.AreEqual(2, _follower.TotalCorners);
        Assert.AreEqual(0, _follower.CurrentCorner);
    }

    [Test]
    public void SetPath_NullCorners_StatusFailed()
    {
        _follower.SetPath(null, new Vector3(10, 0, 0));
        Assert.AreEqual(PathFollowerStatus.Failed, _follower.Status);
        Assert.IsFalse(_follower.HasPath);
    }

    [Test]
    public void SetPath_EmptyCorners_StatusFailed()
    {
        _follower.SetPath(new Vector3[0], new Vector3(10, 0, 0));
        Assert.AreEqual(PathFollowerStatus.Failed, _follower.Status);
    }

    [Test]
    public void ResetPath_ClearsEverything()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));
        _follower.ResetPath();

        Assert.AreEqual(PathFollowerStatus.Idle, _follower.Status);
        Assert.IsFalse(_follower.HasPath);
        Assert.AreEqual(0, _follower.RetryCount);
    }

    [Test]
    public void FailPath_SetsStatusFailed()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));
        _follower.FailPath();

        Assert.AreEqual(PathFollowerStatus.Failed, _follower.Status);
    }

    // --- Corner Reaching ---

    [Test]
    public void HasReachedCorner_Walk_WithinEpsilon_True()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        // Walk epsilon is 0.35m, position is 0.3m away → within epsilon
        var pos = new Vector3(0.3f, 0, 0);
        Assert.IsTrue(_follower.HasReachedCorner(pos, isSprinting: false));
    }

    [Test]
    public void HasReachedCorner_Walk_OutsideEpsilon_False()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        // Walk epsilon is 0.35m, position is 1m away → outside
        var pos = new Vector3(1, 0, 0);
        Assert.IsFalse(_follower.HasReachedCorner(pos, isSprinting: false));
    }

    [Test]
    public void HasReachedCorner_Sprint_WiderEpsilon()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        // Sprint epsilon is 0.6m, position is 0.5m away → within sprint, outside walk
        var pos = new Vector3(0.5f, 0, 0);
        Assert.IsFalse(_follower.HasReachedCorner(pos, isSprinting: false));
        Assert.IsTrue(_follower.HasReachedCorner(pos, isSprinting: true));
    }

    [Test]
    public void HasReachedCorner_NoPath_False()
    {
        Assert.IsFalse(_follower.HasReachedCorner(Vector3.zero, false));
    }

    // --- Corner Advancement ---

    [Test]
    public void AdvanceCorner_IncrementsIndex()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        Assert.AreEqual(0, _follower.CurrentCorner);
        Assert.IsTrue(_follower.AdvanceCorner());
        Assert.AreEqual(1, _follower.CurrentCorner);
        Assert.IsTrue(_follower.AdvanceCorner());
        Assert.AreEqual(2, _follower.CurrentCorner);
        Assert.IsFalse(_follower.AdvanceCorner()); // past end
    }

    [Test]
    public void AdvanceCorner_NoPath_ReturnsFalse()
    {
        Assert.IsFalse(_follower.AdvanceCorner());
    }

    [Test]
    public void IsOnLastCorner_CorrectDetection()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        Assert.IsFalse(_follower.IsOnLastCorner);
        _follower.AdvanceCorner();
        Assert.IsFalse(_follower.IsOnLastCorner);
        _follower.AdvanceCorner();
        Assert.IsTrue(_follower.IsOnLastCorner);
    }

    // --- Destination ---

    [Test]
    public void HasReachedDestination_WithinEpsilon_True()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        // Destination epsilon is 1.5m
        var pos = new Vector3(9.5f, 0, 0);
        Assert.IsTrue(_follower.HasReachedDestination(pos));
    }

    [Test]
    public void HasReachedDestination_OutsideEpsilon_False()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        var pos = new Vector3(5, 0, 0);
        Assert.IsFalse(_follower.HasReachedDestination(pos));
    }

    [Test]
    public void DoesPathReachTarget_LastCornerNearTarget_True()
    {
        var target = new Vector3(10, 0, 0);
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(9.8f, 0, 0) };
        _follower.SetPath(corners, target);

        Assert.IsTrue(_follower.DoesPathReachTarget());
    }

    [Test]
    public void DoesPathReachTarget_LastCornerFarFromTarget_False()
    {
        var target = new Vector3(100, 0, 0);
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, target);

        Assert.IsFalse(_follower.DoesPathReachTarget());
    }

    [Test]
    public void IsTargetCurrent_SameTarget_True()
    {
        _follower.SetPath(new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) }, new Vector3(10, 0, 0));

        Assert.IsTrue(_follower.IsTargetCurrent(new Vector3(10, 0, 0)));
        Assert.IsTrue(_follower.IsTargetCurrent(new Vector3(10.5f, 0, 0))); // within 1.5m
    }

    [Test]
    public void IsTargetCurrent_DifferentTarget_False()
    {
        _follower.SetPath(new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) }, new Vector3(10, 0, 0));

        Assert.IsFalse(_follower.IsTargetCurrent(new Vector3(50, 0, 0)));
    }

    // --- Move Direction ---

    [Test]
    public void ComputeMoveDirection_TowardCurrentCorner()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        // Bot is behind the first corner, direction should be toward it
        var dir = _follower.ComputeMoveDirection(new Vector3(-5, 0, 0));
        Assert.Greater(dir.x, 0f); // heading east
    }

    [Test]
    public void ComputeMoveDirection_IncludesSpringForce()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(20, 0, 0) };
        _follower.SetPath(corners, new Vector3(20, 0, 0));
        _follower.AdvanceCorner(); // now heading toward (10,0,0)

        // Bot is 5m north of the east-west path
        var dir = _follower.ComputeMoveDirection(new Vector3(5, 0, 5));

        // Direction should have a southward (negative Z) component from spring force
        Assert.Less(dir.z, 0f, "Spring force should pull south toward path");
    }

    [Test]
    public void ComputeMoveDirection_NoPath_ReturnsZero()
    {
        var dir = _follower.ComputeMoveDirection(Vector3.zero);
        Assert.AreEqual(0f, dir.x, 0.001f);
        Assert.AreEqual(0f, dir.z, 0.001f);
    }

    [Test]
    public void ComputeRawDirection_TowardCorner()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        var raw = _follower.ComputeRawDirection(new Vector3(-3, 0, 0));
        Assert.AreEqual(3f, raw.x, 0.01f); // (0,0,0) - (-3,0,0) = (3,0,0)
    }

    // --- Sprint Decision ---

    [Test]
    public void CanSprint_StraightPath_AllUrgencies_True()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(10, 0, 0), new Vector3(15, 0, 0) };
        _follower.SetPath(corners, new Vector3(15, 0, 0));

        Assert.IsTrue(_follower.CanSprint(SprintUrgency.Low));
        Assert.IsTrue(_follower.CanSprint(SprintUrgency.Medium));
        Assert.IsTrue(_follower.CanSprint(SprintUrgency.High));
    }

    [Test]
    public void CanSprint_SharpTurn_LowUrgency_False()
    {
        var corners = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(5, 0, 0),
            new Vector3(5, 0, 5), // 90° turn
        };
        _follower.SetPath(corners, new Vector3(5, 0, 5));

        Assert.IsFalse(_follower.CanSprint(SprintUrgency.Low));
    }

    [Test]
    public void ComputeSprintAngleJitter_NoPath_ReturnsZero()
    {
        Assert.AreEqual(0f, _follower.ComputeSprintAngleJitter());
    }

    // --- Tick ---

    [Test]
    public void Tick_AdvancesCornerWhenReached()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        // Bot at first corner (within walk epsilon 0.35m)
        var pos = new Vector3(0.1f, 0, 0);
        _follower.Tick(pos, isSprinting: false);

        Assert.AreEqual(1, _follower.CurrentCorner);
    }

    [Test]
    public void Tick_ReachesDestination_StatusReached()
    {
        var target = new Vector3(10, 0, 0);
        var corners = new[] { new Vector3(0, 0, 0), target };
        _follower.SetPath(corners, target);
        _follower.AdvanceCorner(); // now on last corner

        // Bot near destination
        var pos = new Vector3(9.5f, 0, 0);
        var status = _follower.Tick(pos, isSprinting: false);

        Assert.AreEqual(PathFollowerStatus.Reached, status);
    }

    [Test]
    public void Tick_NotFollowing_ReturnsCurrentStatus()
    {
        Assert.AreEqual(PathFollowerStatus.Idle, _follower.Tick(Vector3.zero, false));
    }

    // --- Retry ---

    [Test]
    public void IncrementRetry_ExhaustsAtMaxRetries()
    {
        var config = CustomMoverConfig.CreateDefault();
        var follower = new CustomPathFollower(config);

        for (int i = 0; i < config.MaxRetries - 1; i++)
        {
            Assert.IsFalse(follower.IncrementRetry());
        }

        Assert.IsTrue(follower.IncrementRetry()); // exhausted
    }

    [Test]
    public void ResetPath_ResetsRetryCount()
    {
        _follower.IncrementRetry();
        _follower.IncrementRetry();
        Assert.AreEqual(2, _follower.RetryCount);

        _follower.ResetPath();
        Assert.AreEqual(0, _follower.RetryCount);
    }

    // --- Corner Cut ---

    [Test]
    public void IsCloseEnoughForCornerCut_Within1m_True()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        Assert.IsTrue(_follower.IsCloseEnoughForCornerCut(new Vector3(0.5f, 0, 0)));
    }

    [Test]
    public void IsCloseEnoughForCornerCut_FarAway_False()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        Assert.IsFalse(_follower.IsCloseEnoughForCornerCut(new Vector3(5, 0, 0)));
    }

    // --- TryCornerCut ---

    [Test]
    public void TryCornerCut_CanSeeNextCorner_WithinRange_AdvancesCorner()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        // Position within 1m of corner 0, can see next corner
        var pos = new Vector3(0.5f, 0, 0);
        bool result = _follower.TryCornerCut(pos, canSeeNextCorner: true);

        Assert.IsTrue(result);
        Assert.AreEqual(1, _follower.CurrentCorner);
    }

    [Test]
    public void TryCornerCut_CannotSeeNextCorner_DoesNotAdvance()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        var pos = new Vector3(0.5f, 0, 0);
        bool result = _follower.TryCornerCut(pos, canSeeNextCorner: false);

        Assert.IsFalse(result);
        Assert.AreEqual(0, _follower.CurrentCorner);
    }

    [Test]
    public void TryCornerCut_TooFarFromCorner_ReturnsFalse()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        // Position > 1m from corner 0
        var pos = new Vector3(3, 0, 0);
        bool result = _follower.TryCornerCut(pos, canSeeNextCorner: true);

        Assert.IsFalse(result);
        Assert.AreEqual(0, _follower.CurrentCorner);
    }

    [Test]
    public void TryCornerCut_OnLastCorner_ReturnsFalse()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 0) };
        _follower.SetPath(corners, new Vector3(5, 0, 0));

        // Advance to last corner
        _follower.AdvanceCorner();
        Assert.IsTrue(_follower.IsOnLastCorner);

        var pos = new Vector3(4.5f, 0, 0);
        bool result = _follower.TryCornerCut(pos, canSeeNextCorner: true);

        Assert.IsFalse(result);
    }

    [Test]
    public void TryCornerCut_NoPath_ReturnsFalse()
    {
        bool result = _follower.TryCornerCut(Vector3.zero, canSeeNextCorner: true);
        Assert.IsFalse(result);
    }

    [Test]
    public void TryCornerCut_NotFollowing_ReturnsFalse()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));
        _follower.FailPath();

        var pos = new Vector3(0.5f, 0, 0);
        bool result = _follower.TryCornerCut(pos, canSeeNextCorner: true);

        Assert.IsFalse(result);
    }

    [Test]
    public void TryCornerCut_MultipleCorners_SkipsCorrectly()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(3, 0, 0), new Vector3(6, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        // Skip corner 0
        var pos = new Vector3(0.5f, 0, 0);
        bool result = _follower.TryCornerCut(pos, canSeeNextCorner: true);

        Assert.IsTrue(result);
        Assert.AreEqual(1, _follower.CurrentCorner);
    }

    [Test]
    public void TryCornerCut_AfterNormalEpsilonReach_CornerAlreadyAdvanced()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(3, 0, 0), new Vector3(6, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        // Tick at corner 0 (within walk epsilon 0.35m) — advances to corner 1
        _follower.Tick(new Vector3(0.1f, 0, 0), isSprinting: false);
        Assert.AreEqual(1, _follower.CurrentCorner);

        // Now TryCornerCut at corner 1 (within 1m), should advance to corner 2
        var pos = new Vector3(2.5f, 0, 0);
        bool result = _follower.TryCornerCut(pos, canSeeNextCorner: true);

        Assert.IsTrue(result);
        Assert.AreEqual(2, _follower.CurrentCorner);
    }

    // --- Y-axis handling ---

    [Test]
    public void HasReachedCorner_IgnoresYAxis()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        // Same XZ position, different Y → should count as reached
        var pos = new Vector3(0, 100, 0);
        Assert.IsTrue(_follower.HasReachedCorner(pos, isSprinting: false));
    }

    [Test]
    public void HasReachedDestination_IgnoresYAxis()
    {
        var target = new Vector3(10, 0, 0);
        _follower.SetPath(new[] { new Vector3(0, 0, 0), target }, target);

        var pos = new Vector3(10, 50, 0);
        Assert.IsTrue(_follower.HasReachedDestination(pos));
    }
}

using System;
using NUnit.Framework;
using SPTQuestingBots.Models.Pathing;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.Models.Pathing;

[TestFixture]
public class InertiaTests
{
    private CustomPathFollower _follower;

    [SetUp]
    public void SetUp()
    {
        _follower = new CustomPathFollower(CustomMoverConfig.CreateDefault());
    }

    // --- ResetInertia ---

    [Test]
    public void ResetInertia_ClearsAllState()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10) };
        _follower.SetPath(corners, new Vector3(10, 0, 10));

        // Run several frames with direction changes to accumulate inertia
        var pos = new Vector3(-1, 0, 0);
        _follower.ComputeMoveDirection(pos, 0.016f);
        _follower.ComputeMoveDirection(new Vector3(-1, 0, 1), 0.016f);

        _follower.ResetInertia();

        Assert.AreEqual(0f, _follower.AccumulatedAngle, 0.001f);
        Assert.AreEqual(0f, _follower.DriftOffset.x, 0.001f);
        Assert.AreEqual(0f, _follower.DriftOffset.z, 0.001f);
    }

    [Test]
    public void SetPath_ResetsInertia()
    {
        var corners1 = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10) };
        _follower.SetPath(corners1, new Vector3(10, 0, 10));

        // Accumulate some angle
        _follower.ComputeMoveDirection(new Vector3(-1, 0, 0), 0.016f);
        _follower.ComputeMoveDirection(new Vector3(-1, 0, 1), 0.016f);
        _follower.ComputeMoveDirection(new Vector3(-1, 0, 2), 0.016f);

        // New path should reset inertia
        var corners2 = new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 10) };
        _follower.SetPath(corners2, new Vector3(0, 0, 10));

        Assert.AreEqual(0f, _follower.AccumulatedAngle, 0.001f);
        Assert.AreEqual(0f, _follower.DriftOffset.x, 0.001f);
    }

    [Test]
    public void ResetPath_ResetsInertia()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        _follower.ComputeMoveDirection(new Vector3(-5, 0, 0), 0.016f);
        _follower.ResetPath();

        Assert.AreEqual(0f, _follower.AccumulatedAngle, 0.001f);
    }

    // --- ApplyInertia basic behavior ---

    [Test]
    public void ApplyInertia_StraightPath_MinimalDrift()
    {
        // Use ApplyInertia directly to test with perfectly consistent directions
        var config = CustomMoverConfig.CreateDefault();
        var follower = new CustomPathFollower(config);
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(100, 0, 0) };
        follower.SetPath(corners, new Vector3(100, 0, 0));

        // Feed identical directions — no turns at all
        for (int i = 0; i < 60; i++)
        {
            follower.ApplyInertia(new Vector3(1, 0, 0), 0.016f);
        }

        // With perfectly constant direction, angle should be zero and drift minimal
        Assert.AreEqual(0f, follower.AccumulatedAngle, 0.1f);
        float driftMag = (float)
            Math.Sqrt(follower.DriftOffset.x * follower.DriftOffset.x + follower.DriftOffset.z * follower.DriftOffset.z);
        Assert.Less(driftMag, 0.01f, "No turns → no drift");
    }

    [Test]
    public void ApplyInertia_SharpTurn_AccumulatesAngle()
    {
        var config = CustomMoverConfig.CreateDefault();
        config.InertiaDecaySpeed = 0f; // disable decay so angle stays
        var follower = new CustomPathFollower(config);

        var corners = new[] { new Vector3(0, 0, 0), new Vector3(100, 0, 0) };
        follower.SetPath(corners, new Vector3(100, 0, 0));

        // Initialize with east direction
        follower.ApplyInertia(new Vector3(1, 0, 0), 0.016f);
        Assert.AreEqual(0f, follower.AccumulatedAngle, 0.1f, "First frame should initialize without angle");

        // 90 degree turn to north
        follower.ApplyInertia(new Vector3(0, 0, 1), 0.016f);
        float angle = Math.Abs(follower.AccumulatedAngle);
        Assert.Greater(angle, 0f, "Turn should accumulate angle");
        Assert.LessOrEqual(angle, 90f, "Single 90° turn should not exceed 90°");
    }

    [Test]
    public void ApplyInertia_DriftCappedAtMaxInertia()
    {
        var config = CustomMoverConfig.CreateDefault();
        config.MaxInertia = 0.3f;
        var follower = new CustomPathFollower(config);

        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10) };
        follower.SetPath(corners, new Vector3(10, 0, 10));

        // Do many frames with aggressive turns to try to exceed drift cap
        for (int i = 0; i < 100; i++)
        {
            float angle = i * 5f * (float)(Math.PI / 180.0);
            var pos = new Vector3((float)Math.Cos(angle), 0, (float)Math.Sin(angle));
            follower.ComputeMoveDirection(pos, 0.016f);
        }

        float driftMag = (float)
            Math.Sqrt(follower.DriftOffset.x * follower.DriftOffset.x + follower.DriftOffset.z * follower.DriftOffset.z);
        Assert.LessOrEqual(driftMag, config.MaxInertia + 0.001f, "Drift should be capped at MaxInertia");
    }

    [Test]
    public void ApplyInertia_AngleClamped()
    {
        var config = CustomMoverConfig.CreateDefault();
        config.InertiaClampAngle = 85f;
        config.InertiaDecaySpeed = 0f; // disable decay so angle accumulates freely
        var follower = new CustomPathFollower(config);

        var corners = new[] { new Vector3(0, 0, 0), new Vector3(100, 0, 0) };
        follower.SetPath(corners, new Vector3(100, 0, 0));

        // Initialize with forward direction
        follower.ApplyInertia(new Vector3(1, 0, 0), 0.016f);

        // Apply a massive 180-degree turn in one frame
        follower.ApplyInertia(new Vector3(-1, 0, 0), 0.016f);

        float absAngle = Math.Abs(follower.AccumulatedAngle);
        Assert.LessOrEqual(absAngle, 85f + 0.01f, "Accumulated angle should be clamped to InertiaClampAngle");
    }

    [Test]
    public void ApplyInertia_ZeroDeltaTime_NoInertia()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        // With deltaTime=0, inertia should not be applied
        var dir1 = _follower.ComputeMoveDirection(new Vector3(-5, 0, 0), 0f);
        Assert.Greater(dir1.x, 0f);
        Assert.AreEqual(0f, _follower.DriftOffset.x, 0.001f);
    }

    [Test]
    public void ApplyInertia_DisabledWhenMaxInertiaZero()
    {
        var config = CustomMoverConfig.CreateDefault();
        config.MaxInertia = 0f;
        var follower = new CustomPathFollower(config);

        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        follower.SetPath(corners, new Vector3(10, 0, 0));

        // Even with deltaTime > 0, should not apply inertia
        follower.ComputeMoveDirection(new Vector3(-5, 0, 0), 0.016f);
        Assert.AreEqual(0f, follower.DriftOffset.x, 0.001f);
    }

    [Test]
    public void ComputeMoveDirection_WithoutDeltaTime_BackwardsCompatible()
    {
        var corners = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        _follower.SetPath(corners, new Vector3(10, 0, 0));

        // Calling without deltaTime (defaults to 0) should still work
        var dir = _follower.ComputeMoveDirection(new Vector3(-5, 0, 0));
        Assert.Greater(dir.x, 0f, "Direction should point toward corner");
    }

    [Test]
    public void ApplyInertia_DecaysOverTime()
    {
        var config = CustomMoverConfig.CreateDefault();
        config.InertiaDecaySpeed = 50f; // very fast decay
        var follower = new CustomPathFollower(config);

        var corners = new[] { new Vector3(0, 0, 0), new Vector3(100, 0, 0) };
        follower.SetPath(corners, new Vector3(100, 0, 0));

        // Initialize
        follower.ApplyInertia(new Vector3(1, 0, 0), 0.016f);
        // Sharp turn to accumulate angle
        follower.ApplyInertia(new Vector3(0, 0, 1), 0.016f);

        float angleAfterTurn = Math.Abs(follower.AccumulatedAngle);
        Assert.Greater(angleAfterTurn, 0f, "Should have accumulated angle from turn");

        // Now go straight for a long time - angle should decay toward zero
        for (int i = 0; i < 100; i++)
        {
            follower.ApplyInertia(new Vector3(0, 0, 1), 0.05f);
        }

        float angleAfterDecay = Math.Abs(follower.AccumulatedAngle);
        Assert.Less(angleAfterDecay, angleAfterTurn, "Angle should decay over time");
    }
}

using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

[TestFixture]
public class TacticalPositionCalculatorTests
{
    private SquadStrategyConfig DefaultConfig()
    {
        return new SquadStrategyConfig
        {
            GuardDistance = 8f,
            FlankDistance = 15f,
            OverwatchDistance = 25f,
            EscortDistance = 5f,
        };
    }

    // ── AssignRoles ─────────────────────────────────

    [Test]
    public void AssignRoles_MoveToPosition_AllEscort()
    {
        var roles = new SquadRole[3];
        TacticalPositionCalculator.AssignRoles(QuestActionId.MoveToPosition, 3, roles);

        Assert.AreEqual(SquadRole.Escort, roles[0]);
        Assert.AreEqual(SquadRole.Escort, roles[1]);
        Assert.AreEqual(SquadRole.Escort, roles[2]);
    }

    [Test]
    public void AssignRoles_Ambush_FlankerOverwatchGuard()
    {
        var roles = new SquadRole[3];
        TacticalPositionCalculator.AssignRoles(QuestActionId.Ambush, 3, roles);

        Assert.AreEqual(SquadRole.Flanker, roles[0]);
        Assert.AreEqual(SquadRole.Overwatch, roles[1]);
        Assert.AreEqual(SquadRole.Guard, roles[2]);
    }

    [Test]
    public void AssignRoles_Snipe_OverwatchGuardGuard()
    {
        var roles = new SquadRole[3];
        TacticalPositionCalculator.AssignRoles(QuestActionId.Snipe, 3, roles);

        Assert.AreEqual(SquadRole.Overwatch, roles[0]);
        Assert.AreEqual(SquadRole.Guard, roles[1]);
        Assert.AreEqual(SquadRole.Guard, roles[2]);
    }

    [Test]
    public void AssignRoles_PlantItem_GuardEscortFlanker()
    {
        var roles = new SquadRole[3];
        TacticalPositionCalculator.AssignRoles(QuestActionId.PlantItem, 3, roles);

        Assert.AreEqual(SquadRole.Guard, roles[0]);
        Assert.AreEqual(SquadRole.Escort, roles[1]);
        Assert.AreEqual(SquadRole.Flanker, roles[2]);
    }

    [Test]
    public void AssignRoles_HoldAtPosition_FlankerOverwatchGuard()
    {
        var roles = new SquadRole[3];
        TacticalPositionCalculator.AssignRoles(QuestActionId.HoldAtPosition, 3, roles);

        Assert.AreEqual(SquadRole.Flanker, roles[0]);
        Assert.AreEqual(SquadRole.Overwatch, roles[1]);
        Assert.AreEqual(SquadRole.Guard, roles[2]);
    }

    [Test]
    public void AssignRoles_ToggleSwitch_AllGuard()
    {
        var roles = new SquadRole[3];
        TacticalPositionCalculator.AssignRoles(QuestActionId.ToggleSwitch, 3, roles);

        Assert.AreEqual(SquadRole.Guard, roles[0]);
        Assert.AreEqual(SquadRole.Guard, roles[1]);
        Assert.AreEqual(SquadRole.Guard, roles[2]);
    }

    [Test]
    public void AssignRoles_CloseNearbyDoors_AllGuard()
    {
        var roles = new SquadRole[2];
        TacticalPositionCalculator.AssignRoles(QuestActionId.CloseNearbyDoors, 2, roles);

        Assert.AreEqual(SquadRole.Guard, roles[0]);
        Assert.AreEqual(SquadRole.Guard, roles[1]);
    }

    [Test]
    public void AssignRoles_Default_GuardFlankerOverwatch()
    {
        var roles = new SquadRole[3];
        TacticalPositionCalculator.AssignRoles(QuestActionId.Undefined, 3, roles);

        Assert.AreEqual(SquadRole.Guard, roles[0]);
        Assert.AreEqual(SquadRole.Flanker, roles[1]);
        Assert.AreEqual(SquadRole.Overwatch, roles[2]);
    }

    [Test]
    public void AssignRoles_SingleFollower_GetsFirstRole()
    {
        var roles = new SquadRole[1];
        TacticalPositionCalculator.AssignRoles(QuestActionId.Ambush, 1, roles);

        Assert.AreEqual(SquadRole.Flanker, roles[0]);
    }

    [Test]
    public void AssignRoles_ZeroFollowers_NoOp()
    {
        var roles = new SquadRole[3];
        // Should not throw with zero count
        Assert.DoesNotThrow(() => TacticalPositionCalculator.AssignRoles(QuestActionId.Ambush, 0, roles));
    }

    // ── ComputeGuardPosition ────────────────────────

    [Test]
    public void ComputeGuardPosition_AtZeroDegrees_PositiveXOffset()
    {
        TacticalPositionCalculator.ComputeGuardPosition(0f, 0f, 0f, 0f, 8f, out float x, out float y, out float z);

        Assert.AreEqual(8f, x, 0.01f);
        Assert.AreEqual(0f, y, 0.01f);
        Assert.AreEqual(0f, z, 0.01f);
    }

    [Test]
    public void ComputeGuardPosition_At90Degrees_PositiveZOffset()
    {
        TacticalPositionCalculator.ComputeGuardPosition(0f, 0f, 0f, 90f, 8f, out float x, out float y, out float z);

        Assert.AreEqual(0f, x, 0.01f);
        Assert.AreEqual(0f, y, 0.01f);
        Assert.AreEqual(8f, z, 0.01f);
    }

    [Test]
    public void ComputeGuardPosition_PreservesY()
    {
        TacticalPositionCalculator.ComputeGuardPosition(10f, 5f, 20f, 0f, 8f, out float x, out float y, out float z);

        Assert.AreEqual(5f, y, 0.01f);
    }

    [Test]
    public void ComputeGuardPosition_At180Degrees_NegativeXOffset()
    {
        TacticalPositionCalculator.ComputeGuardPosition(0f, 0f, 0f, 180f, 10f, out float x, out float y, out float z);

        Assert.AreEqual(-10f, x, 0.01f);
        Assert.AreEqual(0f, z, 0.01f);
    }

    // ── ComputeFlankPosition ────────────────────────

    [Test]
    public void ComputeFlankPosition_PositiveSide_PerpendicularOffset()
    {
        // Approach from (0,0,0) to objective at (10,0,0) — direction is +X
        // Perpendicular (rotate 90) is (0, 0, 1) for positive side
        TacticalPositionCalculator.ComputeFlankPosition(10f, 0f, 0f, 0f, 0f, 1f, 15f, out float x, out float y, out float z);

        Assert.AreEqual(10f, x, 0.01f); // Same X as objective
        Assert.AreEqual(0f, y, 0.01f);
        // Perp of (1,0) is (0,1), so z = 0 + 1*15*1 = 15
        Assert.AreEqual(15f, z, 0.01f);
    }

    [Test]
    public void ComputeFlankPosition_NegativeSide_OtherDirection()
    {
        TacticalPositionCalculator.ComputeFlankPosition(10f, 0f, 0f, 0f, 0f, -1f, 15f, out float x, out float y, out float z);

        Assert.AreEqual(10f, x, 0.01f);
        Assert.AreEqual(0f, y, 0.01f);
        Assert.AreEqual(-15f, z, 0.01f);
    }

    [Test]
    public void ComputeFlankPosition_ZeroLengthApproach_FallsBack()
    {
        TacticalPositionCalculator.ComputeFlankPosition(5f, 2f, 5f, 5f, 5f, 1f, 10f, out float x, out float y, out float z);

        // Degenerate: x = objX + distance * side
        Assert.AreEqual(15f, x, 0.01f);
        Assert.AreEqual(2f, y, 0.01f);
        Assert.AreEqual(5f, z, 0.01f);
    }

    // ── ComputeOverwatchPosition ────────────────────

    [Test]
    public void ComputeOverwatchPosition_BehindApproach()
    {
        // Approach from (0,0,0), objective at (10,0,0)
        // Overwatch goes back toward approach: direction (0,0,0)-(10,0,0) = (-1,0,0) normalized
        // Position = obj + direction * distance = (10,0,0) + (-1,0,0)*25 = (-15,0,0)
        TacticalPositionCalculator.ComputeOverwatchPosition(10f, 0f, 0f, 0f, 0f, 25f, out float x, out float y, out float z);

        Assert.AreEqual(-15f, x, 0.01f);
        Assert.AreEqual(0f, y, 0.01f);
        Assert.AreEqual(0f, z, 0.01f);
    }

    [Test]
    public void ComputeOverwatchPosition_ZeroLengthApproach_FallsBack()
    {
        TacticalPositionCalculator.ComputeOverwatchPosition(5f, 2f, 5f, 5f, 5f, 10f, out float x, out float y, out float z);

        Assert.AreEqual(5f, x, 0.01f);
        Assert.AreEqual(2f, y, 0.01f);
        Assert.AreEqual(-5f, z, 0.01f); // Falls back to obj.z - distance
    }

    // ── ComputeEscortPosition ───────────────────────

    [Test]
    public void ComputeEscortPosition_TrailsBehindBoss()
    {
        // Boss at (0,0,0), objective at (10,0,0)
        // Direction to obj = (1,0,0), trail dist = 5
        // Position = boss + (1,0,0)*5 + perp*lateral
        TacticalPositionCalculator.ComputeEscortPosition(0f, 0f, 0f, 10f, 0f, 5f, 0f, out float x, out float y, out float z);

        Assert.AreEqual(5f, x, 0.01f);
        Assert.AreEqual(0f, y, 0.01f);
        Assert.AreEqual(0f, z, 0.01f);
    }

    [Test]
    public void ComputeEscortPosition_WithLateralOffset()
    {
        // Boss at (0,0,0), objective at (10,0,0)
        // Direction (1,0,0), perp (-0, 1) → lateral offset of 2
        TacticalPositionCalculator.ComputeEscortPosition(0f, 0f, 0f, 10f, 0f, 5f, 2f, out float x, out float y, out float z);

        Assert.AreEqual(5f, x, 0.01f);
        Assert.AreEqual(0f, y, 0.01f);
        // (-nz) * lateral = -(0) * 2 = 0 for z, plus nz * lateral = 0
        // Actually: z = bossZ + nz * trail + nx * lateral = 0 + 0*5 + 1*2 = 2
        Assert.AreEqual(2f, z, 0.01f);
    }

    [Test]
    public void ComputeEscortPosition_ZeroLengthApproach_FallsBack()
    {
        TacticalPositionCalculator.ComputeEscortPosition(5f, 2f, 5f, 5f, 5f, 5f, 3f, out float x, out float y, out float z);

        Assert.AreEqual(8f, x, 0.01f); // bossX + lateralOffset
        Assert.AreEqual(2f, y, 0.01f);
        Assert.AreEqual(5f, z, 0.01f);
    }

    // ── ComputePositions Integration ────────────────

    [Test]
    public void ComputePositions_AssignsCorrectPositionsPerRole()
    {
        var config = DefaultConfig();
        var roles = new SquadRole[] { SquadRole.Guard, SquadRole.Flanker };
        var positions = new float[6]; // 2 * 3

        TacticalPositionCalculator.ComputePositions(10f, 0f, 10f, 0f, 0f, roles, 2, positions, config);

        // Guard at index 0: circle around objective
        // angle = 0 * (360/2) = 0 degrees, radius 8
        Assert.AreEqual(10f + 8f, positions[0], 0.01f); // x
        Assert.AreEqual(0f, positions[1], 0.01f); // y

        // Flanker at index 1: perpendicular, side = -1 (i%2 == 1)
        Assert.AreEqual(0f, positions[4], 0.01f); // y preserved
    }

    [Test]
    public void ComputePositions_EscortRole_UsesEscortDistance()
    {
        var config = DefaultConfig();
        var roles = new SquadRole[] { SquadRole.Escort };
        var positions = new float[3];

        // Boss at (0,0,0), objective at (10,0,0)
        TacticalPositionCalculator.ComputePositions(10f, 5f, 0f, 0f, 0f, roles, 1, positions, config);

        Assert.AreEqual(5f, positions[1], 0.01f); // Y preserved
    }

    [Test]
    public void ComputePositions_OverwatchRole_UsesOverwatchDistance()
    {
        var config = DefaultConfig();
        var roles = new SquadRole[] { SquadRole.Overwatch };
        var positions = new float[3];

        // Objective at (10,0,0), approach from (0,0,0)
        TacticalPositionCalculator.ComputePositions(10f, 0f, 0f, 0f, 0f, roles, 1, positions, config);

        // Overwatch: behind approach, 25m back
        // Direction from obj back to approach = (-1,0,0) * 25
        Assert.AreEqual(-15f, positions[0], 0.01f);
    }

    [Test]
    public void ComputePositions_DefaultRole_ObjectivePosition()
    {
        var config = DefaultConfig();
        // SquadRole.None or SquadRole.Leader fall through to default
        var roles = new SquadRole[] { SquadRole.None };
        var positions = new float[3];

        TacticalPositionCalculator.ComputePositions(10f, 5f, 20f, 0f, 0f, roles, 1, positions, config);

        Assert.AreEqual(10f, positions[0], 0.01f);
        Assert.AreEqual(5f, positions[1], 0.01f);
        Assert.AreEqual(20f, positions[2], 0.01f);
    }
}

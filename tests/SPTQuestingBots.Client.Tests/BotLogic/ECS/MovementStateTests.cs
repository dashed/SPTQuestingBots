using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS;

[TestFixture]
public class MovementStateTests
{
    // --- MovementState struct ---

    [Test]
    public void MovementState_DefaultValues_AllZeroOrNone()
    {
        var state = new MovementState();
        Assert.AreEqual(PathFollowStatus.None, state.Status);
        Assert.IsFalse(state.IsSprinting);
        Assert.AreEqual(0f, state.CurrentPose);
        Assert.AreEqual(StuckPhase.None, state.StuckStatus);
        Assert.AreEqual(0f, state.SprintAngleJitter);
        Assert.AreEqual(0f, state.LastPathUpdateTime);
        Assert.AreEqual(0, state.CurrentCornerIndex);
        Assert.AreEqual(0, state.TotalCorners);
        Assert.AreEqual(0, state.RetryCount);
        Assert.IsFalse(state.IsCustomMoverActive);
    }

    [Test]
    public void MovementState_Reset_ClearsAllFields()
    {
        var state = new MovementState
        {
            Status = PathFollowStatus.Following,
            IsSprinting = true,
            CurrentPose = 0.5f,
            StuckStatus = StuckPhase.HardStuck,
            SprintAngleJitter = 35f,
            LastPathUpdateTime = 100f,
            CurrentCornerIndex = 5,
            TotalCorners = 10,
            RetryCount = 3,
            IsCustomMoverActive = true,
        };

        state.Reset();

        Assert.AreEqual(PathFollowStatus.None, state.Status);
        Assert.IsFalse(state.IsSprinting);
        Assert.AreEqual(1f, state.CurrentPose); // Reset sets standing pose
        Assert.AreEqual(StuckPhase.None, state.StuckStatus);
        Assert.AreEqual(0f, state.SprintAngleJitter);
        Assert.AreEqual(0f, state.LastPathUpdateTime);
        Assert.AreEqual(0, state.CurrentCornerIndex);
        Assert.AreEqual(0, state.TotalCorners);
        Assert.AreEqual(0, state.RetryCount);
        Assert.IsFalse(state.IsCustomMoverActive);
    }

    [Test]
    public void MovementState_SetAndRead_RoundTrip()
    {
        var state = new MovementState
        {
            Status = PathFollowStatus.Reached,
            IsSprinting = true,
            CurrentPose = 0.75f,
            StuckStatus = StuckPhase.SoftStuck,
            SprintAngleJitter = 22.5f,
            LastPathUpdateTime = 42f,
            CurrentCornerIndex = 3,
            TotalCorners = 8,
            RetryCount = 2,
            IsCustomMoverActive = true,
        };

        Assert.AreEqual(PathFollowStatus.Reached, state.Status);
        Assert.IsTrue(state.IsSprinting);
        Assert.AreEqual(0.75f, state.CurrentPose, 0.001f);
        Assert.AreEqual(StuckPhase.SoftStuck, state.StuckStatus);
        Assert.AreEqual(22.5f, state.SprintAngleJitter, 0.001f);
        Assert.AreEqual(42f, state.LastPathUpdateTime, 0.001f);
        Assert.AreEqual(3, state.CurrentCornerIndex);
        Assert.AreEqual(8, state.TotalCorners);
        Assert.AreEqual(2, state.RetryCount);
        Assert.IsTrue(state.IsCustomMoverActive);
    }

    // --- BotEntity.Movement integration ---

    [Test]
    public void BotEntity_Movement_DefaultIsNone()
    {
        var entity = new BotEntity(1);
        Assert.AreEqual(PathFollowStatus.None, entity.Movement.Status);
        Assert.IsFalse(entity.Movement.IsSprinting);
    }

    [Test]
    public void BotEntity_Movement_SetAndRead()
    {
        var entity = new BotEntity(1);
        entity.Movement.Status = PathFollowStatus.Following;
        entity.Movement.IsSprinting = true;
        entity.Movement.CurrentCornerIndex = 5;
        entity.Movement.TotalCorners = 12;

        Assert.AreEqual(PathFollowStatus.Following, entity.Movement.Status);
        Assert.IsTrue(entity.Movement.IsSprinting);
        Assert.AreEqual(5, entity.Movement.CurrentCornerIndex);
        Assert.AreEqual(12, entity.Movement.TotalCorners);
    }

    [Test]
    public void BotEntity_Movement_ResetClearsState()
    {
        var entity = new BotEntity(1);
        entity.Movement.Status = PathFollowStatus.Failed;
        entity.Movement.StuckStatus = StuckPhase.Failed;
        entity.Movement.RetryCount = 10;

        entity.Movement.Reset();

        Assert.AreEqual(PathFollowStatus.None, entity.Movement.Status);
        Assert.AreEqual(StuckPhase.None, entity.Movement.StuckStatus);
        Assert.AreEqual(0, entity.Movement.RetryCount);
    }

    // --- HiveMindSystem movement queries ---

    [Test]
    public void ResetMovementForInactiveEntities_InactiveReset()
    {
        var entities = new List<BotEntity>
        {
            new BotEntity(0) { IsActive = true },
            new BotEntity(1) { IsActive = false },
        };
        entities[0].Movement.Status = PathFollowStatus.Following;
        entities[0].Movement.IsSprinting = true;
        entities[1].Movement.Status = PathFollowStatus.Following;
        entities[1].Movement.IsSprinting = true;

        HiveMindSystem.ResetMovementForInactiveEntities(entities);

        // Active entity unchanged
        Assert.AreEqual(PathFollowStatus.Following, entities[0].Movement.Status);
        Assert.IsTrue(entities[0].Movement.IsSprinting);

        // Inactive entity reset
        Assert.AreEqual(PathFollowStatus.None, entities[1].Movement.Status);
        Assert.IsFalse(entities[1].Movement.IsSprinting);
    }

    [Test]
    public void CountByMovementStatus_CountsCorrectly()
    {
        var entities = new List<BotEntity>
        {
            new BotEntity(0) { IsActive = true },
            new BotEntity(1) { IsActive = true },
            new BotEntity(2) { IsActive = true },
            new BotEntity(3) { IsActive = false },
        };
        entities[0].Movement.Status = PathFollowStatus.Following;
        entities[1].Movement.Status = PathFollowStatus.Following;
        entities[2].Movement.Status = PathFollowStatus.Reached;
        entities[3].Movement.Status = PathFollowStatus.Following; // inactive, not counted

        Assert.AreEqual(2, HiveMindSystem.CountByMovementStatus(entities, PathFollowStatus.Following));
        Assert.AreEqual(1, HiveMindSystem.CountByMovementStatus(entities, PathFollowStatus.Reached));
        Assert.AreEqual(0, HiveMindSystem.CountByMovementStatus(entities, PathFollowStatus.Failed));
    }

    [Test]
    public void CountStuckBots_CountsAnyStuckPhase()
    {
        var entities = new List<BotEntity>
        {
            new BotEntity(0) { IsActive = true },
            new BotEntity(1) { IsActive = true },
            new BotEntity(2) { IsActive = true },
        };
        entities[0].Movement.StuckStatus = StuckPhase.None;
        entities[1].Movement.StuckStatus = StuckPhase.SoftStuck;
        entities[2].Movement.StuckStatus = StuckPhase.HardStuck;

        Assert.AreEqual(2, HiveMindSystem.CountStuckBots(entities));
    }

    [Test]
    public void CountSprintingBots_CountsOnlyActive()
    {
        var entities = new List<BotEntity>
        {
            new BotEntity(0) { IsActive = true },
            new BotEntity(1) { IsActive = true },
            new BotEntity(2) { IsActive = false },
        };
        entities[0].Movement.IsSprinting = true;
        entities[1].Movement.IsSprinting = false;
        entities[2].Movement.IsSprinting = true; // inactive, not counted

        Assert.AreEqual(1, HiveMindSystem.CountSprintingBots(entities));
    }

    [Test]
    public void CountStuckBots_EmptyList_ReturnsZero()
    {
        Assert.AreEqual(0, HiveMindSystem.CountStuckBots(new List<BotEntity>()));
    }

    [Test]
    public void CountSprintingBots_EmptyList_ReturnsZero()
    {
        Assert.AreEqual(0, HiveMindSystem.CountSprintingBots(new List<BotEntity>()));
    }
}

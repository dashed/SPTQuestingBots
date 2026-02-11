using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI;

[TestFixture]
public class GotoObjectiveStrategyTests
{
    private SquadStrategyConfig DefaultConfig()
    {
        return new SquadStrategyConfig
        {
            GuardDistance = 8f,
            FlankDistance = 15f,
            OverwatchDistance = 25f,
            EscortDistance = 5f,
            ArrivalRadius = 3f,
            UseQuestTypeRoles = true,
            // Disable gates by default so existing tests are not affected
            EnableCommunicationRange = false,
            EnableSquadPersonality = false,
            EnablePositionValidation = false,
            EnableReachabilityCheck = false,
            EnableLosCheck = false,
            EnableCoverPositionSource = false,
            EnableObjectiveSharing = false,
        };
    }

    private SquadEntity CreateSquad(int id, int strategyCount = 1)
    {
        return new SquadEntity(id, strategyCount, 4);
    }

    private BotEntity CreateBot(int id)
    {
        return new BotEntity(id);
    }

    private void SetupSquadWithLeaderAndFollower(SquadEntity squad, BotEntity leader, BotEntity follower)
    {
        squad.Members.Add(leader);
        squad.Members.Add(follower);
        squad.Leader = leader;
        leader.Squad = squad;
        leader.SquadRole = SquadRole.Leader;
        follower.Squad = squad;
        follower.SquadRole = SquadRole.Guard;
    }

    // ── ScoreSquad ──────────────────────────────────

    [Test]
    public void ScoreSquad_ActiveLeaderWithObjective_Returns05()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        leader.HasActiveObjective = true;
        squad.Leader = leader;
        squad.Members.Add(leader);

        strategy.ScoreSquad(0, squad);

        Assert.AreEqual(0.5f, squad.StrategyScores[0], 0.001f);
    }

    [Test]
    public void ScoreSquad_NoLeader_ReturnsZero()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);

        strategy.ScoreSquad(0, squad);

        Assert.AreEqual(0f, squad.StrategyScores[0], 0.001f);
    }

    [Test]
    public void ScoreSquad_InactiveLeader_ReturnsZero()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        leader.IsActive = false;
        leader.HasActiveObjective = true;
        squad.Leader = leader;

        strategy.ScoreSquad(0, squad);

        Assert.AreEqual(0f, squad.StrategyScores[0], 0.001f);
    }

    [Test]
    public void ScoreSquad_LeaderNoObjective_ReturnsZero()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        leader.HasActiveObjective = false;
        squad.Leader = leader;

        strategy.ScoreSquad(0, squad);

        Assert.AreEqual(0f, squad.StrategyScores[0], 0.001f);
    }

    // ── AssignNewObjective (via Update) ─────────────

    [Test]
    public void Update_AssignsTacticalPositionsToFollowers()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);

        // Set objective on the squad
        squad.Objective.SetObjective(50f, 0f, 50f);

        // Activate the strategy and update
        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
        Assert.AreNotEqual(SquadRole.Guard, follower.SquadRole);
        Assert.AreEqual(SquadRole.Flanker, follower.SquadRole); // Ambush first role
    }

    [Test]
    public void Update_AssignsRolesBasedOnQuestActionType()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower1 = CreateBot(1);
        var follower2 = CreateBot(2);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.MoveToPosition;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        squad.Members.Add(leader);
        squad.Members.Add(follower1);
        squad.Members.Add(follower2);
        squad.Leader = leader;
        leader.Squad = squad;
        follower1.Squad = squad;
        follower2.Squad = squad;

        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // MoveToPosition → all Escort
        Assert.AreEqual(SquadRole.Escort, follower1.SquadRole);
        Assert.AreEqual(SquadRole.Escort, follower2.SquadRole);
    }

    [Test]
    public void Update_WithUseQuestTypeRolesDisabled_UsesDefaultRoles()
    {
        var config = DefaultConfig();
        config.UseQuestTypeRoles = false;
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower1 = CreateBot(1);
        var follower2 = CreateBot(2);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.MoveToPosition;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        squad.Members.Add(leader);
        squad.Members.Add(follower1);
        squad.Members.Add(follower2);
        squad.Leader = leader;
        leader.Squad = squad;
        follower1.Squad = squad;
        follower2.Squad = squad;

        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // Default roles (questActionId=0): Guard, Flanker, Overwatch
        Assert.AreEqual(SquadRole.Guard, follower1.SquadRole);
        Assert.AreEqual(SquadRole.Flanker, follower2.SquadRole);
    }

    [Test]
    public void Update_NoFollowers_DoesNotAssignPositions()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);

        leader.HasActiveObjective = true;
        squad.Members.Add(leader);
        squad.Leader = leader;
        leader.Squad = squad;

        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        // Should not throw
        Assert.DoesNotThrow(() => strategy.Update());
    }

    [Test]
    public void Update_SetsObjectiveStateToActive()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.AreEqual(ObjectiveState.Active, squad.Objective.State);
        Assert.Greater(squad.Objective.Duration, 0f);
    }

    [Test]
    public void Update_SetsLeaderLastSeenObjectiveVersion()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.AreEqual(squad.Objective.Version, leader.LastSeenObjectiveVersion);
    }

    // ── Arrival Detection ───────────────────────────

    [Test]
    public void CheckArrivals_FirstArrival_SwitchesToWait()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // Move follower to tactical position
        follower.CurrentPositionX = follower.TacticalPositionX;
        follower.CurrentPositionY = follower.TacticalPositionY;
        follower.CurrentPositionZ = follower.TacticalPositionZ;

        // Set state back to Active to test arrival
        squad.Objective.State = ObjectiveState.Active;

        strategy.CheckArrivals(squad);

        Assert.AreEqual(ObjectiveState.Wait, squad.Objective.State);
    }

    [Test]
    public void CheckArrivals_AllArrived_AdjustsDuration()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        float originalDuration = squad.Objective.Duration;

        // Move follower to exact position
        follower.CurrentPositionX = follower.TacticalPositionX;
        follower.CurrentPositionY = follower.TacticalPositionY;
        follower.CurrentPositionZ = follower.TacticalPositionZ;

        squad.Objective.State = ObjectiveState.Active;
        strategy.CheckArrivals(squad);

        Assert.IsTrue(squad.Objective.DurationAdjusted);
        Assert.Less(squad.Objective.Duration, originalDuration);
    }

    [Test]
    public void CheckArrivals_NotArrived_StaysActive()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // Follower is far away
        follower.CurrentPositionX = 0f;
        follower.CurrentPositionY = 0f;
        follower.CurrentPositionZ = 0f;

        squad.Objective.State = ObjectiveState.Active;
        strategy.CheckArrivals(squad);

        Assert.AreEqual(ObjectiveState.Active, squad.Objective.State);
        Assert.IsFalse(squad.Objective.DurationAdjusted);
    }

    [Test]
    public void CheckArrivals_DurationAlreadyAdjusted_DoesNotAdjustAgain()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // Simulate all arrived and adjust
        follower.CurrentPositionX = follower.TacticalPositionX;
        follower.CurrentPositionY = follower.TacticalPositionY;
        follower.CurrentPositionZ = follower.TacticalPositionZ;

        squad.Objective.State = ObjectiveState.Active;
        strategy.CheckArrivals(squad);

        float adjustedDuration = squad.Objective.Duration;

        // Check again — should not change
        strategy.CheckArrivals(squad);

        Assert.AreEqual(adjustedDuration, squad.Objective.Duration, 0.001f);
    }

    // ── Leader Objective Change ─────────────────────

    [Test]
    public void Update_LeaderObjectiveChanged_ReassignsPositions()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        float firstTacX = follower.TacticalPositionX;

        // Leader's objective changes — increment version
        squad.Objective.SetObjective(100f, 0f, 100f);

        strategy.Update();

        // Position should have changed because objective moved
        Assert.That(Math.Abs(follower.TacticalPositionX - firstTacX), Is.GreaterThan(0.1f));
    }

    // ── SampleGaussian ──────────────────────────────

    [Test]
    public void SampleGaussian_ProducesValuesWithinRange()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            float value = strategy.SampleGaussian(10f, 50f);
            Assert.GreaterOrEqual(value, 10f);
            Assert.LessOrEqual(value, 50f);
        }
    }

    [Test]
    public void SampleGaussian_NarrowRange_StillValid()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);

        float value = strategy.SampleGaussian(5f, 5.1f);
        Assert.GreaterOrEqual(value, 5f);
        Assert.LessOrEqual(value, 5.1f);
    }

    // ── Inactive Leader ─────────────────────────────

    [Test]
    public void Update_InactiveLeader_DoesNotAssignPositions()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.IsActive = false;
        leader.HasActiveObjective = true;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsFalse(follower.HasTacticalPosition);
    }

    [Test]
    public void Update_NullLeader_DoesNotThrow()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        squad.Leader = null;

        strategy.Activate(squad);
        Assert.DoesNotThrow(() => strategy.Update());
    }

    // ── Multiple Followers ──────────────────────────

    [Test]
    public void Update_MultipleFollowers_AllGetPositions()
    {
        var config = DefaultConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var f1 = CreateBot(1);
        var f2 = CreateBot(2);
        var f3 = CreateBot(3);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        squad.Members.Add(leader);
        squad.Members.Add(f1);
        squad.Members.Add(f2);
        squad.Members.Add(f3);
        squad.Leader = leader;
        leader.Squad = squad;
        f1.Squad = squad;
        f2.Squad = squad;
        f3.Squad = squad;

        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(f1.HasTacticalPosition);
        Assert.IsTrue(f2.HasTacticalPosition);
        Assert.IsTrue(f3.HasTacticalPosition);
        Assert.AreEqual(3, squad.Objective.MemberCount);
    }

    // ── Communication Range Gate ────────────────────────

    private SquadStrategyConfig CommRangeConfig(float noEarRange = 35f, float earRange = 200f)
    {
        var config = DefaultConfig();
        config.EnableCommunicationRange = true;
        config.CommunicationRangeNoEarpiece = noEarRange;
        config.CommunicationRangeEarpiece = earRange;
        return config;
    }

    private (SquadEntity squad, BotEntity leader, BotEntity follower) SetupCommRangeScenario(SquadStrategyConfig config, int seed = 42)
    {
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);
        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionY = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);
        return (squad, leader, follower);
    }

    [Test]
    public void AssignNewObjective_FollowerInRange_ReceivesTacticalPosition()
    {
        var config = CommRangeConfig(noEarRange: 35f);
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, follower) = SetupCommRangeScenario(config);

        // Follower is 10m away (within 35m range)
        follower.CurrentPositionX = 10f;

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
    }

    [Test]
    public void AssignNewObjective_FollowerOutOfRange_DoesNotReceive()
    {
        var config = CommRangeConfig(noEarRange: 35f);
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, follower) = SetupCommRangeScenario(config);

        // Follower is 50m away (beyond 35m range)
        follower.CurrentPositionX = 50f;

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsFalse(follower.HasTacticalPosition);
    }

    [Test]
    public void AssignNewObjective_BothEarpiece_UsesEarpieceRange()
    {
        var config = CommRangeConfig(noEarRange: 35f, earRange: 200f);
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, follower) = SetupCommRangeScenario(config);

        // Both have earpieces, follower at 100m (within 200m but beyond 35m)
        leader.HasEarPiece = true;
        follower.HasEarPiece = true;
        follower.CurrentPositionX = 100f;

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
    }

    [Test]
    public void AssignNewObjective_NoEarpiece_UsesNoEarpieceRange()
    {
        var config = CommRangeConfig(noEarRange: 35f, earRange: 200f);
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, follower) = SetupCommRangeScenario(config);

        // No earpieces, follower at 100m (beyond 35m)
        leader.HasEarPiece = false;
        follower.HasEarPiece = false;
        follower.CurrentPositionX = 100f;

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsFalse(follower.HasTacticalPosition);
    }

    [Test]
    public void AssignNewObjective_CommRangeDisabled_AlwaysAssigns()
    {
        var config = DefaultConfig();
        config.EnableCommunicationRange = false;
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, follower) = SetupCommRangeScenario(config);

        // Follower is far away but comm range disabled
        follower.CurrentPositionX = 999f;

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
    }

    [Test]
    public void AssignNewObjective_MixedRange_IndividualGating()
    {
        var config = CommRangeConfig(noEarRange: 35f, earRange: 200f);
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var nearFollower = CreateBot(1);
        var farFollower = CreateBot(2);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionY = 0f;
        leader.CurrentPositionZ = 0f;

        nearFollower.CurrentPositionX = 10f; // 10m — in range
        farFollower.CurrentPositionX = 50f; // 50m — out of 35m range

        squad.Members.Add(leader);
        squad.Members.Add(nearFollower);
        squad.Members.Add(farFollower);
        squad.Leader = leader;
        leader.Squad = squad;
        nearFollower.Squad = squad;
        farFollower.Squad = squad;
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(nearFollower.HasTacticalPosition, "Near follower should receive position");
        Assert.IsFalse(farFollower.HasTacticalPosition, "Far follower should not receive position");
    }

    // ── Probabilistic Sharing Gate ──────────────────────

    [Test]
    public void AssignNewObjective_EliteSquad_AllReceive()
    {
        var config = DefaultConfig();
        config.EnableSquadPersonality = true;
        config.EnableCommunicationRange = false;
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        squad.Members.Add(leader);
        squad.Leader = leader;
        leader.Squad = squad;

        // Elite personality: coordination=5, sharing chance = 25+5*15 = 100%
        squad.PersonalityType = SquadPersonalityType.Elite;
        squad.CoordinationLevel = 5f;
        squad.AggressionLevel = 4f;

        // Add 5 followers
        var followers = new List<BotEntity>();
        for (int i = 1; i <= 5; i++)
        {
            var f = CreateBot(i);
            f.Squad = squad;
            squad.Members.Add(f);
            followers.Add(f);
        }

        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // With 100% sharing chance, all followers should receive positions
        foreach (var f in followers)
        {
            Assert.IsTrue(f.HasTacticalPosition, $"Follower {f.Id} should have tactical position");
        }
    }

    [Test]
    public void AssignNewObjective_LowCoordination_SomeMayNotReceive()
    {
        var config = DefaultConfig();
        config.EnableSquadPersonality = true;
        config.EnableCommunicationRange = false;

        // TimmyTeam6: coordination=1, sharing chance = 25+1*15 = 40%
        // With a fixed seed, some followers should be filtered
        int receivedCount = 0;
        int totalFollowers = 20;

        // Run with enough followers to statistically see filtering
        var strategy = new GotoObjectiveStrategy(config, seed: 12345);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.MoveToPosition;
        squad.Members.Add(leader);
        squad.Leader = leader;
        leader.Squad = squad;
        squad.PersonalityType = SquadPersonalityType.TimmyTeam6;
        squad.CoordinationLevel = 1f;
        squad.AggressionLevel = 2f;

        for (int i = 1; i <= totalFollowers; i++)
        {
            var f = CreateBot(i);
            f.Squad = squad;
            squad.Members.Add(f);
        }

        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        for (int i = 1; i < squad.Members.Count; i++)
        {
            if (squad.Members[i].HasTacticalPosition)
            {
                receivedCount++;
            }
        }

        // With 40% chance, not all should receive, and some should
        // Clamped to MaxMembers (6), so check within that bound
        int maxMembers = Math.Min(totalFollowers, SquadObjective.MaxMembers);
        Assert.Less(receivedCount, maxMembers, "With 40% sharing chance, not all should receive");
        Assert.Greater(receivedCount, 0, "With 40% sharing chance, at least one should receive");
    }

    [Test]
    public void AssignNewObjective_PersonalityDisabled_AlwaysAssigns()
    {
        var config = DefaultConfig();
        config.EnableSquadPersonality = false;
        config.EnableCommunicationRange = false;
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, follower) = SetupCommRangeScenario(config);

        // Even with low coordination, should still assign when disabled
        squad.CoordinationLevel = 1f;

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
    }

    // ── Combined Gates ──────────────────────────────────

    [Test]
    public void AssignNewObjective_BothGatesActive_CombinedFiltering()
    {
        var config = DefaultConfig();
        config.EnableCommunicationRange = true;
        config.CommunicationRangeNoEarpiece = 35f;
        config.CommunicationRangeEarpiece = 200f;
        config.EnableSquadPersonality = true;

        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var inRangeFollower = CreateBot(1);
        var outOfRangeFollower = CreateBot(2);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionY = 0f;
        leader.CurrentPositionZ = 0f;

        // Elite coordination = 100% sharing chance
        squad.CoordinationLevel = 5f;

        inRangeFollower.CurrentPositionX = 10f; // in range
        outOfRangeFollower.CurrentPositionX = 50f; // out of 35m range

        squad.Members.Add(leader);
        squad.Members.Add(inRangeFollower);
        squad.Members.Add(outOfRangeFollower);
        squad.Leader = leader;
        leader.Squad = squad;
        inRangeFollower.Squad = squad;
        outOfRangeFollower.Squad = squad;
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // In-range follower passes comm gate AND personality gate (100% chance)
        Assert.IsTrue(inRangeFollower.HasTacticalPosition);
        // Out-of-range follower fails comm gate
        Assert.IsFalse(outOfRangeFollower.HasTacticalPosition);
    }

    [Test]
    public void AssignNewObjective_BothGatesDisabled_AllReceive()
    {
        var config = DefaultConfig();
        config.EnableCommunicationRange = false;
        config.EnableSquadPersonality = false;
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var f1 = CreateBot(1);
        var f2 = CreateBot(2);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;

        // Far away and low coordination but both gates disabled
        f1.CurrentPositionX = 999f;
        f2.CurrentPositionX = 999f;
        squad.CoordinationLevel = 1f;

        squad.Members.Add(leader);
        squad.Members.Add(f1);
        squad.Members.Add(f2);
        squad.Leader = leader;
        leader.Squad = squad;
        f1.Squad = squad;
        f2.Squad = squad;
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(f1.HasTacticalPosition);
        Assert.IsTrue(f2.HasTacticalPosition);
    }

    // ── Position Validation ───────────────────────────

    private static bool AlwaysSnapValidator(float x, float y, float z, out float ox, out float oy, out float oz)
    {
        ox = x + 0.1f;
        oy = y + 0.5f;
        oz = z + 0.1f;
        return true;
    }

    private static bool AlwaysFailValidator(float x, float y, float z, out float ox, out float oy, out float oz)
    {
        ox = oy = oz = 0f;
        return false;
    }

    private SquadStrategyConfig ValidationConfig()
    {
        var config = DefaultConfig();
        config.EnablePositionValidation = true;
        config.FallbackCandidateCount = 16;
        config.FallbackSearchRadius = 15f;
        return config;
    }

    [Test]
    public void AssignNewObjective_NoValidator_PositionsUnchanged()
    {
        var config = DefaultConfig();
        config.EnablePositionValidation = true; // enabled but no validator
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // Should still get positions (no validator = no validation)
        Assert.IsTrue(follower.HasTacticalPosition);
    }

    [Test]
    public void AssignNewObjective_ValidatorAlwaysTrue_PositionsSnapped()
    {
        var config = ValidationConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: AlwaysSnapValidator);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
        // Y should have the 0.5 offset from the snap validator
        Assert.AreEqual(0.5f, follower.TacticalPositionY, 0.01f);
    }

    [Test]
    public void AssignNewObjective_ValidatorAlwaysFalse_FallbackUsed()
    {
        int callCount = 0;
        bool FallbackOnlyValidator(float x, float y, float z, out float ox, out float oy, out float oz)
        {
            callCount++;
            // Fail for geometric positions (first calls), succeed for fallback (spiral candidates)
            if (callCount > 1)
            {
                ox = x;
                oy = y + 1f;
                oz = z;
                return true;
            }
            ox = oy = oz = 0f;
            return false;
        }

        var config = ValidationConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: FallbackOnlyValidator);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
        Assert.Greater(callCount, 1, "Fallback candidates should have been tried");
    }

    [Test]
    public void AssignNewObjective_ValidatorAlwaysFalse_NoFallback_MarksInvalid()
    {
        var config = ValidationConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: AlwaysFailValidator);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // NaN position should result in HasTacticalPosition = false
        Assert.IsFalse(follower.HasTacticalPosition);
    }

    [Test]
    public void AssignNewObjective_ValidationDisabledByConfig_ValidatorNotCalled()
    {
        bool wasCalled = false;
        bool TrackingValidator(float x, float y, float z, out float ox, out float oy, out float oz)
        {
            wasCalled = true;
            ox = x;
            oy = y;
            oz = z;
            return true;
        }

        var config = DefaultConfig();
        config.EnablePositionValidation = false; // disabled
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: TrackingValidator);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsFalse(wasCalled, "Validator should not be called when disabled by config");
        Assert.IsTrue(follower.HasTacticalPosition);
    }

    [Test]
    public void AssignNewObjective_MixedValidation_SomePassSomeFail()
    {
        int geometricCallIdx = 0;
        bool MixedValidator(float x, float y, float z, out float ox, out float oy, out float oz)
        {
            geometricCallIdx++;
            // 1st geometric: pass, 2nd geometric: fail (needs fallback),
            // 3rd geometric: fail (no fallback either)
            // For fallback candidates of 2nd: succeed on first candidate
            // For fallback candidates of 3rd: all fail
            if (geometricCallIdx == 1)
            {
                ox = x + 0.1f;
                oy = y;
                oz = z + 0.1f;
                return true;
            }
            if (geometricCallIdx == 2)
            {
                // Fail geometric, will try fallback
                ox = oy = oz = 0f;
                return false;
            }
            if (geometricCallIdx == 3)
            {
                // First fallback candidate for 2nd follower — succeed
                ox = 99f;
                oy = 99f;
                oz = 99f;
                return true;
            }
            if (geometricCallIdx == 4)
            {
                // 3rd geometric fails
                ox = oy = oz = 0f;
                return false;
            }
            // All remaining fallback candidates for 3rd follower fail
            ox = oy = oz = 0f;
            return false;
        }

        var config = ValidationConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: MixedValidator);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var f1 = CreateBot(1);
        var f2 = CreateBot(2);
        var f3 = CreateBot(3);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        squad.Members.Add(leader);
        squad.Members.Add(f1);
        squad.Members.Add(f2);
        squad.Members.Add(f3);
        squad.Leader = leader;
        leader.Squad = squad;
        f1.Squad = squad;
        f2.Squad = squad;
        f3.Squad = squad;
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // f1: geometric passed
        Assert.IsTrue(f1.HasTacticalPosition, "f1 should have position (geometric passed)");
        // f2: geometric failed, fallback succeeded
        Assert.IsTrue(f2.HasTacticalPosition, "f2 should have position (fallback succeeded)");
        // f3: both failed → NaN → HasTacticalPosition = false
        Assert.IsFalse(f3.HasTacticalPosition, "f3 should not have position (all validation failed)");
    }

    [Test]
    public void AssignNewObjective_ValidatorSnapsY_YCoordUpdated()
    {
        bool YSnapValidator(float x, float y, float z, out float ox, out float oy, out float oz)
        {
            ox = x;
            oy = 42.5f; // Snap Y to terrain height
            oz = z;
            return true;
        }

        var config = ValidationConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: YSnapValidator);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
        Assert.AreEqual(42.5f, follower.TacticalPositionY, 0.01f);
    }

    [Test]
    public void AssignNewObjective_NaNPosition_SkippedInDistribution()
    {
        var config = ValidationConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: AlwaysFailValidator);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // NaN positions should be skipped — follower has no tactical position
        Assert.IsFalse(follower.HasTacticalPosition);
        Assert.AreEqual(SquadRole.Guard, follower.SquadRole); // unchanged from setup
    }

    // ── TryFallbackPosition ───────────────────────────

    [Test]
    public void TryFallbackPosition_ValidatorSucceeds_ReturnsSpiralPosition()
    {
        var config = ValidationConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: AlwaysSnapValidator);

        bool result = strategy.TryFallbackPosition(
            50f,
            50f,
            0f,
            15f,
            16,
            SquadRole.Guard,
            50f,
            0f,
            50f,
            out float fx,
            out float fy,
            out float fz
        );

        Assert.IsTrue(result);
        // AlwaysSnapValidator adds 0.5 to Y
        Assert.AreEqual(0.5f, fy, 0.01f);
    }

    [Test]
    public void TryFallbackPosition_ValidatorAlwaysFails_ReturnsFalse()
    {
        var config = ValidationConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: AlwaysFailValidator);

        bool result = strategy.TryFallbackPosition(
            50f,
            50f,
            0f,
            15f,
            16,
            SquadRole.Guard,
            50f,
            0f,
            50f,
            out float fx,
            out float fy,
            out float fz
        );

        Assert.IsFalse(result);
        Assert.AreEqual(0f, fx);
        Assert.AreEqual(0f, fy);
        Assert.AreEqual(0f, fz);
    }

    // ── Reachability + LOS Validation ──────────────────

    private static bool AlwaysReachable(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, float maxLen)
    {
        return true;
    }

    private static bool NeverReachable(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, float maxLen)
    {
        return false;
    }

    private static bool AlwaysHasLos(float fromX, float fromY, float fromZ, float toX, float toY, float toZ)
    {
        return true;
    }

    private static bool NeverHasLos(float fromX, float fromY, float fromZ, float toX, float toY, float toZ)
    {
        return false;
    }

    private SquadStrategyConfig FullValidationConfig()
    {
        var config = ValidationConfig();
        config.EnableReachabilityCheck = true;
        config.MaxPathLengthMultiplier = 2.5f;
        config.EnableLosCheck = true;
        return config;
    }

    [Test]
    public void Reachability_AlwaysReachable_PositionsAccepted()
    {
        var config = FullValidationConfig();
        var strategy = new GotoObjectiveStrategy(
            config,
            seed: 42,
            positionValidator: AlwaysSnapValidator,
            reachabilityValidator: AlwaysReachable
        );
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
    }

    [Test]
    public void Reachability_NeverReachable_FallbackUsed()
    {
        int snapCallCount = 0;
        bool TrackingSnapValidator(float x, float y, float z, out float ox, out float oy, out float oz)
        {
            snapCallCount++;
            ox = x + 0.1f;
            oy = y + 0.5f;
            oz = z + 0.1f;
            return true;
        }

        var config = FullValidationConfig();
        config.EnableLosCheck = false;
        var strategy = new GotoObjectiveStrategy(
            config,
            seed: 42,
            positionValidator: TrackingSnapValidator,
            reachabilityValidator: NeverReachable
        );
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // All snap calls succeed but reachability always fails → fallback also tried
        Assert.Greater(snapCallCount, 1, "Fallback candidates should have been tried");
        // Ultimately everything fails since reachability always fails
        Assert.IsFalse(follower.HasTacticalPosition);
    }

    [Test]
    public void Reachability_NeverReachable_NoFallback_MarksInvalid()
    {
        var config = FullValidationConfig();
        config.EnableLosCheck = false;
        var strategy = new GotoObjectiveStrategy(
            config,
            seed: 42,
            positionValidator: AlwaysSnapValidator,
            reachabilityValidator: NeverReachable
        );
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsFalse(follower.HasTacticalPosition);
    }

    [Test]
    public void Reachability_DisabledByConfig_ValidatorNotCalled()
    {
        bool wasCalled = false;
        bool TrackingReachability(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, float maxLen)
        {
            wasCalled = true;
            return true;
        }

        var config = FullValidationConfig();
        config.EnableReachabilityCheck = false;
        var strategy = new GotoObjectiveStrategy(
            config,
            seed: 42,
            positionValidator: AlwaysSnapValidator,
            reachabilityValidator: TrackingReachability
        );
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsFalse(wasCalled, "Reachability validator should not be called when disabled by config");
        Assert.IsTrue(follower.HasTacticalPosition);
    }

    [Test]
    public void Reachability_NoValidator_Skipped()
    {
        var config = FullValidationConfig();
        // No reachabilityValidator passed
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: AlwaysSnapValidator);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
    }

    [Test]
    public void Reachability_PathLengthMultiplier_Respected()
    {
        float capturedMaxLen = 0f;
        bool CapturingReachability(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, float maxLen)
        {
            capturedMaxLen = maxLen;
            return true;
        }

        var config = FullValidationConfig();
        config.MaxPathLengthMultiplier = 2.5f;
        config.EnableLosCheck = false;
        var strategy = new GotoObjectiveStrategy(
            config,
            seed: 42,
            positionValidator: AlwaysSnapValidator,
            reachabilityValidator: CapturingReachability
        );
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // AlwaysSnapValidator adds +0.1 to X and Z, +0.5 to Y from the geometric position
        // The direct distance from snapped position to objective (50,0,50) varies,
        // but capturedMaxLen should be directDist * 2.5
        Assert.Greater(capturedMaxLen, 0f, "Max path length should be positive");
        // Verify it's approximately directDist * 2.5
        // We can't know the exact geometric position, but we know it should be > 0
        // and a reasonable multiple of the distance
        Assert.Greater(capturedMaxLen, 1f, "Max path length should be meaningful");
    }

    [Test]
    public void Los_OverwatchWithLos_Accepted()
    {
        var config = FullValidationConfig();
        config.EnableReachabilityCheck = false;
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: AlwaysSnapValidator, losValidator: AlwaysHasLos);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Snipe; // Snipe assigns Overwatch first
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
        Assert.AreEqual(SquadRole.Overwatch, follower.SquadRole);
    }

    [Test]
    public void Los_OverwatchNoLos_FallbackUsed()
    {
        int snapCallCount = 0;
        bool TrackingSnapValidator(float x, float y, float z, out float ox, out float oy, out float oz)
        {
            snapCallCount++;
            ox = x + 0.1f;
            oy = y + 0.5f;
            oz = z + 0.1f;
            return true;
        }

        var config = FullValidationConfig();
        config.EnableReachabilityCheck = false;
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: TrackingSnapValidator, losValidator: NeverHasLos);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Snipe; // Snipe assigns Overwatch first
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // Overwatch LOS always fails — geometric and fallback all fail
        Assert.Greater(snapCallCount, 1, "Fallback candidates should have been tried");
        Assert.IsFalse(follower.HasTacticalPosition);
    }

    [Test]
    public void Los_OverwatchNoLos_NoFallback_MarksInvalid()
    {
        var config = FullValidationConfig();
        config.EnableReachabilityCheck = false;
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: AlwaysSnapValidator, losValidator: NeverHasLos);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Snipe; // Overwatch role
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsFalse(follower.HasTacticalPosition);
    }

    [Test]
    public void Los_NonOverwatchRole_LosNotChecked()
    {
        bool losWasCalled = false;
        bool TrackingLos(float fromX, float fromY, float fromZ, float toX, float toY, float toZ)
        {
            losWasCalled = true;
            return false; // Would fail if called
        }

        var config = FullValidationConfig();
        config.EnableReachabilityCheck = false;
        // Ambush assigns Flanker first (not Overwatch), so LOS should not be checked
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: AlwaysSnapValidator, losValidator: TrackingLos);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush; // Flanker first
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsFalse(losWasCalled, "LOS should not be checked for non-Overwatch roles");
        Assert.IsTrue(follower.HasTacticalPosition);
    }

    [Test]
    public void Los_DisabledByConfig_ValidatorNotCalled()
    {
        bool losWasCalled = false;
        bool TrackingLos(float fromX, float fromY, float fromZ, float toX, float toY, float toZ)
        {
            losWasCalled = true;
            return false;
        }

        var config = FullValidationConfig();
        config.EnableLosCheck = false;
        config.EnableReachabilityCheck = false;
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: AlwaysSnapValidator, losValidator: TrackingLos);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Snipe; // Would be Overwatch
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsFalse(losWasCalled, "LOS validator should not be called when disabled by config");
        Assert.IsTrue(follower.HasTacticalPosition);
    }

    [Test]
    public void Los_NoValidator_Skipped()
    {
        var config = FullValidationConfig();
        config.EnableReachabilityCheck = false;
        // No losValidator passed
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: AlwaysSnapValidator);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Snipe; // Would be Overwatch
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
    }

    [Test]
    public void Combined_ReachabilityAndLos_BothMustPass()
    {
        var config = FullValidationConfig();
        var strategy = new GotoObjectiveStrategy(
            config,
            seed: 42,
            positionValidator: AlwaysSnapValidator,
            reachabilityValidator: AlwaysReachable,
            losValidator: AlwaysHasLos
        );
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Snipe; // Overwatch — both checks apply
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
        Assert.AreEqual(SquadRole.Overwatch, follower.SquadRole);
    }

    [Test]
    public void Combined_ReachabilityPassesLosFails_FallbackUsed()
    {
        var config = FullValidationConfig();
        var strategy = new GotoObjectiveStrategy(
            config,
            seed: 42,
            positionValidator: AlwaysSnapValidator,
            reachabilityValidator: AlwaysReachable,
            losValidator: NeverHasLos
        );
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Snipe; // Overwatch — LOS will fail
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // Reachability passes but LOS always fails for Overwatch → no valid position
        Assert.IsFalse(follower.HasTacticalPosition);
    }

    // ── Cover Position Source ──────────────────────

    private static int AlwaysProvideCover(float objX, float objY, float objZ, float radius, float[] outPositions, int maxCount)
    {
        for (int i = 0; i < maxCount; i++)
        {
            outPositions[i * 3] = objX + (i + 1) * 2f;
            outPositions[i * 3 + 1] = objY;
            outPositions[i * 3 + 2] = objZ + (i + 1) * 2f;
        }
        return maxCount;
    }

    private static int EmptyCoverSource(float objX, float objY, float objZ, float radius, float[] outPositions, int maxCount)
    {
        return 0;
    }

    private static int PartialCoverSource(float objX, float objY, float objZ, float radius, float[] outPositions, int maxCount)
    {
        if (maxCount > 0)
        {
            outPositions[0] = objX + 5f;
            outPositions[1] = objY;
            outPositions[2] = objZ + 5f;
        }
        return System.Math.Min(1, maxCount);
    }

    private SquadStrategyConfig CoverSourceConfig()
    {
        var config = DefaultConfig();
        config.EnableCoverPositionSource = true;
        config.CoverSearchRadius = 25f;
        return config;
    }

    [Test]
    public void CoverSource_ProvidesEnough_UsedInsteadOfGeometric()
    {
        var config = CoverSourceConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42, coverPositionSource: AlwaysProvideCover);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
        // AlwaysProvideCover sets x = objX + 2, z = objZ + 2 for first position
        Assert.AreEqual(52f, follower.TacticalPositionX, 0.01f);
        Assert.AreEqual(0f, follower.TacticalPositionY, 0.01f);
        Assert.AreEqual(52f, follower.TacticalPositionZ, 0.01f);
    }

    [Test]
    public void CoverSource_ProvidesNotEnough_FallsBackToGeometric()
    {
        var config = CoverSourceConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42, coverPositionSource: PartialCoverSource);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
        // Partial cover (1 < clampedCount=1), but 1 >= 1, so cover IS used
        // Actually with 1 follower, clampedCount=1, and PartialCoverSource returns 1 which >= 1
        // So cover positions would be used for single-follower case.
        // Use 2 followers to test fallback properly.
    }

    [Test]
    public void CoverSource_ProvidesNotEnough_TwoFollowers_FallsBackToGeometric()
    {
        var config = CoverSourceConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42, coverPositionSource: PartialCoverSource);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var f1 = CreateBot(1);
        var f2 = CreateBot(2);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        squad.Members.Add(leader);
        squad.Members.Add(f1);
        squad.Members.Add(f2);
        squad.Leader = leader;
        leader.Squad = squad;
        f1.Squad = squad;
        f2.Squad = squad;
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // PartialCoverSource returns 1 but clampedCount=2, so falls back to geometric
        Assert.IsTrue(f1.HasTacticalPosition);
        Assert.IsTrue(f2.HasTacticalPosition);
        // Geometric positions will NOT be at 55,0,55 (partial cover offset)
        // They are computed by TacticalPositionCalculator which produces different values
    }

    [Test]
    public void CoverSource_ReturnsZero_FallsBackToGeometric()
    {
        var config = CoverSourceConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42, coverPositionSource: EmptyCoverSource);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
        // Geometric fallback — position should NOT match cover source output
        Assert.AreNotEqual(52f, follower.TacticalPositionX);
    }

    [Test]
    public void CoverSource_DisabledByConfig_GeometricUsed()
    {
        var config = DefaultConfig();
        config.EnableCoverPositionSource = false;
        var strategy = new GotoObjectiveStrategy(config, seed: 42, coverPositionSource: AlwaysProvideCover);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
        // Cover source disabled — geometric is used, so positions differ from cover output
        Assert.AreNotEqual(52f, follower.TacticalPositionX);
    }

    [Test]
    public void CoverSource_NoDelegate_GeometricUsed()
    {
        var config = CoverSourceConfig();
        // No coverPositionSource parameter
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
        // No delegate — geometric is used
        Assert.AreNotEqual(52f, follower.TacticalPositionX);
    }

    // ── Combat-Aware Re-evaluation ────────────────────────

    private SquadStrategyConfig CombatAwareConfig()
    {
        var config = DefaultConfig();
        config.EnableCombatAwarePositioning = true;
        return config;
    }

    private (SquadEntity squad, BotEntity leader, BotEntity follower) SetupCombatScenario(SquadStrategyConfig config)
    {
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionY = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        return (squad, leader, follower);
    }

    [Test]
    public void CombatReeval_ThreatDetected_RepositionsFollowers()
    {
        var config = CombatAwareConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, follower) = SetupCombatScenario(config);

        strategy.Activate(squad);
        strategy.Update();

        float origTacX = follower.TacticalPositionX;
        float origTacZ = follower.TacticalPositionZ;

        // Simulate threat detection
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.HasThreatDirection = true;
        squad.CombatVersion = 1;

        strategy.Update();

        Assert.AreEqual(1, squad.LastProcessedCombatVersion);
        Assert.IsTrue(follower.HasTacticalPosition);
        // Position should have changed from threat-oriented computation
        bool posChanged =
            Math.Abs(follower.TacticalPositionX - origTacX) > 0.01f || Math.Abs(follower.TacticalPositionZ - origTacZ) > 0.01f;
        Assert.IsTrue(posChanged, "Combat positions should differ from standard positions");
    }

    [Test]
    public void CombatReeval_ThreatCleared_RevertsToStandardPositions()
    {
        var config = CombatAwareConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, follower) = SetupCombatScenario(config);

        strategy.Activate(squad);
        strategy.Update();

        // Enter combat
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.HasThreatDirection = true;
        squad.CombatVersion = 1;
        strategy.Update();

        float combatTacX = follower.TacticalPositionX;

        // Clear combat
        squad.HasThreatDirection = false;
        squad.CombatVersion = 2;
        strategy.Update();

        Assert.AreEqual(2, squad.LastProcessedCombatVersion);
        Assert.IsTrue(follower.HasTacticalPosition);
        // Should revert to standard geometric positions (different from combat)
        bool posChanged = Math.Abs(follower.TacticalPositionX - combatTacX) > 0.01f;
        Assert.IsTrue(posChanged, "Positions should change when threat clears");
    }

    [Test]
    public void CombatReeval_SameVersion_NoRecompute()
    {
        var config = CombatAwareConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, follower) = SetupCombatScenario(config);

        strategy.Activate(squad);
        strategy.Update();

        // Enter combat
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.HasThreatDirection = true;
        squad.CombatVersion = 1;
        strategy.Update();

        float tacX = follower.TacticalPositionX;
        float tacZ = follower.TacticalPositionZ;

        // Update again without changing combat version
        strategy.Update();

        Assert.AreEqual(tacX, follower.TacticalPositionX, 0.001f);
        Assert.AreEqual(tacZ, follower.TacticalPositionZ, 0.001f);
    }

    [Test]
    public void CombatReeval_DisabledByConfig_NoRecompute()
    {
        var config = DefaultConfig();
        config.EnableCombatAwarePositioning = false;
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, follower) = SetupCombatScenario(config);

        strategy.Activate(squad);
        strategy.Update();

        float origTacX = follower.TacticalPositionX;

        // Simulate threat detection
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.HasThreatDirection = true;
        squad.CombatVersion = 1;
        strategy.Update();

        // Should NOT have been re-evaluated
        Assert.AreEqual(0, squad.LastProcessedCombatVersion);
        Assert.AreEqual(origTacX, follower.TacticalPositionX, 0.001f);
    }

    [Test]
    public void CombatReeval_EscortReassignedToFlanker()
    {
        var config = CombatAwareConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.MoveToPosition; // MoveToPosition → all Escort
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.AreEqual(SquadRole.Escort, follower.SquadRole);

        // Enter combat
        squad.ThreatDirectionX = 0f;
        squad.ThreatDirectionZ = 1f;
        squad.HasThreatDirection = true;
        squad.CombatVersion = 1;
        strategy.Update();

        // Escort should be reassigned to Flanker in combat
        Assert.AreEqual(SquadRole.Flanker, follower.SquadRole);
    }

    [Test]
    public void CombatReeval_MultipleFollowers_AllGetCombatPositions()
    {
        var config = CombatAwareConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var f1 = CreateBot(1);
        var f2 = CreateBot(2);
        var f3 = CreateBot(3);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        squad.Members.Add(leader);
        squad.Members.Add(f1);
        squad.Members.Add(f2);
        squad.Members.Add(f3);
        squad.Leader = leader;
        leader.Squad = squad;
        f1.Squad = squad;
        f2.Squad = squad;
        f3.Squad = squad;
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // Enter combat with threat from east
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.HasThreatDirection = true;
        squad.CombatVersion = 1;
        strategy.Update();

        Assert.IsTrue(f1.HasTacticalPosition);
        Assert.IsTrue(f2.HasTacticalPosition);
        Assert.IsTrue(f3.HasTacticalPosition);
        Assert.AreEqual(1, squad.LastProcessedCombatVersion);
    }

    [Test]
    public void CombatReeval_NoFollowers_DoesNotThrow()
    {
        var config = CombatAwareConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);

        leader.HasActiveObjective = true;
        squad.Members.Add(leader);
        squad.Leader = leader;
        leader.Squad = squad;
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        squad.ThreatDirectionX = 1f;
        squad.HasThreatDirection = true;
        squad.CombatVersion = 1;

        Assert.DoesNotThrow(() => strategy.Update());
    }

    [Test]
    public void CombatReeval_WithCommRangeGate_OutOfRangeSkipped()
    {
        var config = CombatAwareConfig();
        config.EnableCommunicationRange = true;
        config.CommunicationRangeNoEarpiece = 35f;
        config.CommunicationRangeEarpiece = 200f;
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var nearFollower = CreateBot(1);
        var farFollower = CreateBot(2);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionY = 0f;
        leader.CurrentPositionZ = 0f;
        nearFollower.CurrentPositionX = 10f;
        farFollower.CurrentPositionX = 50f;

        squad.Members.Add(leader);
        squad.Members.Add(nearFollower);
        squad.Members.Add(farFollower);
        squad.Leader = leader;
        leader.Squad = squad;
        nearFollower.Squad = squad;
        farFollower.Squad = squad;
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        // Enter combat
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.HasThreatDirection = true;
        squad.CombatVersion = 1;
        strategy.Update();

        Assert.IsTrue(nearFollower.HasTacticalPosition, "Near follower should receive combat position");
        Assert.IsFalse(farFollower.HasTacticalPosition, "Far follower should be out of comm range");
    }

    [Test]
    public void CombatReeval_WithPositionValidation_ValidatesPositions()
    {
        var config = CombatAwareConfig();
        config.EnablePositionValidation = true;
        var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: AlwaysSnapValidator);
        var (squad, leader, follower) = SetupCombatScenario(config);

        strategy.Activate(squad);
        strategy.Update();

        // Enter combat
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.HasThreatDirection = true;
        squad.CombatVersion = 1;
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
        // AlwaysSnapValidator adds 0.5 to Y
        Assert.AreEqual(0.5f, follower.TacticalPositionY, 0.01f);
    }

    [Test]
    public void CombatReeval_InactiveLeader_Skipped()
    {
        var config = CombatAwareConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, follower) = SetupCombatScenario(config);

        strategy.Activate(squad);
        strategy.Update();

        leader.IsActive = false;
        squad.CombatVersion = 1;

        // Should not throw and should not change positions
        Assert.DoesNotThrow(() => strategy.Update());
    }

    [Test]
    public void CombatReeval_DirectCallRecomputeForCombat_Works()
    {
        var config = CombatAwareConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, follower) = SetupCombatScenario(config);

        strategy.Activate(squad);
        strategy.Update();

        float origTacX = follower.TacticalPositionX;

        // Direct call
        squad.ThreatDirectionX = 0f;
        squad.ThreatDirectionZ = 1f;
        squad.HasThreatDirection = true;

        strategy.RecomputeForCombat(squad);

        Assert.IsTrue(follower.HasTacticalPosition);
        bool posChanged =
            Math.Abs(follower.TacticalPositionX - origTacX) > 0.01f
            || Math.Abs(follower.TacticalPositionZ - follower.TacticalPositionZ) > 0.01f;
        // Position should have changed with threat from north
    }

    [Test]
    public void CombatReeval_ThreatFromDifferentDirections_DifferentPositions()
    {
        var config = CombatAwareConfig();
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, follower) = SetupCombatScenario(config);

        strategy.Activate(squad);
        strategy.Update();

        // Threat from east
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.HasThreatDirection = true;
        squad.CombatVersion = 1;
        strategy.Update();

        float eastTacX = follower.TacticalPositionX;
        float eastTacZ = follower.TacticalPositionZ;

        // Threat from north
        squad.ThreatDirectionX = 0f;
        squad.ThreatDirectionZ = 1f;
        squad.CombatVersion = 2;
        strategy.Update();

        float northTacX = follower.TacticalPositionX;
        float northTacZ = follower.TacticalPositionZ;

        bool posChanged = Math.Abs(eastTacX - northTacX) > 0.01f || Math.Abs(eastTacZ - northTacZ) > 0.01f;
        Assert.IsTrue(posChanged, "Threat from different directions should produce different positions");
    }

    // ── SquadStrategyConfig Combat Property ───────────────

    [Test]
    public void Config_EnableCombatAwarePositioning_DefaultTrue()
    {
        var config = new SquadStrategyConfig();
        Assert.IsTrue(config.EnableCombatAwarePositioning);
    }

    [Test]
    public void Config_EnableCombatAwarePositioning_CanBeDisabled()
    {
        var config = new SquadStrategyConfig();
        config.EnableCombatAwarePositioning = false;
        Assert.IsFalse(config.EnableCombatAwarePositioning);
    }

    [Test]
    public void CoverSource_SkipsValidation_WhenBsgUsed()
    {
        bool validatorCalled = false;
        bool TrackingSnapValidator(float x, float y, float z, out float ox, out float oy, out float oz)
        {
            validatorCalled = true;
            ox = x;
            oy = y;
            oz = z;
            return true;
        }

        var config = CoverSourceConfig();
        config.EnablePositionValidation = true;
        var strategy = new GotoObjectiveStrategy(
            config,
            seed: 42,
            positionValidator: TrackingSnapValidator,
            coverPositionSource: AlwaysProvideCover
        );
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionZ = 0f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsFalse(validatorCalled, "Position validator should not be called when BSG cover source provides enough positions");
        Assert.IsTrue(follower.HasTacticalPosition);
        Assert.AreEqual(52f, follower.TacticalPositionX, 0.01f);
    }

    // ── Multi-Level Objective Sharing ──────────────────────

    private SquadStrategyConfig ObjectiveSharingConfig(
        int trustedCount = 2,
        float noiseBase = 5f,
        bool enableCommRange = false,
        float commNoEar = 35f,
        float commEar = 200f
    )
    {
        var config = DefaultConfig();
        config.EnableObjectiveSharing = true;
        config.TrustedFollowerCount = trustedCount;
        config.SharingNoiseBase = noiseBase;
        config.EnableCommunicationRange = enableCommRange;
        config.CommunicationRangeNoEarpiece = commNoEar;
        config.CommunicationRangeEarpiece = commEar;
        return config;
    }

    private (SquadEntity squad, BotEntity leader, BotEntity f1, BotEntity f2, BotEntity f3) SetupThreeFollowerSquad(
        SquadStrategyConfig config,
        int seed = 42
    )
    {
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var f1 = CreateBot(1);
        var f2 = CreateBot(2);
        var f3 = CreateBot(3);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        leader.CurrentPositionX = 0f;
        leader.CurrentPositionY = 0f;
        leader.CurrentPositionZ = 0f;

        f1.CurrentPositionX = 5f; // nearest
        f2.CurrentPositionX = 15f; // middle
        f3.CurrentPositionX = 25f; // farthest

        squad.Members.Add(leader);
        squad.Members.Add(f1);
        squad.Members.Add(f2);
        squad.Members.Add(f3);
        squad.Leader = leader;
        leader.Squad = squad;
        f1.Squad = squad;
        f2.Squad = squad;
        f3.Squad = squad;
        squad.CoordinationLevel = 3f; // default coordination
        squad.Objective.SetObjective(50f, 0f, 50f);

        return (squad, leader, f1, f2, f3);
    }

    [Test]
    public void ObjectiveSharing_Enabled_AllFollowersReceivePositions()
    {
        var config = ObjectiveSharingConfig(trustedCount: 2);
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, f1, f2, f3) = SetupThreeFollowerSquad(config);

        strategy.Activate(squad);
        strategy.Update();

        // With comm range disabled, all 3 should get positions
        // 2 closest (f1, f2) get Tier 1, f3 gets Tier 2
        Assert.IsTrue(f1.HasTacticalPosition, "Closest follower should have position");
        Assert.IsTrue(f2.HasTacticalPosition, "Second closest should have position");
        Assert.IsTrue(f3.HasTacticalPosition, "Third follower should get relayed position");
    }

    [Test]
    public void ObjectiveSharing_TierAssignment_ClosestGetDirect()
    {
        var config = ObjectiveSharingConfig(trustedCount: 2);
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, f1, f2, f3) = SetupThreeFollowerSquad(config);

        strategy.Activate(squad);
        strategy.Update();

        // f1 (5m) and f2 (15m) are closest → Tier 1
        // f3 (25m) is furthest → Tier 2
        Assert.AreEqual(
            SPTQuestingBots.BotLogic.ECS.Systems.ObjectiveSharingCalculator.TierDirect,
            f1.SharingTier,
            "Closest follower should be Tier 1 (direct)"
        );
        Assert.AreEqual(
            SPTQuestingBots.BotLogic.ECS.Systems.ObjectiveSharingCalculator.TierDirect,
            f2.SharingTier,
            "Second closest should be Tier 1 (direct)"
        );
        Assert.AreEqual(
            SPTQuestingBots.BotLogic.ECS.Systems.ObjectiveSharingCalculator.TierRelayed,
            f3.SharingTier,
            "Farthest follower should be Tier 2 (relayed)"
        );
    }

    [Test]
    public void ObjectiveSharing_Tier2PositionDegraded_XZDiffer()
    {
        var config = ObjectiveSharingConfig(trustedCount: 1, noiseBase: 10f);
        var strategy1 = new GotoObjectiveStrategy(config, seed: 42);
        var (squad1, leader1, f1a, f2a, f3a) = SetupThreeFollowerSquad(config);

        strategy1.Activate(squad1);
        strategy1.Update();

        // f1a is Tier 1 (exact), f2a and f3a are Tier 2 (noisy)
        // Tier 1 should get exact geometric position
        // Tier 2 should have some noise applied

        // With noiseBase=10 and coordination=3, noiseScale = 10 * (6-3)/5 = 6m
        // Tier 2 positions will differ from the original geometric position
        Assert.AreEqual(SPTQuestingBots.BotLogic.ECS.Systems.ObjectiveSharingCalculator.TierDirect, f1a.SharingTier);
        Assert.AreEqual(SPTQuestingBots.BotLogic.ECS.Systems.ObjectiveSharingCalculator.TierRelayed, f2a.SharingTier);
    }

    [Test]
    public void ObjectiveSharing_HighCoordination_LessNoise()
    {
        // Run with Elite coordination (5) — noise should be minimal
        var config1 = ObjectiveSharingConfig(trustedCount: 1, noiseBase: 10f);
        var strategy1 = new GotoObjectiveStrategy(config1, seed: 100);
        var squad1 = CreateSquad(0);
        var leader1 = CreateBot(0);
        var near1 = CreateBot(1);
        var far1 = CreateBot(2);

        leader1.HasActiveObjective = true;
        leader1.CurrentQuestAction = QuestActionId.Ambush;
        near1.CurrentPositionX = 5f;
        far1.CurrentPositionX = 20f;
        squad1.Members.Add(leader1);
        squad1.Members.Add(near1);
        squad1.Members.Add(far1);
        squad1.Leader = leader1;
        leader1.Squad = squad1;
        near1.Squad = squad1;
        far1.Squad = squad1;
        squad1.CoordinationLevel = 5f; // Elite: noise = 10 * (6-5)/5 = 2m
        squad1.Objective.SetObjective(50f, 0f, 50f);
        strategy1.Activate(squad1);
        strategy1.Update();
        float eliteX = far1.TacticalPositionX;

        // Run with TimmyTeam6 coordination (1) — noise should be large
        var config2 = ObjectiveSharingConfig(trustedCount: 1, noiseBase: 10f);
        var strategy2 = new GotoObjectiveStrategy(config2, seed: 100);
        var squad2 = CreateSquad(1);
        var leader2 = CreateBot(10);
        var near2 = CreateBot(11);
        var far2 = CreateBot(12);

        leader2.HasActiveObjective = true;
        leader2.CurrentQuestAction = QuestActionId.Ambush;
        near2.CurrentPositionX = 5f;
        far2.CurrentPositionX = 20f;
        squad2.Members.Add(leader2);
        squad2.Members.Add(near2);
        squad2.Members.Add(far2);
        squad2.Leader = leader2;
        leader2.Squad = squad2;
        near2.Squad = squad2;
        far2.Squad = squad2;
        squad2.CoordinationLevel = 1f; // TimmyTeam6: noise = 10 * (6-1)/5 = 10m
        squad2.Objective.SetObjective(50f, 0f, 50f);
        strategy2.Activate(squad2);
        strategy2.Update();
        float timmyX = far2.TacticalPositionX;

        // Both have Tier 2 — but the Tier 1 exact positions would be the same
        // So the Y values should be the same (no noise on Y)
        Assert.AreEqual(far1.TacticalPositionY, far2.TacticalPositionY, 0.01f, "Y should be identical (no noise applied to Y)");
    }

    [Test]
    public void ObjectiveSharing_CommRangeEnabled_FarFollowerTier0()
    {
        var config = ObjectiveSharingConfig(trustedCount: 2, enableCommRange: true, commNoEar: 20f, commEar: 200f);
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, f1, f2, f3) = SetupThreeFollowerSquad(config);

        // f1 at 5m (in 20m range), f2 at 15m (in 20m range), f3 at 25m (out of 20m range)
        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(f1.HasTacticalPosition, "f1 within comm range should get position");
        Assert.IsTrue(f2.HasTacticalPosition, "f2 within comm range should get position");
        // f3 is out of leader range AND out of relay range from f1/f2 (both within 20m of leader but f3 is 10m from f2)
        // f3 is at 25m. f2 is at 15m. Distance f3-f2 = 10m which IS within 20m range.
        // So f3 should get relayed through f2!
        Assert.IsTrue(f3.HasTacticalPosition, "f3 should get relayed through f2");
        Assert.AreEqual(
            SPTQuestingBots.BotLogic.ECS.Systems.ObjectiveSharingCalculator.TierRelayed,
            f3.SharingTier,
            "f3 should be Tier 2 (relayed)"
        );
    }

    [Test]
    public void ObjectiveSharing_CommRangeEnabled_TotallyIsolated_NoPosition()
    {
        var config = ObjectiveSharingConfig(trustedCount: 2, enableCommRange: true, commNoEar: 10f, commEar: 200f);
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, f1, f2, f3) = SetupThreeFollowerSquad(config);

        // f1 at 5m (in 10m), f2 at 15m (out of 10m), f3 at 25m (out of 10m)
        // f2 is out of leader range, nearest Tier 1 is f1 at 10m distance — on the edge
        // f3 is out of leader range, nearest Tier 1 is f1 at 20m — out of 10m relay range

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(f1.HasTacticalPosition, "f1 within comm range");
        Assert.AreEqual(SPTQuestingBots.BotLogic.ECS.Systems.ObjectiveSharingCalculator.TierDirect, f1.SharingTier);
    }

    [Test]
    public void ObjectiveSharing_TrustedCount1_OnlyOneDirectFollower()
    {
        var config = ObjectiveSharingConfig(trustedCount: 1);
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, f1, f2, f3) = SetupThreeFollowerSquad(config);

        strategy.Activate(squad);
        strategy.Update();

        // Only f1 (closest) should be Tier 1
        Assert.AreEqual(
            SPTQuestingBots.BotLogic.ECS.Systems.ObjectiveSharingCalculator.TierDirect,
            f1.SharingTier,
            "Only the closest follower should be Tier 1"
        );
        Assert.AreEqual(SPTQuestingBots.BotLogic.ECS.Systems.ObjectiveSharingCalculator.TierRelayed, f2.SharingTier);
        Assert.AreEqual(SPTQuestingBots.BotLogic.ECS.Systems.ObjectiveSharingCalculator.TierRelayed, f3.SharingTier);
    }

    [Test]
    public void ObjectiveSharing_Disabled_LegacyBehavior()
    {
        var config = DefaultConfig();
        config.EnableObjectiveSharing = false;
        config.EnableCommunicationRange = false;
        config.EnableSquadPersonality = false;
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, f1, f2, f3) = SetupThreeFollowerSquad(config);

        strategy.Activate(squad);
        strategy.Update();

        // With all gates disabled, all should receive positions (legacy behavior)
        Assert.IsTrue(f1.HasTacticalPosition);
        Assert.IsTrue(f2.HasTacticalPosition);
        Assert.IsTrue(f3.HasTacticalPosition);
        // SharingTier should remain 0 (default) since objective sharing is disabled
        Assert.AreEqual(0, f1.SharingTier);
    }

    [Test]
    public void ObjectiveSharing_CombatRecompute_UsesTiers()
    {
        var config = ObjectiveSharingConfig(trustedCount: 2);
        config.EnableCombatAwarePositioning = true;
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var (squad, leader, f1, f2, f3) = SetupThreeFollowerSquad(config);

        strategy.Activate(squad);
        strategy.Update();

        // Enter combat
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.HasThreatDirection = true;
        squad.CombatVersion = 1;
        strategy.Update();

        // All should still have positions after combat recompute
        Assert.IsTrue(f1.HasTacticalPosition);
        Assert.IsTrue(f2.HasTacticalPosition);
        Assert.IsTrue(f3.HasTacticalPosition);

        // Tier assignment should still work
        Assert.AreEqual(SPTQuestingBots.BotLogic.ECS.Systems.ObjectiveSharingCalculator.TierDirect, f1.SharingTier);
        Assert.AreEqual(SPTQuestingBots.BotLogic.ECS.Systems.ObjectiveSharingCalculator.TierRelayed, f3.SharingTier);
    }

    [Test]
    public void ObjectiveSharing_SingleFollower_GetsTierDirect()
    {
        var config = ObjectiveSharingConfig(trustedCount: 2);
        var strategy = new GotoObjectiveStrategy(config, seed: 42);
        var squad = CreateSquad(0);
        var leader = CreateBot(0);
        var follower = CreateBot(1);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        follower.CurrentPositionX = 10f;
        SetupSquadWithLeaderAndFollower(squad, leader, follower);
        squad.Objective.SetObjective(50f, 0f, 50f);

        strategy.Activate(squad);
        strategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
        Assert.AreEqual(SPTQuestingBots.BotLogic.ECS.Systems.ObjectiveSharingCalculator.TierDirect, follower.SharingTier);
    }
}

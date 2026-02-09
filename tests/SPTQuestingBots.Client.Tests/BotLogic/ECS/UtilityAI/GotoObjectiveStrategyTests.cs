using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI
{
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
    }
}

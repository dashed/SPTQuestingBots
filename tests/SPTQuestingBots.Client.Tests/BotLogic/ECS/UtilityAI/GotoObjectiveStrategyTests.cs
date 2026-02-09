using System;
using System.Collections.Generic;
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
                // Disable gates by default so existing tests are not affected
                EnableCommunicationRange = false,
                EnableSquadPersonality = false,
                EnablePositionValidation = false,
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
                    receivedCount++;
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

            bool result = strategy.TryFallbackPosition(50f, 50f, 0f, 15f, 16, out float fx, out float fy, out float fz);

            Assert.IsTrue(result);
            // AlwaysSnapValidator adds 0.5 to Y
            Assert.AreEqual(0.5f, fy, 0.01f);
        }

        [Test]
        public void TryFallbackPosition_ValidatorAlwaysFails_ReturnsFalse()
        {
            var config = ValidationConfig();
            var strategy = new GotoObjectiveStrategy(config, seed: 42, positionValidator: AlwaysFailValidator);

            bool result = strategy.TryFallbackPosition(50f, 50f, 0f, 15f, 16, out float fx, out float fy, out float fz);

            Assert.IsFalse(result);
            Assert.AreEqual(0f, fx);
            Assert.AreEqual(0f, fy);
            Assert.AreEqual(0f, fz);
        }
    }
}

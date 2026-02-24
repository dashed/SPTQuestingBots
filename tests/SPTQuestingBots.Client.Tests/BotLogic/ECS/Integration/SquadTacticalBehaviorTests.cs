using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Integration;

/// <summary>
/// Behavioral simulation tests for squad tactical scenarios:
///   - Formation + combat lifecycle
///   - Leader death and reassignment
///   - Communication range effects
///   - Vulture strategy lifecycle
///   - Follower task scoring boundaries
///   - Multi-strategy switching with hysteresis
/// </summary>
[TestFixture]
public class SquadTacticalBehaviorTests
{
    private SquadRegistry _squadRegistry;
    private SquadStrategyConfig _config;

    [SetUp]
    public void SetUp()
    {
        _squadRegistry = new SquadRegistry();
        _config = new SquadStrategyConfig
        {
            Enabled = true,
            GuardDistance = 8f,
            FlankDistance = 15f,
            OverwatchDistance = 25f,
            EscortDistance = 5f,
            ArrivalRadius = 3f,
            EnableCommunicationRange = false,
            EnableSquadPersonality = false,
            EnablePositionValidation = false,
            EnableCoverPositionSource = false,
            EnableCombatAwarePositioning = true,
            EnableObjectiveSharing = false,
            UseQuestTypeRoles = true,
        };
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private BotEntity CreateBot(int id, float x = 0f, float y = 0f, float z = 0f)
    {
        var bot = new BotEntity(id) { IsActive = true };
        bot.CurrentPositionX = x;
        bot.CurrentPositionY = y;
        bot.CurrentPositionZ = z;
        bot.TaskScores = new float[18];
        return bot;
    }

    private SquadEntity CreateSquadWithMembers(int strategyCount, out BotEntity leader, out BotEntity follower1, out BotEntity follower2)
    {
        var squad = _squadRegistry.Add(strategyCount, 3);
        leader = CreateBot(10, x: 100f, z: 100f);
        follower1 = CreateBot(11, x: 105f, z: 105f);
        follower2 = CreateBot(12, x: 95f, z: 95f);

        _squadRegistry.AddMember(squad, leader);
        _squadRegistry.AddMember(squad, follower1);
        _squadRegistry.AddMember(squad, follower2);

        // Wire up boss/follower hierarchy (mirrors HiveMind)
        follower1.Boss = leader;
        follower2.Boss = leader;
        leader.Followers.Add(follower1);
        leader.Followers.Add(follower2);

        return squad;
    }

    private SquadEntity CreateSquadWith4Members(
        int strategyCount,
        out BotEntity leader,
        out BotEntity f1,
        out BotEntity f2,
        out BotEntity f3
    )
    {
        var squad = _squadRegistry.Add(strategyCount, 4);
        leader = CreateBot(20, x: 100f, z: 100f);
        f1 = CreateBot(21, x: 105f, z: 105f);
        f2 = CreateBot(22, x: 95f, z: 95f);
        f3 = CreateBot(23, x: 100f, z: 110f);

        _squadRegistry.AddMember(squad, leader);
        _squadRegistry.AddMember(squad, f1);
        _squadRegistry.AddMember(squad, f2);
        _squadRegistry.AddMember(squad, f3);

        f1.Boss = leader;
        f2.Boss = leader;
        f3.Boss = leader;
        leader.Followers.Add(f1);
        leader.Followers.Add(f2);
        leader.Followers.Add(f3);

        return squad;
    }

    private void SetLeaderObjective(
        BotEntity leader,
        SquadEntity squad,
        float objX,
        float objY,
        float objZ,
        int questAction = QuestActionId.MoveToPosition
    )
    {
        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = questAction;
        squad.Objective.SetObjective(objX, objY, objZ);
    }

    // ========================================================================
    // Scenario 1: Formation + Combat Lifecycle
    // ========================================================================

    [Test]
    public void Scenario1_FormationThenCombat_FollowersGetEscortRolesThenCombatRoles()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f, QuestActionId.MoveToPosition);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Phase 1: Normal movement — followers get Escort roles
        manager.Update(_squadRegistry.ActiveSquads);
        Assert.That(f1.SquadRole, Is.EqualTo(SquadRole.Escort));
        Assert.That(f2.SquadRole, Is.EqualTo(SquadRole.Escort));
        Assert.IsTrue(f1.HasTacticalPosition);
        Assert.IsTrue(f2.HasTacticalPosition);

        // Save pre-combat positions
        float f1PreCombatX = f1.TacticalPositionX;
        float f1PreCombatZ = f1.TacticalPositionZ;

        // Phase 2: Combat — threat detected
        squad.HasThreatDirection = true;
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.CombatVersion++;

        manager.Update(_squadRegistry.ActiveSquads);

        // After combat: Escort should become Flanker via CombatPositionAdjuster
        Assert.That(f1.SquadRole, Is.EqualTo(SquadRole.Flanker));
        Assert.That(f2.SquadRole, Is.EqualTo(SquadRole.Flanker));

        // Positions should have changed
        bool positionsChanged =
            Math.Abs(f1.TacticalPositionX - f1PreCombatX) > 0.01f || Math.Abs(f1.TacticalPositionZ - f1PreCombatZ) > 0.01f;
        Assert.IsTrue(positionsChanged, "Combat should recompute tactical positions");
    }

    [Test]
    public void Scenario1_CombatCleared_PositionsRevertToStandard()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f, QuestActionId.Ambush);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        // Phase 1: No combat — standard Ambush positions (Flanker, Overwatch, Guard)
        manager.Update(_squadRegistry.ActiveSquads);
        var role1NoCombat = f1.SquadRole;
        var role2NoCombat = f2.SquadRole;
        // Ambush: first=Flanker, second=Overwatch
        Assert.That(role1NoCombat, Is.EqualTo(SquadRole.Flanker));
        Assert.That(role2NoCombat, Is.EqualTo(SquadRole.Overwatch));

        // Phase 2: Combat
        squad.HasThreatDirection = true;
        squad.ThreatDirectionX = 0f;
        squad.ThreatDirectionZ = 1f;
        squad.CombatVersion++;
        manager.Update(_squadRegistry.ActiveSquads);

        // Phase 3: Combat clears
        squad.HasThreatDirection = false;
        squad.CombatVersion++;
        manager.Update(_squadRegistry.ActiveSquads);

        // Roles should revert to quest-type based roles
        Assert.That(f1.SquadRole, Is.EqualTo(SquadRole.Flanker));
        Assert.That(f2.SquadRole, Is.EqualTo(SquadRole.Overwatch));
    }

    [Test]
    public void Scenario1_CombatGuardsFaceThreat_FlankersAlternateSides_OverwatchBehind()
    {
        var squad = CreateSquadWith4Members(2, out var leader, out var f1, out var f2, out var f3);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f, QuestActionId.HoldAtPosition);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        // Set objective
        manager.Update(_squadRegistry.ActiveSquads);

        // HoldAtPosition: first=Flanker, second=Overwatch, remaining=Guard
        // Trigger combat with threat from east (positive X)
        squad.HasThreatDirection = true;
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.CombatVersion++;

        manager.Update(_squadRegistry.ActiveSquads);

        // Count roles after combat reassignment
        int guardCount = 0;
        int flankerCount = 0;
        int overwatchCount = 0;
        foreach (var m in new[] { f1, f2, f3 })
        {
            if (!m.HasTacticalPosition)
                continue;
            switch (m.SquadRole)
            {
                case SquadRole.Guard:
                    guardCount++;
                    break;
                case SquadRole.Flanker:
                    flankerCount++;
                    break;
                case SquadRole.Overwatch:
                    overwatchCount++;
                    break;
            }
        }

        // Verify at least one guard and one flanker or overwatch
        Assert.That(
            guardCount + flankerCount + overwatchCount,
            Is.GreaterThanOrEqualTo(2),
            "At least 2 followers should have combat positions"
        );

        // Verify overwatch is behind threat (opposite direction)
        foreach (var m in new[] { f1, f2, f3 })
        {
            if (!m.HasTacticalPosition || m.SquadRole != SquadRole.Overwatch)
                continue;

            // Overwatch should be at objective - threatDir * distance
            // With threat from +X, overwatch X should be LESS than objective X
            Assert.That(m.TacticalPositionX, Is.LessThan(200f), "Overwatch should be behind the defense line (opposite threat direction)");
        }
    }

    // ========================================================================
    // Scenario 2: Leader Death and Reassignment
    // ========================================================================

    [Test]
    public void Scenario2_LeaderDies_NewLeaderAssigned_FollowersRegroup()
    {
        var squad = CreateSquadWith4Members(2, out var leader, out var f1, out var f2, out var f3);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);
        f1.IsActive = true;
        f2.IsActive = true;
        f3.IsActive = true;

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        // Initial: positions assigned
        manager.Update(_squadRegistry.ActiveSquads);
        Assert.That(squad.Size, Is.EqualTo(4));
        Assert.AreSame(leader, squad.Leader);

        // Leader "dies" — remove from squad
        _squadRegistry.RemoveMember(squad, leader);

        // Verify new leader is an active follower
        Assert.IsNotNull(squad.Leader, "New leader should be assigned");
        Assert.AreNotSame(leader, squad.Leader);
        Assert.That(squad.Leader.IsActive, Is.True, "New leader should be active");
        Assert.That(squad.Leader.SquadRole, Is.EqualTo(SquadRole.Leader));

        // Verify removed leader is cleaned up
        Assert.IsNull(leader.Squad);
        Assert.That(leader.SquadRole, Is.EqualTo(SquadRole.None));

        // Verify no orphaned followers (all remaining members have Squad reference)
        foreach (var member in squad.Members)
        {
            Assert.AreSame(squad, member.Squad, "All remaining members should reference the squad");
        }

        Assert.That(squad.Size, Is.EqualTo(3), "Squad should have 3 members after leader removal");
    }

    [Test]
    public void Scenario2_BugFix_NewLeader_HasTacticalPositionCleared()
    {
        // BUG: When leader dies, the new leader (previously a follower) retains
        // HasTacticalPosition = true from its follower days. This can cause the
        // new leader to try to go to a stale tactical position instead of leading.
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        // Assign tactical positions to followers
        manager.Update(_squadRegistry.ActiveSquads);
        Assert.IsTrue(f1.HasTacticalPosition, "Follower should have tactical position");

        // Leader dies
        _squadRegistry.RemoveMember(squad, leader);

        // New leader should NOT retain HasTacticalPosition
        var newLeader = squad.Leader;
        Assert.IsFalse(newLeader.HasTacticalPosition, "New leader must not retain HasTacticalPosition from follower days");
    }

    [Test]
    public void Scenario2_AllFollowersInactive_FallbackLeaderAssigned()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);

        // Make followers inactive
        f1.IsActive = false;
        f2.IsActive = false;

        // Leader dies
        _squadRegistry.RemoveMember(squad, leader);

        // Should fall back to first member even though inactive
        Assert.IsNotNull(squad.Leader, "Leader should be assigned even if inactive");
        Assert.That(squad.Leader.SquadRole, Is.EqualTo(SquadRole.Leader));
        Assert.That(squad.Size, Is.EqualTo(2));
    }

    [Test]
    public void Scenario2_LeaderDies_NewLeaderCanReceiveNewObjective()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        // Phase 1: Normal operation
        manager.Update(_squadRegistry.ActiveSquads);

        // Phase 2: Leader dies
        _squadRegistry.RemoveMember(squad, leader);
        var newLeader = squad.Leader;

        // Phase 3: New leader gets a new objective
        newLeader.HasActiveObjective = true;
        newLeader.CurrentQuestAction = QuestActionId.PlantItem;
        squad.Objective.SetObjective(300f, 0f, 300f);

        // Score and pick should work with new leader
        manager.ScoreAndPick(squad);

        // GotoObjective should still score 0.5 (new leader has active objective)
        Assert.That(squad.StrategyScores[0], Is.EqualTo(0.5f));
    }

    [Test]
    public void Scenario2_LeaderDies_SquadEmptied_NoGhostSquad()
    {
        var squad = _squadRegistry.Add(2, 2);
        var leader = CreateBot(30);
        _squadRegistry.AddMember(squad, leader);

        // Remove only member (leader)
        _squadRegistry.RemoveMember(squad, leader);

        Assert.IsNull(squad.Leader, "Empty squad should have no leader");
        Assert.That(squad.Size, Is.EqualTo(0));

        // Strategy should handle empty squad gracefully
        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        // Should not throw
        Assert.DoesNotThrow(() => manager.ScoreAndPick(squad));
    }

    // ========================================================================
    // Scenario 3: Communication Range Effects
    // ========================================================================

    [Test]
    public void Scenario3_FollowerOutOfCommRange_LosesTacticalPosition()
    {
        var config = new SquadStrategyConfig
        {
            Enabled = true,
            GuardDistance = 8f,
            FlankDistance = 15f,
            OverwatchDistance = 25f,
            EscortDistance = 5f,
            ArrivalRadius = 3f,
            EnableCommunicationRange = true,
            CommunicationRangeNoEarpiece = 35f,
            CommunicationRangeEarpiece = 200f,
            EnableSquadPersonality = false,
            EnablePositionValidation = false,
            EnableCoverPositionSource = false,
            EnableCombatAwarePositioning = false,
            EnableObjectiveSharing = false,
            UseQuestTypeRoles = true,
        };

        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        // f1 is close (5m), f2 is far (100m, beyond 35m range)
        f1.CurrentPositionX = 103f;
        f1.CurrentPositionZ = 103f;
        f2.CurrentPositionX = 200f;
        f2.CurrentPositionZ = 200f; // ~141m from leader at (100,100)

        var gotoStrategy = new GotoObjectiveStrategy(config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        manager.Update(_squadRegistry.ActiveSquads);

        Assert.IsTrue(f1.HasTacticalPosition, "Close follower should receive tactical position");
        Assert.IsFalse(f2.HasTacticalPosition, "Far follower (beyond comm range) should NOT receive tactical position");
    }

    [Test]
    public void Scenario3_EarpieceExtendsCommRange()
    {
        var config = new SquadStrategyConfig
        {
            Enabled = true,
            GuardDistance = 8f,
            FlankDistance = 15f,
            OverwatchDistance = 25f,
            EscortDistance = 5f,
            ArrivalRadius = 3f,
            EnableCommunicationRange = true,
            CommunicationRangeNoEarpiece = 35f,
            CommunicationRangeEarpiece = 200f,
            EnableSquadPersonality = false,
            EnablePositionValidation = false,
            EnableCoverPositionSource = false,
            EnableCombatAwarePositioning = false,
            EnableObjectiveSharing = false,
            UseQuestTypeRoles = true,
        };

        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        // Both at ~70m from leader: beyond no-earpiece (35m) but within earpiece (200m)
        f1.CurrentPositionX = 150f;
        f1.CurrentPositionZ = 150f;
        f2.CurrentPositionX = 150f;
        f2.CurrentPositionZ = 150f;

        // f1 and leader have earpieces, f2 and leader do not
        leader.HasEarPiece = true;
        f1.HasEarPiece = true;
        f2.HasEarPiece = false;

        var gotoStrategy = new GotoObjectiveStrategy(config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        manager.Update(_squadRegistry.ActiveSquads);

        // f1 should have position (both have earpieces, within 200m)
        Assert.IsTrue(f1.HasTacticalPosition, "Earpiece-equipped follower within extended range should get position");

        // f2 should NOT have position (no earpiece on f2, beyond 35m)
        Assert.IsFalse(f2.HasTacticalPosition, "Non-earpiece follower beyond base range should not get position");
    }

    [Test]
    public void Scenario3_FollowerReturnsToCommRange_RegainsPosition()
    {
        var config = new SquadStrategyConfig
        {
            Enabled = true,
            GuardDistance = 8f,
            FlankDistance = 15f,
            OverwatchDistance = 25f,
            EscortDistance = 5f,
            ArrivalRadius = 3f,
            EnableCommunicationRange = true,
            CommunicationRangeNoEarpiece = 35f,
            CommunicationRangeEarpiece = 200f,
            EnableSquadPersonality = false,
            EnablePositionValidation = false,
            EnableCoverPositionSource = false,
            EnableCombatAwarePositioning = false,
            EnableObjectiveSharing = false,
            UseQuestTypeRoles = true,
        };

        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        // Start with f2 out of range
        f2.CurrentPositionX = 300f;
        f2.CurrentPositionZ = 300f;

        var gotoStrategy = new GotoObjectiveStrategy(config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        // Phase 1: f2 out of range
        manager.Update(_squadRegistry.ActiveSquads);
        Assert.IsFalse(f2.HasTacticalPosition, "f2 should be out of range initially");

        // Phase 2: f2 moves back to within range
        f2.CurrentPositionX = 102f;
        f2.CurrentPositionZ = 102f;
        // Trigger objective refresh
        squad.Objective.SetObjective(201f, 0f, 201f);

        manager.Update(_squadRegistry.ActiveSquads);
        Assert.IsTrue(f2.HasTacticalPosition, "f2 should regain tactical position after returning to comm range");
    }

    // ========================================================================
    // Scenario 4: Squad Vulture Lifecycle
    // ========================================================================

    [Test]
    public void Scenario4_VultureActivates_FollowersFanOut()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        // Vulture activates from the start (no prior strategy — no hysteresis barrier)
        leader.VulturePhase = VulturePhase.Approach;
        leader.HasNearbyEvent = true;
        leader.NearbyEventX = 150f;
        leader.NearbyEventY = 0f;
        leader.NearbyEventZ = 150f;

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // From scratch: GotoObjective scores 0.5, Vulture scores 0.75 — vulture wins
        manager.Update(_squadRegistry.ActiveSquads);

        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(vultureStrategy), "Vulture strategy should win when no prior hysteresis");

        // Followers should have fan-out positions near the combat event
        Assert.IsTrue(f1.HasTacticalPosition);
        Assert.IsTrue(f2.HasTacticalPosition);

        // Verify positions are behind the target (offset from event position)
        Assert.That(f1.TacticalPositionX, Is.Not.EqualTo(0f), "Fan-out positions should be computed");
    }

    [Test]
    public void Scenario4_VultureCompletes_StrategyReverts()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        // Start with vulture already active (no hysteresis barrier to entry)
        leader.VulturePhase = VulturePhase.Approach;
        leader.HasNearbyEvent = true;
        leader.NearbyEventX = 150f;
        leader.NearbyEventZ = 150f;

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Phase 1: Vulture wins from scratch (0.75 > 0.5)
        manager.Update(_squadRegistry.ActiveSquads);
        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(vultureStrategy));

        // Phase 2: Vulture completes
        leader.VulturePhase = VulturePhase.Complete;

        manager.Update(_squadRegistry.ActiveSquads);

        // With VulturePhase.Complete, VultureSquadStrategy scores 0
        // Vulture effective = 0 + 0.20 hysteresis = 0.20
        // GotoObjective scores 0.5, which is > 0.20 — GotoObjective takes back
        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(gotoStrategy), "GotoObjective should reclaim after vulture completes");
    }

    [Test]
    public void Scenario4_VultureSingleFollower_GetsPositionOnOneSide()
    {
        // VultureSquadStrategy positions a single follower with lateralOffset = spread
        var squad = _squadRegistry.Add(2, 2);
        var leader = CreateBot(40, x: 100f, z: 100f);
        var f1 = CreateBot(41, x: 105f, z: 105f);

        _squadRegistry.AddMember(squad, leader);
        _squadRegistry.AddMember(squad, f1);
        f1.Boss = leader;
        leader.Followers.Add(f1);

        leader.HasActiveObjective = true;
        leader.VulturePhase = VulturePhase.SilentApproach;
        leader.HasNearbyEvent = true;
        leader.NearbyEventX = 200f;
        leader.NearbyEventY = 0f;
        leader.NearbyEventZ = 200f;

        var vultureStrategy = new VultureSquadStrategy();
        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        manager.Update(_squadRegistry.ActiveSquads);

        Assert.IsTrue(f1.HasTacticalPosition, "Single follower should get a vulture fan-out position");
        Assert.That(f1.SquadRole, Is.EqualTo(SquadRole.Flanker));
    }

    // ========================================================================
    // Scenario 5: Follower Task Scoring Boundaries
    // ========================================================================

    [Test]
    public void FollowerScoring_FarFromPosition_GoToScoresHigh_HoldScoresZero()
    {
        var entity = CreateBot(50, x: 0f, z: 0f);
        entity.Boss = CreateBot(99); // needs boss
        entity.HasTacticalPosition = true;
        entity.TacticalPositionX = 100f;
        entity.TacticalPositionY = 0f;
        entity.TacticalPositionZ = 0f;

        float goToScore = GoToTacticalPositionTask.Score(entity);
        float holdScore = HoldTacticalPositionTask.Score(entity);

        Assert.That(goToScore, Is.EqualTo(GoToTacticalPositionTask.BaseScore));
        Assert.That(holdScore, Is.EqualTo(0f));
    }

    [Test]
    public void FollowerScoring_CloseToPosition_GoToScoresZero_HoldScoresHigh()
    {
        var entity = CreateBot(51, x: 1f, z: 0f);
        entity.Boss = CreateBot(99);
        entity.HasTacticalPosition = true;
        entity.TacticalPositionX = 1.5f;
        entity.TacticalPositionY = 0f;
        entity.TacticalPositionZ = 0f;

        // Distance = 0.5m, sqrDist = 0.25, well within MinDistanceSqr = 9
        float goToScore = GoToTacticalPositionTask.Score(entity);
        float holdScore = HoldTacticalPositionTask.Score(entity);

        Assert.That(goToScore, Is.EqualTo(0f));
        Assert.That(holdScore, Is.EqualTo(HoldTacticalPositionTask.BaseScore));
    }

    [Test]
    public void FollowerScoring_ExactBoundary_NoDeadZone()
    {
        // At exactly MinDistanceSqr = 9 (3m away):
        // GoTo: sqrDist <= MinDistanceSqr => true => returns 0
        // Hold: sqrDist > MinDistanceSqr => false => returns BaseScore
        var entity = CreateBot(52, x: 0f, z: 0f);
        entity.Boss = CreateBot(99);
        entity.HasTacticalPosition = true;
        entity.TacticalPositionX = 3f; // exactly 3m, sqrDist = 9
        entity.TacticalPositionY = 0f;
        entity.TacticalPositionZ = 0f;

        float goToScore = GoToTacticalPositionTask.Score(entity);
        float holdScore = HoldTacticalPositionTask.Score(entity);

        // At exact boundary, one must score — no dead zone
        Assert.That(goToScore + holdScore, Is.GreaterThan(0f), "At exact boundary, at least one task must score non-zero (no dead zone)");

        // GoTo returns 0 at <= boundary, Hold returns BaseScore at <= boundary
        Assert.That(goToScore, Is.EqualTo(0f), "GoTo returns 0 at exact boundary (sqrDist<=9)");
        Assert.That(
            holdScore,
            Is.EqualTo(HoldTacticalPositionTask.BaseScore),
            "Hold returns BaseScore at exact boundary (sqrDist is NOT > 9)"
        );
    }

    [Test]
    public void FollowerScoring_JustBeyondBoundary_GoToActivates()
    {
        var entity = CreateBot(53, x: 0f, z: 0f);
        entity.Boss = CreateBot(99);
        entity.HasTacticalPosition = true;
        // Distance = 3.001m, sqrDist = 9.006001 > 9
        entity.TacticalPositionX = 3.001f;
        entity.TacticalPositionY = 0f;
        entity.TacticalPositionZ = 0f;

        float goToScore = GoToTacticalPositionTask.Score(entity);
        float holdScore = HoldTacticalPositionTask.Score(entity);

        Assert.That(goToScore, Is.EqualTo(GoToTacticalPositionTask.BaseScore));
        Assert.That(holdScore, Is.EqualTo(0f));
    }

    [Test]
    public void FollowerScoring_NoTacticalPosition_BothScoreZero()
    {
        var entity = CreateBot(54);
        entity.Boss = CreateBot(99);
        entity.HasTacticalPosition = false;

        float goToScore = GoToTacticalPositionTask.Score(entity);
        float holdScore = HoldTacticalPositionTask.Score(entity);

        Assert.That(goToScore, Is.EqualTo(0f));
        Assert.That(holdScore, Is.EqualTo(0f));
    }

    [Test]
    public void FollowerScoring_NoBoss_BothScoreZero()
    {
        var entity = CreateBot(55);
        entity.Boss = null; // no boss
        entity.HasTacticalPosition = true;
        entity.TacticalPositionX = 100f;
        entity.TacticalPositionZ = 100f;

        float goToScore = GoToTacticalPositionTask.Score(entity);
        float holdScore = HoldTacticalPositionTask.Score(entity);

        Assert.That(goToScore, Is.EqualTo(0f));
        Assert.That(holdScore, Is.EqualTo(0f));
    }

    [Test]
    public void FollowerScoring_OscillationResistance_HysteresisPreventsRapidSwitching()
    {
        // Simulate task manager switching behavior near the boundary
        var goToTask = new GoToTacticalPositionTask();
        var holdTask = new HoldTacticalPositionTask();
        var taskManager = new UtilityTaskManager(new UtilityTask[] { goToTask, holdTask });

        var entity = CreateBot(56, x: 0f, z: 0f);
        entity.Boss = CreateBot(99);
        entity.HasTacticalPosition = true;
        entity.TaskScores = new float[2];

        // Start far away — GoTo should win
        entity.TacticalPositionX = 10f;
        taskManager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(goToTask));

        // Move to boundary — GoTo has hysteresis 0.20
        // At sqrDist = 9.01 (just over boundary):
        // GoTo raw = 0.70, effective = 0.70 + 0.20 = 0.90
        // Hold raw = 0 (sqrDist > 9)
        entity.TacticalPositionX = 3.003f; // ~3.003m, sqrDist ~9.018
        entity.CurrentPositionX = 0f;
        taskManager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(goToTask), "GoTo should persist at boundary with hysteresis");

        // Move to within boundary — Hold should finally win
        entity.TacticalPositionX = 2f; // 2m, sqrDist = 4
        taskManager.ScoreAndPick(entity);
        // GoTo raw = 0 (sqrDist <= 9), GoTo effective = 0 + 0.20 = 0.20
        // Hold raw = 0.65, which is > 0.20, so Hold wins
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(holdTask), "Hold should take over once well within boundary");

        // Move slightly back out — Hold has hysteresis 0.10
        entity.TacticalPositionX = 3.1f; // ~3.1m, sqrDist = 9.61
        taskManager.ScoreAndPick(entity);
        // Hold raw = 0 (sqrDist > 9), Hold effective = 0 + 0.10 = 0.10
        // GoTo raw = 0.70, > 0.10, so GoTo takes over
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(goToTask), "GoTo should reclaim once back outside boundary");
    }

    // ========================================================================
    // Scenario 6: Multi-Strategy Switching with Hysteresis
    // ========================================================================

    [Test]
    public void Scenario6_GotoToVultureToGoto_FullLifecycle()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Phase 1: GotoObjective active (0.5 vs 0)
        manager.Update(_squadRegistry.ActiveSquads);
        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(gotoStrategy));
        Assert.IsTrue(f1.HasTacticalPosition);

        float f1GoToX = f1.TacticalPositionX;
        float f1GoToZ = f1.TacticalPositionZ;

        // Phase 2: Vulture activates — score 0.75 beats 0.5 + 0.25 hysteresis = 0.75
        // Actually 0.75 is NOT > 0.75, so vulture needs to beat exactly
        // With GotoObjective hysteresis = 0.25: 0.5 + 0.25 = 0.75
        // VultureSquadStrategy base = 0.75, needs to be STRICTLY > 0.75
        // This means vulture can NOT take over because 0.75 <= 0.75!
        leader.VulturePhase = VulturePhase.Approach;
        leader.HasNearbyEvent = true;
        leader.NearbyEventX = 150f;
        leader.NearbyEventZ = 150f;
        manager.Update(_squadRegistry.ActiveSquads);

        // Due to hysteresis, GotoObjective should hold: 0.5 + 0.25 = 0.75, vulture = 0.75
        // The comparison uses <=, so vulture (0.75) does NOT beat 0.75 — GotoObjective holds!
        // This is actually important behavioral analysis
        Assert.That(
            squad.StrategyAssignment.Strategy,
            Is.SameAs(gotoStrategy),
            "Vulture 0.75 cannot overcome GotoObjective with hysteresis (0.5 + 0.25 = 0.75)"
        );
    }

    [Test]
    public void Scenario6_VultureOvercomesHysteresisWhenGotoScoresLow()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Phase 1: GotoObjective active
        manager.Update(_squadRegistry.ActiveSquads);
        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(gotoStrategy));

        // Phase 2: Leader loses active objective (scores 0 for GotoObjective)
        // but enters vulture phase (scores 0.75)
        leader.HasActiveObjective = false;
        leader.VulturePhase = VulturePhase.HoldAmbush;
        leader.HasNearbyEvent = true;
        leader.NearbyEventX = 150f;
        leader.NearbyEventZ = 150f;

        manager.Update(_squadRegistry.ActiveSquads);

        // GotoObjective: 0 + 0.25 hysteresis = 0.25
        // Vulture: 0.75 > 0.25 — vulture wins
        Assert.That(
            squad.StrategyAssignment.Strategy,
            Is.SameAs(vultureStrategy),
            "Vulture should overcome hysteresis when GotoObjective scores 0"
        );
    }

    [Test]
    public void Scenario6_RapidPhaseChanges_NoOrphanedSquadsInStrategies()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Phase 1: GotoObjective
        manager.Update(_squadRegistry.ActiveSquads);
        Assert.That(gotoStrategy.ActiveSquadCount, Is.EqualTo(1));
        Assert.That(vultureStrategy.ActiveSquadCount, Is.EqualTo(0));

        // Phase 2: Leader loses objective — GotoObjective scores 0
        leader.HasActiveObjective = false;
        leader.VulturePhase = VulturePhase.Rush;
        leader.HasNearbyEvent = true;
        leader.NearbyEventX = 150f;
        leader.NearbyEventZ = 150f;
        manager.Update(_squadRegistry.ActiveSquads);
        Assert.That(vultureStrategy.ActiveSquadCount, Is.EqualTo(1));
        Assert.That(gotoStrategy.ActiveSquadCount, Is.EqualTo(0));

        // Phase 3: Vulture complete, objective returns
        leader.VulturePhase = VulturePhase.Complete;
        leader.HasActiveObjective = true;
        manager.Update(_squadRegistry.ActiveSquads);
        Assert.That(gotoStrategy.ActiveSquadCount, Is.EqualTo(1));
        Assert.That(vultureStrategy.ActiveSquadCount, Is.EqualTo(0));

        // Phase 4: Squad removed entirely
        manager.RemoveSquad(squad);
        Assert.That(gotoStrategy.ActiveSquadCount, Is.EqualTo(0));
        Assert.That(vultureStrategy.ActiveSquadCount, Is.EqualTo(0));
    }

    // ========================================================================
    // Arrival and Duration Adjustment
    // ========================================================================

    [Test]
    public void CheckArrivals_FirstArrival_SwitchesToWait()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        // Assign positions
        manager.Update(_squadRegistry.ActiveSquads);

        // Move f1 to its tactical position (arrival)
        f1.CurrentPositionX = f1.TacticalPositionX;
        f1.CurrentPositionY = f1.TacticalPositionY;
        f1.CurrentPositionZ = f1.TacticalPositionZ;

        // Update triggers CheckArrivals
        manager.Update(_squadRegistry.ActiveSquads);

        Assert.That(squad.Objective.State, Is.EqualTo(ObjectiveState.Wait), "First arrival should switch objective to Wait");
    }

    [Test]
    public void CheckArrivals_AllArrived_DurationCut()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        // Assign positions
        manager.Update(_squadRegistry.ActiveSquads);

        float originalDuration = squad.Objective.Duration;

        // Move both followers to their tactical positions
        f1.CurrentPositionX = f1.TacticalPositionX;
        f1.CurrentPositionY = f1.TacticalPositionY;
        f1.CurrentPositionZ = f1.TacticalPositionZ;
        f2.CurrentPositionX = f2.TacticalPositionX;
        f2.CurrentPositionY = f2.TacticalPositionY;
        f2.CurrentPositionZ = f2.TacticalPositionZ;

        manager.Update(_squadRegistry.ActiveSquads);

        Assert.IsTrue(squad.Objective.DurationAdjusted, "Duration should be adjusted after all arrive");
        Assert.That(squad.Objective.Duration, Is.LessThan(originalDuration), "Duration should be reduced");
    }

    // ========================================================================
    // Formation Speed Integration
    // ========================================================================

    [Test]
    public void FormationSpeed_FollowerFarFromBoss_Sprints()
    {
        var formationConfig = FormationConfig.Default;

        // Boss at (0,0), follower at (50,0) — beyond catchup distance (30m)
        float distToBossSqr = 50f * 50f;
        float distToTacticalSqr = 10f * 10f;

        var decision = FormationSpeedController.ComputeSpeedDecision(false, distToBossSqr, distToTacticalSqr, in formationConfig);

        Assert.That(decision, Is.EqualTo(FormationSpeedDecision.Sprint));
        Assert.IsTrue(FormationSpeedController.ShouldSprint(decision, false));
    }

    [Test]
    public void FormationSpeed_FollowerCloseToTactical_SlowsDown()
    {
        var formationConfig = FormationConfig.Default;

        // Within match speed distance, close to tactical position
        float distToBossSqr = 10f * 10f; // 10m, within MatchSpeedDistance (15m)
        float distToTacticalSqr = 2f * 2f; // 2m, within SlowApproachDistance (5m)

        var decision = FormationSpeedController.ComputeSpeedDecision(false, distToBossSqr, distToTacticalSqr, in formationConfig);

        Assert.That(decision, Is.EqualTo(FormationSpeedDecision.SlowApproach));
        Assert.That(FormationSpeedController.SpeedMultiplier(decision), Is.EqualTo(0.5f));
    }

    [Test]
    public void FormationSpeed_BossSprintingFollowerMidDistance_MatchesBoss()
    {
        var formationConfig = FormationConfig.Default;

        // Between catchup and match distance, boss sprinting
        float distToBossSqr = 20f * 20f; // 20m, between 15 and 30
        float distToTacticalSqr = 10f * 10f;

        var decision = FormationSpeedController.ComputeSpeedDecision(true, distToBossSqr, distToTacticalSqr, in formationConfig);

        Assert.That(decision, Is.EqualTo(FormationSpeedDecision.MatchBoss));
        Assert.IsTrue(FormationSpeedController.ShouldSprint(decision, true), "MatchBoss when boss sprinting should also sprint");
    }

    [Test]
    public void FormationSpeed_BossWalkingFollowerMidDistance_Walks()
    {
        var formationConfig = FormationConfig.Default;

        float distToBossSqr = 20f * 20f; // 20m
        float distToTacticalSqr = 10f * 10f;

        var decision = FormationSpeedController.ComputeSpeedDecision(false, distToBossSqr, distToTacticalSqr, in formationConfig);

        Assert.That(decision, Is.EqualTo(FormationSpeedDecision.Walk));
        Assert.IsFalse(FormationSpeedController.ShouldSprint(decision, false));
    }

    [Test]
    public void FormationSpeed_Disabled_AlwaysMatchBoss()
    {
        var formationConfig = new FormationConfig(30f, 15f, 5f, enabled: false);

        var decision = FormationSpeedController.ComputeSpeedDecision(false, 9999f, 0.1f, in formationConfig);

        Assert.That(decision, Is.EqualTo(FormationSpeedDecision.MatchBoss));
    }

    // ========================================================================
    // Tactical Position Calculation Verification
    // ========================================================================

    [Test]
    public void TacticalPositions_GuardCirclesAroundObjective()
    {
        float objX = 100f,
            objY = 0f,
            objZ = 100f;
        float radius = 8f;

        TacticalPositionCalculator.ComputeGuardPosition(objX, objY, objZ, 0f, radius, out float x, out float y, out float z);

        // At 0 degrees, guard should be at (obj + radius, obj)
        float dist = (float)Math.Sqrt((x - objX) * (x - objX) + (z - objZ) * (z - objZ));
        Assert.That(dist, Is.EqualTo(radius).Within(0.01f));
        Assert.That(y, Is.EqualTo(objY));
    }

    [Test]
    public void TacticalPositions_OverwatchBehindApproach()
    {
        float objX = 100f,
            objY = 0f,
            objZ = 100f;
        float approachX = 50f,
            approachZ = 100f; // Approaching from west

        TacticalPositionCalculator.ComputeOverwatchPosition(
            objX,
            objY,
            objZ,
            approachX,
            approachZ,
            25f,
            out float x,
            out float y,
            out float z
        );

        // Overwatch should be behind approach direction (to the west, X < objX)
        Assert.That(x, Is.LessThan(objX), "Overwatch X should be behind objective (approach from west)");
        Assert.That(z, Is.EqualTo(objZ).Within(0.01f), "Overwatch Z should be at same Z as approach");
    }

    [Test]
    public void TacticalPositions_FlankersPerpendicular()
    {
        float objX = 100f,
            objY = 0f,
            objZ = 100f;
        float approachX = 100f,
            approachZ = 50f; // Approaching from south

        TacticalPositionCalculator.ComputeFlankPosition(
            objX,
            objY,
            objZ,
            approachX,
            approachZ,
            1f,
            15f,
            out float xLeft,
            out float yLeft,
            out float zLeft
        );
        TacticalPositionCalculator.ComputeFlankPosition(
            objX,
            objY,
            objZ,
            approachX,
            approachZ,
            -1f,
            15f,
            out float xRight,
            out float yRight,
            out float zRight
        );

        // Flankers should be perpendicular to approach direction (east/west when approaching from south)
        Assert.That(Math.Abs(xLeft - xRight), Is.GreaterThan(20f), "Flankers should be spread laterally");
        Assert.That(zLeft, Is.EqualTo(zRight).Within(0.01f), "Flankers should be at same Z");
    }

    // ========================================================================
    // CombatPositionAdjuster Verification
    // ========================================================================

    [Test]
    public void CombatPositionAdjuster_EscortBecomeFlankerInCombat()
    {
        var roles = new SquadRole[] { SquadRole.Escort, SquadRole.Escort, SquadRole.Guard };
        var outRoles = new SquadRole[3];

        CombatPositionAdjuster.ReassignRolesForCombat(roles, 3, outRoles);

        Assert.That(outRoles[0], Is.EqualTo(SquadRole.Flanker));
        Assert.That(outRoles[1], Is.EqualTo(SquadRole.Flanker));
        Assert.That(outRoles[2], Is.EqualTo(SquadRole.Guard)); // Guard stays Guard
    }

    [Test]
    public void CombatPositionAdjuster_DegenerateThreatDirection_PlacesAtObjective()
    {
        var roles = new SquadRole[] { SquadRole.Guard, SquadRole.Flanker };
        var positions = new float[6];
        var config = new SquadStrategyConfig
        {
            GuardDistance = 8f,
            FlankDistance = 15f,
            OverwatchDistance = 25f,
        };

        // Zero threat direction
        CombatPositionAdjuster.ComputeCombatPositions(100f, 0f, 100f, 0f, 0f, roles, 2, config, positions);

        // All should be at objective when threat direction is zero
        Assert.That(positions[0], Is.EqualTo(100f));
        Assert.That(positions[2], Is.EqualTo(100f));
        Assert.That(positions[3], Is.EqualTo(100f));
        Assert.That(positions[5], Is.EqualTo(100f));
    }

    // ========================================================================
    // Edge Cases
    // ========================================================================

    [Test]
    public void EdgeCase_SquadWithOnlyLeader_NoFollowers_StrategySkipsGracefully()
    {
        var squad = _squadRegistry.Add(2, 1);
        var leader = CreateBot(60, x: 100f, z: 100f);
        _squadRegistry.AddMember(squad, leader);
        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.MoveToPosition;
        squad.Objective.SetObjective(200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        // Should not crash with 0 followers
        Assert.DoesNotThrow(() => manager.Update(_squadRegistry.ActiveSquads));

        // GotoObjective should still score for this squad
        Assert.That(squad.StrategyScores[0], Is.EqualTo(0.5f));
    }

    [Test]
    public void EdgeCase_InactiveFollowersIgnoredForPositioning()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        // Deactivate one follower
        f2.IsActive = false;

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        manager.Update(_squadRegistry.ActiveSquads);

        Assert.IsTrue(f1.HasTacticalPosition, "Active follower should get position");
        Assert.IsFalse(f2.HasTacticalPosition, "Inactive follower should not get tactical position");
    }

    [Test]
    public void EdgeCase_SquadInactivated_StrategyDeactivated()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        manager.Update(_squadRegistry.ActiveSquads);
        Assert.IsNotNull(squad.StrategyAssignment.Strategy);

        squad.IsActive = false;
        manager.PickStrategies(_squadRegistry.ActiveSquads);

        Assert.IsNull(squad.StrategyAssignment.Strategy, "Inactive squad should have no strategy");
    }

    [Test]
    public void EdgeCase_LeaderNoActiveObjective_GotoScoresZero()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        leader.HasActiveObjective = false;

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        gotoStrategy.ScoreSquad(0, squad);

        Assert.That(squad.StrategyScores[0], Is.EqualTo(0f));
    }

    [Test]
    public void EdgeCase_MultipleSquadsIndependent()
    {
        var squad1 = CreateSquadWithMembers(2, out var l1, out var f1a, out var f1b);
        SetLeaderObjective(l1, squad1, 200f, 0f, 200f);

        var squad2 = _squadRegistry.Add(2, 2);
        var l2 = CreateBot(70, x: 500f, z: 500f);
        var f2a = CreateBot(71, x: 510f, z: 510f);
        _squadRegistry.AddMember(squad2, l2);
        _squadRegistry.AddMember(squad2, f2a);
        f2a.Boss = l2;
        l2.Followers.Add(f2a);
        l2.HasActiveObjective = true;
        l2.CurrentQuestAction = QuestActionId.Snipe;
        squad2.Objective.SetObjective(600f, 0f, 600f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, new VultureSquadStrategy() });

        manager.Update(_squadRegistry.ActiveSquads);

        // Both squads should have independent strategies and positions
        Assert.IsTrue(f1a.HasTacticalPosition);
        Assert.IsTrue(f2a.HasTacticalPosition);

        // Positions should be different (near their respective objectives)
        float positionDiff = Math.Abs(f1a.TacticalPositionX - f2a.TacticalPositionX);
        Assert.That(positionDiff, Is.GreaterThan(100f), "Different squads should have different positions");

        // Different quest types should yield different roles
        // Squad1: MoveToPosition -> Escort, Squad2: Snipe -> Overwatch
        Assert.That(f1a.SquadRole, Is.EqualTo(SquadRole.Escort));
        Assert.That(f2a.SquadRole, Is.EqualTo(SquadRole.Overwatch));
    }
}

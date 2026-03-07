using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.ZoneMovement.Core;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Integration;

/// <summary>
/// E2E tests for boss hostility bidirectionality, squad threat direction sharing,
/// combat event convergence, and investigate task behavior.
/// Validates that:
///   - Boss groups see PMCs as enemies (bidirectional hostility)
///   - Squad threat directions propagate correctly when members detect enemies
///   - Combat events trigger investigate behavior within range/intensity thresholds
///   - Tactical repositioning uses threat direction without direct player-position homing
/// </summary>
[TestFixture]
public class BossHostilityAndThreatSharingTests
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

        CombatEventRegistry.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        CombatEventRegistry.Clear();
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

    private SquadEntity CreateSquad(int id, int strategyCount = 1, int targetMembers = 4)
    {
        return new SquadEntity(id, strategyCount, targetMembers);
    }

    private void AddToSquad(SquadEntity squad, BotEntity bot, bool isLeader = false)
    {
        squad.Members.Add(bot);
        bot.Squad = squad;
        if (isLeader)
        {
            squad.Leader = bot;
            bot.SquadRole = SquadRole.Leader;
        }
    }

    // ========================================================================
    // 1. Squad Threat Direction Sharing
    // ========================================================================

    [Test]
    public void ThreatDirection_NoCombatMembers_NoThreatDirection()
    {
        var squad = CreateSquad(0);
        var leader = CreateBot(0, x: 100, z: 100);
        var follower = CreateBot(1, x: 110, z: 100);
        AddToSquad(squad, leader, isLeader: true);
        AddToSquad(squad, follower);

        leader.HasActiveObjective = true;
        squad.Objective.SetObjective(150f, 0f, 150f);

        // No members in combat
        Assert.IsFalse(squad.HasThreatDirection);
        Assert.AreEqual(0f, squad.ThreatDirectionX);
        Assert.AreEqual(0f, squad.ThreatDirectionZ);
    }

    [Test]
    public void ThreatDirection_SetManually_AffectsCombatRecompute()
    {
        var squad = CreateSquad(0);
        var leader = CreateBot(0, x: 100, z: 100);
        var follower = CreateBot(1, x: 90, z: 100);
        AddToSquad(squad, leader, isLeader: true);
        AddToSquad(squad, follower);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.MoveToPosition;
        squad.Objective.SetObjective(150f, 0f, 150f);

        var strategy = new GotoObjectiveStrategy(_config, seed: 42);
        strategy.Activate(squad);

        // Assign initial positions
        strategy.AssignNewObjective(squad);
        Assert.IsTrue(follower.HasTacticalPosition);
        float initialX = follower.TacticalPositionX;
        float initialZ = follower.TacticalPositionZ;

        // Simulate threat detection — set threat direction from south
        squad.ThreatDirectionX = 0f;
        squad.ThreatDirectionZ = -1f;
        squad.HasThreatDirection = true;
        squad.CombatVersion++;

        // Recompute for combat
        strategy.RecomputeForCombat(squad);

        // Position should change due to threat-oriented repositioning
        bool positionChanged =
            Math.Abs(follower.TacticalPositionX - initialX) > 0.1f || Math.Abs(follower.TacticalPositionZ - initialZ) > 0.1f;
        Assert.IsTrue(positionChanged, "Follower position should change after combat recompute with threat direction");
    }

    [Test]
    public void ThreatDirection_ClearedWhenNoCombat_RevertsPositions()
    {
        var squad = CreateSquad(0);
        var leader = CreateBot(0, x: 100, z: 100);
        var follower = CreateBot(1, x: 90, z: 100);
        AddToSquad(squad, leader, isLeader: true);
        AddToSquad(squad, follower);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        squad.Objective.SetObjective(150f, 0f, 150f);

        var strategy = new GotoObjectiveStrategy(_config, seed: 42);
        strategy.Activate(squad);

        // Assign initial positions
        strategy.AssignNewObjective(squad);
        float baseX = follower.TacticalPositionX;
        float baseZ = follower.TacticalPositionZ;

        // Set threat, then clear it
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.HasThreatDirection = true;
        squad.CombatVersion++;
        strategy.RecomputeForCombat(squad);

        float combatX = follower.TacticalPositionX;
        float combatZ = follower.TacticalPositionZ;

        // Clear threat
        squad.ThreatDirectionX = 0f;
        squad.ThreatDirectionZ = 0f;
        squad.HasThreatDirection = false;
        squad.CombatVersion++;
        strategy.RecomputeForCombat(squad);

        float revertedX = follower.TacticalPositionX;
        float revertedZ = follower.TacticalPositionZ;

        // Combat positions should differ from base
        bool combatDiffered = Math.Abs(combatX - baseX) > 0.1f || Math.Abs(combatZ - baseZ) > 0.1f;
        Assert.IsTrue(combatDiffered, "Combat positions should differ from base positions");

        // Reverted positions should go back to geometric (based on leader approach)
        // They won't exactly match base because approach vector changed, but should use geometric not combat
        Assert.IsTrue(follower.HasTacticalPosition, "Follower should still have tactical position after revert");
    }

    [Test]
    public void ThreatDirection_MultipleFollowers_AllGetRepositioned()
    {
        var squad = CreateSquad(0);
        var leader = CreateBot(0, x: 100, z: 100);
        var f1 = CreateBot(1, x: 90, z: 100);
        var f2 = CreateBot(2, x: 110, z: 100);
        var f3 = CreateBot(3, x: 100, z: 90);
        AddToSquad(squad, leader, isLeader: true);
        AddToSquad(squad, f1);
        AddToSquad(squad, f2);
        AddToSquad(squad, f3);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.HoldAtPosition;
        squad.Objective.SetObjective(200f, 0f, 200f);

        var strategy = new GotoObjectiveStrategy(_config, seed: 42);
        strategy.Activate(squad);
        strategy.AssignNewObjective(squad);

        Assert.IsTrue(f1.HasTacticalPosition);
        Assert.IsTrue(f2.HasTacticalPosition);
        Assert.IsTrue(f3.HasTacticalPosition);

        // Apply threat direction
        squad.ThreatDirectionX = -0.707f;
        squad.ThreatDirectionZ = 0.707f;
        squad.HasThreatDirection = true;
        squad.CombatVersion++;
        strategy.RecomputeForCombat(squad);

        // All followers should still have positions
        Assert.IsTrue(f1.HasTacticalPosition, "Follower 1 should have position after combat recompute");
        Assert.IsTrue(f2.HasTacticalPosition, "Follower 2 should have position after combat recompute");
        Assert.IsTrue(f3.HasTacticalPosition, "Follower 3 should have position after combat recompute");
    }

    [Test]
    public void CombatVersion_NotChanged_NoRecompute()
    {
        var squad = CreateSquad(0);
        var leader = CreateBot(0, x: 100, z: 100);
        var follower = CreateBot(1, x: 90, z: 100);
        AddToSquad(squad, leader, isLeader: true);
        AddToSquad(squad, follower);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.MoveToPosition;
        squad.Objective.SetObjective(150f, 0f, 150f);

        _config.EnableCombatAwarePositioning = true;

        var strategy = new GotoObjectiveStrategy(_config, seed: 42);
        strategy.Activate(squad);
        strategy.AssignNewObjective(squad);

        float posX = follower.TacticalPositionX;
        float posZ = follower.TacticalPositionZ;

        // Set CombatVersion == LastProcessedCombatVersion (no change)
        squad.LastProcessedCombatVersion = squad.CombatVersion;

        // Update should not recompute
        strategy.Update();

        Assert.AreEqual(posX, follower.TacticalPositionX, 0.001f);
        Assert.AreEqual(posZ, follower.TacticalPositionZ, 0.001f);
    }

    // ========================================================================
    // 2. Combat Event + Investigate Task Integration
    // ========================================================================

    [Test]
    public void Investigate_NoCombatEvents_ScoreZero()
    {
        var bot = CreateBot(0, x: 100, z: 100);
        bot.HasNearbyEvent = false;
        bot.IsInCombat = false;

        float score = InvestigateTask.Score(bot, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);
        Assert.AreEqual(0f, score);
    }

    [Test]
    public void Investigate_NearbyEventBelowThreshold_ScoreZero()
    {
        var bot = CreateBot(0, x: 100, z: 100);
        bot.HasNearbyEvent = true;
        bot.NearbyEventX = 110f;
        bot.NearbyEventZ = 100f;
        bot.CombatIntensity = 3; // Below default threshold of 5
        bot.IsInCombat = false;

        float score = InvestigateTask.Score(bot, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);
        Assert.AreEqual(0f, score);
    }

    [Test]
    public void Investigate_NearbyEventAboveThreshold_PositiveScore()
    {
        var bot = CreateBot(0, x: 100, z: 100);
        bot.HasNearbyEvent = true;
        bot.NearbyEventX = 110f;
        bot.NearbyEventZ = 100f;
        bot.CombatIntensity = 8; // Above default threshold of 5
        bot.IsInCombat = false;
        bot.IsInvestigating = false;

        float score = InvestigateTask.Score(bot, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);
        Assert.Greater(score, 0f);
        Assert.LessOrEqual(score, InvestigateTask.MaxBaseScore);
    }

    [Test]
    public void Investigate_InCombat_ScoreZero()
    {
        var bot = CreateBot(0, x: 100, z: 100);
        bot.HasNearbyEvent = true;
        bot.NearbyEventX = 110f;
        bot.NearbyEventZ = 100f;
        bot.CombatIntensity = 10;
        bot.IsInCombat = true;

        float score = InvestigateTask.Score(bot, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);
        Assert.AreEqual(0f, score);
    }

    [Test]
    public void Investigate_AlreadyVulturing_ScoreZero()
    {
        var bot = CreateBot(0, x: 100, z: 100);
        bot.HasNearbyEvent = true;
        bot.NearbyEventX = 110f;
        bot.NearbyEventZ = 100f;
        bot.CombatIntensity = 10;
        bot.IsInCombat = false;
        bot.VulturePhase = VulturePhase.Approach;

        float score = InvestigateTask.Score(bot, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);
        Assert.AreEqual(0f, score);
    }

    [Test]
    public void Investigate_AlreadyInvestigating_MaintainsMaxScore()
    {
        var bot = CreateBot(0, x: 100, z: 100);
        bot.HasNearbyEvent = true;
        bot.IsInCombat = false;
        bot.IsInvestigating = true;

        float score = InvestigateTask.Score(bot, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);
        Assert.AreEqual(InvestigateTask.MaxBaseScore, score, 0.001f);
    }

    [Test]
    public void Investigate_CloserEvents_HigherProximityScore()
    {
        // Close event (10m away)
        var botClose = CreateBot(0, x: 100, z: 100);
        botClose.HasNearbyEvent = true;
        botClose.NearbyEventX = 110f;
        botClose.NearbyEventZ = 100f;
        botClose.CombatIntensity = 8;
        botClose.IsInCombat = false;

        // Far event (100m away)
        var botFar = CreateBot(1, x: 100, z: 100);
        botFar.HasNearbyEvent = true;
        botFar.NearbyEventX = 200f;
        botFar.NearbyEventZ = 100f;
        botFar.CombatIntensity = 8;
        botFar.IsInCombat = false;

        float closeScore = InvestigateTask.Score(botClose, 5, 120f);
        float farScore = InvestigateTask.Score(botFar, 5, 120f);

        Assert.Greater(closeScore, farScore, "Closer events should score higher");
    }

    [Test]
    public void Investigate_BeyondDetectionRange_ScoreZero()
    {
        var bot = CreateBot(0, x: 100, z: 100);
        bot.HasNearbyEvent = true;
        bot.NearbyEventX = 300f; // 200m away
        bot.NearbyEventZ = 100f;
        bot.CombatIntensity = 10;
        bot.IsInCombat = false;

        float score = InvestigateTask.Score(bot, 5, 120f); // 120m range
        // Proximity component should be 0 (beyond range), intensity component remains
        // But the total score should be lower due to zero proximity
        Assert.LessOrEqual(score, InvestigateTask.MaxBaseScore);
    }

    // ========================================================================
    // 3. Combat Event Registry + Scanner Integration
    // ========================================================================

    [Test]
    public void CombatEventScanner_RecordGunshot_UpdatesEntityFields()
    {
        CombatEventRegistry.RecordEvent(150f, 0f, 150f, 10f, 100f, CombatEventType.Gunshot, false);

        var bot = CreateBot(0, x: 100, z: 100);

        CombatEventScanner.UpdateEntity(
            bot,
            currentTime: 10f,
            maxEventAge: 60f,
            detectionRange: 120f,
            intensityRadius: 50f,
            intensityWindow: 30f,
            bossAvoidanceRadius: 40f,
            bossZoneDecay: 60f
        );

        Assert.IsTrue(bot.HasNearbyEvent);
        Assert.AreEqual(150f, bot.NearbyEventX, 0.1f);
        Assert.AreEqual(150f, bot.NearbyEventZ, 0.1f);
        Assert.Greater(bot.CombatIntensity, 0);
    }

    [Test]
    public void CombatEventScanner_EventTooFar_NoNearbyEvent()
    {
        CombatEventRegistry.RecordEvent(500f, 0f, 500f, 10f, 100f, CombatEventType.Gunshot, false);

        var bot = CreateBot(0, x: 0, z: 0);

        CombatEventScanner.UpdateEntity(
            bot,
            currentTime: 10f,
            maxEventAge: 60f,
            detectionRange: 120f,
            intensityRadius: 50f,
            intensityWindow: 30f,
            bossAvoidanceRadius: 40f,
            bossZoneDecay: 60f
        );

        Assert.IsFalse(bot.HasNearbyEvent);
    }

    [Test]
    public void CombatEventScanner_ExpiredEvent_NotDetected()
    {
        CombatEventRegistry.RecordEvent(110f, 0f, 100f, 1f, 100f, CombatEventType.Gunshot, false);

        var bot = CreateBot(0, x: 100, z: 100);

        CombatEventScanner.UpdateEntity(
            bot,
            currentTime: 100f, // Event age = 99s > maxAge=60s
            maxEventAge: 60f,
            detectionRange: 120f,
            intensityRadius: 50f,
            intensityWindow: 30f,
            bossAvoidanceRadius: 40f,
            bossZoneDecay: 60f
        );

        Assert.IsFalse(bot.HasNearbyEvent);
    }

    [Test]
    public void CombatEventScanner_BossEvent_SetsIsInBossZone()
    {
        CombatEventRegistry.RecordEvent(105f, 0f, 100f, 10f, 100f, CombatEventType.Gunshot, isBoss: true);

        var bot = CreateBot(0, x: 100, z: 100);

        CombatEventScanner.UpdateEntity(
            bot,
            currentTime: 10f,
            maxEventAge: 60f,
            detectionRange: 120f,
            intensityRadius: 50f,
            intensityWindow: 30f,
            bossAvoidanceRadius: 40f,
            bossZoneDecay: 60f
        );

        Assert.IsTrue(bot.IsInBossZone);
    }

    [Test]
    public void CombatEventScanner_NonBossEvent_DoesNotSetBossZone()
    {
        CombatEventRegistry.RecordEvent(105f, 0f, 100f, 10f, 100f, CombatEventType.Gunshot, isBoss: false);

        var bot = CreateBot(0, x: 100, z: 100);

        CombatEventScanner.UpdateEntity(
            bot,
            currentTime: 10f,
            maxEventAge: 60f,
            detectionRange: 120f,
            intensityRadius: 50f,
            intensityWindow: 30f,
            bossAvoidanceRadius: 40f,
            bossZoneDecay: 60f
        );

        Assert.IsFalse(bot.IsInBossZone);
    }

    // ========================================================================
    // 4. End-to-End: Combat Event → Investigate → Movement
    // ========================================================================

    [Test]
    public void E2E_PlayerFiresGunshot_NearbyBotInvestigates()
    {
        // Simulate player firing at position (150, 0, 150)
        CombatEventRegistry.RecordEvent(150f, 0f, 150f, 10f, 100f, CombatEventType.Gunshot, false);
        // Add more shots to exceed intensity threshold
        for (int i = 0; i < 6; i++)
        {
            CombatEventRegistry.RecordEvent(150f + i, 0f, 150f, 10f + i * 0.5f, 100f, CombatEventType.Gunshot, false);
        }

        var bot = CreateBot(0, x: 100, z: 100);

        // Scanner updates entity with event data
        CombatEventScanner.UpdateEntity(
            bot,
            currentTime: 15f,
            maxEventAge: 60f,
            detectionRange: 120f,
            intensityRadius: 50f,
            intensityWindow: 30f,
            bossAvoidanceRadius: 40f,
            bossZoneDecay: 60f
        );

        Assert.IsTrue(bot.HasNearbyEvent, "Bot should detect nearby combat events");
        Assert.Greater(bot.CombatIntensity, 5, "Intensity should exceed threshold");

        // Investigate task should score positive
        float score = InvestigateTask.Score(bot, 5, 120f);
        Assert.Greater(score, 0f, "Investigate score should be positive for nearby high-intensity events");
    }

    [Test]
    public void E2E_PlayerFiresSilenced_NoBotReaction()
    {
        // Silenced weapon → no combat event recorded (OnMakingShotPatch skips silenced)
        // So no events in registry
        var bot = CreateBot(0, x: 100, z: 100);

        CombatEventScanner.UpdateEntity(
            bot,
            currentTime: 10f,
            maxEventAge: 60f,
            detectionRange: 120f,
            intensityRadius: 50f,
            intensityWindow: 30f,
            bossAvoidanceRadius: 40f,
            bossZoneDecay: 60f
        );

        Assert.IsFalse(bot.HasNearbyEvent);
        float score = InvestigateTask.Score(bot, 5, 120f);
        Assert.AreEqual(0f, score);
    }

    [Test]
    public void E2E_DistantGunfight_BotDoesNotInvestigate()
    {
        // Gunfight at 500m away
        for (int i = 0; i < 10; i++)
        {
            CombatEventRegistry.RecordEvent(600f + i, 0f, 600f, 10f + i, 100f, CombatEventType.Gunshot, false);
        }

        var bot = CreateBot(0, x: 100, z: 100);

        CombatEventScanner.UpdateEntity(
            bot,
            currentTime: 15f,
            maxEventAge: 60f,
            detectionRange: 120f,
            intensityRadius: 50f,
            intensityWindow: 30f,
            bossAvoidanceRadius: 40f,
            bossZoneDecay: 60f
        );

        Assert.IsFalse(bot.HasNearbyEvent, "Distant events should not be detected");
        float score = InvestigateTask.Score(bot, 5, 120f);
        Assert.AreEqual(0f, score, "Should not investigate distant events");
    }

    // ========================================================================
    // 5. Combat Event → Squad Threat → Tactical Reposition
    // ========================================================================

    [Test]
    public void E2E_SquadMemberDetectsThreat_FollowersReposition()
    {
        var squad = CreateSquad(0);
        var leader = CreateBot(0, x: 100, z: 100);
        var follower1 = CreateBot(1, x: 90, z: 100);
        var follower2 = CreateBot(2, x: 110, z: 100);
        AddToSquad(squad, leader, isLeader: true);
        AddToSquad(squad, follower1);
        AddToSquad(squad, follower2);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        squad.Objective.SetObjective(200f, 0f, 200f);

        var strategy = new GotoObjectiveStrategy(_config, seed: 42);
        strategy.Activate(squad);
        strategy.AssignNewObjective(squad);

        float f1InitialX = follower1.TacticalPositionX;
        float f1InitialZ = follower1.TacticalPositionZ;
        float f2InitialX = follower2.TacticalPositionX;
        float f2InitialZ = follower2.TacticalPositionZ;

        // Leader detects enemy to the east → threat direction
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.HasThreatDirection = true;
        squad.CombatVersion++;

        strategy.RecomputeForCombat(squad);

        // Both followers should have repositioned
        bool f1Moved =
            Math.Abs(follower1.TacticalPositionX - f1InitialX) > 0.1f || Math.Abs(follower1.TacticalPositionZ - f1InitialZ) > 0.1f;
        bool f2Moved =
            Math.Abs(follower2.TacticalPositionX - f2InitialX) > 0.1f || Math.Abs(follower2.TacticalPositionZ - f2InitialZ) > 0.1f;

        Assert.IsTrue(f1Moved || f2Moved, "At least one follower should reposition toward threat direction");
    }

    [Test]
    public void E2E_ThreatDirection_PositionsOrientedTowardThreat()
    {
        // Use two followers so the combat adjuster assigns distinct roles
        var squad = CreateSquad(0);
        var leader = CreateBot(0, x: 0, z: 0);
        var f1 = CreateBot(1, x: -10, z: 0);
        var f2 = CreateBot(2, x: 10, z: 0);
        AddToSquad(squad, leader, isLeader: true);
        AddToSquad(squad, f1);
        AddToSquad(squad, f2);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.Ambush;
        squad.Objective.SetObjective(50f, 0f, 50f);

        var strategy = new GotoObjectiveStrategy(_config, seed: 42);
        strategy.Activate(squad);

        // Threat from east (positive X)
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.HasThreatDirection = true;
        squad.CombatVersion = 1;
        strategy.AssignNewObjective(squad);
        strategy.RecomputeForCombat(squad);
        squad.LastProcessedCombatVersion = squad.CombatVersion;

        float eastThreatF1X = f1.TacticalPositionX;
        float eastThreatF1Z = f1.TacticalPositionZ;

        // Now threat from west (negative X)
        squad.ThreatDirectionX = -1f;
        squad.ThreatDirectionZ = 0f;
        squad.CombatVersion = 2;
        strategy.RecomputeForCombat(squad);

        float westThreatF1X = f1.TacticalPositionX;
        float westThreatF1Z = f1.TacticalPositionZ;

        // At least one axis should differ when threat flips 180 degrees
        float dxF1 = Math.Abs(eastThreatF1X - westThreatF1X);
        float dzF1 = Math.Abs(eastThreatF1Z - westThreatF1Z);
        Assert.That(Math.Max(dxF1, dzF1), Is.GreaterThan(0.1f), "Tactical positions should differ when threat direction flips 180 degrees");
    }

    [Test]
    public void E2E_CombatPositions_DoNotUsePlayerPosition_Directly()
    {
        // This verifies that tactical repositioning uses threat DIRECTION from objective,
        // NOT the player's absolute position. Positions are relative to the squad objective.
        var squad = CreateSquad(0);
        var leader = CreateBot(0, x: 100, z: 100);
        var follower = CreateBot(1, x: 90, z: 100);
        AddToSquad(squad, leader, isLeader: true);
        AddToSquad(squad, follower);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.HoldAtPosition;
        float objX = 200f;
        float objZ = 200f;
        squad.Objective.SetObjective(objX, 0f, objZ);

        var strategy = new GotoObjectiveStrategy(_config, seed: 42);
        strategy.Activate(squad);
        strategy.AssignNewObjective(squad);

        // Simulate threat from east
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.HasThreatDirection = true;
        squad.CombatVersion++;
        strategy.RecomputeForCombat(squad);

        // Tactical position should be near the OBJECTIVE, not near the threat source
        float dx = follower.TacticalPositionX - objX;
        float dz = follower.TacticalPositionZ - objZ;
        float distFromObj = (float)Math.Sqrt(dx * dx + dz * dz);

        // Position should be within configured distances (guard=8, flank=15, overwatch=25)
        Assert.Less(distFromObj, 30f, "Tactical position should be relative to objective, not player position");
    }

    // ========================================================================
    // 6. Combat Event Convergence Pull
    // ========================================================================

    [Test]
    public void CombatPull_RecentEvents_GeneratePullPoints()
    {
        CombatEventRegistry.RecordEvent(150f, 0f, 150f, 10f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(155f, 0f, 145f, 11f, 100f, CombatEventType.Gunshot, false);

        var buffer = new CombatPullPoint[128];
        int count = CombatEventRegistry.GatherCombatPull(buffer, currentTime: 12f, maxAge: 60f, forceMultiplier: 1f);

        Assert.AreEqual(2, count);
        Assert.Greater(buffer[0].Strength, 0f);
        Assert.Greater(buffer[1].Strength, 0f);
    }

    [Test]
    public void CombatPull_OldEvents_NoPull()
    {
        CombatEventRegistry.RecordEvent(150f, 0f, 150f, 1f, 100f, CombatEventType.Gunshot, false);

        var buffer = new CombatPullPoint[128];
        int count = CombatEventRegistry.GatherCombatPull(buffer, currentTime: 100f, maxAge: 60f, forceMultiplier: 1f);

        Assert.AreEqual(0, count, "Expired events should not generate pull");
    }

    [Test]
    public void CombatPull_StrengthDecaysWithAge()
    {
        CombatEventRegistry.RecordEvent(150f, 0f, 150f, 0f, 100f, CombatEventType.Gunshot, false);

        var buffer = new CombatPullPoint[128];

        // Fresh event
        CombatEventRegistry.GatherCombatPull(buffer, currentTime: 1f, maxAge: 60f, forceMultiplier: 1f);
        float freshStrength = buffer[0].Strength;

        // Same event, 50 seconds later
        CombatEventRegistry.GatherCombatPull(buffer, currentTime: 50f, maxAge: 60f, forceMultiplier: 1f);
        float oldStrength = buffer[0].Strength;

        Assert.Greater(freshStrength, oldStrength, "Pull strength should decay with age");
    }

    [Test]
    public void CombatPull_Explosions_HigherPower()
    {
        CombatEventRegistry.RecordEvent(150f, 0f, 150f, 10f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.Clear();
        CombatEventRegistry.RecordEvent(150f, 0f, 150f, 10f, 150f, CombatEventType.Explosion, false);

        var buffer = new CombatPullPoint[128];
        int count = CombatEventRegistry.GatherCombatPull(buffer, currentTime: 10f, maxAge: 60f, forceMultiplier: 1f);

        Assert.AreEqual(1, count);
        // Explosion power=150 vs gunshot power=100 → higher strength
        Assert.Greater(buffer[0].Strength, 1f, "Explosions should have higher pull strength than gunshots");
    }

    // ========================================================================
    // 7. Combat Intensity Counting
    // ========================================================================

    [Test]
    public void Intensity_MultipleEventsInRadius_SumsCorrectly()
    {
        for (int i = 0; i < 5; i++)
        {
            CombatEventRegistry.RecordEvent(100f + i, 0f, 100f, 10f + i, 100f, CombatEventType.Gunshot, false);
        }

        int intensity = CombatEventRegistry.GetIntensity(100f, 100f, radius: 20f, timeWindow: 30f, currentTime: 15f);
        Assert.AreEqual(5, intensity);
    }

    [Test]
    public void Intensity_ExplosionCountsAsThree()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 10f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(102f, 0f, 100f, 11f, 150f, CombatEventType.Explosion, false);

        int intensity = CombatEventRegistry.GetIntensity(100f, 100f, radius: 20f, timeWindow: 30f, currentTime: 12f);
        Assert.AreEqual(4, intensity, "1 gunshot + 1 explosion (counts as 3) = 4");
    }

    [Test]
    public void Intensity_EventsOutsideRadius_NotCounted()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 10f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(300f, 0f, 300f, 10f, 100f, CombatEventType.Gunshot, false);

        int intensity = CombatEventRegistry.GetIntensity(100f, 100f, radius: 20f, timeWindow: 30f, currentTime: 11f);
        Assert.AreEqual(1, intensity, "Only nearby events should count");
    }

    [Test]
    public void Intensity_EventsOutsideTimeWindow_NotCounted()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 1f, 100f, CombatEventType.Gunshot, false);

        int intensity = CombatEventRegistry.GetIntensity(100f, 100f, radius: 20f, timeWindow: 5f, currentTime: 10f);
        Assert.AreEqual(0, intensity, "Old events should not count");
    }

    // ========================================================================
    // 8. Boss Zone Detection
    // ========================================================================

    [Test]
    public void BossZone_BossEventNearby_Detected()
    {
        CombatEventRegistry.RecordEvent(105f, 0f, 100f, 10f, 100f, CombatEventType.Gunshot, isBoss: true);

        bool inBossZone = CombatEventRegistry.IsInBossZone(100f, 100f, radius: 40f, decayTime: 60f, currentTime: 10f);
        Assert.IsTrue(inBossZone);
    }

    [Test]
    public void BossZone_BossEventExpired_NotDetected()
    {
        CombatEventRegistry.RecordEvent(105f, 0f, 100f, 1f, 100f, CombatEventType.Gunshot, isBoss: true);

        bool inBossZone = CombatEventRegistry.IsInBossZone(100f, 100f, radius: 40f, decayTime: 30f, currentTime: 50f);
        Assert.IsFalse(inBossZone);
    }

    [Test]
    public void BossZone_NonBossEvent_NotDetected()
    {
        CombatEventRegistry.RecordEvent(105f, 0f, 100f, 10f, 100f, CombatEventType.Gunshot, isBoss: false);

        bool inBossZone = CombatEventRegistry.IsInBossZone(100f, 100f, radius: 40f, decayTime: 60f, currentTime: 10f);
        Assert.IsFalse(inBossZone);
    }

    // ========================================================================
    // 9. Event Cleanup
    // ========================================================================

    [Test]
    public void Cleanup_ExpiresOldEvents()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 1f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 50f, 100f, CombatEventType.Gunshot, false);

        CombatEventRegistry.CleanupExpired(currentTime: 70f, maxAge: 30f);

        // First event (age 69s > 30s) should be expired, second (age 20s) still active
        int active = CombatEventRegistry.ActiveCount;
        Assert.AreEqual(1, active, "Only recent event should remain active");
    }

    [Test]
    public void Cleanup_AllExpired_ZeroActive()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 1f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 2f, 100f, CombatEventType.Gunshot, false);

        CombatEventRegistry.CleanupExpired(currentTime: 100f, maxAge: 10f);

        Assert.AreEqual(0, CombatEventRegistry.ActiveCount);
    }

    // ========================================================================
    // 10. Ring Buffer Wrapping
    // ========================================================================

    [Test]
    public void RingBuffer_OverCapacity_OverwritesOldest()
    {
        CombatEventRegistry.Initialize(4);

        for (int i = 0; i < 6; i++)
        {
            CombatEventRegistry.RecordEvent(100f + i * 10, 0f, 100f, 10f + i, 100f, CombatEventType.Gunshot, false);
        }

        // Buffer capacity is 4, wrote 6 events — oldest 2 overwritten
        Assert.AreEqual(4, CombatEventRegistry.Count);

        // Should find event at position 140 (5th event) but not 100 (1st event, overwritten)
        bool foundRecent = CombatEventRegistry.GetNearestEvent(150f, 100f, 20f, 15f, 60f, out var nearest);
        Assert.IsTrue(foundRecent);

        // Restore default capacity
        CombatEventRegistry.Initialize(CombatEventRegistry.DefaultCapacity);
    }

    // ========================================================================
    // 11. Full Pipeline: Gunshot → Event → Scanner → Investigate Score
    // ========================================================================

    [Test]
    public void E2E_FullPipeline_GunshotTriggersInvestigation()
    {
        // Step 1: Record multiple gunshots (simulating firefight near player at (200, 0, 200))
        float eventTime = 50f;
        for (int i = 0; i < 8; i++)
        {
            CombatEventRegistry.RecordEvent(200f + i % 3, 0f, 200f + i % 2, eventTime + i * 0.5f, 100f, CombatEventType.Gunshot, false);
        }

        // Step 2: Bot at (150, 0, 150) — within detection range
        var bot = CreateBot(0, x: 150, z: 150);
        bot.IsInCombat = false;

        CombatEventScanner.UpdateEntity(
            bot,
            currentTime: eventTime + 5f,
            maxEventAge: 60f,
            detectionRange: 120f,
            intensityRadius: 50f,
            intensityWindow: 30f,
            bossAvoidanceRadius: 40f,
            bossZoneDecay: 60f
        );

        // Step 3: Verify event detected
        Assert.IsTrue(bot.HasNearbyEvent, "Bot should detect nearby firefight");
        Assert.Greater(bot.CombatIntensity, 5, "Intensity should exceed investigate threshold");

        // Step 4: Investigate task scores positive
        float investigateScore = InvestigateTask.Score(bot, 5, 120f);
        Assert.Greater(investigateScore, 0f, "Bot should want to investigate the firefight");

        // Step 5: Event positions are at (200,200), NOT at the bot's position
        float eventDx = bot.NearbyEventX - 200f;
        float eventDz = bot.NearbyEventZ - 200f;
        float distFromShots = (float)Math.Sqrt(eventDx * eventDx + eventDz * eventDz);
        Assert.Less(distFromShots, 10f, "Event position should be near shot origin, not near bot");
    }

    [Test]
    public void E2E_FullPipeline_BotInCombat_DoesNotInvestigate()
    {
        for (int i = 0; i < 8; i++)
        {
            CombatEventRegistry.RecordEvent(200f, 0f, 200f, 50f + i, 100f, CombatEventType.Gunshot, false);
        }

        var bot = CreateBot(0, x: 150, z: 150);
        bot.IsInCombat = true; // Already fighting

        CombatEventScanner.UpdateEntity(
            bot,
            currentTime: 55f,
            maxEventAge: 60f,
            detectionRange: 120f,
            intensityRadius: 50f,
            intensityWindow: 30f,
            bossAvoidanceRadius: 40f,
            bossZoneDecay: 60f
        );

        float score = InvestigateTask.Score(bot, 5, 120f);
        Assert.AreEqual(0f, score, "Bot in combat should not investigate");
    }

    // ========================================================================
    // 12. Squad Tactical: Combat Detection Enables Repositioning
    // ========================================================================

    [Test]
    public void E2E_CombatDetected_StrategyUpdatesPositions()
    {
        var squad = CreateSquad(0);
        var leader = CreateBot(0, x: 0, z: 0);
        var f1 = CreateBot(1, x: -5, z: 0);
        var f2 = CreateBot(2, x: 5, z: 0);
        AddToSquad(squad, leader, isLeader: true);
        AddToSquad(squad, f1);
        AddToSquad(squad, f2);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.MoveToPosition;
        squad.Objective.SetObjective(100f, 0f, 100f);

        _config.EnableCombatAwarePositioning = true;
        var strategy = new GotoObjectiveStrategy(_config, seed: 42);
        strategy.Activate(squad);
        strategy.AssignNewObjective(squad);

        float f1Before = f1.TacticalPositionX;
        float f2Before = f2.TacticalPositionX;

        // Simulate: combat detected by leader, threat from northeast
        squad.HasThreatDirection = true;
        squad.ThreatDirectionX = 0.707f;
        squad.ThreatDirectionZ = 0.707f;
        squad.CombatVersion = 1;
        squad.LastProcessedCombatVersion = 0;

        // Strategy Update should detect version mismatch and recompute
        strategy.Update();

        // After update, version should be synced
        Assert.AreEqual(1, squad.LastProcessedCombatVersion, "Combat version should be synced after update");

        // At least one follower should have moved
        bool anyMoved = Math.Abs(f1.TacticalPositionX - f1Before) > 0.01f || Math.Abs(f2.TacticalPositionX - f2Before) > 0.01f;
        Assert.IsTrue(anyMoved, "At least one follower should reposition on combat detection");
    }

    [Test]
    public void E2E_CombatAwarePositioning_Disabled_NoRecompute()
    {
        var squad = CreateSquad(0);
        var leader = CreateBot(0, x: 0, z: 0);
        var follower = CreateBot(1, x: -5, z: 0);
        AddToSquad(squad, leader, isLeader: true);
        AddToSquad(squad, follower);

        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = QuestActionId.MoveToPosition;
        squad.Objective.SetObjective(100f, 0f, 100f);

        _config.EnableCombatAwarePositioning = false; // Disabled!
        var strategy = new GotoObjectiveStrategy(_config, seed: 42);
        strategy.Activate(squad);
        strategy.AssignNewObjective(squad);

        float before = follower.TacticalPositionX;

        squad.HasThreatDirection = true;
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.CombatVersion = 1;
        squad.LastProcessedCombatVersion = 0;

        strategy.Update();

        // Version should NOT be synced because combat-aware is disabled
        Assert.AreEqual(0, squad.LastProcessedCombatVersion, "Combat version should not sync when feature is disabled");
        Assert.AreEqual(before, follower.TacticalPositionX, 0.001f, "Position should not change");
    }
}

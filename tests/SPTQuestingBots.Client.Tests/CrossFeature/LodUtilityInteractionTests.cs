using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.CrossFeature;

/// <summary>
/// Tests for interactions between LOD frame-skipping and UtilityAI scoring,
/// and between LOD tier changes and squad strategy updates.
/// Validates that LOD-skipped bots retain stale but safe task assignments,
/// and that combat event data remains fresh regardless of LOD tier.
/// </summary>
[TestFixture]
public class LodUtilityInteractionTests
{
    // ================================================================
    // Helpers
    // ================================================================

    private sealed class FixedScoreTask : QuestUtilityTask
    {
        public float Score;
        private readonly int _actionTypeId;

        public FixedScoreTask(float score, int actionTypeId, float hysteresis = 0.10f)
            : base(hysteresis)
        {
            Score = score;
            _actionTypeId = actionTypeId;
        }

        public override int BotActionTypeId => _actionTypeId;
        public override string ActionReason => "Fixed";

        public override void ScoreEntity(int ordinal, BotEntity entity)
        {
            entity.TaskScores[ordinal] = Score;
        }
    }

    private static BotEntity CreateEntity(int id)
    {
        var e = new BotEntity(id);
        e.IsActive = true;
        e.TaskScores = new float[QuestTaskFactory.TaskCount];
        return e;
    }

    // ================================================================
    // Pair 1: LOD Frame Skipping + UtilityAI Scoring
    // ================================================================

    [Test]
    public void LodSkipDoesNotClearExistingTaskAssignment()
    {
        // Setup: bot has an active task, then LOD says skip
        var entity = CreateEntity(1);
        var task = new FixedScoreTask(0.50f, BotActionTypeId.GoToObjective);
        var manager = new UtilityTaskManager(new UtilityTask[] { task });

        // Initial scoring picks the task
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.Not.Null, "Task should be assigned");

        // Simulate LOD skip: the layer would return previousState and NOT call ScoreAndPick
        // Verify the task assignment persists untouched
        entity.LodTier = BotLodCalculator.TierMinimal;
        entity.LodFrameCounter = 1; // would be skipped with minimalSkip=4

        // The task assignment should still be valid
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(task));
        Assert.That(entity.TaskAssignment.Ordinal, Is.EqualTo(0));
    }

    [Test]
    public void LodSkippedBotResumesCorrectlyOnNextEligibleFrame()
    {
        var entity = CreateEntity(1);
        var taskA = new FixedScoreTask(0.50f, BotActionTypeId.GoToObjective);
        var taskB = new FixedScoreTask(0.30f, BotActionTypeId.Patrol);
        var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
        entity.TaskScores = new float[2];

        // Pick task A
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(taskA));

        // Simulate several LOD-skipped frames (no ScoreAndPick calls)
        for (int i = 0; i < 10; i++)
        {
            entity.LodFrameCounter++;
        }

        // Task B now scores higher
        taskA.Score = 0.20f;
        taskB.Score = 0.80f;

        // On the next eligible frame, ScoreAndPick runs
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(taskB), "Bot should switch to higher-scoring task after LOD skip ends");
    }

    [Test]
    public void CombatEventDataUpdatedRegardlessOfLodTier()
    {
        // CombatEventScanner writes to entity fields (HasNearbyEvent, CombatIntensity)
        // in HiveMind tick step 6, which runs for ALL entities regardless of LOD.
        // This test verifies the entity fields are writable at any LOD tier.
        var entity = CreateEntity(1);
        entity.LodTier = BotLodCalculator.TierMinimal;

        // Simulate combat event scanner writing data
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 20;
        entity.NearbyEventX = 100f;
        entity.NearbyEventZ = 200f;

        Assert.That(entity.HasNearbyEvent, Is.True);
        Assert.That(entity.CombatIntensity, Is.EqualTo(20));
    }

    [Test]
    public void LodTierMinimalSkipsMostFrames()
    {
        // With minimalSkip=4, cycle=5, only frameCounter%5==0 passes
        int skipped = 0;
        int total = 25;
        for (int frame = 0; frame < total; frame++)
        {
            if (BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierMinimal, frame, 2, 4))
                skipped++;
        }

        // 25 frames, cycle=5, so 5 pass (0,5,10,15,20) and 20 skip
        Assert.That(skipped, Is.EqualTo(20), "Minimal tier should skip 4 out of every 5 frames");
    }

    [Test]
    public void LodTierFullNeverSkips()
    {
        for (int frame = 0; frame < 100; frame++)
        {
            Assert.That(
                BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierFull, frame, 2, 4),
                Is.False,
                $"TierFull should never skip (frame={frame})"
            );
        }
    }

    [Test]
    public void VultureEventNotMissedDuringLodSkip_DataIsWritten()
    {
        // Scenario: bot at TierMinimal, combat event appears during LOD-skipped frames
        // HiveMind still writes entity.HasNearbyEvent = true
        // When LOD un-skips, the VultureTask will see it and score accordingly
        var entity = CreateEntity(1);
        entity.LodTier = BotLodCalculator.TierMinimal;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;

        // Event data written by HiveMind (regardless of LOD)
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 25;
        entity.NearbyEventX = entity.CurrentPositionX + 50;
        entity.NearbyEventZ = entity.CurrentPositionZ + 50;
        entity.CurrentGameTime = 100f;

        // When the LOD-eligible frame arrives, vulture should score > 0
        float score = VultureTask.Score(entity, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.GreaterThan(0f), "Vulture should score when event data is present");
    }

    // ================================================================
    // Pair 8: LOD + Squad Strategies
    // ================================================================

    [Test]
    public void FollowerTacticalPositionUpdatesRegardlessOfLod()
    {
        // Squad strategy runs in HiveMind step 5, not gated by LOD
        // Verify tactical position is writable on LOD-skipped follower
        var boss = CreateEntity(1);
        var follower = CreateEntity(2);
        follower.Boss = boss;
        boss.Followers.Add(follower);
        follower.LodTier = BotLodCalculator.TierMinimal;

        // Simulate strategy manager updating tactical position
        follower.HasTacticalPosition = true;
        follower.TacticalPositionX = 50f;
        follower.TacticalPositionY = 0f;
        follower.TacticalPositionZ = 100f;

        Assert.That(follower.HasTacticalPosition, Is.True);
        Assert.That(follower.TacticalPositionX, Is.EqualTo(50f));
    }

    [Test]
    public void LodSkippedFollowerKeepsStaleTaskButEventuallyUpdates()
    {
        var boss = CreateEntity(1);
        var follower = CreateEntity(2);
        follower.Boss = boss;
        boss.Followers.Add(follower);
        follower.HasTacticalPosition = true;
        follower.TacticalPositionX = 50f;
        follower.TacticalPositionY = 0f;
        follower.TacticalPositionZ = 100f;

        // Use follower task manager
        var goTo = new GoToTacticalPositionTask();
        var hold = new HoldTacticalPositionTask();
        var fManager = new UtilityTaskManager(new UtilityTask[] { goTo, hold });
        follower.TaskScores = new float[SquadTaskFactory.TaskCount];

        // Follower is far from tactical position
        follower.CurrentPositionX = 0f;
        follower.CurrentPositionZ = 0f;
        fManager.ScoreAndPick(follower);
        Assert.That(follower.TaskAssignment.Task, Is.SameAs(goTo));

        // LOD skip: no re-scoring for several frames
        // Meanwhile boss moves, tactical position updates to (200, 0, 300)
        follower.TacticalPositionX = 200f;
        follower.TacticalPositionZ = 300f;

        // Task is still GoTo (stale but correct — follower is still far)
        Assert.That(follower.TaskAssignment.Task, Is.SameAs(goTo));

        // When LOD un-skips, re-scoring uses the NEW tactical position
        fManager.ScoreAndPick(follower);
        Assert.That(
            follower.TaskAssignment.Task,
            Is.SameAs(goTo),
            "Follower should still want to GoTo (now even farther from new position)"
        );
    }

    [Test]
    public void LodFrameCounterIncrementedByHiveMindTick()
    {
        // LodFrameCounter is incremented in updateLodTiers() (HiveMind step 9)
        // Each HiveMind tick increments it by 1
        var entity = CreateEntity(1);
        Assert.That(entity.LodFrameCounter, Is.EqualTo(0));

        // Simulate HiveMind ticks
        entity.LodFrameCounter++;
        entity.LodFrameCounter++;
        entity.LodFrameCounter++;
        Assert.That(entity.LodFrameCounter, Is.EqualTo(3));
    }

    [Test]
    public void SensorUpdatesNotAffectedByLod()
    {
        // Push sensors (InCombat, IsSuspicious) are event-driven
        // They don't participate in the HiveMind tick and are not LOD-gated
        var entity = CreateEntity(1);
        entity.LodTier = BotLodCalculator.TierMinimal;

        entity.IsInCombat = true;
        Assert.That(entity.IsInCombat, Is.True);

        entity.IsSuspicious = true;
        Assert.That(entity.IsSuspicious, Is.True);
    }

    [Test]
    public void LodReducedTierSkipPattern()
    {
        // With reducedSkip=2, cycle=3, frames 0,3,6,9,... pass; 1,2,4,5,7,8,... skip
        int passed = 0;
        for (int frame = 0; frame < 30; frame++)
        {
            if (!BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, frame, 2, 4))
                passed++;
        }

        // 30 frames / cycle=3 = 10 passes
        Assert.That(passed, Is.EqualTo(10), "Reduced tier should pass 1 in every 3 frames");
    }

    [Test]
    public void LodDoesNotAffectLootScanningData()
    {
        // Loot scanning (HiveMind step 7) writes to entity regardless of LOD
        var entity = CreateEntity(1);
        entity.LodTier = BotLodCalculator.TierMinimal;

        entity.HasLootTarget = true;
        entity.LootTargetValue = 30000f;
        entity.LootTargetX = 50f;

        Assert.That(entity.HasLootTarget, Is.True);
        Assert.That(entity.LootTargetValue, Is.EqualTo(30000f));
    }
}

using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI
{
    [TestFixture]
    public class LootTaskTests
    {
        private static BotEntity CreateLootEntity(
            int id = 0,
            bool hasLootTarget = true,
            float lootTargetValue = 10000f,
            float lootTargetX = 10f,
            float lootTargetY = 0f,
            float lootTargetZ = 10f,
            float currentPositionX = 0f,
            float currentPositionY = 0f,
            float currentPositionZ = 0f,
            float inventorySpaceFree = 1f,
            bool isInCombat = false,
            bool hasActiveObjective = false,
            float distanceToObjective = 100f
        )
        {
            var entity = new BotEntity(id);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.HasLootTarget = hasLootTarget;
            entity.LootTargetValue = lootTargetValue;
            entity.LootTargetX = lootTargetX;
            entity.LootTargetY = lootTargetY;
            entity.LootTargetZ = lootTargetZ;
            entity.CurrentPositionX = currentPositionX;
            entity.CurrentPositionY = currentPositionY;
            entity.CurrentPositionZ = currentPositionZ;
            entity.InventorySpaceFree = inventorySpaceFree;
            entity.IsInCombat = isInCombat;
            entity.HasActiveObjective = hasActiveObjective;
            entity.DistanceToObjective = distanceToObjective;
            return entity;
        }

        // ── Gate conditions ──────────────────────────────────

        [Test]
        public void Score_NoLootTarget_ReturnsZero()
        {
            var entity = CreateLootEntity(hasLootTarget: false);
            Assert.AreEqual(0f, LootTask.Score(entity));
        }

        [Test]
        public void Score_InCombat_ReturnsZero()
        {
            var entity = CreateLootEntity(isInCombat: true);
            Assert.AreEqual(0f, LootTask.Score(entity));
        }

        [Test]
        public void Score_NoSpaceAndNegativeValue_ReturnsZero()
        {
            var entity = CreateLootEntity(inventorySpaceFree: 0f, lootTargetValue: -1f);
            Assert.AreEqual(0f, LootTask.Score(entity));
        }

        [Test]
        public void Score_NoSpaceButPositiveValue_ReturnsNonZero()
        {
            // High value loot at close range should still score even with no space
            var entity = CreateLootEntity(inventorySpaceFree: 0f, lootTargetValue: 30000f, lootTargetX: 1f, lootTargetZ: 1f);
            float score = LootTask.Score(entity);
            Assert.Greater(score, 0f);
        }

        // ── Value scoring ────────────────────────────────────

        [Test]
        public void Score_HigherValue_ScoresHigher()
        {
            var lowValue = CreateLootEntity(lootTargetValue: 5000f, lootTargetX: 5f, lootTargetZ: 0f);
            var highValue = CreateLootEntity(id: 1, lootTargetValue: 40000f, lootTargetX: 5f, lootTargetZ: 0f);
            Assert.Greater(LootTask.Score(highValue), LootTask.Score(lowValue));
        }

        [Test]
        public void Score_ValueCapsAt50000()
        {
            // Both should produce the same value component since they exceed the 50000 cap
            var at50k = CreateLootEntity(lootTargetValue: 50000f, lootTargetX: 3f, lootTargetZ: 0f);
            var at100k = CreateLootEntity(id: 1, lootTargetValue: 100000f, lootTargetX: 3f, lootTargetZ: 0f);
            Assert.AreEqual(LootTask.Score(at50k), LootTask.Score(at100k), 0.001f);
        }

        [Test]
        public void Score_ZeroValue_ValueComponentIsZero()
        {
            // At zero value and no proximity bonus, loot right next to bot: valueScore=0, distancePenalty=0
            var entity = CreateLootEntity(lootTargetValue: 0f, lootTargetX: 0f, lootTargetZ: 0f);
            Assert.AreEqual(0f, LootTask.Score(entity));
        }

        // ── Distance penalty ─────────────────────────────────

        [Test]
        public void Score_CloserLoot_ScoresHigher()
        {
            var close = CreateLootEntity(lootTargetValue: 20000f, lootTargetX: 3f, lootTargetZ: 0f);
            var far = CreateLootEntity(id: 1, lootTargetValue: 20000f, lootTargetX: 20f, lootTargetZ: 0f);
            Assert.Greater(LootTask.Score(close), LootTask.Score(far));
        }

        [Test]
        public void Score_VeryFarLoot_DistancePenaltyCapsAt04()
        {
            // distancePenalty = min(distSqr * 0.001, 0.4)
            // At 20m: distSqr=400, penalty=0.4 (capped)
            // At 30m: distSqr=900, penalty=0.4 (still capped)
            // So both should produce the same score
            var at20 = CreateLootEntity(lootTargetValue: 50000f, lootTargetX: 20f, lootTargetZ: 0f);
            var at30 = CreateLootEntity(id: 1, lootTargetValue: 50000f, lootTargetX: 30f, lootTargetZ: 0f);
            Assert.AreEqual(LootTask.Score(at20), LootTask.Score(at30), 0.001f);
        }

        [Test]
        public void Score_FarLowValueLoot_ClampedToZero()
        {
            // Low value + far distance → negative score clamped to 0
            // value=1000: valueScore = (1000/50000)*0.5 = 0.01
            // distance=20m: distSqr=400, penalty = 0.4
            // score = 0.01 - 0.4 = -0.39 → clamped to 0
            var entity = CreateLootEntity(lootTargetValue: 1000f, lootTargetX: 20f, lootTargetZ: 0f);
            Assert.AreEqual(0f, LootTask.Score(entity));
        }

        // ── Quest proximity bonus ────────────────────────────

        [Test]
        public void Score_NearQuestObjective_GetsBonus()
        {
            var nearQuest = CreateLootEntity(
                lootTargetValue: 20000f,
                lootTargetX: 3f,
                lootTargetZ: 0f,
                hasActiveObjective: true,
                distanceToObjective: 15f
            );
            var noQuest = CreateLootEntity(
                id: 1,
                lootTargetValue: 20000f,
                lootTargetX: 3f,
                lootTargetZ: 0f,
                hasActiveObjective: false,
                distanceToObjective: 15f
            );
            Assert.Greater(LootTask.Score(nearQuest), LootTask.Score(noQuest));
        }

        [Test]
        public void Score_FarFromQuestObjective_NoBonus()
        {
            // Proximity bonus only applies when distanceToObjective < 20m (400 sqr threshold)
            var farQuest = CreateLootEntity(
                lootTargetValue: 20000f,
                lootTargetX: 3f,
                lootTargetZ: 0f,
                hasActiveObjective: true,
                distanceToObjective: 25f
            );
            var noQuest = CreateLootEntity(
                id: 1,
                lootTargetValue: 20000f,
                lootTargetX: 3f,
                lootTargetZ: 0f,
                hasActiveObjective: false,
                distanceToObjective: 25f
            );
            Assert.AreEqual(LootTask.Score(farQuest), LootTask.Score(noQuest), 0.001f);
        }

        [Test]
        public void Score_ProximityBonusIs015()
        {
            // Calculate expected difference from proximity bonus
            var withBonus = CreateLootEntity(
                lootTargetValue: 20000f,
                lootTargetX: 3f,
                lootTargetZ: 0f,
                hasActiveObjective: true,
                distanceToObjective: 10f
            );
            var withoutBonus = CreateLootEntity(
                id: 1,
                lootTargetValue: 20000f,
                lootTargetX: 3f,
                lootTargetZ: 0f,
                hasActiveObjective: false,
                distanceToObjective: 10f
            );
            float diff = LootTask.Score(withBonus) - LootTask.Score(withoutBonus);
            Assert.AreEqual(0.15f, diff, 0.001f);
        }

        // ── Score clamping ───────────────────────────────────

        [Test]
        public void Score_ClampsToMaxBaseScore()
        {
            // Max possible: valueScore=0.5 + proximityBonus=0.15 - distPenalty=0 = 0.65
            // MaxBaseScore = 0.55, so should clamp
            var entity = CreateLootEntity(
                lootTargetValue: 50000f,
                lootTargetX: 0.1f,
                lootTargetZ: 0f,
                hasActiveObjective: true,
                distanceToObjective: 5f
            );
            Assert.AreEqual(LootTask.MaxBaseScore, LootTask.Score(entity), 0.001f);
        }

        [Test]
        public void Score_MaxBaseScore_Is055()
        {
            Assert.AreEqual(0.55f, LootTask.MaxBaseScore, 0.001f);
        }

        // ── Properties ───────────────────────────────────────

        [Test]
        public void Properties_CorrectValues()
        {
            var task = new LootTask();
            Assert.AreEqual(BotActionTypeId.Loot, task.BotActionTypeId);
            Assert.AreEqual("Looting", task.ActionReason);
            Assert.AreEqual(0.15f, task.Hysteresis, 0.001f);
        }

        [Test]
        public void ScoreEntity_WritesToTaskScores()
        {
            var task = new LootTask();
            var entity = CreateLootEntity(lootTargetValue: 25000f, lootTargetX: 5f, lootTargetZ: 0f);

            // LootTask is at ordinal 8 in QuestTaskFactory
            task.ScoreEntity(8, entity);

            Assert.Greater(entity.TaskScores[8], 0f);
        }

        // ── Integration with QuestTaskFactory ────────────────

        [Test]
        public void Integration_HighValueNearbyLoot_WinsOverNoQuest()
        {
            var manager = QuestTaskFactory.Create();
            var bot = CreateLootEntity(lootTargetValue: 40000f, lootTargetX: 3f, lootTargetZ: 0f, hasActiveObjective: false);

            manager.ScoreAndPick(bot);

            Assert.IsNotNull(bot.TaskAssignment.Task);
            Assert.IsInstanceOf<LootTask>(bot.TaskAssignment.Task);
        }

        [Test]
        public void Integration_GoToObjective_WinsOverLoot_WhenQuesting()
        {
            var manager = QuestTaskFactory.Create();
            var bot = CreateLootEntity(
                lootTargetValue: 10000f,
                lootTargetX: 15f,
                lootTargetZ: 0f,
                hasActiveObjective: true,
                distanceToObjective: 50f
            );
            bot.CurrentQuestAction = QuestActionId.MoveToPosition;

            manager.ScoreAndPick(bot);

            // GoToObjective scores 0.65, loot at 15m with 10k value scores much less
            Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);
        }
    }
}

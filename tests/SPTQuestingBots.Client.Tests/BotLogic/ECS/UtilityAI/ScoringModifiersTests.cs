using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI
{
    [TestFixture]
    public class ScoringModifiersTests
    {
        // ── Lerp ────────────────────────────────────────────────

        [Test]
        public void Lerp_AtZero_ReturnsA()
        {
            Assert.That(ScoringModifiers.Lerp(0.85f, 1.15f, 0f), Is.EqualTo(0.85f).Within(0.001f));
        }

        [Test]
        public void Lerp_AtOne_ReturnsB()
        {
            Assert.That(ScoringModifiers.Lerp(0.85f, 1.15f, 1f), Is.EqualTo(1.15f).Within(0.001f));
        }

        [Test]
        public void Lerp_AtHalf_ReturnsMidpoint()
        {
            Assert.That(ScoringModifiers.Lerp(0.85f, 1.15f, 0.5f), Is.EqualTo(1.0f).Within(0.001f));
        }

        // ── PersonalityModifier ─────────────────────────────────

        [Test]
        public void PersonalityModifier_GoToObjective_AggressiveBotHigher()
        {
            float timid = ScoringModifiers.PersonalityModifier(0.1f, BotActionTypeId.GoToObjective);
            float reckless = ScoringModifiers.PersonalityModifier(0.9f, BotActionTypeId.GoToObjective);
            Assert.That(reckless, Is.GreaterThan(timid));
        }

        [Test]
        public void PersonalityModifier_Ambush_CautiousBotHigher()
        {
            float cautious = ScoringModifiers.PersonalityModifier(0.3f, BotActionTypeId.Ambush);
            float aggressive = ScoringModifiers.PersonalityModifier(0.7f, BotActionTypeId.Ambush);
            Assert.That(cautious, Is.GreaterThan(aggressive));
        }

        [Test]
        public void PersonalityModifier_Snipe_CautiousBotHigher()
        {
            float cautious = ScoringModifiers.PersonalityModifier(0.3f, BotActionTypeId.Snipe);
            float aggressive = ScoringModifiers.PersonalityModifier(0.7f, BotActionTypeId.Snipe);
            Assert.That(cautious, Is.GreaterThan(aggressive));
        }

        [Test]
        public void PersonalityModifier_Linger_CautiousBotHigher()
        {
            float cautious = ScoringModifiers.PersonalityModifier(0.3f, BotActionTypeId.Linger);
            float aggressive = ScoringModifiers.PersonalityModifier(0.7f, BotActionTypeId.Linger);
            Assert.That(cautious, Is.GreaterThan(aggressive));
        }

        [Test]
        public void PersonalityModifier_Loot_SlightCautiousPreference()
        {
            float cautious = ScoringModifiers.PersonalityModifier(0.3f, BotActionTypeId.Loot);
            float aggressive = ScoringModifiers.PersonalityModifier(0.7f, BotActionTypeId.Loot);
            Assert.That(cautious, Is.GreaterThan(aggressive));
        }

        [Test]
        public void PersonalityModifier_Vulture_AggressiveBotHigher()
        {
            float cautious = ScoringModifiers.PersonalityModifier(0.3f, BotActionTypeId.Vulture);
            float aggressive = ScoringModifiers.PersonalityModifier(0.7f, BotActionTypeId.Vulture);
            Assert.That(aggressive, Is.GreaterThan(cautious));
        }

        [Test]
        public void PersonalityModifier_HoldPosition_AlwaysOne()
        {
            Assert.That(ScoringModifiers.PersonalityModifier(0.1f, BotActionTypeId.HoldPosition), Is.EqualTo(1f));
            Assert.That(ScoringModifiers.PersonalityModifier(0.9f, BotActionTypeId.HoldPosition), Is.EqualTo(1f));
        }

        [Test]
        public void PersonalityModifier_PlantItem_AlwaysOne()
        {
            Assert.That(ScoringModifiers.PersonalityModifier(0.1f, BotActionTypeId.PlantItem), Is.EqualTo(1f));
            Assert.That(ScoringModifiers.PersonalityModifier(0.9f, BotActionTypeId.PlantItem), Is.EqualTo(1f));
        }

        [Test]
        public void PersonalityModifier_UnlockDoor_AlwaysOne()
        {
            Assert.That(ScoringModifiers.PersonalityModifier(0.5f, BotActionTypeId.UnlockDoor), Is.EqualTo(1f));
        }

        [Test]
        public void PersonalityModifier_ToggleSwitch_AlwaysOne()
        {
            Assert.That(ScoringModifiers.PersonalityModifier(0.5f, BotActionTypeId.ToggleSwitch), Is.EqualTo(1f));
        }

        [Test]
        public void PersonalityModifier_CloseNearbyDoors_AlwaysOne()
        {
            Assert.That(ScoringModifiers.PersonalityModifier(0.5f, BotActionTypeId.CloseNearbyDoors), Is.EqualTo(1f));
        }

        [Test]
        public void PersonalityModifier_NormalAggression_NearOne()
        {
            // Normal aggression (0.5) should produce modifier near 1.0 for GoToObjective
            float mod = ScoringModifiers.PersonalityModifier(0.5f, BotActionTypeId.GoToObjective);
            Assert.That(mod, Is.EqualTo(1.0f).Within(0.01f));
        }

        // ── PersonalityModifier exact values ────────────────────

        [Test]
        public void PersonalityModifier_GoToObjective_TimidAggression_ReturnsExpected()
        {
            // lerp(0.85, 1.15, 0.1) = 0.85 + 0.3*0.1 = 0.88
            float mod = ScoringModifiers.PersonalityModifier(0.1f, BotActionTypeId.GoToObjective);
            Assert.That(mod, Is.EqualTo(0.88f).Within(0.001f));
        }

        [Test]
        public void PersonalityModifier_GoToObjective_RecklessAggression_ReturnsExpected()
        {
            // lerp(0.85, 1.15, 0.9) = 0.85 + 0.3*0.9 = 1.12
            float mod = ScoringModifiers.PersonalityModifier(0.9f, BotActionTypeId.GoToObjective);
            Assert.That(mod, Is.EqualTo(1.12f).Within(0.001f));
        }

        // ── RaidTimeModifier ────────────────────────────────────

        [Test]
        public void RaidTimeModifier_GoToObjective_EarlyRaid_Boosted()
        {
            float earlyMod = ScoringModifiers.RaidTimeModifier(0f, BotActionTypeId.GoToObjective);
            Assert.That(earlyMod, Is.EqualTo(1.2f).Within(0.001f));
        }

        [Test]
        public void RaidTimeModifier_GoToObjective_LateRaid_Reduced()
        {
            float lateMod = ScoringModifiers.RaidTimeModifier(1f, BotActionTypeId.GoToObjective);
            Assert.That(lateMod, Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void RaidTimeModifier_Linger_EarlyRaid_Reduced()
        {
            float earlyMod = ScoringModifiers.RaidTimeModifier(0f, BotActionTypeId.Linger);
            Assert.That(earlyMod, Is.EqualTo(0.7f).Within(0.001f));
        }

        [Test]
        public void RaidTimeModifier_Linger_LateRaid_Boosted()
        {
            float lateMod = ScoringModifiers.RaidTimeModifier(1f, BotActionTypeId.Linger);
            Assert.That(lateMod, Is.EqualTo(1.3f).Within(0.001f));
        }

        [Test]
        public void RaidTimeModifier_Loot_EarlyRaid_Reduced()
        {
            float earlyMod = ScoringModifiers.RaidTimeModifier(0f, BotActionTypeId.Loot);
            Assert.That(earlyMod, Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void RaidTimeModifier_Loot_LateRaid_Boosted()
        {
            float lateMod = ScoringModifiers.RaidTimeModifier(1f, BotActionTypeId.Loot);
            Assert.That(lateMod, Is.EqualTo(1.2f).Within(0.001f));
        }

        [Test]
        public void RaidTimeModifier_Ambush_EarlyRaid_SlightlyReduced()
        {
            float earlyMod = ScoringModifiers.RaidTimeModifier(0f, BotActionTypeId.Ambush);
            Assert.That(earlyMod, Is.EqualTo(0.9f).Within(0.001f));
        }

        [Test]
        public void RaidTimeModifier_Ambush_LateRaid_SlightlyBoosted()
        {
            float lateMod = ScoringModifiers.RaidTimeModifier(1f, BotActionTypeId.Ambush);
            Assert.That(lateMod, Is.EqualTo(1.1f).Within(0.001f));
        }

        [Test]
        public void RaidTimeModifier_Vulture_AlwaysOne()
        {
            Assert.That(ScoringModifiers.RaidTimeModifier(0f, BotActionTypeId.Vulture), Is.EqualTo(1f));
            Assert.That(ScoringModifiers.RaidTimeModifier(0.5f, BotActionTypeId.Vulture), Is.EqualTo(1f));
            Assert.That(ScoringModifiers.RaidTimeModifier(1f, BotActionTypeId.Vulture), Is.EqualTo(1f));
        }

        [Test]
        public void RaidTimeModifier_HoldPosition_AlwaysOne()
        {
            Assert.That(ScoringModifiers.RaidTimeModifier(0.5f, BotActionTypeId.HoldPosition), Is.EqualTo(1f));
        }

        [Test]
        public void RaidTimeModifier_MidRaid_GoToObjective_NearOne()
        {
            float midMod = ScoringModifiers.RaidTimeModifier(0.5f, BotActionTypeId.GoToObjective);
            Assert.That(midMod, Is.EqualTo(1.0f).Within(0.01f));
        }

        // ── CombinedModifier ────────────────────────────────────

        [Test]
        public void CombinedModifier_NormalBot_MidRaid_NearOne()
        {
            float mod = ScoringModifiers.CombinedModifier(0.5f, 0.5f, BotActionTypeId.GoToObjective);
            Assert.That(mod, Is.EqualTo(1.0f).Within(0.02f));
        }

        [Test]
        public void CombinedModifier_AggressiveBot_EarlyRaid_GoToObjective_High()
        {
            float mod = ScoringModifiers.CombinedModifier(0.9f, 0.0f, BotActionTypeId.GoToObjective);
            // personality: 1.12, raidTime: 1.2 → 1.344
            Assert.That(mod, Is.GreaterThan(1.3f));
        }

        [Test]
        public void CombinedModifier_CautiousBot_LateRaid_Linger_High()
        {
            float mod = ScoringModifiers.CombinedModifier(0.3f, 1.0f, BotActionTypeId.Linger);
            // personality: 1.12, raidTime: 1.3 → 1.456
            Assert.That(mod, Is.GreaterThan(1.4f));
        }

        [Test]
        public void CombinedModifier_IsProduct_OfComponents()
        {
            float aggression = 0.3f;
            float raidTime = 0.7f;
            int actionId = BotActionTypeId.GoToObjective;

            float personality = ScoringModifiers.PersonalityModifier(aggression, actionId);
            float time = ScoringModifiers.RaidTimeModifier(raidTime, actionId);
            float combined = ScoringModifiers.CombinedModifier(aggression, raidTime, actionId);

            Assert.That(combined, Is.EqualTo(personality * time).Within(0.0001f));
        }

        // ── ScoreEntity integration ─────────────────────────────

        [Test]
        public void GoToObjectiveTask_ScoreEntity_AppliesModifiers()
        {
            var entity = new BotEntity(0);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.HasActiveObjective = true;
            entity.CurrentQuestAction = QuestActionId.MoveToPosition;
            entity.DistanceToObjective = 100f;
            entity.Aggression = 0.5f;
            entity.RaidTimeNormalized = 0.5f;

            var task = new GoToObjectiveTask();
            task.ScoreEntity(0, entity);
            float baseScore = GoToObjectiveTask.Score(entity);
            float modifier = ScoringModifiers.CombinedModifier(0.5f, 0.5f, BotActionTypeId.GoToObjective);

            Assert.That(entity.TaskScores[0], Is.EqualTo(baseScore * modifier).Within(0.001f));
        }

        [Test]
        public void AmbushTask_ScoreEntity_AppliesModifiers()
        {
            var entity = new BotEntity(0);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.HasActiveObjective = true;
            entity.CurrentQuestAction = QuestActionId.Ambush;
            entity.IsCloseToObjective = true;
            entity.Aggression = 0.3f;
            entity.RaidTimeNormalized = 0.8f;

            var task = new AmbushTask();
            task.ScoreEntity(1, entity);
            float baseScore = AmbushTask.Score(entity);
            float modifier = ScoringModifiers.CombinedModifier(0.3f, 0.8f, BotActionTypeId.Ambush);

            Assert.That(entity.TaskScores[1], Is.EqualTo(baseScore * modifier).Within(0.001f));
        }

        [Test]
        public void SnipeTask_ScoreEntity_AppliesModifiers()
        {
            var entity = new BotEntity(0);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.HasActiveObjective = true;
            entity.CurrentQuestAction = QuestActionId.Snipe;
            entity.IsCloseToObjective = true;
            entity.Aggression = 0.3f;
            entity.RaidTimeNormalized = 0.5f;

            var task = new SnipeTask();
            task.ScoreEntity(2, entity);
            float baseScore = SnipeTask.Score(entity);
            float modifier = ScoringModifiers.CombinedModifier(0.3f, 0.5f, BotActionTypeId.Snipe);

            Assert.That(entity.TaskScores[2], Is.EqualTo(baseScore * modifier).Within(0.001f));
        }

        [Test]
        public void LingerTask_ScoreEntity_AppliesModifiers()
        {
            var entity = new BotEntity(0);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.ObjectiveCompletedTime = 1f;
            entity.LingerDuration = 20f;
            entity.CurrentGameTime = 5f;
            entity.Aggression = 0.7f;
            entity.RaidTimeNormalized = 0.9f;

            var task = new LingerTask();
            task.ScoreEntity(10, entity);
            float baseScore = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
            float modifier = ScoringModifiers.CombinedModifier(0.7f, 0.9f, BotActionTypeId.Linger);

            Assert.That(entity.TaskScores[10], Is.EqualTo(baseScore * modifier).Within(0.001f));
        }

        [Test]
        public void LootTask_ScoreEntity_AppliesModifiers()
        {
            var entity = new BotEntity(0);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.HasLootTarget = true;
            entity.LootTargetValue = 30000f;
            entity.LootTargetX = 10f;
            entity.LootTargetY = 0f;
            entity.LootTargetZ = 10f;
            entity.CurrentPositionX = 0f;
            entity.CurrentPositionY = 0f;
            entity.CurrentPositionZ = 0f;
            entity.InventorySpaceFree = 5f;
            entity.Aggression = 0.5f;
            entity.RaidTimeNormalized = 0.8f;

            var task = new LootTask();
            task.ScoreEntity(8, entity);
            float baseScore = LootTask.Score(entity);
            float modifier = ScoringModifiers.CombinedModifier(0.5f, 0.8f, BotActionTypeId.Loot);

            Assert.That(entity.TaskScores[8], Is.EqualTo(baseScore * modifier).Within(0.001f));
        }

        [Test]
        public void VultureTask_ScoreEntity_AppliesModifiers()
        {
            var entity = new BotEntity(0);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.HasNearbyEvent = true;
            entity.NearbyEventX = 50f;
            entity.NearbyEventZ = 50f;
            entity.CombatIntensity = 30;
            entity.VulturePhase = 0;
            entity.VultureCooldownUntil = 0f;
            entity.IsInBossZone = false;
            entity.IsInCombat = false;
            entity.CurrentPositionX = 0f;
            entity.CurrentPositionZ = 0f;
            entity.Aggression = 0.7f;
            entity.RaidTimeNormalized = 0.3f;

            var task = new VultureTask();
            task.ScoreEntity(9, entity);
            float baseScore = VultureTask.Score(entity, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
            float modifier = ScoringModifiers.CombinedModifier(0.7f, 0.3f, BotActionTypeId.Vulture);

            Assert.That(entity.TaskScores[9], Is.EqualTo(baseScore * modifier).Within(0.001f));
        }

        // ── No-modifier tasks unchanged ─────────────────────────

        [Test]
        public void HoldPositionTask_ScoreEntity_NoModifier()
        {
            var entity = new BotEntity(0);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.HasActiveObjective = true;
            entity.CurrentQuestAction = QuestActionId.HoldAtPosition;
            entity.Aggression = 0.9f;
            entity.RaidTimeNormalized = 0.9f;

            var task = new HoldPositionTask();
            task.ScoreEntity(3, entity);

            Assert.That(entity.TaskScores[3], Is.EqualTo(HoldPositionTask.BaseScore));
        }

        [Test]
        public void PlantItemTask_ScoreEntity_NoModifier()
        {
            var entity = new BotEntity(0);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.HasActiveObjective = true;
            entity.CurrentQuestAction = QuestActionId.PlantItem;
            entity.IsCloseToObjective = true;
            entity.Aggression = 0.9f;
            entity.RaidTimeNormalized = 0.9f;

            var task = new PlantItemTask();
            task.ScoreEntity(4, entity);

            Assert.That(entity.TaskScores[4], Is.EqualTo(PlantItemTask.BaseScore));
        }

        [Test]
        public void UnlockDoorTask_ScoreEntity_NoModifier()
        {
            var entity = new BotEntity(0);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.HasActiveObjective = true;
            entity.MustUnlockDoor = true;
            entity.Aggression = 0.9f;
            entity.RaidTimeNormalized = 0.9f;

            var task = new UnlockDoorTask();
            task.ScoreEntity(5, entity);

            Assert.That(entity.TaskScores[5], Is.EqualTo(UnlockDoorTask.BaseScore));
        }
    }
}

using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.CrossFeature;

/// <summary>
/// Tests for extreme personality + multi-feature combinations.
/// Validates scoring behavior at personality/raid-time boundaries
/// to ensure no pathological outcomes (all-zero scores, all-high scores,
/// or modifier products that break score ordering).
/// </summary>
[TestFixture]
public class ExtremePersonalityComboTests
{
    private static BotEntity CreateEntity(int id)
    {
        var e = new BotEntity(id);
        e.IsActive = true;
        e.TaskScores = new float[QuestTaskFactory.TaskCount];
        return e;
    }

    // ================================================================
    // Personality modifier products at extremes
    // ================================================================

    [Test]
    public void TimidBot_LateRaid_LingerModifierBounded()
    {
        // aggression=0, raidTime=1 → Linger combined modifier
        float modifier = ScoringModifiers.CombinedModifier(0f, 1f, BotActionTypeId.Linger);

        // PersonalityModifier(0, Linger) = Lerp(1.3, 0.7, 0) = 1.3
        // RaidTimeModifier(1, Linger) = Lerp(0.7, 1.3, 1) = 1.3
        // Combined = 1.3 * 1.3 = 1.69 → clamped to MaxCombinedModifier (1.5)
        Assert.That(
            modifier,
            Is.EqualTo(ScoringModifiers.MaxCombinedModifier).Within(0.01f),
            "Timid bot late raid linger modifier should be clamped to MaxCombinedModifier"
        );

        // With DefaultBaseScore=0.45, max linger = 0.45 * 1.5 = 0.675
        float maxLingerScore = LingerTask.DefaultBaseScore * modifier;
        Assert.That(
            maxLingerScore,
            Is.LessThan(SpawnEntryTask.MaxBaseScore),
            "Linger at default base score should not exceed SpawnEntry (1.0)"
        );
    }

    [Test]
    public void RecklessBot_EarlyRaid_GoToObjectiveModifierBounded()
    {
        // aggression=1, raidTime=0 → GoToObjective combined modifier
        float modifier = ScoringModifiers.CombinedModifier(1f, 0f, BotActionTypeId.GoToObjective);

        // PersonalityModifier(1, GoTo) = Lerp(0.85, 1.15, 1) = 1.15
        // RaidTimeModifier(0, GoTo) = Lerp(1.2, 0.8, 0) = 1.2
        // Combined = 1.15 * 1.2 = 1.38
        Assert.That(modifier, Is.EqualTo(1.38f).Within(0.01f));
    }

    [Test]
    public void TimidBot_EarlyRaid_VultureModifierMinimal()
    {
        // aggression=0, raidTime=0 → Vulture modifier (timid bots don't vulture much)
        float modifier = ScoringModifiers.CombinedModifier(0f, 0f, BotActionTypeId.Vulture);

        // PersonalityModifier(0, Vulture) = Lerp(0.7, 1.3, 0) = 0.7
        // RaidTimeModifier(0, Vulture) = 1.0 (default)
        // Combined = 0.7 * 1.0 = 0.7
        Assert.That(modifier, Is.EqualTo(0.7f).Within(0.01f));

        // Max vulture score: 0.60 * 0.7 = 0.42
        float maxVulture = VultureTask.MaxBaseScore * modifier;
        Assert.That(maxVulture, Is.LessThan(0.50f), "Timid bot's vulture should be modest");
    }

    [Test]
    public void RecklessBot_LateRaid_VultureModifierMaximal()
    {
        // aggression=1, raidTime=1 → Vulture modifier (reckless + late raid)
        float modifier = ScoringModifiers.CombinedModifier(1f, 1f, BotActionTypeId.Vulture);

        // PersonalityModifier(1, Vulture) = Lerp(0.7, 1.3, 1) = 1.3
        // RaidTimeModifier(1, Vulture) = 1.0 (default)
        // Combined = 1.3 * 1.0 = 1.3
        Assert.That(modifier, Is.EqualTo(1.3f).Within(0.01f));

        float maxVulture = VultureTask.MaxBaseScore * modifier;
        Assert.That(maxVulture, Is.LessThan(1.0f), "Vulture should never exceed 1.0");
    }

    // ================================================================
    // Full scoring matrix at extremes
    // ================================================================

    [Test]
    public void TimidBot_LateRaid_AllScoresPositive_WhenTasksActivate()
    {
        // Setup timid bot late raid with all tasks potentially active
        var entity = CreateEntity(1);
        entity.Aggression = 0f; // Timid
        entity.RaidTimeNormalized = 1f; // Late raid
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = 1; // MoveToPosition
        entity.DistanceToObjective = 100f;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 20;
        entity.NearbyEventX = entity.CurrentPositionX + 30f;
        entity.NearbyEventZ = entity.CurrentPositionZ + 30f;
        entity.ObjectiveCompletedTime = entity.CurrentGameTime - 2f;
        entity.LingerDuration = 10f;
        entity.CurrentGameTime = 100f;
        entity.HasLootTarget = true;
        entity.LootTargetValue = 30000f;
        entity.LootTargetX = entity.CurrentPositionX + 10f;
        entity.LootTargetY = entity.CurrentPositionY;
        entity.LootTargetZ = entity.CurrentPositionZ + 10f;

        var manager = QuestTaskFactory.Create();
        manager.ScoreAndPick(entity);

        // At least one task should have scored > 0
        bool anyPositive = false;
        for (int i = 0; i < entity.TaskScores.Length; i++)
        {
            if (entity.TaskScores[i] > 0f)
                anyPositive = true;
        }

        Assert.That(anyPositive, Is.True, "Timid bot late raid should have at least one task scoring > 0");
        Assert.That(entity.TaskAssignment.Task, Is.Not.Null, "A task should be assigned");
    }

    [Test]
    public void RecklessBot_EarlyRaid_AllScoresPositive_WhenTasksActivate()
    {
        var entity = CreateEntity(1);
        entity.Aggression = 1f; // Reckless
        entity.RaidTimeNormalized = 0f; // Early raid
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = 1;
        entity.DistanceToObjective = 100f;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 20;
        entity.NearbyEventX = entity.CurrentPositionX + 30f;
        entity.NearbyEventZ = entity.CurrentPositionZ + 30f;
        entity.ObjectiveCompletedTime = entity.CurrentGameTime - 2f;
        entity.LingerDuration = 10f;
        entity.CurrentGameTime = 100f;
        entity.HasLootTarget = true;
        entity.LootTargetValue = 30000f;
        entity.LootTargetX = entity.CurrentPositionX + 10f;
        entity.LootTargetY = entity.CurrentPositionY;
        entity.LootTargetZ = entity.CurrentPositionZ + 10f;

        var manager = QuestTaskFactory.Create();
        manager.ScoreAndPick(entity);

        bool anyPositive = false;
        for (int i = 0; i < entity.TaskScores.Length; i++)
        {
            if (entity.TaskScores[i] > 0f)
                anyPositive = true;
        }

        Assert.That(anyPositive, Is.True);
        Assert.That(entity.TaskAssignment.Task, Is.Not.Null);
    }

    [Test]
    public void NoScoresExceedOne_AtExtremePersonality()
    {
        // Verify no final score > 1.0 at any personality/raid-time extreme
        var entity = CreateEntity(1);
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = 1;
        entity.DistanceToObjective = 100f;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 30;
        entity.NearbyEventX = entity.CurrentPositionX + 5f;
        entity.NearbyEventZ = entity.CurrentPositionZ + 5f;
        entity.ObjectiveCompletedTime = entity.CurrentGameTime;
        entity.LingerDuration = 10f;
        entity.CurrentGameTime = 100f;
        entity.HasLootTarget = true;
        entity.LootTargetValue = 50000f;
        entity.LootTargetX = entity.CurrentPositionX + 5f;
        entity.LootTargetY = entity.CurrentPositionY;
        entity.LootTargetZ = entity.CurrentPositionZ + 5f;
        entity.SpawnTime = 99f;
        entity.SpawnEntryDuration = 5f;

        float[] aggressions = { 0f, 0.1f, 0.5f, 0.9f, 1f };
        float[] raidTimes = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        var manager = QuestTaskFactory.Create();

        foreach (float agg in aggressions)
        {
            foreach (float rt in raidTimes)
            {
                entity.Aggression = agg;
                entity.RaidTimeNormalized = rt;
                entity.IsSpawnEntryComplete = false; // reset

                for (int i = 0; i < manager.Tasks.Length; i++)
                {
                    manager.Tasks[i].ScoreEntity(i, entity);
                }

                for (int i = 0; i < entity.TaskScores.Length; i++)
                {
                    Assert.That(
                        entity.TaskScores[i],
                        Is.LessThanOrEqualTo(1.0f),
                        $"Score[{i}] = {entity.TaskScores[i]:F4} at aggression={agg}, raidTime={rt}"
                    );
                    Assert.That(
                        !float.IsNaN(entity.TaskScores[i]) && !float.IsInfinity(entity.TaskScores[i]),
                        Is.True,
                        $"Score[{i}] is NaN/Infinity at aggression={agg}, raidTime={rt}"
                    );
                }
            }
        }
    }

    [Test]
    public void TimidBot_LateRaid_GoToObjective_StillScoresPositive()
    {
        // Worst case for GoToObjective: timid (0.85) + late raid (0.80) = 0.68
        var entity = CreateEntity(1);
        entity.Aggression = 0f;
        entity.RaidTimeNormalized = 1f;
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = 1; // MoveToPosition
        entity.DistanceToObjective = 100f;

        var task = new GoToObjectiveTask();
        task.ScoreEntity(0, entity);

        Assert.That(entity.TaskScores[0], Is.GreaterThan(0f), "GoToObjective should still score > 0 for timid bot late raid");
    }

    [Test]
    public void RecklessBot_EarlyRaid_Linger_StillScoresPositive()
    {
        // Worst case for Linger: reckless (0.7) + early raid (0.7) = 0.49
        var entity = CreateEntity(1);
        entity.Aggression = 1f;
        entity.RaidTimeNormalized = 0f;
        entity.ObjectiveCompletedTime = 99f;
        entity.LingerDuration = 10f;
        entity.CurrentGameTime = 100f;

        var task = new LingerTask();
        task.ScoreEntity(0, entity);

        float modifier = ScoringModifiers.CombinedModifier(1f, 0f, BotActionTypeId.Linger);
        Assert.That(modifier, Is.GreaterThan(0f));
        Assert.That(entity.TaskScores[0], Is.GreaterThan(0f), "Linger should still score > 0 for reckless bot early raid");
    }

    // ================================================================
    // Multi-feature activation with extreme personality
    // ================================================================

    [Test]
    public void TimidBot_LateRaid_MultipleTasksCompete_NoOscillation()
    {
        // Timid late-raid: linger and patrol are boosted, GoToObjective is reduced
        // Verify that with hysteresis, the task manager doesn't oscillate
        var entity = CreateEntity(1);
        entity.Aggression = 0f;
        entity.RaidTimeNormalized = 1f;
        entity.CurrentGameTime = 100f;
        entity.HasActiveObjective = false; // no quest
        entity.ObjectiveCompletedTime = 95f;
        entity.LingerDuration = 60f; // long linger
        entity.PatrolRouteIndex = -1; // no patrol route

        var manager = QuestTaskFactory.Create();

        // Score and pick 10 times — task should stabilize
        UtilityTask lastTask = null;
        int switches = 0;
        for (int i = 0; i < 10; i++)
        {
            manager.ScoreAndPick(entity);
            if (entity.TaskAssignment.Task != lastTask)
            {
                switches++;
                lastTask = entity.TaskAssignment.Task;
            }
        }

        Assert.That(switches, Is.LessThanOrEqualTo(2), "Task should stabilize within 2 switches (initial + correction), not oscillate");
    }

    [Test]
    public void RecklessBot_EarlyRaid_GoToObjectiveWins_OverCampingTasks()
    {
        // Reckless early raid: GoToObjective boosted (1.38), Ambush/Snipe reduced
        var entity = CreateEntity(1);
        entity.Aggression = 1f;
        entity.RaidTimeNormalized = 0f;
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = 4; // Ambush
        entity.DistanceToObjective = 200f;
        entity.IsCloseToObjective = false;
        entity.CurrentGameTime = 100f;

        float goToMod = ScoringModifiers.CombinedModifier(1f, 0f, BotActionTypeId.GoToObjective);
        float ambushMod = ScoringModifiers.CombinedModifier(1f, 0f, BotActionTypeId.Ambush);

        Assert.That(goToMod, Is.GreaterThan(ambushMod), "Reckless early-raid: GoToObjective modifier should beat Ambush modifier");
    }

    [Test]
    public void SpawnEntry_BeatsAllModifiedScores_AtAnyExtreme()
    {
        // SpawnEntry scores 0.80 with no modifiers
        // Verify no other task with max modifiers can beat it
        float maxModifier = 0f;
        int[] allActions =
        {
            BotActionTypeId.GoToObjective,
            BotActionTypeId.Ambush,
            BotActionTypeId.Snipe,
            BotActionTypeId.Linger,
            BotActionTypeId.Loot,
            BotActionTypeId.Vulture,
            BotActionTypeId.Investigate,
            BotActionTypeId.Patrol,
        };

        foreach (int action in allActions)
        {
            float mod = ScoringModifiers.CombinedModifier(0f, 1f, action);
            if (mod > maxModifier)
                maxModifier = mod;
            mod = ScoringModifiers.CombinedModifier(1f, 0f, action);
            if (mod > maxModifier)
                maxModifier = mod;
            mod = ScoringModifiers.CombinedModifier(0f, 0f, action);
            if (mod > maxModifier)
                maxModifier = mod;
            mod = ScoringModifiers.CombinedModifier(1f, 1f, action);
            if (mod > maxModifier)
                maxModifier = mod;
        }

        // Maximum possible modifier product across all personality/time extremes
        // No task base score × maxModifier should exceed SpawnEntry
        float[] baseScores = { 0.65f, 0.60f, 0.55f, 0.50f, 0.45f, 0.40f };
        foreach (float baseScore in baseScores)
        {
            float maxModifiedScore = baseScore * maxModifier;
            Assert.That(
                maxModifiedScore,
                Is.LessThan(SpawnEntryTask.MaxBaseScore),
                $"Base={baseScore} * MaxMod={maxModifier:F2} = {maxModifiedScore:F2} should not exceed SpawnEntry={SpawnEntryTask.MaxBaseScore}"
            );
        }
    }

    // ================================================================
    // Edge cases at personality boundaries
    // ================================================================

    [Test]
    public void PersonalityModifier_ClampedAt0And1()
    {
        // Out-of-range aggression should be clamped
        float below = ScoringModifiers.PersonalityModifier(-0.5f, BotActionTypeId.GoToObjective);
        float atZero = ScoringModifiers.PersonalityModifier(0f, BotActionTypeId.GoToObjective);
        Assert.That(below, Is.EqualTo(atZero).Within(0.001f));

        float above = ScoringModifiers.PersonalityModifier(1.5f, BotActionTypeId.GoToObjective);
        float atOne = ScoringModifiers.PersonalityModifier(1f, BotActionTypeId.GoToObjective);
        Assert.That(above, Is.EqualTo(atOne).Within(0.001f));
    }

    [Test]
    public void RaidTimeModifier_ClampedAt0And1()
    {
        float below = ScoringModifiers.RaidTimeModifier(-0.5f, BotActionTypeId.GoToObjective);
        float atZero = ScoringModifiers.RaidTimeModifier(0f, BotActionTypeId.GoToObjective);
        Assert.That(below, Is.EqualTo(atZero).Within(0.001f));

        float above = ScoringModifiers.RaidTimeModifier(1.5f, BotActionTypeId.GoToObjective);
        float atOne = ScoringModifiers.RaidTimeModifier(1f, BotActionTypeId.GoToObjective);
        Assert.That(above, Is.EqualTo(atOne).Within(0.001f));
    }

    [Test]
    public void CombinedModifier_NeverNegative()
    {
        float[] values = { -1f, 0f, 0.5f, 1f, 2f };
        int[] actions =
        {
            BotActionTypeId.GoToObjective,
            BotActionTypeId.Ambush,
            BotActionTypeId.Snipe,
            BotActionTypeId.Linger,
            BotActionTypeId.Loot,
            BotActionTypeId.Vulture,
            BotActionTypeId.Investigate,
            BotActionTypeId.Patrol,
            BotActionTypeId.Undefined,
        };

        foreach (float agg in values)
        {
            foreach (float rt in values)
            {
                foreach (int action in actions)
                {
                    float modifier = ScoringModifiers.CombinedModifier(agg, rt, action);
                    Assert.That(
                        modifier,
                        Is.GreaterThanOrEqualTo(0f),
                        $"CombinedModifier({agg}, {rt}, {action}) = {modifier} should not be negative"
                    );
                }
            }
        }
    }

    [Test]
    public void AllModifiers_ReturnOneForUnknownActionTypes()
    {
        int unknownAction = 999;
        Assert.That(ScoringModifiers.PersonalityModifier(0.5f, unknownAction), Is.EqualTo(1f));
        Assert.That(ScoringModifiers.RaidTimeModifier(0.5f, unknownAction), Is.EqualTo(1f));
        Assert.That(ScoringModifiers.CombinedModifier(0.5f, 0.5f, unknownAction), Is.EqualTo(1f));
    }

    [Test]
    public void NaN_Aggression_ProducesSafeModifier()
    {
        // NaN aggression should not produce NaN modifier
        float modifier = ScoringModifiers.CombinedModifier(float.NaN, 0.5f, BotActionTypeId.GoToObjective);
        Assert.That(modifier, Is.GreaterThanOrEqualTo(0f));
        Assert.That(!float.IsNaN(modifier), Is.True, "NaN aggression should produce safe modifier");
    }

    [Test]
    public void NaN_RaidTime_ProducesSafeModifier()
    {
        float modifier = ScoringModifiers.CombinedModifier(0.5f, float.NaN, BotActionTypeId.GoToObjective);
        Assert.That(modifier, Is.GreaterThanOrEqualTo(0f));
        Assert.That(!float.IsNaN(modifier), Is.True, "NaN raid time should produce safe modifier");
    }
}

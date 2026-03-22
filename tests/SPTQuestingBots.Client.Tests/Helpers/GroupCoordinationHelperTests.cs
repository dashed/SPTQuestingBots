using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Client.Tests.Helpers;

/// <summary>
/// Tests for GroupCoordinationHelper constants, GroupTacticType values,
/// and GoToObjectiveTask.TacticModifier pure-logic scoring.
/// BSG API methods (TrySendFollowRequest, etc.) require Unity runtime and
/// are tested via integration tests at the behavioral level.
/// </summary>
[TestFixture]
public class GroupCoordinationHelperTests
{
    // ── GroupTacticType Constants ─────────────────────────────────────

    [Test]
    public void GroupTacticType_None_IsZero()
    {
        Assert.That(GroupTacticType.None, Is.EqualTo(0));
    }

    [Test]
    public void GroupTacticType_Attack_IsOne()
    {
        Assert.That(GroupTacticType.Attack, Is.EqualTo(1));
    }

    [Test]
    public void GroupTacticType_Ambush_IsTwo()
    {
        Assert.That(GroupTacticType.Ambush, Is.EqualTo(2));
    }

    [Test]
    public void GroupTacticType_Protect_IsThree()
    {
        Assert.That(GroupTacticType.Protect, Is.EqualTo(3));
    }

    [Test]
    public void GroupTacticType_AllValuesAreUnique()
    {
        var values = new[] { GroupTacticType.None, GroupTacticType.Attack, GroupTacticType.Ambush, GroupTacticType.Protect };
        Assert.That(values, Is.Unique);
    }

    // ── TacticModifier Scoring ────────────────────────────────────────

    [Test]
    public void TacticModifier_NoTactic_ReturnsOne()
    {
        var entity = new BotEntity(1);
        entity.GroupTactic = GroupTacticType.None;

        float result = GoToObjectiveTask.TacticModifier(entity, 100f);
        Assert.That(result, Is.EqualTo(1f));
    }

    [Test]
    public void TacticModifier_Attack_BoostsScore()
    {
        var entity = new BotEntity(1);
        entity.GroupTactic = GroupTacticType.Attack;

        float result = GoToObjectiveTask.TacticModifier(entity, 100f);
        Assert.That(result, Is.EqualTo(1.1f).Within(0.01f));
    }

    [Test]
    public void TacticModifier_Ambush_ReducesScore()
    {
        var entity = new BotEntity(1);
        entity.GroupTactic = GroupTacticType.Ambush;

        float result = GoToObjectiveTask.TacticModifier(entity, 100f);
        Assert.That(result, Is.EqualTo(0.8f).Within(0.01f));
    }

    [Test]
    public void TacticModifier_Protect_PenalizesCloseDistance()
    {
        var entity = new BotEntity(1);
        entity.GroupTactic = GroupTacticType.Protect;

        float resultClose = GoToObjectiveTask.TacticModifier(entity, 10f);
        Assert.That(resultClose, Is.GreaterThan(0.5f), "Close distance should have mild penalty");
        Assert.That(resultClose, Is.LessThan(1.0f), "Close distance should still be penalized");
    }

    [Test]
    public void TacticModifier_Protect_PenalizesDistantObjectivesHeavily()
    {
        var entity = new BotEntity(1);
        entity.GroupTactic = GroupTacticType.Protect;

        float resultFar = GoToObjectiveTask.TacticModifier(entity, 200f);
        Assert.That(resultFar, Is.LessThan(0.3f), "Far distance should have heavy penalty");
        Assert.That(resultFar, Is.GreaterThanOrEqualTo(0.2f), "Penalty should be clamped at 0.2");
    }

    [Test]
    public void TacticModifier_Protect_ZeroDistance_ReturnsOne()
    {
        var entity = new BotEntity(1);
        entity.GroupTactic = GroupTacticType.Protect;

        float result = GoToObjectiveTask.TacticModifier(entity, 0f);
        Assert.That(result, Is.EqualTo(1f).Within(0.01f));
    }

    [Test]
    public void TacticModifier_Protect_VeryLargeDistance_ClampedAtMinimum()
    {
        var entity = new BotEntity(1);
        entity.GroupTactic = GroupTacticType.Protect;

        float result = GoToObjectiveTask.TacticModifier(entity, 10000f);
        Assert.That(result, Is.EqualTo(0.2f).Within(0.01f));
    }

    [Test]
    public void TacticModifier_UnknownTactic_ReturnsOne()
    {
        var entity = new BotEntity(1);
        entity.GroupTactic = 99; // Unknown value

        float result = GoToObjectiveTask.TacticModifier(entity, 100f);
        Assert.That(result, Is.EqualTo(1f));
    }

    // ── GoToObjectiveTask.Score with TacticModifier ──────────────────

    [Test]
    public void GoToObjectiveScore_WithProtectTactic_LowersScoreForFarObjective()
    {
        var entity = new BotEntity(1);
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 200f;
        entity.NavMeshDistanceToObjective = float.MaxValue;
        entity.CurrentQuestAction = 1; // MoveToPosition
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;

        // Score without tactic
        entity.GroupTactic = GroupTacticType.None;
        float scoreNone = GoToObjectiveTask.Score(entity);

        // Score with Protect tactic
        entity.GroupTactic = GroupTacticType.Protect;
        float scoreProtect = GoToObjectiveTask.Score(entity);

        Assert.That(scoreProtect, Is.LessThan(scoreNone), "Protect tactic should reduce score");
    }

    [Test]
    public void GoToObjectiveScore_WithAttackTactic_BoostsScoreForFarObjective()
    {
        var entity = new BotEntity(1);
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 100f;
        entity.NavMeshDistanceToObjective = float.MaxValue;
        entity.CurrentQuestAction = 1; // MoveToPosition
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;

        entity.GroupTactic = GroupTacticType.None;
        float scoreNone = GoToObjectiveTask.Score(entity);

        entity.GroupTactic = GroupTacticType.Attack;
        float scoreAttack = GoToObjectiveTask.Score(entity);

        Assert.That(scoreAttack, Is.GreaterThan(scoreNone), "Attack tactic should boost score");
    }

    // ── BotEntity Fields ─────────────────────────────────────────────

    [Test]
    public void BotEntity_GroupTactic_DefaultsToZero()
    {
        var entity = new BotEntity(1);
        Assert.That(entity.GroupTactic, Is.EqualTo(0));
    }

    [Test]
    public void BotEntity_FollowerIndex_DefaultsToNegativeOne()
    {
        var entity = new BotEntity(1);
        Assert.That(entity.FollowerIndex, Is.EqualTo(-1));
    }

    // ── Formation Follower Index ─────────────────────────────────────

    [Test]
    public void FollowerIndex_CanBeAssigned()
    {
        var entity = new BotEntity(1);
        entity.FollowerIndex = 2;
        Assert.That(entity.FollowerIndex, Is.EqualTo(2));
    }

    [Test]
    public void FollowerIndex_NegativeOne_MeansNotAFollower()
    {
        var entity = new BotEntity(1);
        Assert.That(entity.FollowerIndex, Is.EqualTo(-1));
        Assert.That(entity.FollowerIndex < 0, Is.True, "Negative index means not a follower");
    }
}

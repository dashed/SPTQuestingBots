using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

[TestFixture]
public class HoldTacticalPositionTaskTests
{
    private static BotEntity CreateFollowerWithTacticalPosition(float posX, float posY, float posZ, float tactX, float tactY, float tactZ)
    {
        var boss = new BotEntity(0);
        var entity = new BotEntity(1);
        entity.Boss = boss;
        entity.HasTacticalPosition = true;
        entity.CurrentPositionX = posX;
        entity.CurrentPositionY = posY;
        entity.CurrentPositionZ = posZ;
        entity.TacticalPositionX = tactX;
        entity.TacticalPositionY = tactY;
        entity.TacticalPositionZ = tactZ;
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];
        return entity;
    }

    [Test]
    public void Score_NoTacticalPosition_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.Boss = new BotEntity(1);
        entity.HasTacticalPosition = false;
        Assert.AreEqual(0f, HoldTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Score_NoBoss_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.HasTacticalPosition = true;
        entity.TacticalPositionX = 1f;
        Assert.AreEqual(0f, HoldTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Score_FarFromPosition_ReturnsZero()
    {
        // Distance = 10m (over 3m threshold)
        var entity = CreateFollowerWithTacticalPosition(0, 0, 0, 10, 0, 0);
        Assert.AreEqual(0f, HoldTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Score_CloseToPosition_ReturnsBaseScore()
    {
        // Distance = 2m (under 3m threshold)
        var entity = CreateFollowerWithTacticalPosition(0, 0, 0, 2, 0, 0);
        Assert.AreEqual(HoldTacticalPositionTask.BaseScore, HoldTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Score_ExactlyAtThreshold_ReturnsBaseScore()
    {
        // Distance = 3m exactly => sqrDist = 9 = MinDistanceSqr => NOT > threshold => returns BaseScore
        var entity = CreateFollowerWithTacticalPosition(0, 0, 0, 3, 0, 0);
        Assert.AreEqual(HoldTacticalPositionTask.BaseScore, HoldTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Score_JustOverThreshold_ReturnsZero()
    {
        // Distance = 3.1m => sqrDist = 9.61 > 9
        var entity = CreateFollowerWithTacticalPosition(0, 0, 0, 3.1f, 0, 0);
        Assert.AreEqual(0f, HoldTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Score_AtSamePosition_ReturnsBaseScore()
    {
        var entity = CreateFollowerWithTacticalPosition(5, 10, 15, 5, 10, 15);
        Assert.AreEqual(HoldTacticalPositionTask.BaseScore, HoldTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Properties_CorrectValues()
    {
        var task = new HoldTacticalPositionTask();
        Assert.AreEqual(BotActionTypeId.HoldPosition, task.BotActionTypeId);
        Assert.AreEqual("HoldTacticalPosition", task.ActionReason);
        Assert.AreEqual(0.10f, task.Hysteresis, 0.001f);
    }

    [Test]
    public void ScoreEntity_WritesToTaskScores()
    {
        var task = new HoldTacticalPositionTask();
        var entity = CreateFollowerWithTacticalPosition(0, 0, 0, 1, 0, 0);

        task.ScoreEntity(0, entity);

        Assert.AreEqual(HoldTacticalPositionTask.BaseScore, entity.TaskScores[0], 0.001f);
    }

    [Test]
    public void Scenario_FarThenClose_TransitionsCorrectly()
    {
        var manager = SquadTaskFactory.Create();

        var boss = new BotEntity(0);
        var entity = new BotEntity(1);
        entity.Boss = boss;
        entity.HasTacticalPosition = true;
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];

        // Start far away — GoToTacticalPosition wins
        entity.CurrentPositionX = 0;
        entity.TacticalPositionX = 20;
        manager.ScoreAndPick(entity);
        Assert.IsInstanceOf<GoToTacticalPositionTask>(entity.TaskAssignment.Task);

        // Move close — HoldTacticalPosition takes over
        entity.CurrentPositionX = 19;
        manager.ScoreAndPick(entity);

        // GoToTacticalPosition scores 0 (close), HoldTacticalPosition scores 0.65
        // 0.65 > 0 + 0.20 (GoTo hysteresis) => switch
        Assert.IsInstanceOf<HoldTacticalPositionTask>(entity.TaskAssignment.Task);
    }
}

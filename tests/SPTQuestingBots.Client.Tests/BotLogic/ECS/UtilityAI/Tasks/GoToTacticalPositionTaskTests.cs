using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

[TestFixture]
public class GoToTacticalPositionTaskTests
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
        Assert.AreEqual(0f, GoToTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Score_NoBoss_ReturnsZero()
    {
        var entity = new BotEntity(0);
        entity.HasTacticalPosition = true;
        entity.TacticalPositionX = 100f;
        Assert.AreEqual(0f, GoToTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Score_FarFromPosition_ReturnsBaseScore()
    {
        // Distance = 10m (well over 3m threshold)
        var entity = CreateFollowerWithTacticalPosition(0, 0, 0, 10, 0, 0);
        Assert.AreEqual(GoToTacticalPositionTask.BaseScore, GoToTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Score_CloseToPosition_ReturnsZero()
    {
        // Distance = 2m (under 3m threshold)
        var entity = CreateFollowerWithTacticalPosition(0, 0, 0, 2, 0, 0);
        Assert.AreEqual(0f, GoToTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Score_ExactlyAtThreshold_ReturnsZero()
    {
        // Distance = 3m exactly => sqrDist = 9 = MinDistanceSqr => returns 0
        var entity = CreateFollowerWithTacticalPosition(0, 0, 0, 3, 0, 0);
        Assert.AreEqual(0f, GoToTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Score_JustOverThreshold_ReturnsBaseScore()
    {
        // Distance = 3.1m => sqrDist = 9.61 > 9
        var entity = CreateFollowerWithTacticalPosition(0, 0, 0, 3.1f, 0, 0);
        Assert.AreEqual(GoToTacticalPositionTask.BaseScore, GoToTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Score_3DDistance_CalculatedCorrectly()
    {
        // Distance = sqrt(16+16+16) = sqrt(48) ~= 6.93m => sqrDist = 48 > 9
        var entity = CreateFollowerWithTacticalPosition(0, 0, 0, 4, 4, 4);
        Assert.AreEqual(GoToTacticalPositionTask.BaseScore, GoToTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Score_AtSamePosition_ReturnsZero()
    {
        var entity = CreateFollowerWithTacticalPosition(5, 10, 15, 5, 10, 15);
        Assert.AreEqual(0f, GoToTacticalPositionTask.Score(entity));
    }

    [Test]
    public void Properties_CorrectValues()
    {
        var task = new GoToTacticalPositionTask();
        Assert.AreEqual(BotActionTypeId.GoToObjective, task.BotActionTypeId);
        Assert.AreEqual("GoToTacticalPosition", task.ActionReason);
        Assert.AreEqual(0.20f, task.Hysteresis, 0.001f);
    }

    [Test]
    public void ScoreEntity_WritesToTaskScores()
    {
        var task = new GoToTacticalPositionTask();
        var entity = CreateFollowerWithTacticalPosition(0, 0, 0, 10, 0, 0);

        task.ScoreEntity(0, entity);

        Assert.AreEqual(GoToTacticalPositionTask.BaseScore, entity.TaskScores[0], 0.001f);
    }
}

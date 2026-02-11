using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI;

[TestFixture]
public class SquadTaskFactoryTests
{
    [Test]
    public void TaskCount_Is2()
    {
        Assert.AreEqual(2, SquadTaskFactory.TaskCount);
    }

    [Test]
    public void Create_Returns2Tasks()
    {
        var manager = SquadTaskFactory.Create();
        Assert.AreEqual(2, manager.Tasks.Length);
    }

    [Test]
    public void Create_FirstTaskIsGoToTacticalPosition()
    {
        var manager = SquadTaskFactory.Create();
        Assert.IsInstanceOf<GoToTacticalPositionTask>(manager.Tasks[0]);
    }

    [Test]
    public void Create_SecondTaskIsHoldTacticalPosition()
    {
        var manager = SquadTaskFactory.Create();
        Assert.IsInstanceOf<HoldTacticalPositionTask>(manager.Tasks[1]);
    }

    [Test]
    public void Create_ManagerWorksEndToEnd()
    {
        var manager = SquadTaskFactory.Create();

        var boss = new BotEntity(0);
        var follower = new BotEntity(1);
        follower.Boss = boss;
        follower.HasTacticalPosition = true;
        follower.CurrentPositionX = 0;
        follower.TacticalPositionX = 50;
        follower.TaskScores = new float[SquadTaskFactory.TaskCount];

        manager.ScoreAndPick(follower);

        Assert.IsInstanceOf<GoToTacticalPositionTask>(follower.TaskAssignment.Task);
    }
}

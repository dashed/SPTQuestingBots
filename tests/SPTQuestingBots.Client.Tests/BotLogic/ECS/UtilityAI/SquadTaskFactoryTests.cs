using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI;

[TestFixture]
public class SquadTaskFactoryTests
{
    [Test]
    public void TaskCount_Is6()
    {
        Assert.AreEqual(6, SquadTaskFactory.TaskCount);
    }

    [Test]
    public void Create_Returns6Tasks()
    {
        var manager = SquadTaskFactory.Create();
        Assert.AreEqual(6, manager.Tasks.Length);
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
    public void Create_ThirdTaskIsLoot()
    {
        var manager = SquadTaskFactory.Create();
        Assert.IsInstanceOf<LootTask>(manager.Tasks[2]);
    }

    [Test]
    public void Create_FourthTaskIsInvestigate()
    {
        var manager = SquadTaskFactory.Create();
        Assert.IsInstanceOf<InvestigateTask>(manager.Tasks[3]);
    }

    [Test]
    public void Create_FifthTaskIsLinger()
    {
        var manager = SquadTaskFactory.Create();
        Assert.IsInstanceOf<LingerTask>(manager.Tasks[4]);
    }

    [Test]
    public void Create_SixthTaskIsPatrol()
    {
        var manager = SquadTaskFactory.Create();
        Assert.IsInstanceOf<PatrolTask>(manager.Tasks[5]);
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

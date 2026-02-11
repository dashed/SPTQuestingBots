using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS;

[TestFixture]
public class SquadObjectiveTests
{
    private SquadObjective _objective;

    [SetUp]
    public void SetUp()
    {
        _objective = new SquadObjective();
    }

    [Test]
    public void Constructor_Defaults()
    {
        Assert.IsFalse(_objective.HasObjective);
        Assert.IsFalse(_objective.HasPreviousObjective);
        Assert.AreEqual(0, _objective.Version);
        Assert.AreEqual(0, _objective.MemberCount);
        Assert.AreEqual(ObjectiveState.Active, _objective.State);
        Assert.AreEqual(18, _objective.TacticalPositions.Length);
        Assert.AreEqual(6, _objective.MemberRoles.Length);
    }

    [Test]
    public void SetObjective_SetsPositionAndVersion()
    {
        _objective.SetObjective(1f, 2f, 3f);

        Assert.IsTrue(_objective.HasObjective);
        Assert.AreEqual(1f, _objective.ObjectiveX);
        Assert.AreEqual(2f, _objective.ObjectiveY);
        Assert.AreEqual(3f, _objective.ObjectiveZ);
        Assert.AreEqual(1, _objective.Version);
        Assert.AreEqual(ObjectiveState.Active, _objective.State);
    }

    [Test]
    public void SetObjective_SavesPreviousPosition()
    {
        _objective.SetObjective(1f, 2f, 3f);
        _objective.SetObjective(4f, 5f, 6f);

        Assert.IsTrue(_objective.HasPreviousObjective);
        Assert.AreEqual(1f, _objective.PreviousX);
        Assert.AreEqual(2f, _objective.PreviousY);
        Assert.AreEqual(3f, _objective.PreviousZ);
        Assert.AreEqual(4f, _objective.ObjectiveX);
        Assert.AreEqual(5f, _objective.ObjectiveY);
        Assert.AreEqual(6f, _objective.ObjectiveZ);
        Assert.AreEqual(2, _objective.Version);
    }

    [Test]
    public void SetObjective_FirstTime_NoPreviousObjective()
    {
        _objective.SetObjective(1f, 2f, 3f);

        Assert.IsFalse(_objective.HasPreviousObjective);
    }

    [Test]
    public void ClearObjective_ResetsState()
    {
        _objective.SetObjective(1f, 2f, 3f);
        _objective.StartTime = 10f;
        _objective.Duration = 5f;
        _objective.DurationAdjusted = true;
        _objective.SetTacticalPosition(0, 1f, 2f, 3f, SquadRole.Guard);

        _objective.ClearObjective();

        Assert.IsFalse(_objective.HasObjective);
        Assert.AreEqual(0, _objective.MemberCount);
        Assert.AreEqual(0f, _objective.StartTime);
        Assert.AreEqual(0f, _objective.Duration);
        Assert.IsFalse(_objective.DurationAdjusted);
        Assert.AreEqual(ObjectiveState.Active, _objective.State);
        // Version should have incremented (SetObjective=1, ClearObjective=2)
        Assert.AreEqual(2, _objective.Version);
    }

    [Test]
    public void IncrementVersion_IncreasesVersion()
    {
        Assert.AreEqual(0, _objective.Version);

        _objective.IncrementVersion();
        Assert.AreEqual(1, _objective.Version);

        _objective.IncrementVersion();
        Assert.AreEqual(2, _objective.Version);
    }

    [Test]
    public void SetTacticalPosition_SetsPositionAndRole()
    {
        _objective.SetTacticalPosition(0, 10f, 20f, 30f, SquadRole.Guard);

        Assert.AreEqual(10f, _objective.TacticalPositions[0]);
        Assert.AreEqual(20f, _objective.TacticalPositions[1]);
        Assert.AreEqual(30f, _objective.TacticalPositions[2]);
        Assert.AreEqual(SquadRole.Guard, _objective.MemberRoles[0]);
        Assert.AreEqual(1, _objective.MemberCount);
    }

    [Test]
    public void SetTacticalPosition_MultipleMembers()
    {
        _objective.SetTacticalPosition(0, 1f, 2f, 3f, SquadRole.Leader);
        _objective.SetTacticalPosition(1, 4f, 5f, 6f, SquadRole.Guard);
        _objective.SetTacticalPosition(2, 7f, 8f, 9f, SquadRole.Flanker);

        Assert.AreEqual(3, _objective.MemberCount);

        // Check member 2
        Assert.AreEqual(7f, _objective.TacticalPositions[6]);
        Assert.AreEqual(8f, _objective.TacticalPositions[7]);
        Assert.AreEqual(9f, _objective.TacticalPositions[8]);
        Assert.AreEqual(SquadRole.Flanker, _objective.MemberRoles[2]);
    }

    [Test]
    public void SetTacticalPosition_OutOfRange_Ignored()
    {
        _objective.SetTacticalPosition(-1, 1f, 2f, 3f, SquadRole.Guard);
        _objective.SetTacticalPosition(6, 1f, 2f, 3f, SquadRole.Guard);

        Assert.AreEqual(0, _objective.MemberCount);
    }

    [Test]
    public void SetTacticalPosition_UpdatesMemberCount_OnlyForward()
    {
        _objective.SetTacticalPosition(2, 1f, 2f, 3f, SquadRole.Flanker);
        Assert.AreEqual(3, _objective.MemberCount); // index 2 → count = 3

        _objective.SetTacticalPosition(0, 4f, 5f, 6f, SquadRole.Leader);
        Assert.AreEqual(3, _objective.MemberCount); // count stays at 3, not reduced to 1
    }
}

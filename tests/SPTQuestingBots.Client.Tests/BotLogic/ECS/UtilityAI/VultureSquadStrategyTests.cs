using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI;

[TestFixture]
public class VultureSquadStrategyTests
{
    // ── Score ─────────────────────────────────────────────

    [Test]
    public void Score_NoLeader_ReturnsZero()
    {
        var squad = MakeSquad(strategyCount: 2);
        squad.Leader = null;
        float score = VultureSquadStrategy.Score(squad);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_LeaderVulturePhaseNone_ReturnsZero()
    {
        var squad = MakeSquad(strategyCount: 2);
        squad.Leader.VulturePhase = VulturePhase.None;
        squad.Leader.HasNearbyEvent = true;
        float score = VultureSquadStrategy.Score(squad);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_LeaderVulturePhaseComplete_ReturnsZero()
    {
        var squad = MakeSquad(strategyCount: 2);
        squad.Leader.VulturePhase = VulturePhase.Complete;
        squad.Leader.HasNearbyEvent = true;
        float score = VultureSquadStrategy.Score(squad);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_LeaderVultureApproach_ReturnsBaseScore()
    {
        var squad = MakeSquad(strategyCount: 2);
        squad.Leader.VulturePhase = VulturePhase.Approach;
        squad.Leader.HasNearbyEvent = true;
        float score = VultureSquadStrategy.Score(squad);
        Assert.That(score, Is.EqualTo(VultureSquadStrategy.BaseScore));
    }

    [Test]
    public void Score_LeaderVultureHoldAmbush_ReturnsBaseScore()
    {
        var squad = MakeSquad(strategyCount: 2);
        squad.Leader.VulturePhase = VulturePhase.HoldAmbush;
        squad.Leader.HasNearbyEvent = true;
        float score = VultureSquadStrategy.Score(squad);
        Assert.That(score, Is.EqualTo(VultureSquadStrategy.BaseScore));
    }

    [Test]
    public void Score_LeaderNoNearbyEvent_ReturnsZero()
    {
        var squad = MakeSquad(strategyCount: 2);
        squad.Leader.VulturePhase = VulturePhase.Approach;
        squad.Leader.HasNearbyEvent = false;
        float score = VultureSquadStrategy.Score(squad);
        Assert.That(score, Is.EqualTo(0f));
    }

    // ── ScoreSquad ────────────────────────────────────────

    [Test]
    public void ScoreSquad_WritesToStrategyScores()
    {
        var squad = MakeSquad(strategyCount: 2);
        squad.Leader.VulturePhase = VulturePhase.SilentApproach;
        squad.Leader.HasNearbyEvent = true;

        var strategy = new VultureSquadStrategy();
        strategy.ScoreSquad(1, squad);
        Assert.That(squad.StrategyScores[1], Is.EqualTo(VultureSquadStrategy.BaseScore));
    }

    // ── Update: Position Assignment ───────────────────────

    [Test]
    public void Update_AssignsPositionsToFollowers()
    {
        var squad = MakeSquadWithFollowers(2);
        squad.Leader.VulturePhase = VulturePhase.Approach;
        squad.Leader.HasNearbyEvent = true;
        squad.Leader.NearbyEventX = 100f;
        squad.Leader.NearbyEventY = 0f;
        squad.Leader.NearbyEventZ = 100f;
        squad.Leader.CurrentPositionX = 50f;
        squad.Leader.CurrentPositionZ = 50f;

        var strategy = new VultureSquadStrategy();
        strategy.Activate(squad);
        strategy.Update();

        // Both followers should have tactical positions
        var f1 = squad.Members[1];
        var f2 = squad.Members[2];
        Assert.IsTrue(f1.HasTacticalPosition);
        Assert.IsTrue(f2.HasTacticalPosition);
    }

    [Test]
    public void Update_FollowerPositions_AreDifferent()
    {
        var squad = MakeSquadWithFollowers(2);
        squad.Leader.VulturePhase = VulturePhase.Approach;
        squad.Leader.HasNearbyEvent = true;
        squad.Leader.NearbyEventX = 100f;
        squad.Leader.NearbyEventY = 0f;
        squad.Leader.NearbyEventZ = 100f;
        squad.Leader.CurrentPositionX = 50f;
        squad.Leader.CurrentPositionZ = 50f;

        var strategy = new VultureSquadStrategy();
        strategy.Activate(squad);
        strategy.Update();

        var f1 = squad.Members[1];
        var f2 = squad.Members[2];
        bool xDiff = f1.TacticalPositionX != f2.TacticalPositionX;
        bool zDiff = f1.TacticalPositionZ != f2.TacticalPositionZ;
        Assert.IsTrue(xDiff || zDiff, "Followers should get different positions");
    }

    [Test]
    public void Update_AssignsFlankerRoles()
    {
        var squad = MakeSquadWithFollowers(2);
        squad.Leader.VulturePhase = VulturePhase.Rush;
        squad.Leader.HasNearbyEvent = true;
        squad.Leader.NearbyEventX = 100f;
        squad.Leader.NearbyEventY = 0f;
        squad.Leader.NearbyEventZ = 100f;
        squad.Leader.CurrentPositionX = 50f;
        squad.Leader.CurrentPositionZ = 50f;

        var strategy = new VultureSquadStrategy();
        strategy.Activate(squad);
        strategy.Update();

        var f1 = squad.Members[1];
        var f2 = squad.Members[2];
        // With 2 followers, both get Flanker
        Assert.That(f1.SquadRole, Is.EqualTo(SquadRole.Flanker));
        Assert.That(f2.SquadRole, Is.EqualTo(SquadRole.Flanker));
    }

    [Test]
    public void Update_SingleFollower_GetsFlankerRole()
    {
        var squad = MakeSquadWithFollowers(1);
        squad.Leader.VulturePhase = VulturePhase.Approach;
        squad.Leader.HasNearbyEvent = true;
        squad.Leader.NearbyEventX = 100f;
        squad.Leader.NearbyEventY = 0f;
        squad.Leader.NearbyEventZ = 100f;
        squad.Leader.CurrentPositionX = 50f;
        squad.Leader.CurrentPositionZ = 50f;

        var strategy = new VultureSquadStrategy();
        strategy.Activate(squad);
        strategy.Update();

        var f1 = squad.Members[1];
        Assert.IsTrue(f1.HasTacticalPosition);
        Assert.That(f1.SquadRole, Is.EqualTo(SquadRole.Flanker));
    }

    [Test]
    public void Update_NoFollowers_NoCrash()
    {
        var squad = MakeSquad(strategyCount: 2);
        squad.Leader.VulturePhase = VulturePhase.Approach;
        squad.Leader.HasNearbyEvent = true;
        squad.Leader.NearbyEventX = 100f;
        squad.Leader.NearbyEventY = 0f;
        squad.Leader.NearbyEventZ = 100f;
        squad.Leader.CurrentPositionX = 50f;
        squad.Leader.CurrentPositionZ = 50f;

        var strategy = new VultureSquadStrategy();
        strategy.Activate(squad);
        Assert.DoesNotThrow(() => strategy.Update());
    }

    [Test]
    public void Update_NoNearbyEvent_NoPositions()
    {
        var squad = MakeSquadWithFollowers(1);
        squad.Leader.VulturePhase = VulturePhase.Approach;
        squad.Leader.HasNearbyEvent = false;

        var strategy = new VultureSquadStrategy();
        strategy.Activate(squad);
        strategy.Update();

        var f1 = squad.Members[1];
        Assert.IsFalse(f1.HasTacticalPosition);
    }

    [Test]
    public void Update_ThreeFollowers_MiddleGetsGuard()
    {
        var squad = MakeSquadWithFollowers(3);
        squad.Leader.VulturePhase = VulturePhase.SilentApproach;
        squad.Leader.HasNearbyEvent = true;
        squad.Leader.NearbyEventX = 100f;
        squad.Leader.NearbyEventY = 0f;
        squad.Leader.NearbyEventZ = 100f;
        squad.Leader.CurrentPositionX = 50f;
        squad.Leader.CurrentPositionZ = 50f;

        var strategy = new VultureSquadStrategy();
        strategy.Activate(squad);
        strategy.Update();

        var f1 = squad.Members[1];
        var f2 = squad.Members[2];
        var f3 = squad.Members[3];
        Assert.That(f1.SquadRole, Is.EqualTo(SquadRole.Flanker));
        Assert.That(f2.SquadRole, Is.EqualTo(SquadRole.Guard));
        Assert.That(f3.SquadRole, Is.EqualTo(SquadRole.Flanker));
    }

    // ── Helpers ───────────────────────────────────────────

    private static SquadEntity MakeSquad(int strategyCount)
    {
        var squad = new SquadEntity(0, strategyCount, 4);
        var leader = new BotEntity(0);
        squad.Leader = leader;
        squad.Members.Add(leader);
        return squad;
    }

    private static SquadEntity MakeSquadWithFollowers(int followerCount)
    {
        var squad = MakeSquad(strategyCount: 2);
        for (int i = 0; i < followerCount; i++)
        {
            var follower = new BotEntity(i + 1);
            follower.Boss = squad.Leader;
            squad.Members.Add(follower);
            squad.Leader.Followers.Add(follower);
        }
        return squad;
    }
}

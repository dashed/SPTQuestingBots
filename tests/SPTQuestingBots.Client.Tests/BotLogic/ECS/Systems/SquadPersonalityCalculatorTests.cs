using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

[TestFixture]
public class SquadPersonalityCalculatorTests
{
    [Test]
    public void AllBoss_ReturnsElite()
    {
        var types = new[] { BotType.Boss, BotType.Boss, BotType.Boss };
        var result = SquadPersonalityCalculator.DeterminePersonality(types, 3);

        Assert.AreEqual(SquadPersonalityType.Elite, result);
    }

    [Test]
    public void AllPMC_ReturnsGigaChads()
    {
        var types = new[] { BotType.PMC, BotType.PMC, BotType.PMC };
        var result = SquadPersonalityCalculator.DeterminePersonality(types, 3);

        Assert.AreEqual(SquadPersonalityType.GigaChads, result);
    }

    [Test]
    public void AllScav_ReturnsRats()
    {
        var types = new[] { BotType.Scav, BotType.Scav };
        var result = SquadPersonalityCalculator.DeterminePersonality(types, 2);

        Assert.AreEqual(SquadPersonalityType.Rats, result);
    }

    [Test]
    public void AllPScav_ReturnsTimmyTeam6()
    {
        var types = new[] { BotType.PScav, BotType.PScav, BotType.PScav };
        var result = SquadPersonalityCalculator.DeterminePersonality(types, 3);

        Assert.AreEqual(SquadPersonalityType.TimmyTeam6, result);
    }

    [Test]
    public void MixedPMCAndScav_MajorityWins()
    {
        var types = new[] { BotType.PMC, BotType.PMC, BotType.Scav };
        var result = SquadPersonalityCalculator.DeterminePersonality(types, 3);

        Assert.AreEqual(SquadPersonalityType.GigaChads, result);
    }

    [Test]
    public void Tie_HigherPersonalityWins()
    {
        // 1 PMC (GigaChads=3) vs 1 Scav (Rats=2) — tie, higher enum wins
        var types = new[] { BotType.PMC, BotType.Scav };
        var result = SquadPersonalityCalculator.DeterminePersonality(types, 2);

        Assert.AreEqual(SquadPersonalityType.GigaChads, result);
    }

    [Test]
    public void AllUnknown_ReturnsNone()
    {
        var types = new[] { BotType.Unknown, BotType.Unknown };
        var result = SquadPersonalityCalculator.DeterminePersonality(types, 2);

        Assert.AreEqual(SquadPersonalityType.None, result);
    }

    [Test]
    public void EmptyArray_ReturnsNone()
    {
        var types = new BotType[0];
        var result = SquadPersonalityCalculator.DeterminePersonality(types, 0);

        Assert.AreEqual(SquadPersonalityType.None, result);
    }

    [Test]
    public void SingleBoss_ReturnsElite()
    {
        var types = new[] { BotType.Boss };
        var result = SquadPersonalityCalculator.DeterminePersonality(types, 1);

        Assert.AreEqual(SquadPersonalityType.Elite, result);
    }

    [Test]
    public void MixedBossAndPMC_BossWinsTie()
    {
        // 1 Boss (Elite=4) vs 1 PMC (GigaChads=3) — tie, Elite > GigaChads
        var types = new[] { BotType.Boss, BotType.PMC };
        var result = SquadPersonalityCalculator.DeterminePersonality(types, 2);

        Assert.AreEqual(SquadPersonalityType.Elite, result);
    }
}

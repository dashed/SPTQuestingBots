using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

[TestFixture]
public class SquadCalloutIdTests
{
    [Test]
    public void NoneIsZero()
    {
        Assert.AreEqual(0, SquadCalloutId.None);
    }

    [Test]
    public void AllConstantsUnique()
    {
        var seen = new HashSet<int>();
        var fields = typeof(SquadCalloutId).GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in fields)
        {
            int value = (int)field.GetValue(null);
            Assert.IsTrue(seen.Add(value), $"Duplicate value {value} for {field.Name}");
        }
    }

    [Test]
    public void AllNonNoneValuesPositive()
    {
        var fields = typeof(SquadCalloutId).GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in fields)
        {
            if (field.Name == nameof(SquadCalloutId.None))
            {
                continue;
            }

            int value = (int)field.GetValue(null);
            Assert.Greater(value, 0, $"{field.Name} should be positive");
        }
    }

    [Test]
    public void KnownValues()
    {
        Assert.AreEqual(1, SquadCalloutId.FollowMe);
        Assert.AreEqual(3, SquadCalloutId.HoldPosition);
        Assert.AreEqual(7, SquadCalloutId.OnSix);
        Assert.AreEqual(13, SquadCalloutId.OnFirstContact);
    }

    [Test]
    public void HasExpectedCount()
    {
        var fields = typeof(SquadCalloutId).GetFields(BindingFlags.Public | BindingFlags.Static);
        Assert.AreEqual(14, fields.Length);
    }
}

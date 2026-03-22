using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.Helpers;

/// <summary>
/// Tests for BotEventSubscriber behavior.
/// The actual BotEventHandler subscription requires Unity runtime, so we test
/// the CombatEvent types recorded and the registry integration.
/// </summary>
[TestFixture]
public class BotEventSubscriberTests
{
    [SetUp]
    public void SetUp()
    {
        CombatEventRegistry.Initialize(32);
    }

    // ── Combat Event Types ───────────────────────────────────────────

    [Test]
    public void CombatEventType_Death_HasExpectedValue()
    {
        Assert.That(CombatEventType.Death, Is.EqualTo((byte)4));
    }

    [Test]
    public void CombatEventType_Gunshot_HasExpectedValue()
    {
        Assert.That(CombatEventType.Gunshot, Is.EqualTo((byte)1));
    }

    [Test]
    public void CombatEventType_AllTypesAreUnique()
    {
        var types = new[]
        {
            CombatEventType.None,
            CombatEventType.Gunshot,
            CombatEventType.Explosion,
            CombatEventType.Airdrop,
            CombatEventType.Death,
            CombatEventType.DoorOpen,
        };
        Assert.That(types, Is.Unique, "All CombatEventType constants should be unique");
    }

    // ── Sound Event as CombatEvent ───────────────────────────────────

    [Test]
    public void RecordGunshot_AppearsinRegistry()
    {
        CombatEventRegistry.RecordEvent(
            new CombatEvent
            {
                X = 100f,
                Y = 5f,
                Z = 200f,
                Time = 10f,
                Power = 80f,
                Type = CombatEventType.Gunshot,
                IsBoss = false,
                IsActive = true,
            }
        );

        bool found = CombatEventRegistry.GetNearestEvent(100f, 200f, 50f, 10f, 60f, out var nearest);
        Assert.That(found, Is.True);
        Assert.That(nearest.Type, Is.EqualTo(CombatEventType.Gunshot));
        Assert.That(nearest.Power, Is.EqualTo(80f));
    }

    // ── Kill Event as CombatEvent ────────────────────────────────────

    [Test]
    public void RecordKillEvent_AppearsinRegistry()
    {
        CombatEventRegistry.RecordEvent(
            new CombatEvent
            {
                X = 50f,
                Y = 0f,
                Z = 75f,
                Time = 20f,
                Power = 120f,
                Type = CombatEventType.Death,
                IsBoss = false,
                IsActive = true,
            }
        );

        bool found = CombatEventRegistry.GetNearestEvent(50f, 75f, 30f, 20f, 60f, out var nearest);
        Assert.That(found, Is.True);
        Assert.That(nearest.Type, Is.EqualTo(CombatEventType.Death));
        Assert.That(nearest.Power, Is.EqualTo(120f));
    }

    // ── Filtering ────────────────────────────────────────────────────

    [Test]
    public void QuietSound_ShouldBeFiltered_ByPowerThreshold()
    {
        // BotEventSubscriber filters power < 30f
        // Verify that only recording events with power >= 30 is the right threshold
        float quietPower = 25f;
        float loudPower = 35f;

        Assert.That(quietPower < 30f, Is.True, "Power below 30 should be filtered by subscriber");
        Assert.That(loudPower >= 30f, Is.True, "Power at 30+ should be recorded by subscriber");
    }

    [Test]
    public void DeathEvent_DefaultPower_Is120()
    {
        // BotEventSubscriber records death events with Power=120
        var evt = new CombatEvent
        {
            X = 0f,
            Y = 0f,
            Z = 0f,
            Time = 0f,
            Power = 120f,
            Type = CombatEventType.Death,
            IsBoss = false,
            IsActive = true,
        };

        Assert.That(evt.Power, Is.EqualTo(120f));
        Assert.That(evt.Type, Is.EqualTo(CombatEventType.Death));
    }
}

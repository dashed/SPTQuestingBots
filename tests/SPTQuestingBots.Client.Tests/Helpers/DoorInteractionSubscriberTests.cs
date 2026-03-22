using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.Helpers;

[TestFixture]
public class DoorInteractionSubscriberTests
{
    // DoorInteractionSubscriber uses Unity's Time.time which is not available in test context.
    // We test the pure-logic CombatEventType.DoorOpen constant and config deserialization instead.

    [Test]
    public void CombatEventType_DoorOpen_HasExpectedValue()
    {
        Assert.That(CombatEventType.DoorOpen, Is.EqualTo((byte)5));
    }

    [Test]
    public void CombatEventType_DoorOpen_IsDistinctFromOtherTypes()
    {
        var values = new[]
        {
            CombatEventType.None,
            CombatEventType.Gunshot,
            CombatEventType.Explosion,
            CombatEventType.Airdrop,
            CombatEventType.Death,
            CombatEventType.DoorOpen,
        };

        Assert.That(values, Is.Unique, "All CombatEventType constants should be unique");
    }

    [Test]
    public void CombatEventRegistry_AcceptsDoorOpenEvents()
    {
        CombatEventRegistry.Initialize(16);

        CombatEventRegistry.RecordEvent(
            new CombatEvent
            {
                X = 10f,
                Y = 0f,
                Z = 20f,
                Time = 100f,
                Power = 50f,
                Type = CombatEventType.DoorOpen,
                IsBoss = false,
                IsActive = true,
            }
        );

        Assert.That(CombatEventRegistry.Count, Is.EqualTo(1));

        bool found = CombatEventRegistry.GetNearestEvent(10f, 20f, 100f, 100f, 60f, out var nearest);
        Assert.That(found, Is.True);
        Assert.That(nearest.Type, Is.EqualTo(CombatEventType.DoorOpen));
        Assert.That(nearest.Power, Is.EqualTo(50f));
    }

    [Test]
    public void CombatEventRegistry_DoorOpenDoesNotCountAsExplosionIntensity()
    {
        CombatEventRegistry.Initialize(16);

        CombatEventRegistry.RecordEvent(
            new CombatEvent
            {
                X = 10f,
                Y = 0f,
                Z = 20f,
                Time = 100f,
                Power = 50f,
                Type = CombatEventType.DoorOpen,
                IsBoss = false,
                IsActive = true,
            }
        );

        // DoorOpen should count as 1 event intensity (not 3 like explosions)
        int intensity = CombatEventRegistry.GetIntensity(10f, 20f, 100f, 60f, 100f);
        Assert.That(intensity, Is.EqualTo(1), "DoorOpen should count as 1 event, not 3 like explosions");
    }
}

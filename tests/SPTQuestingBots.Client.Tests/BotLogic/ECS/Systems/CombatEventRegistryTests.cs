using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

[TestFixture]
public class CombatEventRegistryTests
{
    [SetUp]
    public void SetUp()
    {
        CombatEventRegistry.Initialize(128);
    }

    [TearDown]
    public void TearDown()
    {
        CombatEventRegistry.Clear();
    }

    // ── RecordEvent ─────────────────────────────────────────

    [Test]
    public void RecordEvent_SingleEvent_CountIsOne()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 20f, 1.0f, 100f, CombatEventType.Gunshot, false);
        Assert.That(CombatEventRegistry.Count, Is.EqualTo(1));
    }

    [Test]
    public void RecordEvent_MultipleEvents_CountIncreases()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 20f, 1.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(30f, 0f, 40f, 2.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(50f, 0f, 60f, 3.0f, 150f, CombatEventType.Explosion, false);
        Assert.That(CombatEventRegistry.Count, Is.EqualTo(3));
    }

    [Test]
    public void RecordEvent_OverflowWraps_CountCapsAtCapacity()
    {
        CombatEventRegistry.Initialize(4);
        for (int i = 0; i < 10; i++)
        {
            CombatEventRegistry.RecordEvent((float)i, 0f, (float)i, (float)i, 100f, CombatEventType.Gunshot, false);
        }
        Assert.That(CombatEventRegistry.Count, Is.EqualTo(4));
    }

    [Test]
    public void RecordEvent_OverflowOverwritesOldest()
    {
        CombatEventRegistry.Initialize(2);
        // Event 1: at (10, 10)
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        // Event 2: at (20, 20)
        CombatEventRegistry.RecordEvent(20f, 0f, 20f, 2.0f, 100f, CombatEventType.Gunshot, false);
        // Event 3: at (30, 30) — should overwrite Event 1
        CombatEventRegistry.RecordEvent(30f, 0f, 30f, 3.0f, 100f, CombatEventType.Gunshot, false);

        // Query near (10, 10) — Event 1 should be gone
        bool found = CombatEventRegistry.GetNearestEvent(10f, 10f, 5f, 4.0f, 300f, out _);
        Assert.IsFalse(found);

        // Query near (30, 30) — Event 3 should be found
        found = CombatEventRegistry.GetNearestEvent(30f, 30f, 5f, 4.0f, 300f, out var evt);
        Assert.IsTrue(found);
        Assert.That(evt.Time, Is.EqualTo(3.0f));
    }

    // ── GetNearestEvent ─────────────────────────────────────

    [Test]
    public void GetNearestEvent_EmptyBuffer_ReturnsFalse()
    {
        bool found = CombatEventRegistry.GetNearestEvent(0f, 0f, 1000f, 1.0f, 300f, out _);
        Assert.IsFalse(found);
    }

    [Test]
    public void GetNearestEvent_EventInRange_ReturnsTrue()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);

        bool found = CombatEventRegistry.GetNearestEvent(12f, 12f, 50f, 2.0f, 300f, out var evt);
        Assert.IsTrue(found);
        Assert.That(evt.X, Is.EqualTo(10f));
        Assert.That(evt.Z, Is.EqualTo(10f));
    }

    [Test]
    public void GetNearestEvent_EventOutOfRange_ReturnsFalse()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 1.0f, 100f, CombatEventType.Gunshot, false);

        bool found = CombatEventRegistry.GetNearestEvent(0f, 0f, 10f, 2.0f, 300f, out _);
        Assert.IsFalse(found);
    }

    [Test]
    public void GetNearestEvent_MultipleEvents_ReturnsClosest()
    {
        CombatEventRegistry.RecordEvent(50f, 0f, 50f, 1.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(15f, 0f, 15f, 2.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(80f, 0f, 80f, 3.0f, 100f, CombatEventType.Gunshot, false);

        bool found = CombatEventRegistry.GetNearestEvent(10f, 10f, 200f, 4.0f, 300f, out var evt);
        Assert.IsTrue(found);
        Assert.That(evt.X, Is.EqualTo(15f)); // Closest to (10,10)
    }

    [Test]
    public void GetNearestEvent_ExpiredEvent_Skipped()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);

        // maxAge=5, currentTime=100 → event age is 99, well past maxAge
        bool found = CombatEventRegistry.GetNearestEvent(10f, 10f, 50f, 100.0f, 5f, out _);
        Assert.IsFalse(found);
    }

    [Test]
    public void GetNearestEvent_InactiveEvent_Skipped()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.CleanupExpired(1000f, 5f); // Expire the event

        bool found = CombatEventRegistry.GetNearestEvent(10f, 10f, 50f, 1000f, 5000f, out _);
        Assert.IsFalse(found);
    }

    // ── GetIntensity ────────────────────────────────────────

    [Test]
    public void GetIntensity_EmptyBuffer_ReturnsZero()
    {
        int intensity = CombatEventRegistry.GetIntensity(0f, 0f, 100f, 60f, 1.0f);
        Assert.That(intensity, Is.EqualTo(0));
    }

    [Test]
    public void GetIntensity_SingleGunshot_ReturnsOne()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        int intensity = CombatEventRegistry.GetIntensity(10f, 10f, 50f, 60f, 2.0f);
        Assert.That(intensity, Is.EqualTo(1));
    }

    [Test]
    public void GetIntensity_Explosion_CountsAsThree()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 150f, CombatEventType.Explosion, false);
        int intensity = CombatEventRegistry.GetIntensity(10f, 10f, 50f, 60f, 2.0f);
        Assert.That(intensity, Is.EqualTo(3)); // 1 base + 2 explosion bonus
    }

    [Test]
    public void GetIntensity_MultipleEvents_SumsCorrectly()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(12f, 0f, 12f, 2.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(11f, 0f, 11f, 3.0f, 150f, CombatEventType.Explosion, false);

        int intensity = CombatEventRegistry.GetIntensity(10f, 10f, 50f, 60f, 4.0f);
        Assert.That(intensity, Is.EqualTo(5)); // 1 + 1 + 3
    }

    [Test]
    public void GetIntensity_OutOfRange_NotCounted()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(200f, 0f, 200f, 2.0f, 100f, CombatEventType.Gunshot, false);

        int intensity = CombatEventRegistry.GetIntensity(10f, 10f, 50f, 60f, 3.0f);
        Assert.That(intensity, Is.EqualTo(1)); // Only the close one
    }

    [Test]
    public void GetIntensity_OutOfTimeWindow_NotCounted()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);

        // timeWindow=5, currentTime=100 → event is too old
        int intensity = CombatEventRegistry.GetIntensity(10f, 10f, 50f, 5f, 100f);
        Assert.That(intensity, Is.EqualTo(0));
    }

    // ── IsInBossZone ────────────────────────────────────────

    [Test]
    public void IsInBossZone_NoBossEvents_ReturnsFalse()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        bool inZone = CombatEventRegistry.IsInBossZone(10f, 10f, 75f, 120f, 2.0f);
        Assert.IsFalse(inZone);
    }

    [Test]
    public void IsInBossZone_BossEventInRange_ReturnsTrue()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, true);
        bool inZone = CombatEventRegistry.IsInBossZone(15f, 15f, 75f, 120f, 2.0f);
        Assert.IsTrue(inZone);
    }

    [Test]
    public void IsInBossZone_BossEventOutOfRange_ReturnsFalse()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, true);
        bool inZone = CombatEventRegistry.IsInBossZone(200f, 200f, 75f, 120f, 2.0f);
        Assert.IsFalse(inZone);
    }

    [Test]
    public void IsInBossZone_BossEventDecayed_ReturnsFalse()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, true);
        // decayTime=120, currentTime=200 → age is 199, past decay
        bool inZone = CombatEventRegistry.IsInBossZone(10f, 10f, 75f, 120f, 200f);
        Assert.IsFalse(inZone);
    }

    // ── CleanupExpired ──────────────────────────────────────

    [Test]
    public void CleanupExpired_MarksOldEventsInactive()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(20f, 0f, 20f, 50.0f, 100f, CombatEventType.Gunshot, false);

        CombatEventRegistry.CleanupExpired(60f, 30f); // maxAge=30, at time=60
        // Event 1 (time=1) is 59s old → expired
        // Event 2 (time=50) is 10s old → still active

        Assert.That(CombatEventRegistry.ActiveCount, Is.EqualTo(1));
    }

    [Test]
    public void CleanupExpired_FreshEventsUntouched()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 100f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.CleanupExpired(105f, 300f);
        Assert.That(CombatEventRegistry.ActiveCount, Is.EqualTo(1));
    }

    // ── Clear ───────────────────────────────────────────────

    [Test]
    public void Clear_ResetsCountToZero()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(20f, 0f, 20f, 2.0f, 100f, CombatEventType.Gunshot, false);

        CombatEventRegistry.Clear();

        Assert.That(CombatEventRegistry.Count, Is.EqualTo(0));
        Assert.That(CombatEventRegistry.ActiveCount, Is.EqualTo(0));
    }

    [Test]
    public void Clear_NoEventsFoundAfterClear()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.Clear();

        bool found = CombatEventRegistry.GetNearestEvent(10f, 10f, 100f, 2.0f, 300f, out _);
        Assert.IsFalse(found);
    }

    // ── Initialize ──────────────────────────────────────────

    [Test]
    public void Initialize_CustomCapacity_ClearsAndResizes()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);

        CombatEventRegistry.Initialize(16);

        Assert.That(CombatEventRegistry.Count, Is.EqualTo(0));
    }

    [Test]
    public void Initialize_InvalidCapacity_UsesDefault()
    {
        CombatEventRegistry.Initialize(0);
        // Should not crash, uses DefaultCapacity
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        Assert.That(CombatEventRegistry.Count, Is.EqualTo(1));
    }

    // ── ActiveCount ─────────────────────────────────────────

    [Test]
    public void ActiveCount_AllActive_EqualsCount()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(20f, 0f, 20f, 2.0f, 100f, CombatEventType.Gunshot, false);
        Assert.That(CombatEventRegistry.ActiveCount, Is.EqualTo(2));
    }

    [Test]
    public void ActiveCount_AfterCleanup_OnlyCountsActive()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1.0f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(20f, 0f, 20f, 100f, 100f, CombatEventType.Gunshot, false);

        CombatEventRegistry.CleanupExpired(200f, 50f);
        // Event 1 (time=1) → age 199 → expired
        // Event 2 (time=100) → age 100 → expired

        Assert.That(CombatEventRegistry.Count, Is.EqualTo(2)); // Still in buffer
        Assert.That(CombatEventRegistry.ActiveCount, Is.EqualTo(0)); // But inactive
    }

    // ── CombatEventType constants ───────────────────────────

    [Test]
    public void CombatEventType_Constants_HaveExpectedValues()
    {
        Assert.That(CombatEventType.None, Is.EqualTo((byte)0));
        Assert.That(CombatEventType.Gunshot, Is.EqualTo((byte)1));
        Assert.That(CombatEventType.Explosion, Is.EqualTo((byte)2));
        Assert.That(CombatEventType.Airdrop, Is.EqualTo((byte)3));
    }
}

using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

[TestFixture]
public class RoomClearControllerTests
{
    private BotEntity _entity;

    [SetUp]
    public void SetUp()
    {
        _entity = new BotEntity(0);
    }

    // ── Update: environment transitions ────────────────────────────

    [Test]
    public void Update_OutdoorToIndoor_StartsRoomClear()
    {
        // LastEnvironmentId defaults to -1 (uninitialized), which counts as outdoor
        // Set to explicit outdoor first
        _entity.LastEnvironmentId = 1;

        var result = RoomClearController.Update(_entity, 0, 10f, 15f, 30f, 1.5f);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
            Assert.That(_entity.IsInRoomClear, Is.True);
            Assert.That(_entity.RoomClearUntil, Is.GreaterThanOrEqualTo(10f + 15f));
            Assert.That(_entity.RoomClearUntil, Is.LessThanOrEqualTo(10f + 30f));
        });
    }

    [Test]
    public void Update_IndoorToIndoor_DoesNotRestartRoomClear()
    {
        // Already indoor
        _entity.LastEnvironmentId = 0;

        var result = RoomClearController.Update(_entity, 0, 10f, 15f, 30f, 1.5f);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RoomClearInstruction.None));
            Assert.That(_entity.IsInRoomClear, Is.False);
        });
    }

    [Test]
    public void Update_IndoorToOutdoor_DoesNotStartRoomClear()
    {
        _entity.LastEnvironmentId = 0;

        var result = RoomClearController.Update(_entity, 1, 10f, 15f, 30f, 1.5f);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RoomClearInstruction.None));
            Assert.That(_entity.IsInRoomClear, Is.False);
        });
    }

    [Test]
    public void Update_TimerExpiry_ReturnsNone()
    {
        // Start room clear
        _entity.LastEnvironmentId = 1;
        RoomClearController.Update(_entity, 0, 10f, 15f, 30f, 1.5f);

        // Advance time past max duration
        var result = RoomClearController.Update(_entity, 0, 50f, 15f, 30f, 1.5f);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RoomClearInstruction.None));
            Assert.That(_entity.IsInRoomClear, Is.False);
        });
    }

    [Test]
    public void Update_CornerPauseActive_ReturnsPauseAtCorner()
    {
        // Start room clear
        _entity.LastEnvironmentId = 1;
        RoomClearController.Update(_entity, 0, 10f, 15f, 30f, 1.5f);

        // Set corner pause
        _entity.CornerPauseUntil = 15f;

        // Still within room clear and corner pause
        var result = RoomClearController.Update(_entity, 0, 12f, 15f, 30f, 1.5f);

        Assert.That(result, Is.EqualTo(RoomClearInstruction.PauseAtCorner));
    }

    [Test]
    public void Update_AlreadyInRoomClear_OutdoorToIndoor_DoesNotRestart()
    {
        // Start room clear
        _entity.LastEnvironmentId = 1;
        RoomClearController.Update(_entity, 0, 10f, 15f, 30f, 1.5f);
        float originalExpiry = _entity.RoomClearUntil;

        // Go back outdoor
        _entity.LastEnvironmentId = 1;
        _entity.IsInRoomClear = true; // still active

        // Try another outdoor->indoor transition while already in room clear
        var result = RoomClearController.Update(_entity, 0, 12f, 15f, 30f, 1.5f);

        Assert.Multiple(() =>
        {
            // Should return SlowWalk (still in room clear) but NOT restart
            Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
            Assert.That(_entity.IsInRoomClear, Is.True);
            // RoomClearUntil should not have been overwritten with new timer
            Assert.That(_entity.RoomClearUntil, Is.EqualTo(originalExpiry));
        });
    }

    [Test]
    public void Update_UninitializedEnvironment_IndoorTransition_StartsRoomClear()
    {
        // LastEnvironmentId defaults to -1 (uninitialized), which != 0, so counts as "outdoor"
        Assert.That(_entity.LastEnvironmentId, Is.EqualTo(-1));

        var result = RoomClearController.Update(_entity, 0, 10f, 15f, 30f, 1.5f);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
            Assert.That(_entity.IsInRoomClear, Is.True);
        });
    }

    [Test]
    public void Update_CornerPauseExpired_ReturnsSlowWalk()
    {
        // Start room clear
        _entity.LastEnvironmentId = 1;
        RoomClearController.Update(_entity, 0, 10f, 15f, 30f, 1.5f);

        // Set corner pause that has already expired
        _entity.CornerPauseUntil = 11f;

        // Time is past pause but still in room clear
        var result = RoomClearController.Update(_entity, 0, 12f, 15f, 30f, 1.5f);

        Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
    }

    // ── IsSharpCorner: angle detection ─────────────────────────────

    [Test]
    public void IsSharpCorner_90DegreesTurn_AboveThreshold_ReturnsTrue()
    {
        // From (0,0) -> corner (1,0) -> to (1,1) = 90 degree turn
        bool result = RoomClearController.IsSharpCorner(0, 0, 1, 0, 1, 1, 60f);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsSharpCorner_StraightLine_ReturnsFalse()
    {
        // From (0,0) -> corner (1,0) -> to (2,0) = 0 degree turn (straight)
        bool result = RoomClearController.IsSharpCorner(0, 0, 1, 0, 2, 0, 60f);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsSharpCorner_30DegreeTurn_BelowThreshold_ReturnsFalse()
    {
        // Create a ~30 degree turn: from (0,0) -> corner (1,0) -> to (2, tan(30)*1)
        // tan(30) ~= 0.577
        float toZ = (float)System.Math.Tan(30.0 * System.Math.PI / 180.0);
        bool result = RoomClearController.IsSharpCorner(0, 0, 1, 0, 2, toZ, 60f);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsSharpCorner_180DegreeUTurn_ReturnsTrue()
    {
        // From (0,0) -> corner (1,0) -> to (0,0) = 180 degree U-turn
        bool result = RoomClearController.IsSharpCorner(0, 0, 1, 0, 0, 0, 60f);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsSharpCorner_NearZeroLength_ReturnsFalse()
    {
        // Degenerate case: from and corner are the same point
        bool result = RoomClearController.IsSharpCorner(1, 1, 1, 1, 2, 2, 60f);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsSharpCorner_AtExactlyThreshold_ReturnsTrue()
    {
        // Create exactly 60.01 degree turn to be just above threshold
        // Using two vectors with known angle
        // Vector a = (1, 0), vector b needs to make angle > 60 degrees with a
        // cos(60) = 0.5 => b direction = (cos(120), sin(120)) = (-0.5, 0.866)
        // From (0,0) -> corner (1,0) -> to needs angle just above 60
        // The angle between (1,0) and (-0.5, 0.866) = 120 degrees > 60
        // Let's use a precise 60.1-degree case instead
        float angleRad = 60.1f * (float)(System.Math.PI / 180.0);
        float bx = (float)System.Math.Cos(angleRad);
        float bz = (float)System.Math.Sin(angleRad);

        // from = (0,0), corner = (1,0), to = (1+bx, bz)
        bool result = RoomClearController.IsSharpCorner(0, 0, 1, 0, 1 + bx, bz, 60f);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsSharpCorner_JustBelowThreshold_ReturnsFalse()
    {
        // Create a 59-degree turn (below 60 threshold)
        float angleRad = 59f * (float)(System.Math.PI / 180.0);
        float bx = (float)System.Math.Cos(angleRad);
        float bz = (float)System.Math.Sin(angleRad);

        bool result = RoomClearController.IsSharpCorner(0, 0, 1, 0, 1 + bx, bz, 60f);

        Assert.That(result, Is.False);
    }

    // ── TriggerCornerPause ─────────────────────────────────────────

    [Test]
    public void TriggerCornerPause_SetsTimer()
    {
        RoomClearController.TriggerCornerPause(_entity, 10f, 1.5f);

        Assert.That(_entity.CornerPauseUntil, Is.EqualTo(11.5f).Within(0.01f));
    }

    [Test]
    public void TriggerCornerPause_DoesNotOverrideActivePause()
    {
        // Set an existing pause that hasn't expired
        _entity.CornerPauseUntil = 15f;

        RoomClearController.TriggerCornerPause(_entity, 10f, 1.5f);

        // Should NOT override — still 15
        Assert.That(_entity.CornerPauseUntil, Is.EqualTo(15f));
    }

    [Test]
    public void TriggerCornerPause_OverridesExpiredPause()
    {
        // Set a pause that has already expired
        _entity.CornerPauseUntil = 5f;

        RoomClearController.TriggerCornerPause(_entity, 10f, 1.5f);

        // Should override — expired pause
        Assert.That(_entity.CornerPauseUntil, Is.EqualTo(11.5f).Within(0.01f));
    }
}

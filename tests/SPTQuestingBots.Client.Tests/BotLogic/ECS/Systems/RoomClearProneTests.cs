using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

/// <summary>
/// Tests verifying that RoomClearController correctly produces instructions
/// that would cause a prone bot to stand up when entering a building.
/// The actual pose change happens in the action layer that reads these instructions,
/// but we verify the controller produces the right output for prone scenarios.
/// </summary>
[TestFixture]
public class RoomClearProneTests
{
    private BotEntity _entity;

    [SetUp]
    public void SetUp()
    {
        _entity = new BotEntity(0);
    }

    // ── Prone bot enters building: SlowWalk instruction triggers stand ───

    [Test]
    public void Update_ProneBotEntersBuilding_ReturnsSlowWalk()
    {
        // Simulate a prone bot (GamePoseVisibilityCoef is low when prone)
        _entity.GamePoseVisibilityCoef = 0.2f;
        _entity.LastEnvironmentId = 0; // outdoor

        var result = RoomClearController.Update(_entity, true, 10f, 15f, 30f, 1.5f);

        // SlowWalk instruction tells the action layer to set walk speed + stand pose
        Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
        Assert.That(_entity.IsInRoomClear, Is.True);
    }

    [Test]
    public void Update_ProneBotAlreadyIndoor_NoTransition()
    {
        _entity.GamePoseVisibilityCoef = 0.2f;
        _entity.LastEnvironmentId = 1; // already indoor

        var result = RoomClearController.Update(_entity, true, 10f, 15f, 30f, 1.5f);

        // No outdoor->indoor transition = no room clear start
        Assert.That(result, Is.EqualTo(RoomClearInstruction.None));
        Assert.That(_entity.IsInRoomClear, Is.False);
    }

    [Test]
    public void Update_StandingBotEntersBuilding_SameAsProneResult()
    {
        // Standing bot gets same instruction — the action layer handles pose
        _entity.GamePoseVisibilityCoef = 1.0f;
        _entity.LastEnvironmentId = 0;

        var result = RoomClearController.Update(_entity, true, 10f, 15f, 30f, 1.5f);

        Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
        Assert.That(_entity.IsInRoomClear, Is.True);
    }

    // ── Room clear duration is bounded ──────────────────────────────────

    [Test]
    public void Update_RoomClearDuration_BoundedByMinMax()
    {
        _entity.LastEnvironmentId = 0;
        RoomClearController.Update(_entity, true, 10f, 5f, 20f, 1.5f);

        // Duration should be between min and max
        Assert.That(_entity.RoomClearUntil, Is.GreaterThanOrEqualTo(10f + 5f));
        Assert.That(_entity.RoomClearUntil, Is.LessThanOrEqualTo(10f + 20f));
    }

    // ── Room clear cancels when moving outdoor ─────────────────────────

    [Test]
    public void Update_BotMovesOutdoorDuringRoomClear_Cancels()
    {
        // Start room clear
        _entity.LastEnvironmentId = 0;
        RoomClearController.Update(_entity, true, 10f, 15f, 30f, 1.5f);
        Assert.That(_entity.IsInRoomClear, Is.True);

        // Move back outdoor
        var result = RoomClearController.Update(_entity, false, 11f, 15f, 30f, 1.5f);

        Assert.That(result, Is.EqualTo(RoomClearInstruction.None));
        Assert.That(_entity.IsInRoomClear, Is.False);
    }

    // ── Corner pause still works during room clear ─────────────────────

    [Test]
    public void Update_CornerPauseDuringRoomClear_ReturnsPause()
    {
        _entity.LastEnvironmentId = 0;
        RoomClearController.Update(_entity, true, 10f, 15f, 30f, 1.5f);

        // Trigger corner pause
        RoomClearController.TriggerCornerPause(_entity, 11f, 2f);

        var result = RoomClearController.Update(_entity, true, 12f, 15f, 30f, 1.5f);
        Assert.That(result, Is.EqualTo(RoomClearInstruction.PauseAtCorner));
    }

    // ── PlaceInfoIsInside override works for outdoor→indoor detection ───

    [Test]
    public void Update_PlaceInfoOverride_TriggersRoomClear()
    {
        _entity.LastEnvironmentId = 0;

        // Player.Environment says outdoor, but PlaceInfo says indoor
        var result = RoomClearController.Update(_entity, false, 10f, 15f, 30f, 1.5f, placeInfoIsInside: true);

        Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
        Assert.That(_entity.IsInRoomClear, Is.True);
    }

    [Test]
    public void Update_BothOutdoor_NeitherTriggersRoomClear()
    {
        _entity.LastEnvironmentId = 0;

        var result = RoomClearController.Update(_entity, false, 10f, 15f, 30f, 1.5f, placeInfoIsInside: false);

        Assert.That(result, Is.EqualTo(RoomClearInstruction.None));
        Assert.That(_entity.IsInRoomClear, Is.False);
    }

    // ── Timer expiry during room clear ──────────────────────────────────

    [Test]
    public void Update_TimerExpiry_ReturnsNoneAndClearsState()
    {
        _entity.LastEnvironmentId = 0;
        RoomClearController.Update(_entity, true, 10f, 5f, 5f, 1.5f);
        Assert.That(_entity.IsInRoomClear, Is.True);

        // Advance past the duration
        var result = RoomClearController.Update(_entity, true, 20f, 5f, 5f, 1.5f);

        Assert.That(result, Is.EqualTo(RoomClearInstruction.None));
        Assert.That(_entity.IsInRoomClear, Is.False);
    }

    // ── Uninitialized environment transitions correctly ─────────────────

    [Test]
    public void Update_UninitializedLastEnv_IndoorTriggersRoomClear()
    {
        // Default LastEnvironmentId is -1 (uninitialized), counts as "not indoor"
        Assert.That(_entity.LastEnvironmentId, Is.EqualTo(-1));

        var result = RoomClearController.Update(_entity, true, 5f, 10f, 20f, 1.5f);

        Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
        Assert.That(_entity.IsInRoomClear, Is.True);
    }
}

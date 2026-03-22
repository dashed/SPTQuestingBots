using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

[TestFixture]
public class RoomClearPlaceInfoTests
{
    private BotEntity CreateEntity()
    {
        var entity = new BotEntity(1);
        entity.LastEnvironmentId = 0; // start outdoor
        return entity;
    }

    [Test]
    public void Update_PlaceInfoNull_UsesIsIndoorOnly()
    {
        var entity = CreateEntity();

        // isIndoor=true, no placeInfo override
        var result = RoomClearController.Update(entity, true, 10f, 5f, 10f, 0.5f, placeInfoIsInside: null);

        // Should trigger room clear because isIndoor=true
        Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
        Assert.That(entity.IsInRoomClear, Is.True);
    }

    [Test]
    public void Update_PlaceInfoTrue_IsIndoorFalse_TriggersRoomClear()
    {
        var entity = CreateEntity();

        // Player.Environment says outdoor, but AIPlaceInfo says indoor
        var result = RoomClearController.Update(entity, false, 10f, 5f, 10f, 0.5f, placeInfoIsInside: true);

        // PlaceInfo overrides to indoor, should trigger room clear
        Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
        Assert.That(entity.IsInRoomClear, Is.True);
    }

    [Test]
    public void Update_PlaceInfoFalse_IsIndoorTrue_StillTriggersRoomClear()
    {
        var entity = CreateEntity();

        // Player.Environment says indoor, AIPlaceInfo says not inside — OR logic keeps indoor
        var result = RoomClearController.Update(entity, true, 10f, 5f, 10f, 0.5f, placeInfoIsInside: false);

        Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
        Assert.That(entity.IsInRoomClear, Is.True);
    }

    [Test]
    public void Update_BothFalse_NoRoomClear()
    {
        var entity = CreateEntity();

        var result = RoomClearController.Update(entity, false, 10f, 5f, 10f, 0.5f, placeInfoIsInside: false);

        Assert.That(result, Is.EqualTo(RoomClearInstruction.None));
        Assert.That(entity.IsInRoomClear, Is.False);
    }

    [Test]
    public void Update_PlaceInfoCancelsRoomClear_WhenBotMovesOutdoors()
    {
        var entity = CreateEntity();

        // Enter indoor via PlaceInfo
        RoomClearController.Update(entity, false, 10f, 5f, 10f, 0.5f, placeInfoIsInside: true);
        Assert.That(entity.IsInRoomClear, Is.True);

        // Move back outdoors (both signals say outdoor)
        var result = RoomClearController.Update(entity, false, 11f, 5f, 10f, 0.5f, placeInfoIsInside: false);

        Assert.That(result, Is.EqualTo(RoomClearInstruction.None));
        Assert.That(entity.IsInRoomClear, Is.False);
    }

    [Test]
    public void Update_NoPlaceInfo_BackwardsCompatible()
    {
        var entity = CreateEntity();

        // Test without the optional parameter — should work exactly as before
        var result1 = RoomClearController.Update(entity, false, 10f, 5f, 10f, 0.5f);
        Assert.That(result1, Is.EqualTo(RoomClearInstruction.None));

        var result2 = RoomClearController.Update(entity, true, 10f, 5f, 10f, 0.5f);
        Assert.That(result2, Is.EqualTo(RoomClearInstruction.SlowWalk));
    }

    [Test]
    public void Update_PlaceInfoTransition_OutdoorToIndoorViaPlaceInfo()
    {
        var entity = CreateEntity();

        // First tick: both outdoor
        RoomClearController.Update(entity, false, 10f, 5f, 10f, 0.5f, placeInfoIsInside: false);
        Assert.That(entity.IsInRoomClear, Is.False);

        // Second tick: PlaceInfo detects indoor
        var result = RoomClearController.Update(entity, false, 11f, 5f, 10f, 0.5f, placeInfoIsInside: true);
        Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
        Assert.That(entity.IsInRoomClear, Is.True);
    }

    [Test]
    public void Update_RoomClearExpires_WithPlaceInfo()
    {
        var entity = CreateEntity();

        // Enter indoor
        RoomClearController.Update(entity, true, 10f, 5f, 5f, 0.5f, placeInfoIsInside: true);
        Assert.That(entity.IsInRoomClear, Is.True);

        // Wait for timer to expire (max duration is 5s)
        var result = RoomClearController.Update(entity, true, 20f, 5f, 5f, 0.5f, placeInfoIsInside: true);

        Assert.That(result, Is.EqualTo(RoomClearInstruction.None));
        Assert.That(entity.IsInRoomClear, Is.False);
    }
}

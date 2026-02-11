namespace SPTQuestingBots.BotLogic.ECS.UtilityAI;

/// <summary>
/// Integer constants mirroring <c>Models.Questing.QuestAction</c> enum values.
/// Used by utility tasks for quest-action gating without depending on game assemblies.
/// <para>
/// Values MUST stay in sync with the QuestAction enum ordinals.
/// </para>
/// </summary>
public static class QuestActionId
{
    public const int Undefined = 0;
    public const int MoveToPosition = 1;
    public const int HoldAtPosition = 2;
    public const int Ambush = 3;
    public const int Snipe = 4;
    public const int PlantItem = 5;
    public const int ToggleSwitch = 6;
    public const int RequestExtract = 7;
    public const int CloseNearbyDoors = 8;
}

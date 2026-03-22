namespace SPTQuestingBots.Helpers;

/// <summary>
/// Test shim for GroupTacticType constants. Mirrors the real class in
/// GroupCoordinationHelper.cs which cannot be linked due to EFT/Unity dependencies.
/// </summary>
public static class GroupTacticType
{
    public const int None = 0;
    public const int Attack = 1;
    public const int Ambush = 2;
    public const int Protect = 3;
}

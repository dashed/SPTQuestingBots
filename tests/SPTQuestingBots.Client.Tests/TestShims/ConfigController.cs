namespace SPTQuestingBots.Controllers;

/// <summary>
/// Minimal ConfigController shim for testing linked source files that reference
/// ConfigController.ModPathRelative without requiring the full client assembly.
/// </summary>
public static class ConfigController
{
    public static string ModPathRelative { get; } = "/BepInEx/plugins/DanW-SPTQuestingBots";
}

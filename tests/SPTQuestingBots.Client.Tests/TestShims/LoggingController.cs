namespace SPTQuestingBots.Controllers;

/// <summary>
/// No-op LoggingController shim for testing linked source files without BepInEx assemblies.
/// All methods silently discard messages.
/// </summary>
public static class LoggingController
{
    public static void LogDebug(string message) { }

    public static void LogInfo(string message, bool alwaysShow = false) { }

    public static void LogWarning(string message, bool onlyForDebug = false) { }

    public static void LogError(string message, bool onlyForDebug = false) { }
}

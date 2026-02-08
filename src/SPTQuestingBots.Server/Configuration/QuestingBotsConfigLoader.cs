using Newtonsoft.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;

namespace SPTQuestingBots.Server.Configuration;

/// <summary>
/// Loads and provides access to the QuestingBots mod configuration.
///
/// <para>
/// On first access the loader reads <c>config/config.json</c> from the mod's
/// installation directory and deserialises it into a <see cref="QuestingBotsConfig"/>
/// instance. The result is cached for the lifetime of the server process.
/// </para>
///
/// <para>
/// Additional JSON data files (e.g. <c>eftQuestSettings.json</c>,
/// <c>zoneAndItemQuestPositions.json</c>) can be loaded on demand via
/// <see cref="LoadJsonFile{T}"/>.
/// </para>
///
/// <para>
/// <b>Ported from:</b> inline config loading in <c>mod.ts</c> (TypeScript / SPT 3.x).
/// In the original mod, the <c>VFS</c> helper read and parsed JSON files directly
/// inside lifecycle hooks. This service centralises all file I/O for the server
/// plugin.
/// </para>
/// </summary>
[Injectable(InjectionType.Singleton)]
public class QuestingBotsConfigLoader
{
    private readonly ISptLogger<QuestingBotsConfigLoader> _logger;
    private QuestingBotsConfig? _config;

    public QuestingBotsConfigLoader(ISptLogger<QuestingBotsConfigLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// The deserialised mod configuration. Lazily loaded on first access by
    /// calling <see cref="Load"/>. Subsequent accesses return the cached instance.
    /// </summary>
    public QuestingBotsConfig Config
    {
        get
        {
            if (_config == null)
            {
                Load();
            }

            return _config!;
        }
    }

    /// <summary>
    /// Reads and deserialises <c>config/config.json</c> from the mod directory.
    ///
    /// <para>
    /// If the file is missing or deserialisation fails, a default (empty)
    /// <see cref="QuestingBotsConfig"/> is used and an error is logged so the
    /// server can still start (the mod will effectively be disabled because the
    /// default <c>Enabled</c> flag is <c>false</c>).
    /// </para>
    /// </summary>
    public void Load()
    {
        var configPath = Path.Combine(GetModPath(), "config", "config.json");

        if (!File.Exists(configPath))
        {
            _logger.Error($"[Questing Bots] Config file not found at: {configPath}");
            _config = new QuestingBotsConfig();
            return;
        }

        var json = File.ReadAllText(configPath);
        _config = JsonConvert.DeserializeObject<QuestingBotsConfig>(json);

        if (_config == null)
        {
            _logger.Error("[Questing Bots] Failed to deserialize config.json");
            _config = new QuestingBotsConfig();
        }
    }

    /// <summary>
    /// Loads an arbitrary JSON file relative to the mod's installation directory.
    ///
    /// <para>
    /// Used by the static router to serve supplementary data files to the client
    /// plugin, such as <c>config/eftQuestSettings.json</c> (EFT quest overrides)
    /// and <c>config/zoneAndItemQuestPositions.json</c> (quest zone coordinates).
    /// </para>
    /// </summary>
    /// <typeparam name="T">The type to deserialise the JSON into.</typeparam>
    /// <param name="relativePath">
    /// Path relative to the mod directory (e.g. <c>"config/eftQuestSettings.json"</c>).
    /// </param>
    /// <returns>
    /// The deserialised object, or <c>null</c> if the file does not exist or
    /// deserialisation fails.
    /// </returns>
    public T? LoadJsonFile<T>(string relativePath)
        where T : class
    {
        var filePath = Path.Combine(GetModPath(), relativePath);

        if (!File.Exists(filePath))
        {
            _logger.Error($"[Questing Bots] File not found: {filePath}");
            return null;
        }

        var json = File.ReadAllText(filePath);
        return JsonConvert.DeserializeObject<T>(json);
    }

    /// <summary>
    /// Returns the absolute path to the mod's installation directory.
    ///
    /// <para>
    /// In a standard SPT installation the mod DLL is deployed to
    /// <c>user/mods/SPTQuestingBots/</c>. The assembly's location is used as
    /// the base path for resolving config files and data directories.
    /// </para>
    /// </summary>
    /// <returns>The directory containing the mod assembly.</returns>
    public static string GetModPath()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(QuestingBotsConfigLoader).Assembly.Location);
        return assemblyDir ?? ".";
    }
}

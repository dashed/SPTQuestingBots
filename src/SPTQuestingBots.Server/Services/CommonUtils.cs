using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTQuestingBots.Server.Configuration;

namespace SPTQuestingBots.Server.Services;

/// <summary>
/// Provides common logging and item-name lookup utilities for the
/// QuestingBots server plugin.
///
/// <para>
/// All log messages are prefixed with <c>[Questing Bots]</c> for easy
/// identification in the server console. <see cref="LogDebug"/> and
/// <see cref="LogInfo"/> are gated behind the mod's <c>enabled</c> flag
/// to reduce noise when the mod is disabled — unless <paramref name="alwaysShow"/>
/// is set.
/// </para>
///
/// <para>
/// <b>Ported from:</b> <c>src/CommonUtils.ts</c> (TypeScript / SPT 3.x).
/// </para>
/// </summary>
[Injectable(InjectionType.Singleton)]
public class CommonUtils
{
    /// <summary>Prefix prepended to every log message.</summary>
    private const string DebugMessagePrefix = "[Questing Bots] ";

    private readonly ISptLogger<CommonUtils> _logger;
    private readonly DatabaseService _databaseService;
    private readonly LocaleService _localeService;
    private readonly QuestingBotsConfigLoader _configLoader;
    private Dictionary<string, string>? _translations;

    public CommonUtils(
        ISptLogger<CommonUtils> logger,
        DatabaseService databaseService,
        LocaleService localeService,
        QuestingBotsConfigLoader configLoader
    )
    {
        _logger = logger;
        _databaseService = databaseService;
        _localeService = localeService;
        _configLoader = configLoader;
    }

    /// <summary>
    /// Lazily loads and caches the locale translation dictionary.
    /// The dictionary maps translation keys (e.g. <c>"item_id Name"</c>)
    /// to their localized strings.
    /// </summary>
    private Dictionary<string, string> Translations
    {
        get
        {
            _translations ??= _localeService.GetLocaleDb();
            return _translations;
        }
    }

    /// <summary>
    /// Logs a debug-level message. Suppressed when the mod is disabled
    /// unless <paramref name="alwaysShow"/> is <c>true</c>.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="alwaysShow">If <c>true</c>, logs even when the mod is disabled.</param>
    public void LogDebug(string message, bool alwaysShow = false)
    {
        if (_configLoader.Config.Enabled || alwaysShow)
        {
            _logger.Debug(DebugMessagePrefix + message);
        }
    }

    /// <summary>
    /// Logs an info-level message. Suppressed when the mod is disabled
    /// unless <paramref name="alwaysShow"/> is <c>true</c>.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="alwaysShow">If <c>true</c>, logs even when the mod is disabled.</param>
    public void LogInfo(string message, bool alwaysShow = false)
    {
        if (_configLoader.Config.Enabled || alwaysShow)
        {
            _logger.Info(DebugMessagePrefix + message);
        }
    }

    /// <summary>
    /// Logs a warning-level message. Always shown regardless of the mod's
    /// enabled state.
    /// </summary>
    /// <param name="message">The warning message.</param>
    public void LogWarning(string message)
    {
        _logger.Warning(DebugMessagePrefix + message);
    }

    /// <summary>
    /// Logs an error-level message. Always shown regardless of the mod's
    /// enabled state.
    /// </summary>
    /// <param name="message">The error message.</param>
    public void LogError(string message)
    {
        _logger.Error(DebugMessagePrefix + message);
    }

    /// <summary>
    /// Gets the localized name for an item by its template ID.
    ///
    /// <para>
    /// First checks the locale translations dictionary using the key
    /// <c>"{itemId} Name"</c>. If no translation is found, falls back
    /// to the item template's <c>Name</c> property from the database.
    /// </para>
    /// </summary>
    /// <param name="itemId">The item template ID (e.g. <c>"5447a9cd4bdc2dbd208b4567"</c>).</param>
    /// <returns>The localized item name, or <c>null</c> if the item is unknown.</returns>
    public string? GetItemName(string itemId)
    {
        var translationKey = $"{itemId} Name";
        if (Translations.TryGetValue(translationKey, out var translatedName))
        {
            return translatedName;
        }

        // Fall back to the template data from the database
        var items = _databaseService.GetItems();
        if (!items.TryGetValue(itemId, out var item))
        {
            return null;
        }

        return item.Name;
    }
}

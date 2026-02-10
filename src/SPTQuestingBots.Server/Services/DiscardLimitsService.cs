using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace SPTQuestingBots.Server.Services;

/// <summary>
/// Disables the EFT DiscardLimits flag in the globals database at startup.
///
/// <para>
/// When DiscardLimitsEnabled is <c>true</c>, bots cannot freely discard items
/// from their inventory during a raid. This prevents the native looting system
/// from functioning correctly because bots need to drop lower-value items to
/// make room for upgrades.
/// </para>
///
/// <para>
/// This service runs as a standalone <see cref="IOnLoad"/> hook so it executes
/// regardless of whether the rest of the QuestingBots mod is enabled.
/// </para>
/// </summary>
[Injectable(InjectionType.Singleton, typePriority: 101)]
public class DiscardLimitsService(ISptLogger<DiscardLimitsService> logger, DatabaseService databaseService) : IOnLoad
{
    private const string LogPrefix = "[Questing Bots] ";

    /// <summary>
    /// Sets <c>Globals.Configuration.DiscardLimitsEnabled</c> to <c>false</c>
    /// so bots can freely manage their inventory during raids.
    /// </summary>
    public Task OnLoad()
    {
        try
        {
            var globals = databaseService.GetGlobals();
            if (globals?.Configuration == null)
            {
                logger.Warning(LogPrefix + "Globals or Configuration not available. Cannot disable DiscardLimits.");
                return Task.CompletedTask;
            }

            globals.Configuration.DiscardLimitsEnabled = false;
            logger.Info(LogPrefix + "Disabled DiscardLimitsEnabled in globals database");
        }
        catch (Exception ex)
        {
            logger.Error(LogPrefix + $"Failed to disable DiscardLimits: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}

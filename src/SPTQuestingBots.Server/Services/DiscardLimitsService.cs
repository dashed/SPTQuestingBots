using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace SPTQuestingBots.Server.Services;

/// <summary>
/// Disables the EFT DiscardLimits flag in the globals database after DB import.
///
/// <para>
/// When DiscardLimitsEnabled is <c>true</c>, bots cannot freely discard items
/// from their inventory during a raid. This prevents the native looting system
/// from functioning correctly because bots need to drop lower-value items to
/// make room for upgrades.
/// </para>
///
/// <para>
/// Disabling DiscardLimits has a side effect: items with a DiscardLimit become
/// insurable when they normally should not be (BSG uses DiscardLimit as an
/// implicit insurance gate). To compensate, this service marks those items as
/// <c>InsuranceDisabled = true</c> (same approach as LootingBots).
/// </para>
///
/// <para>
/// Runs at <see cref="OnLoadOrder.PostDBModLoader"/> + 1 so the database is
/// guaranteed to be fully imported before we access globals/templates.
/// </para>
/// </summary>
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class DiscardLimitsService(ISptLogger<DiscardLimitsService> logger, DatabaseService databaseService) : IOnLoad
{
    private const string LogPrefix = "[Questing Bots] ";

    /// <summary>
    /// Marks items with DiscardLimit as InsuranceDisabled, then sets
    /// <c>Globals.Configuration.DiscardLimitsEnabled</c> to <c>false</c>
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

            var items = databaseService.GetItems();
            var fixedCount = MarkDiscardLimitItemsAsUninsurable(items);

            globals.Configuration.DiscardLimitsEnabled = false;
            logger.Info(LogPrefix + $"Disabled DiscardLimitsEnabled (marked {fixedCount} items as InsuranceDisabled)");
        }
        catch (Exception ex)
        {
            logger.Error(LogPrefix + $"Failed to disable DiscardLimits: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Items with a DiscardLimit are implicitly non-insurable in BSG's code.
    /// When we disable DiscardLimitsEnabled, that implicit gate is removed.
    /// Compensate by explicitly marking them InsuranceDisabled.
    /// </summary>
    /// <returns>Number of items marked.</returns>
    internal static int MarkDiscardLimitItemsAsUninsurable(Dictionary<MongoId, TemplateItem> items)
    {
        var count = 0;
        foreach (var (_, template) in items)
        {
            if (template.Properties is { DiscardLimit: >= 0, IsAlwaysAvailableForInsurance: false })
            {
                template.Properties.InsuranceDisabled = true;
                count++;
            }
        }
        return count;
    }
}

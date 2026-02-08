using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Routers.Static;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTQuestingBots.Server.Models;
using SPTQuestingBots.Server.Services;

namespace SPTQuestingBots.Server.Routers;

/// <summary>
/// Replaces SPT's <see cref="BotStaticRouter"/> to intercept the
/// <c>/client/game/bot/generate</c> endpoint and add Player Scav
/// conversion logic.
///
/// <para>
/// When the QuestingBots client plugin signals <c>GeneratePScav = true</c>
/// in the request body, this router post-processes each generated assault
/// bot to look like a Player Scav by:
/// </para>
///
/// <list type="bullet">
///   <item>Adding a random PMC name to <c>MainProfileNickname</c>.</item>
///   <item>Setting a weighted-random <c>GameVersion</c> and matching
///         <c>MemberCategory</c>.</item>
/// </list>
///
/// <para>
/// <b>Ported from:</b> <c>mod.ts → container.afterResolution("BotCallbacks")</c>
/// and <c>generateBots()</c> / <c>setRandomisedGameVersionAndCategory()</c>
/// methods (TypeScript / SPT 3.x).
/// </para>
/// </summary>
[Injectable(typeOverride: typeof(BotStaticRouter))]
public class QuestingBotGenerateRouter(
    JsonUtil jsonUtil,
    BotController botController,
    HttpResponseUtil httpResponseUtil,
    BotNameService botNameService,
    WeightedRandomHelper weightedRandomHelper,
    ConfigServer configServer,
    CommonUtils commonUtils)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction<GenerateBotsRequestDataWithPScav>(
                "/client/game/bot/generate",
                async (url, info, sessionId, output) =>
                {
                    var bots = await botController.Generate(sessionId, info);

                    if (info.GeneratePScav)
                    {
                        var pmcConfig = configServer.GetConfig<PmcConfig>();

                        foreach (var bot in bots)
                        {
                            if (bot?.Info?.Settings?.Role is not "assault")
                            {
                                commonUtils.LogDebug(
                                    $"Tried generating a player Scav, but a bot with role {bot?.Info?.Settings?.Role} was returned");
                                continue;
                            }

                            botNameService.AddRandomPmcNameToBotMainProfileNicknameProperty(bot);
                            SetRandomisedGameVersionAndCategory(
                                bot.Info, pmcConfig, weightedRandomHelper);
                        }
                    }

                    return httpResponseUtil.GetBody(bots);
                }
            ),
        ])
{
    /// <summary>
    /// Sets a random game version and the corresponding member category on
    /// a bot's <see cref="Info"/> record. Mirrors the logic from
    /// <c>BotGenerator.SetRandomisedGameVersionAndCategory</c>, which is
    /// <c>protected</c> and cannot be called directly.
    /// </summary>
    private static void SetRandomisedGameVersionAndCategory(
        Info botInfo,
        PmcConfig pmcConfig,
        WeightedRandomHelper weightedRandom)
    {
        // Special case
        if (string.Equals(botInfo.Nickname, "nikita", StringComparison.OrdinalIgnoreCase))
        {
            botInfo.GameVersion = GameEditions.UNHEARD;
            botInfo.MemberCategory = MemberCategory.Developer;
            botInfo.SelectedMemberCategory = botInfo.MemberCategory;
            return;
        }

        // Choose random weighted game version for bot
        botInfo.GameVersion = weightedRandom.GetWeightedValue(pmcConfig.GameVersionWeight);

        // Choose appropriate member category value
        switch (botInfo.GameVersion)
        {
            case GameEditions.EDGE_OF_DARKNESS:
                botInfo.MemberCategory = MemberCategory.UniqueId;
                break;
            case GameEditions.UNHEARD:
                botInfo.MemberCategory = MemberCategory.Unheard;
                break;
            default:
                botInfo.MemberCategory = weightedRandom.GetWeightedValue(pmcConfig.AccountTypeWeight);
                break;
        }

        // Ensure selected category matches
        botInfo.SelectedMemberCategory = botInfo.MemberCategory;
    }
}

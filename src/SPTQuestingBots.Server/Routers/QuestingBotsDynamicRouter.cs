using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using SPTQuestingBots.Server.Configuration;
using SPTQuestingBots.Server.Services;

namespace SPTQuestingBots.Server.Routers;

/// <summary>
/// Registers a dynamic HTTP route for adjusting the Player Scav conversion
/// chance at runtime.
///
/// <para>
/// Dynamic routes match URLs by prefix rather than exact string, allowing
/// the client to pass parameters as path segments. This route handles:
/// </para>
///
/// <list type="table">
///   <listheader>
///     <term>Endpoint</term>
///     <description>Purpose</description>
///   </listheader>
///   <item>
///     <term><c>/QuestingBots/AdjustPScavChance/{factor}</c></term>
///     <description>
///       Multiplies the original PScav conversion chance by <c>factor</c>
///       and writes the result into SPT's <see cref="BotConfig"/>. The
///       client calls this periodically during a raid to ramp PScav spawns
///       up or down based on remaining raid time.
///     </description>
///   </item>
/// </list>
///
/// <para>
/// <b>Ported from:</b> <c>mod.ts → preSptLoad()</c> dynamic router registration
/// (TypeScript / SPT 3.x). The original used
/// <c>container.resolve&lt;DynamicRouterModService&gt;().registerDynamicRouter()</c>.
/// In SPT 4.x, extending <see cref="DynamicRouter"/> and decorating with
/// <c>[Injectable]</c> causes the DI system to discover and register the
/// route automatically.
/// </para>
/// </summary>
[Injectable]
public class QuestingBotsDynamicRouter(
    JsonUtil jsonUtil,
    ISptLogger<QuestingBotsDynamicRouter> logger,
    QuestingBotsConfigLoader configLoader,
    CommonUtils commonUtils,
    ConfigServer configServer
)
    : DynamicRouter(
        jsonUtil,
        [
            // ── POST /QuestingBots/AdjustPScavChance/{factor} ────────────
            // The final URL segment is a floating-point multiplier applied
            // to the base PScav conversion chance that was stored at startup.
            // A factor of 0.0 disables PScav spawning entirely; 1.0 restores
            // the original chance.
            new RouteAction<EmptyRequestData>(
                "/QuestingBots/AdjustPScavChance/",
                async (url, info, sessionId, output) =>
                {
                    if (!configLoader.Config.Enabled)
                    {
                        return jsonUtil.Serialize(new { resp = "OK" }) ?? "{}";
                    }

                    // Extract the factor from the last URL segment
                    var urlParts = url.Split('/');
                    if (!double.TryParse(urlParts[^1], out var factor))
                    {
                        return jsonUtil.Serialize(new { resp = "ERROR", message = "Invalid factor" }) ?? "{}";
                    }

                    var botConfig = configServer.GetConfig<BotConfig>();
                    var basePScavChance = configLoader.Config.BasePScavConversionChance;
                    botConfig.ChanceAssaultScavHasPlayerScavName = (int)Math.Round(basePScavChance * factor);
                    commonUtils.LogInfo($"Adjusted PScav spawn chance to {botConfig.ChanceAssaultScavHasPlayerScavName}%");

                    return jsonUtil.Serialize(new { resp = "OK" }) ?? "{}";
                }
            ),
        ]
    ) { }

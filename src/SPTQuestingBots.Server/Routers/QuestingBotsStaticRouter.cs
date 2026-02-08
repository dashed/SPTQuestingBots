using Newtonsoft.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using SPTQuestingBots.Server.Configuration;

namespace SPTQuestingBots.Server.Routers;

/// <summary>
/// Registers static HTTP routes consumed by the QuestingBots client plugin.
///
/// <para>
/// Static routes have a fixed URL and always return the same type of data
/// regardless of request content. The client plugin calls these endpoints
/// during raid initialisation to fetch configuration and quest data.
/// </para>
///
/// <para>
/// <b>Routes registered:</b>
/// </para>
///
/// <list type="table">
///   <listheader>
///     <term>Endpoint</term>
///     <description>Purpose</description>
///   </listheader>
///   <item>
///     <term><c>/QuestingBots/GetConfig</c></term>
///     <description>Returns the full mod configuration (<see cref="QuestingBotsConfig"/>).</description>
///   </item>
///   <item>
///     <term><c>/QuestingBots/GetAllQuestTemplates</c></term>
///     <description>Returns all EFT quest templates from the database.</description>
///   </item>
///   <item>
///     <term><c>/QuestingBots/GetEFTQuestSettings</c></term>
///     <description>Returns the <c>eftQuestSettings.json</c> quest override data.</description>
///   </item>
///   <item>
///     <term><c>/QuestingBots/GetZoneAndItemQuestPositions</c></term>
///     <description>Returns quest zone and item position coordinates.</description>
///   </item>
///   <item>
///     <term><c>/QuestingBots/GetScavRaidSettings</c></term>
///     <description>Returns the Scav raid time settings per map.</description>
///   </item>
///   <item>
///     <term><c>/QuestingBots/GetUSECChance</c></term>
///     <description>Returns the configured USEC faction probability for PMCs.</description>
///   </item>
/// </list>
///
/// <para>
/// <b>Ported from:</b> <c>mod.ts → preSptLoad()</c> static router registrations
/// (TypeScript / SPT 3.x). The original used
/// <c>container.resolve&lt;StaticRouterModService&gt;().registerStaticRouter()</c>.
/// In SPT 4.x, extending <see cref="StaticRouter"/> and decorating with
/// <c>[Injectable]</c> causes the DI system to discover and register the
/// routes automatically.
/// </para>
/// </summary>
[Injectable]
public class QuestingBotsStaticRouter(
    JsonUtil jsonUtil,
    QuestingBotsConfigLoader configLoader,
    QuestHelper questHelper,
    ConfigServer configServer)
    : StaticRouter(
        jsonUtil,
        [
            // ── GET /QuestingBots/GetConfig ───────────────────────────────
            // Returns the complete mod configuration so the client plugin
            // can mirror server-side settings (spawning rules, quest
            // parameters, debug flags, etc.).
            new RouteAction<EmptyRequestData>(
                "/QuestingBots/GetConfig",
                async (url, info, sessionId, output) =>
                {
                    // Use Newtonsoft serialization because QuestingBotsConfig
                    // uses [JsonProperty] attributes (Newtonsoft) for snake_case
                    // mapping. SPT's jsonUtil uses System.Text.Json which would
                    // produce PascalCase keys, breaking the client plugin.
                    return JsonConvert.SerializeObject(configLoader.Config);
                }
            ),
            // ── GET /QuestingBots/GetAllQuestTemplates ────────────────────
            // Returns every quest template from the SPT database. The
            // client uses this to build the quest pool that bots can pursue.
            new RouteAction<EmptyRequestData>(
                "/QuestingBots/GetAllQuestTemplates",
                async (url, info, sessionId, output) =>
                {
                    var templates = questHelper.GetQuestsFromDb();
                    return jsonUtil.Serialize(new { templates }) ?? "{}";
                }
            ),
            // ── GET /QuestingBots/GetEFTQuestSettings ─────────────────────
            // Returns the eftQuestSettings.json file which contains per-quest
            // overrides (e.g. custom waypoints, step modifications).
            new RouteAction<EmptyRequestData>(
                "/QuestingBots/GetEFTQuestSettings",
                async (url, info, sessionId, output) =>
                {
                    var settings = configLoader.LoadJsonFile<object>(
                        "config/eftQuestSettings.json"
                    );
                    return jsonUtil.Serialize(new { settings }) ?? "{}";
                }
            ),
            // ── GET /QuestingBots/GetZoneAndItemQuestPositions ────────────
            // Returns coordinates for quest zones and item placement
            // positions used when generating bot quest objectives.
            new RouteAction<EmptyRequestData>(
                "/QuestingBots/GetZoneAndItemQuestPositions",
                async (url, info, sessionId, output) =>
                {
                    var zoneAndItemPositions = configLoader.LoadJsonFile<object>(
                        "config/zoneAndItemQuestPositions.json"
                    );
                    return jsonUtil.Serialize(new { zoneAndItemPositions }) ?? "{}";
                }
            ),
            // ── GET /QuestingBots/GetScavRaidSettings ─────────────────────
            // Returns SPT's Scav raid time settings. The client uses these
            // to calculate remaining raid time when spawning as a Scav,
            // which affects quest eligibility and bot behaviour.
            new RouteAction<EmptyRequestData>(
                "/QuestingBots/GetScavRaidSettings",
                async (url, info, sessionId, output) =>
                {
                    var locationConfig = configServer.GetConfig<LocationConfig>();
                    return jsonUtil.Serialize(
                            new { maps = locationConfig.ScavRaidTimeSettings.Maps }
                        ) ?? "{}";
                }
            ),
            // ── GET /QuestingBots/GetUSECChance ──────────────────────────
            // Returns the probability that a randomly generated PMC will be
            // USEC (vs BEAR). Used by the client's PMC generation logic to
            // match the server's faction distribution.
            new RouteAction<EmptyRequestData>(
                "/QuestingBots/GetUSECChance",
                async (url, info, sessionId, output) =>
                {
                    var pmcConfig = configServer.GetConfig<PmcConfig>();
                    return jsonUtil.Serialize(new { usecChance = pmcConfig.IsUsec }) ?? "{}";
                }
            ),
        ]
    )
{ }

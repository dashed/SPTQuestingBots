using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Bot;

namespace SPTQuestingBots.Server.Models;

/// <summary>
/// Extended bot generation request that includes a flag indicating whether
/// the generated bots should be converted into Player Scavs.
///
/// <para>
/// The QuestingBots client plugin adds a <c>GeneratePScav</c> field to the
/// standard <c>/client/game/bot/generate</c> request body. When set to
/// <c>true</c>, the server post-processes each assault bot to look like a
/// Player Scav: adding a random PMC name and setting a randomised game
/// version and member category.
/// </para>
///
/// <para>
/// <b>Ported from:</b> <c>mod.ts → IGenerateBotsRequestDataWithPScavForced</c>
/// (TypeScript / SPT 3.x).
/// </para>
/// </summary>
public record GenerateBotsRequestDataWithPScav : GenerateBotsRequestData
{
    [JsonPropertyName("GeneratePScav")]
    public bool GeneratePScav { get; set; }
}

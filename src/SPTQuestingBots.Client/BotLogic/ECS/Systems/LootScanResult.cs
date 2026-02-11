using System.Runtime.CompilerServices;

namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// Result of a single loot detection from the scanning system.
/// Value type for SoA-friendly buffer storage, zero-allocation iteration.
/// Pure C# — no Unity or EFT dependencies.
/// </summary>
public struct LootScanResult
{
    /// <summary>Unique ID of the loot object (Item.Id hash or container instance ID).</summary>
    public int Id;

    /// <summary>World position X of the loot.</summary>
    public float X;

    /// <summary>World position Y of the loot.</summary>
    public float Y;

    /// <summary>World position Z of the loot.</summary>
    public float Z;

    /// <summary>Loot type (see <see cref="LootTargetType"/>).</summary>
    public byte Type;

    /// <summary>Estimated value of the loot (handbook price or gear value).</summary>
    public float Value;

    /// <summary>Squared distance from the bot to this loot.</summary>
    public float DistanceSqr;

    /// <summary>Whether this result represents a gear upgrade opportunity.</summary>
    public bool IsGearUpgrade;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ComputeDistanceSqr(float botX, float botY, float botZ, float lootX, float lootY, float lootZ)
    {
        float dx = botX - lootX;
        float dy = botY - lootY;
        float dz = botZ - lootZ;
        return dx * dx + dy * dy + dz * dz;
    }
}

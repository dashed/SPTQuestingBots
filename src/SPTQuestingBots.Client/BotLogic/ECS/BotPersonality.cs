namespace SPTQuestingBots.BotLogic.ECS;

/// <summary>
/// Bot personality classification derived from difficulty setting.
/// Stored as byte in <see cref="BotEntity.Personality"/> for dense packing.
/// </summary>
public static class BotPersonality
{
    public const byte Timid = 0;
    public const byte Cautious = 1;
    public const byte Normal = 2;
    public const byte Aggressive = 3;
    public const byte Reckless = 4;
}

/// <summary>
/// Pure C# helper for personality assignment and aggression lookup.
/// No Unity or EFT dependencies — fully testable in net9.0.
/// </summary>
public static class PersonalityHelper
{
    /// <summary>Aggression values indexed by personality byte.</summary>
    private static readonly float[] AggressionTable = { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f };

    /// <summary>Cumulative weights for weighted random fallback (out of 100).</summary>
    private static readonly int[] CumulativeWeights = { 10, 35, 70, 90, 100 };

    /// <summary>
    /// Get the aggression value (0.0-1.0) for a personality.
    /// Returns 0.5 (Normal) for out-of-range values.
    /// </summary>
    public static float GetAggression(byte personality)
    {
        if (personality < AggressionTable.Length)
        {
            return AggressionTable[personality];
        }

        return 0.5f;
    }

    /// <summary>
    /// Map BotDifficulty ordinal to a personality with randomized spread.
    /// Each difficulty tier has a primary personality (60%) and adjacent ones (20% each).
    /// easy(0)→mostly Cautious, normal(1)→mostly Normal, hard(2)→mostly Aggressive, impossible(3)→mostly Reckless.
    /// </summary>
    public static byte FromDifficulty(int difficultyOrdinal, System.Random rng)
    {
        byte center;
        switch (difficultyOrdinal)
        {
            case 0:
                center = BotPersonality.Cautious;
                break;
            case 1:
                center = BotPersonality.Normal;
                break;
            case 2:
                center = BotPersonality.Aggressive;
                break;
            case 3:
                center = BotPersonality.Reckless;
                break;
            default:
                return RandomFallback(rng);
        }

        int roll = rng.Next(100);
        if (roll < 60)
            return center;
        if (roll < 80)
            return (byte)System.Math.Max(0, center - 1);
        return (byte)System.Math.Min(4, center + 1);
    }

    /// <summary>
    /// Weighted random personality: 10% Timid, 25% Cautious, 35% Normal, 20% Aggressive, 10% Reckless.
    /// </summary>
    public static byte RandomFallback(System.Random rng)
    {
        int roll = rng.Next(100);
        for (int i = 0; i < CumulativeWeights.Length; i++)
        {
            if (roll < CumulativeWeights[i])
            {
                return (byte)i;
            }
        }

        return BotPersonality.Normal;
    }
}

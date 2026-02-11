using System;
using System.Collections.Generic;

namespace SPTQuestingBots.ZoneMovement.Core;

/// <summary>
/// Per-map convergence field parameters: radius, force multiplier, and enable flag.
/// Pure C# — no Unity dependencies.
/// </summary>
public sealed class ConvergenceMapConfig
{
    /// <summary>Maximum distance (meters) at which the convergence field attracts bots.</summary>
    public float Radius { get; set; }

    /// <summary>Force multiplier applied to the convergence pull (1.0 = normal).</summary>
    public float Force { get; set; }

    /// <summary>Whether convergence is active on this map.</summary>
    public bool Enabled { get; set; }

    public ConvergenceMapConfig(float radius, float force, bool enabled)
    {
        Radius = radius;
        Force = force;
        Enabled = enabled;
    }

    /// <summary>
    /// Returns per-map defaults. Keys are lowercase map IDs matching BSG location names.
    /// </summary>
    public static Dictionary<string, ConvergenceMapConfig> GetDefaults()
    {
        return new Dictionary<string, ConvergenceMapConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["factory4_day"] = new ConvergenceMapConfig(0f, 0f, false),
            ["factory4_night"] = new ConvergenceMapConfig(0f, 0f, false),
            ["bigmap"] = new ConvergenceMapConfig(250f, 1.0f, true),
            ["woods"] = new ConvergenceMapConfig(400f, 0.8f, true),
            ["shoreline"] = new ConvergenceMapConfig(300f, 0.9f, true),
            ["lighthouse"] = new ConvergenceMapConfig(300f, 0.9f, true),
            ["rezervbase"] = new ConvergenceMapConfig(250f, 1.0f, true),
            ["interchange"] = new ConvergenceMapConfig(300f, 0.9f, true),
            ["laboratory"] = new ConvergenceMapConfig(150f, 1.2f, true),
            ["tarkovstreets"] = new ConvergenceMapConfig(350f, 0.9f, true),
            ["sandbox"] = new ConvergenceMapConfig(300f, 0.9f, true),
            ["sandbox_high"] = new ConvergenceMapConfig(300f, 0.9f, true),
        };
    }

    /// <summary>Default config used when a map is not found in the per-map dictionary.</summary>
    public static readonly ConvergenceMapConfig Default = new ConvergenceMapConfig(300f, 1.0f, true);

    /// <summary>
    /// Looks up the config for a map, falling back to <see cref="Default"/>.
    /// </summary>
    /// <param name="mapId">BSG location ID (case-insensitive).</param>
    /// <param name="overrides">Per-map overrides from user config. May be null.</param>
    public static ConvergenceMapConfig GetForMap(string mapId, Dictionary<string, ConvergenceMapConfig> overrides)
    {
        if (overrides != null && overrides.TryGetValue(mapId, out var cfg))
        {
            return cfg;
        }

        var defaults = GetDefaults();
        if (defaults.TryGetValue(mapId, out var def))
        {
            return def;
        }

        return Default;
    }
}

/// <summary>
/// A point of convergence pull from a combat event, with pre-computed decayed strength.
/// Pure value type — no allocations.
/// </summary>
public struct CombatPullPoint
{
    /// <summary>World X position of the combat event.</summary>
    public float X;

    /// <summary>World Z position of the combat event.</summary>
    public float Z;

    /// <summary>Decayed pull strength (0.0–1.0+). Higher = stronger attraction.</summary>
    public float Strength;
}

/// <summary>
/// Computes a time-based multiplier for convergence weight based on raid progression.
/// Pure C# — no Unity dependencies.
/// </summary>
public static class ConvergenceTimeWeight
{
    /// <summary>Weight at the start of the raid (raidTimeNormalized = 0.0).</summary>
    public const float EarlyWeight = 1.3f;

    /// <summary>Weight during the middle of the raid.</summary>
    public const float MidWeight = 1.0f;

    /// <summary>Weight at the end of the raid (raidTimeNormalized = 1.0).</summary>
    public const float LateWeight = 0.7f;

    /// <summary>Raid time at which the early→mid transition ends.</summary>
    public const float EarlyEnd = 0.2f;

    /// <summary>Raid time at which the mid→late transition begins.</summary>
    public const float LateStart = 0.7f;

    /// <summary>
    /// Computes the convergence weight multiplier for a given raid time.
    /// Smoothly lerps between early (1.3×), mid (1.0×), and late (0.7×) phases.
    /// </summary>
    /// <param name="raidTimeNormalized">Raid progress from 0.0 (start) to 1.0 (end).</param>
    /// <returns>A multiplier in the range [0.7, 1.3].</returns>
    public static float ComputeMultiplier(float raidTimeNormalized)
    {
        float t = Math.Max(0f, Math.Min(1f, raidTimeNormalized));

        if (t <= EarlyEnd)
        {
            // Early phase: lerp from EarlyWeight down to MidWeight
            float phase = t / EarlyEnd;
            return EarlyWeight + (MidWeight - EarlyWeight) * phase;
        }

        if (t >= LateStart)
        {
            // Late phase: lerp from MidWeight down to LateWeight
            float phase = (t - LateStart) / (1f - LateStart);
            return MidWeight + (LateWeight - MidWeight) * phase;
        }

        // Mid phase: constant
        return MidWeight;
    }
}

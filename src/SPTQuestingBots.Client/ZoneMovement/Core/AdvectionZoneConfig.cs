using System;
using System.Collections.Generic;

namespace SPTQuestingBots.ZoneMovement.Core;

/// <summary>
/// Base entry for an advection zone with force range, radius, decay, and time/boss multipliers.
/// Pure C# — no Unity dependencies.
/// </summary>
public class AdvectionZoneEntry
{
    /// <summary>Minimum sampled force (can be negative for repulsor).</summary>
    public float ForceMin { get; set; }

    /// <summary>Maximum sampled force.</summary>
    public float ForceMax { get; set; }

    /// <summary>Maximum effective radius in meters.</summary>
    public float Radius { get; set; }

    /// <summary>Falloff exponent. 1.0 = linear, &lt;1 = soft falloff, &gt;1 = sharp edge.</summary>
    public float Decay { get; set; } = 1.0f;

    /// <summary>Force multiplier at the start of the raid (raidTimeNormalized = 0).</summary>
    public float EarlyMultiplier { get; set; } = 1.0f;

    /// <summary>Force multiplier at the end of the raid (raidTimeNormalized = 1).</summary>
    public float LateMultiplier { get; set; } = 1.0f;

    /// <summary>Force multiplier when the map boss is alive.</summary>
    public float BossAliveMultiplier { get; set; } = 1.0f;

    public AdvectionZoneEntry() { }

    public AdvectionZoneEntry(
        float forceMin,
        float forceMax,
        float radius,
        float decay = 1.0f,
        float earlyMultiplier = 1.0f,
        float lateMultiplier = 1.0f,
        float bossAliveMultiplier = 1.0f
    )
    {
        ForceMin = forceMin;
        ForceMax = forceMax;
        Radius = radius;
        Decay = decay;
        EarlyMultiplier = earlyMultiplier;
        LateMultiplier = lateMultiplier;
        BossAliveMultiplier = bossAliveMultiplier;
    }
}

/// <summary>
/// A builtin zone tied to a BSG BotZone name (e.g. "ZoneDormitory").
/// Position is resolved at runtime from spawn point centroids.
/// </summary>
public class BuiltinZoneEntry : AdvectionZoneEntry
{
    /// <summary>BSG BotZone name (e.g. "ZoneDormitory", "ZoneGasStation").</summary>
    public string ZoneName { get; set; }

    public BuiltinZoneEntry() { }

    public BuiltinZoneEntry(
        string zoneName,
        float forceMin,
        float forceMax,
        float radius,
        float decay = 1.0f,
        float earlyMultiplier = 1.0f,
        float lateMultiplier = 1.0f,
        float bossAliveMultiplier = 1.0f
    )
        : base(forceMin, forceMax, radius, decay, earlyMultiplier, lateMultiplier, bossAliveMultiplier)
    {
        ZoneName = zoneName;
    }
}

/// <summary>
/// A custom zone at an arbitrary world position (X/Z floats, no Unity Vector3).
/// </summary>
public class CustomZoneEntry : AdvectionZoneEntry
{
    /// <summary>World X position.</summary>
    public float X { get; set; }

    /// <summary>World Z position.</summary>
    public float Z { get; set; }

    public CustomZoneEntry() { }

    public CustomZoneEntry(
        float x,
        float z,
        float forceMin,
        float forceMax,
        float radius,
        float decay = 1.0f,
        float earlyMultiplier = 1.0f,
        float lateMultiplier = 1.0f,
        float bossAliveMultiplier = 1.0f
    )
        : base(forceMin, forceMax, radius, decay, earlyMultiplier, lateMultiplier, bossAliveMultiplier)
    {
        X = x;
        Z = z;
    }
}

/// <summary>
/// Per-map advection zone definitions: builtin zones (by name) + custom zones (by position).
/// </summary>
public class AdvectionMapZones
{
    /// <summary>Builtin zones keyed by BSG BotZone name.</summary>
    public Dictionary<string, BuiltinZoneEntry> BuiltinZones { get; set; } =
        new Dictionary<string, BuiltinZoneEntry>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Custom zones at arbitrary world positions.</summary>
    public List<CustomZoneEntry> CustomZones { get; set; } = new List<CustomZoneEntry>();

    public AdvectionMapZones() { }

    public AdvectionMapZones(Dictionary<string, BuiltinZoneEntry> builtinZones, List<CustomZoneEntry> customZones)
    {
        BuiltinZones = builtinZones ?? new Dictionary<string, BuiltinZoneEntry>(StringComparer.OrdinalIgnoreCase);
        CustomZones = customZones ?? new List<CustomZoneEntry>();
    }
}

/// <summary>
/// Provides per-map advection zone defaults with JSON override resolution.
/// Follows the same pattern as <see cref="ConvergenceMapConfig"/>: hardcoded defaults
/// with optional JSON overrides.
/// Pure C# — no Unity dependencies.
/// </summary>
public static class AdvectionZoneConfig
{
    /// <summary>
    /// Returns per-map advection zone defaults. Keys are lowercase BSG location IDs.
    /// </summary>
    public static Dictionary<string, AdvectionMapZones> GetDefaults()
    {
        return new Dictionary<string, AdvectionMapZones>(StringComparer.OrdinalIgnoreCase)
        {
            ["bigmap"] = new AdvectionMapZones(
                new Dictionary<string, BuiltinZoneEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ZoneDormitory"] = new BuiltinZoneEntry(
                        "ZoneDormitory",
                        -0.5f,
                        1.5f,
                        250f,
                        earlyMultiplier: 1.5f,
                        lateMultiplier: 0.5f
                    ),
                    ["ZoneGasStation"] = new BuiltinZoneEntry("ZoneGasStation", -0.25f, 0.75f, 200f),
                    ["ZoneScavBase"] = new BuiltinZoneEntry("ZoneScavBase", -0.5f, 1.0f, 350f),
                    ["ZoneOldAZS"] = new BuiltinZoneEntry("ZoneOldAZS", -0.25f, 0.25f, 150f),
                },
                new List<CustomZoneEntry>()
            ),
            ["interchange"] = new AdvectionMapZones(
                new Dictionary<string, BuiltinZoneEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ZoneCenter"] = new BuiltinZoneEntry("ZoneCenter", -0.25f, 1.0f, 500f, decay: 0.75f, bossAliveMultiplier: 1.5f),
                },
                new List<CustomZoneEntry>()
            ),
            ["shoreline"] = new AdvectionMapZones(
                new Dictionary<string, BuiltinZoneEntry>(StringComparer.OrdinalIgnoreCase),
                new List<CustomZoneEntry>
                {
                    new CustomZoneEntry(-250f, -100f, 0.5f, 1.2f, 300f, earlyMultiplier: 1.3f),
                    new CustomZoneEntry(160f, -270f, 0.3f, 0.8f, 250f),
                }
            ),
            ["woods"] = new AdvectionMapZones(
                new Dictionary<string, BuiltinZoneEntry>(StringComparer.OrdinalIgnoreCase),
                new List<CustomZoneEntry>
                {
                    new CustomZoneEntry(-550f, -200f, 0.5f, 1.0f, 300f),
                    new CustomZoneEntry(135f, -750f, 0.3f, 0.7f, 200f),
                }
            ),
            ["rezervbase"] = new AdvectionMapZones(
                new Dictionary<string, BuiltinZoneEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ZoneSubStorage"] = new BuiltinZoneEntry("ZoneSubStorage", 0.3f, 0.8f, 300f),
                    ["ZoneBarrack"] = new BuiltinZoneEntry("ZoneBarrack", 0.3f, 0.8f, 300f),
                },
                new List<CustomZoneEntry>()
            ),
            ["laboratory"] = new AdvectionMapZones(
                new Dictionary<string, BuiltinZoneEntry>(StringComparer.OrdinalIgnoreCase),
                new List<CustomZoneEntry> { new CustomZoneEntry(0f, 0f, 0.8f, 1.2f, 150f) }
            ),
            ["factory4_day"] = new AdvectionMapZones(),
            ["factory4_night"] = new AdvectionMapZones(),
        };
    }

    /// <summary>Default empty zone config used when a map has no zone definitions.</summary>
    public static readonly AdvectionMapZones Default = new AdvectionMapZones();

    /// <summary>
    /// Looks up the zone config for a map, falling back to <see cref="Default"/>.
    /// Overrides take precedence over hardcoded defaults.
    /// </summary>
    /// <param name="mapId">BSG location ID (case-insensitive).</param>
    /// <param name="overrides">Per-map overrides from user config. May be null.</param>
    public static AdvectionMapZones GetForMap(string mapId, Dictionary<string, AdvectionMapZones> overrides)
    {
        if (overrides != null && overrides.TryGetValue(mapId, out var cfg))
            return cfg;

        var defaults = GetDefaults();
        if (defaults.TryGetValue(mapId, out var def))
            return def;

        return Default;
    }
}

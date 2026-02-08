using System;
using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Core;

/// <summary>
/// Tracks per-bot state for field composition, giving each bot a unique momentum
/// direction and noise angle. This eliminates herd movement where all bots in the
/// same cell would otherwise choose the same direction.
/// </summary>
/// <remarks>
/// <para>
/// Momentum is derived on-the-fly from the bot's previous destination (matching
/// Phobos's <c>LocationSystem.RequestNear()</c> pattern: <c>requestCoords - previousCoords</c>).
/// </para>
/// <para>
/// Noise is seeded per-bot so each bot gets a consistent but unique random angle
/// that varies over time (rotates every 5 seconds).
/// </para>
/// </remarks>
public sealed class BotFieldState
{
    /// <summary>The bot's last assigned destination position.</summary>
    public Vector3 PreviousDestination { get; set; }

    /// <summary>Seed for per-bot noise generation (set once, derived from bot profile ID).</summary>
    public int NoiseSeed { get; }

    /// <summary>
    /// Creates a new per-bot field state.
    /// </summary>
    /// <param name="noiseSeed">
    /// Seed for noise generation, typically from <c>botProfileId.GetHashCode()</c>.
    /// </param>
    public BotFieldState(int noiseSeed)
    {
        NoiseSeed = noiseSeed;
        PreviousDestination = Vector3.zero;
    }

    /// <summary>
    /// Computes the normalized XZ-plane momentum direction from the previous destination
    /// to the given position.
    /// </summary>
    /// <param name="currentPosition">The bot's current world position.</param>
    /// <returns>Normalized (momX, momZ) tuple, or (0, 0) if positions are coincident.</returns>
    public (float momX, float momZ) ComputeMomentum(Vector3 currentPosition) =>
        ZoneMathUtils.ComputeMomentum(PreviousDestination, currentPosition);

    /// <summary>
    /// Returns a per-bot noise angle in the range [-PI, PI].
    /// The angle is deterministic for a given seed and time bucket (changes every 5 seconds).
    /// </summary>
    /// <param name="time">Current time in seconds (e.g. <c>Time.time</c>).</param>
    /// <returns>Noise angle in radians, in [-PI, PI].</returns>
    public float GetNoiseAngle(float time)
    {
        int timeBucket = (int)(time / 5f);
        var rng = new System.Random(NoiseSeed ^ timeBucket);
        return (float)(rng.NextDouble() * 2.0 * Math.PI - Math.PI);
    }
}

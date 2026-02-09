using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SPTQuestingBots.Models.Pathing;

/// <summary>
/// Urgency level that controls how tolerant the sprint decision is of path turns.
/// Higher urgency allows sprinting through sharper turns.
/// </summary>
public enum SprintUrgency
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Pure-logic sprint decision based on path smoothness analysis.
/// Measures "angle jitter" — the cumulative sharpness of upcoming path corners
/// within a lookahead window. Bots should slow down before sharp turns and
/// sprint on straight paths.
///
/// Inspired by Phobos's CalculatePathAngleJitter / CanSprint pattern.
/// </summary>
public static class SprintAngleJitter
{
    /// <summary>
    /// Default angle jitter thresholds (degrees) per urgency level.
    /// </summary>
    public const float ThresholdHigh = 45f;
    public const float ThresholdMedium = 30f;
    public const float ThresholdLow = 20f;

    /// <summary>
    /// Computes the maximum angle between consecutive path segments within a
    /// lookahead distance from startIndex. Angles are measured in the XZ plane
    /// to avoid terrain height distortion.
    ///
    /// Returns the max angle change in degrees. Returns 0 if fewer than 3 corners
    /// are available in the window.
    /// </summary>
    /// <param name="corners">Path corners array.</param>
    /// <param name="startIndex">Index to start looking ahead from.</param>
    /// <param name="lookaheadDistance">Max cumulative distance to consider.</param>
    /// <returns>Maximum angle jitter in degrees (0 = perfectly straight).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ComputeAngleJitter(Vector3[] corners, int startIndex, float lookaheadDistance)
    {
        if (corners == null || corners.Length < 2)
            return 0f;

        if (startIndex < 0 || startIndex >= corners.Length)
            return 0f;

        float maxAngle = 0f;
        float accumulatedDistance = 0f;

        // Need at least two segments (three points) to measure an angle change.
        // Walk from startIndex, accumulating distance, and measure the angle
        // between each pair of consecutive segments.
        for (int i = startIndex; i < corners.Length - 2; i++)
        {
            // Segment A: corners[i] → corners[i+1]
            // Segment B: corners[i+1] → corners[i+2]
            float segAx = corners[i + 1].x - corners[i].x;
            float segAz = corners[i + 1].z - corners[i].z;
            float segBx = corners[i + 2].x - corners[i + 1].x;
            float segBz = corners[i + 2].z - corners[i + 1].z;

            float segALen = (float)Math.Sqrt(segAx * segAx + segAz * segAz);
            accumulatedDistance += segALen;

            if (accumulatedDistance > lookaheadDistance)
                break;

            float segBLen = (float)Math.Sqrt(segBx * segBx + segBz * segBz);

            // Skip degenerate segments
            if (segALen < 1e-6f || segBLen < 1e-6f)
                continue;

            // Angle via dot product: cos(theta) = (A · B) / (|A| * |B|)
            float dot = segAx * segBx + segAz * segBz;
            float cosAngle = dot / (segALen * segBLen);

            // Clamp to [-1, 1] to handle floating-point edge cases
            cosAngle = Math.Max(-1f, Math.Min(1f, cosAngle));

            float angle = (float)Math.Acos(cosAngle) * (180f / (float)Math.PI);

            if (angle > maxAngle)
                maxAngle = angle;
        }

        return maxAngle;
    }

    /// <summary>
    /// Determines whether a bot can sprint given the angle jitter of its path and
    /// the current urgency level. Uses the default thresholds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanSprint(float angleJitter, SprintUrgency urgency)
    {
        return CanSprint(angleJitter, urgency, ThresholdHigh, ThresholdMedium, ThresholdLow);
    }

    /// <summary>
    /// Determines whether a bot can sprint given the angle jitter of its path,
    /// urgency level, and custom thresholds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanSprint(float angleJitter, SprintUrgency urgency, float thresholdHigh, float thresholdMedium, float thresholdLow)
    {
        float threshold = urgency switch
        {
            SprintUrgency.High => thresholdHigh,
            SprintUrgency.Medium => thresholdMedium,
            SprintUrgency.Low => thresholdLow,
            _ => thresholdMedium,
        };

        return angleJitter <= threshold;
    }
}

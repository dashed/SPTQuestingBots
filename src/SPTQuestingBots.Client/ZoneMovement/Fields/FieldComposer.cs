using System;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.ZoneMovement.Fields;

/// <summary>
/// Combines multiple vector field components (advection, convergence, momentum, noise)
/// into a single composite direction vector that guides bot movement.
/// </summary>
/// <remarks>
/// <para>
/// Each field component is weighted independently:
/// <list type="bullet">
///   <item><description><b>Convergence</b> (default 1.0): Strongest — pulls bots toward human players.</description></item>
///   <item><description><b>Advection</b> (default 0.5): Moderate — pushes bots toward interesting geographic zones.</description></item>
///   <item><description><b>Momentum</b> (default 0.5): Moderate — smooths paths, prevents zigzagging.</description></item>
///   <item><description><b>Noise</b> (default 0.3): Mild — adds randomness to prevent deterministic movement.</description></item>
/// </list>
/// </para>
/// <para>
/// Noise is applied as a rotation to the composite vector rather than an additive component,
/// ensuring it doesn't change the magnitude of the result.
/// </para>
/// </remarks>
public sealed class FieldComposer
{
    /// <summary>Weight applied to the convergence (player attraction) component.</summary>
    public float ConvergenceWeight { get; }

    /// <summary>Weight applied to the advection (zone attraction + crowd repulsion) component.</summary>
    public float AdvectionWeight { get; }

    /// <summary>Weight applied to the bot's current momentum (travel direction).</summary>
    public float MomentumWeight { get; }

    /// <summary>Weight applied to the noise rotation angle.</summary>
    public float NoiseWeight { get; }

    /// <summary>
    /// Creates a new field composer with configurable weights.
    /// </summary>
    /// <param name="convergenceWeight">Weight for player attraction. Default 1.0.</param>
    /// <param name="advectionWeight">Weight for zone attraction. Default 0.5.</param>
    /// <param name="momentumWeight">Weight for momentum smoothing. Default 0.5.</param>
    /// <param name="noiseWeight">Weight for random rotation. Default 0.3.</param>
    public FieldComposer(
        float convergenceWeight = 1.0f,
        float advectionWeight = 0.5f,
        float momentumWeight = 0.5f,
        float noiseWeight = 0.3f
    )
    {
        ConvergenceWeight = convergenceWeight;
        AdvectionWeight = advectionWeight;
        MomentumWeight = momentumWeight;
        NoiseWeight = noiseWeight;
    }

    /// <summary>
    /// Computes the normalized composite direction from all field components.
    /// </summary>
    /// <param name="advectionX">X component of the advection field vector.</param>
    /// <param name="advectionZ">Z component of the advection field vector.</param>
    /// <param name="convergenceX">X component of the convergence field vector.</param>
    /// <param name="convergenceZ">Z component of the convergence field vector.</param>
    /// <param name="momentumX">X component of the bot's current travel direction.</param>
    /// <param name="momentumZ">Z component of the bot's current travel direction.</param>
    /// <param name="noiseAngleRadians">
    /// Random angle in radians (typically from <c>UnityEngine.Random.Range(-π, π)</c>).
    /// Scaled by <see cref="NoiseWeight"/> before application.
    /// </param>
    /// <param name="outX">X component of the normalized composite direction.</param>
    /// <param name="outZ">Z component of the normalized composite direction.</param>
    public void GetCompositeDirection(
        float advectionX,
        float advectionZ,
        float convergenceX,
        float convergenceZ,
        float momentumX,
        float momentumZ,
        float noiseAngleRadians,
        out float outX,
        out float outZ
    )
    {
        // Weighted sum of field components
        float rx = advectionX * AdvectionWeight + convergenceX * ConvergenceWeight + momentumX * MomentumWeight;
        float rz = advectionZ * AdvectionWeight + convergenceZ * ConvergenceWeight + momentumZ * MomentumWeight;

        // Apply noise as rotation to the composite direction
        if (NoiseWeight > 0.001f && (Math.Abs(rx) > 0.001f || Math.Abs(rz) > 0.001f))
        {
            float angle = noiseAngleRadians * NoiseWeight;
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);
            float nx = rx * cos - rz * sin;
            float nz = rx * sin + rz * cos;
            rx = nx;
            rz = nz;
        }

        // Normalize to unit direction
        float mag = (float)Math.Sqrt(rx * rx + rz * rz);
        if (mag > 0.001f)
        {
            outX = rx / mag;
            outZ = rz / mag;
        }
        else
        {
            outX = 0f;
            outZ = 0f;
        }
        LoggingController.LogDebug(
            "[FieldComposer] Composed dir=("
                + outX.ToString("F2")
                + ","
                + outZ.ToString("F2")
                + ") adv=("
                + advectionX.ToString("F2")
                + ","
                + advectionZ.ToString("F2")
                + ") conv=("
                + convergenceX.ToString("F2")
                + ","
                + convergenceZ.ToString("F2")
                + ") mom=("
                + momentumX.ToString("F2")
                + ","
                + momentumZ.ToString("F2")
                + ")"
        );
    }
}

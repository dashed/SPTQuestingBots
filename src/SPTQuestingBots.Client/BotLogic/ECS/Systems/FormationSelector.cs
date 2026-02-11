using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// Delegate for measuring walkable path width at a position along a heading.
/// Returns total width in meters (left + right perpendicular distance).
/// Injected to keep formation logic Unity-free.
/// </summary>
public delegate float PathWidthProbe(float posX, float posY, float posZ, float headingX, float headingZ);

/// <summary>
/// Pure-logic formation type selection with spacing resolution.
/// Wraps <see cref="FormationPositionUpdater.SelectFormation"/> with
/// spacing output for the selected formation type.
/// <para>
/// Pure C# — no Unity or EFT dependencies — fully testable.
/// </para>
/// </summary>
public static class FormationSelector
{
    /// <summary>
    /// Select formation type based on available path width and return the appropriate spacing.
    /// </summary>
    /// <param name="pathWidth">Measured path width in meters.</param>
    /// <param name="switchWidth">Threshold width to switch from Column to Spread.</param>
    /// <param name="spacing">Output: the spacing to use for the selected formation.</param>
    /// <param name="columnSpacing">Spacing for Column formation.</param>
    /// <param name="spreadSpacing">Spacing for Spread formation.</param>
    /// <returns>The selected formation type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FormationType SelectWithSpacing(
        float pathWidth,
        float switchWidth,
        out float spacing,
        float columnSpacing,
        float spreadSpacing
    )
    {
        var type = FormationPositionUpdater.SelectFormation(pathWidth, switchWidth);
        spacing = type == FormationType.Column ? columnSpacing : spreadSpacing;
        LoggingController.LogDebug(
            "[FormationSelector] Selected "
                + type
                + " (pathWidth="
                + pathWidth
                + ", switchWidth="
                + switchWidth
                + ", spacing="
                + spacing
                + ")"
        );
        return type;
    }
}

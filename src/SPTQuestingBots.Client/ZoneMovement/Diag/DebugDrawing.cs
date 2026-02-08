using UnityEngine;

namespace SPTQuestingBots.ZoneMovement.Diag;

/// <summary>
/// Static helpers for drawing 2D debug primitives using Unity's OnGUI system.
/// <para>
/// Mirrors Phobos's <c>Diag/DebugUI.cs</c> drawing pattern: uses
/// <see cref="GUI.DrawTexture"/> with a 1×1 white pixel and <see cref="GUI.color"/>
/// tinting for all solid shapes. Lines are drawn via <see cref="GUIUtility.RotateAroundPivot"/>.
/// </para>
/// </summary>
public static class DebugDrawing
{
    private static Texture2D whiteTex;

    /// <summary>
    /// Gets or creates a cached 1×1 white texture for drawing.
    /// </summary>
    private static Texture2D WhiteTex
    {
        get
        {
            if (whiteTex == null)
            {
                whiteTex = new Texture2D(1, 1);
                whiteTex.SetPixel(0, 0, Color.white);
                whiteTex.Apply();
            }
            return whiteTex;
        }
    }

    /// <summary>
    /// Draws a filled rectangle with the specified color.
    /// </summary>
    public static void DrawFilledRect(Rect rect, Color color)
    {
        var prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, WhiteTex);
        GUI.color = prev;
    }

    /// <summary>
    /// Draws a rectangle outline with the specified color and thickness.
    /// </summary>
    public static void DrawRectOutline(Rect rect, Color color, float thickness = 1f)
    {
        DrawFilledRect(new Rect(rect.x, rect.y, rect.width, thickness), color); // Top
        DrawFilledRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color); // Bottom
        DrawFilledRect(new Rect(rect.x, rect.y, thickness, rect.height), color); // Left
        DrawFilledRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color); // Right
    }

    /// <summary>
    /// Draws a dot (small filled square) centered at the given position.
    /// </summary>
    public static void DrawDot(Vector2 center, float radius, Color color)
    {
        DrawFilledRect(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), color);
    }

    /// <summary>
    /// Draws a line between two points using rotation-based rendering.
    /// Uses <see cref="GUIUtility.RotateAroundPivot"/> to orient a thin rectangle
    /// along the line direction (standard OnGUI line-drawing pattern).
    /// </summary>
    public static void DrawLine(Vector2 start, Vector2 end, float thickness, Color color)
    {
        var prev = GUI.color;
        GUI.color = color;

        var delta = end - start;
        float length = delta.magnitude;
        if (length < 0.5f)
        {
            GUI.color = prev;
            return;
        }

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        var savedMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y - thickness * 0.5f, length, thickness), WhiteTex);
        GUI.matrix = savedMatrix;

        GUI.color = prev;
    }

    /// <summary>
    /// Draws an arrow from <paramref name="start"/> in the direction of
    /// (<paramref name="dirX"/>, <paramref name="dirY"/>) with the given length.
    /// The arrowhead is two short lines at ±30° from the tip.
    /// </summary>
    public static void DrawArrow(Vector2 start, float dirX, float dirY, float length, float thickness, Color color)
    {
        float mag = Mathf.Sqrt(dirX * dirX + dirY * dirY);
        if (mag < 0.001f)
            return;

        float nx = dirX / mag;
        float ny = dirY / mag;
        var end = new Vector2(start.x + nx * length, start.y + ny * length);
        DrawLine(start, end, thickness, color);

        // Arrowhead: two lines at ±30° from the tip, length = 30% of shaft
        float headLen = length * 0.3f;
        float cos30 = 0.866f;
        float sin30 = 0.5f;
        // Rotate the negative direction by ±30°
        float bx = -nx;
        float by = -ny;
        var head1 = new Vector2(end.x + (bx * cos30 - by * sin30) * headLen, end.y + (bx * sin30 + by * cos30) * headLen);
        var head2 = new Vector2(end.x + (bx * cos30 + by * sin30) * headLen, end.y + (-bx * sin30 + by * cos30) * headLen);
        DrawLine(end, head1, thickness, color);
        DrawLine(end, head2, thickness, color);
    }
}

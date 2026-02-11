using System.Runtime.CompilerServices;

namespace SPTQuestingBots.Helpers;

/// <summary>
/// Reusable frame-based rate limiter. Call <see cref="ShouldRun"/> each frame;
/// it returns true at most once per <see cref="IntervalFrames"/> frames.
/// Inspired by Phobos FramePacing pattern.
/// Pure C# — no Unity dependencies — fully testable in net9.0.
/// </summary>
public sealed class FramePacing
{
    /// <summary>Minimum interval between runs, in frames.</summary>
    public int IntervalFrames { get; }

    private int _nextRunFrame;

    /// <param name="intervalFrames">Minimum frames between runs.</param>
    /// <param name="startFrame">Optional initial frame (defaults to 0, meaning first call always runs).</param>
    public FramePacing(int intervalFrames, int startFrame = 0)
    {
        IntervalFrames = intervalFrames;
        _nextRunFrame = startFrame;
    }

    /// <summary>
    /// Returns true if enough frames have elapsed since the last run.
    /// Advances the next-run threshold on success.
    /// </summary>
    /// <param name="currentFrame">Current frame number (e.g. Time.frameCount).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ShouldRun(int currentFrame)
    {
        if (currentFrame < _nextRunFrame)
        {
            return false;
        }

        _nextRunFrame = currentFrame + IntervalFrames;
        return true;
    }

    /// <summary>
    /// Reset the timer so the next <see cref="ShouldRun"/> call will return true.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _nextRunFrame = 0;
    }
}

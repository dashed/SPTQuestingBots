using System.Runtime.CompilerServices;

namespace SPTQuestingBots.Helpers;

/// <summary>
/// Reusable time-based rate limiter. Call <see cref="ShouldRun"/> each frame;
/// it returns true at most once per <see cref="IntervalSeconds"/>.
/// Inspired by Phobos TimePacing pattern.
/// Pure C# — no Unity dependencies — fully testable in net9.0.
/// </summary>
public sealed class TimePacing
{
    /// <summary>Minimum interval between runs, in seconds.</summary>
    public float IntervalSeconds { get; }

    private float _nextRunTime;

    /// <param name="intervalSeconds">Minimum seconds between runs.</param>
    /// <param name="startTime">Optional initial time (defaults to 0, meaning first call always runs).</param>
    public TimePacing(float intervalSeconds, float startTime = 0f)
    {
        IntervalSeconds = intervalSeconds;
        _nextRunTime = startTime;
    }

    /// <summary>
    /// Returns true if enough time has elapsed since the last run.
    /// Advances the next-run threshold on success.
    /// </summary>
    /// <param name="currentTime">Current time in seconds (e.g. Time.time).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ShouldRun(float currentTime)
    {
        if (currentTime < _nextRunTime)
        {
            return false;
        }

        _nextRunTime = currentTime + IntervalSeconds;
        return true;
    }

    /// <summary>
    /// Reset the timer so the next <see cref="ShouldRun"/> call will return true.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _nextRunTime = 0f;
    }
}

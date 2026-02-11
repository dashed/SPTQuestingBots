using System.Diagnostics;

namespace SPTQuestingBots.BehaviorExtensions;

/// <summary>
/// Per-instance update throttle for BigBrain layers. Encapsulates the delay
/// interval and pause timers so each layer instance maintains independent
/// timing — fixing the previous bug where a <c>static</c> field caused all
/// layer instances across all bots to share a single interval value.
/// </summary>
internal sealed class UpdateThrottle
{
    public const int DefaultIntervalMs = 100;

    private readonly int _intervalMs;
    private readonly Stopwatch _updateTimer = Stopwatch.StartNew();
    private readonly Stopwatch _pauseTimer = Stopwatch.StartNew();
    private float _pauseTimeSeconds;

    public UpdateThrottle(int intervalMs = DefaultIntervalMs)
    {
        _intervalMs = intervalMs;
    }

    public int IntervalMs
    {
        get { return _intervalMs; }
    }

    public bool CanUpdate()
    {
        if (_updateTimer.ElapsedMilliseconds < _intervalMs)
        {
            return false;
        }

        if (_pauseTimer.ElapsedMilliseconds < 1000 * _pauseTimeSeconds)
        {
            return false;
        }

        _updateTimer.Restart();
        return true;
    }

    public void Pause(float minTimeSeconds = 0)
    {
        _pauseTimeSeconds = minTimeSeconds;
        _pauseTimer.Restart();
    }
}

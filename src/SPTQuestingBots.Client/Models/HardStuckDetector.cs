using System.Runtime.CompilerServices;
using SPTQuestingBots.Helpers;
using UnityEngine;

namespace SPTQuestingBots.Models;

public enum HardStuckStatus
{
    None,
    Retrying,
    Teleport,
    Failed,
}

public class HardStuckDetector
{
    private const float StuckRadiusSqr = 3f * 3f;
    private const float StaleThreshold = 0.2f;

    /// <summary>
    /// Timer multiplier when bot is far from its destination (faster escalation).
    /// </summary>
    private const float FarFromDestMultiplier = 1.5f;

    private readonly float _pathRetryDelay;
    private readonly float _teleportDelay;
    private readonly float _failDelay;
    private readonly PositionHistory _positionHistory;
    private readonly RollingAverage _averageSpeed;

    private float _lastUpdateTime;
    private float _timer;
    private bool _initialized;

    public HardStuckStatus Status { get; private set; }
    public float Timer
    {
        get { return _timer; }
    }

    public HardStuckDetector(int historySize = 50, float pathRetryDelay = 5f, float teleportDelay = 10f, float failDelay = 15f)
    {
        _pathRetryDelay = pathRetryDelay;
        _teleportDelay = teleportDelay;
        _failDelay = failDelay;
        _positionHistory = new PositionHistory(historySize);
        _averageSpeed = new RollingAverage(historySize);
    }

    /// <summary>
    /// Returns true if a state transition occurred (caller should check Status for the action).
    /// </summary>
    /// <param name="currentPosition">Bot's current world position.</param>
    /// <param name="currentMoveSpeed">Bot's current movement speed.</param>
    /// <param name="currentTime">Current game time (Time.time).</param>
    /// <param name="squaredDistToDestination">Optional BotMover.SDistDestination. When far from destination, stuck escalation is faster.</param>
    public bool Update(Vector3 currentPosition, float currentMoveSpeed, float currentTime, float? squaredDistToDestination = null)
    {
        _positionHistory.Update(currentPosition);
        _averageSpeed.Update(currentMoveSpeed);

        if (!_initialized)
        {
            _lastUpdateTime = currentTime;
            _initialized = true;
            return false;
        }

        var deltaTime = currentTime - _lastUpdateTime;
        _lastUpdateTime = currentTime;

        // Discard measurements after long periods of dormancy
        if (deltaTime > StaleThreshold)
        {
            ResetInternal();
            return false;
        }

        // Asymmetric speed: use min of current and rolling average
        // (if current is slower, use it to avoid overestimating; else trust average)
        var averageSpeed = _averageSpeed.Value;
        var moveSpeed = currentMoveSpeed <= averageSpeed ? currentMoveSpeed : averageSpeed;

        // Don't check if stationary (unless already in a stuck state)
        if (moveSpeed <= 0.01f && Status != HardStuckStatus.None)
        {
            ResetInternal();
            return false;
        }

        // Check if bot moved outside stuck radius (scaled by speed)
        var moveDistanceSqr = _positionHistory.GetDistanceSqr();
        var stuckThresholdSqr = StuckRadiusSqr * moveSpeed;

        if (moveDistanceSqr > stuckThresholdSqr)
        {
            ResetInternal();
            return false;
        }

        // Bot appears stuck -- increment timer.
        // If bot is far from its destination, escalate faster.
        var timerMultiplier =
            squaredDistToDestination.HasValue && squaredDistToDestination.Value > StuckRadiusSqr ? FarFromDestMultiplier : 1.0f;
        _timer += deltaTime * timerMultiplier;
        var previousStatus = Status;

        switch (Status)
        {
            case HardStuckStatus.None when _timer >= _pathRetryDelay:
                Status = HardStuckStatus.Retrying;
                break;
            case HardStuckStatus.Retrying when _timer >= _teleportDelay:
                Status = HardStuckStatus.Teleport;
                break;
            case HardStuckStatus.Teleport when _timer >= _failDelay:
                Status = HardStuckStatus.Failed;
                break;
        }

        return Status != previousStatus;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        ResetInternal();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetInternal()
    {
        // If we weren't stuck, only reset the timer (keep position history warm)
        if (Status == HardStuckStatus.None)
        {
            _timer = 0f;
            return;
        }

        _averageSpeed.Reset();
        _positionHistory.Reset();
        Status = HardStuckStatus.None;
        _timer = 0f;
        _initialized = false;
    }
}

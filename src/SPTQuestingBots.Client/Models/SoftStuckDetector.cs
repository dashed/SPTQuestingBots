using System.Runtime.CompilerServices;
using UnityEngine;

namespace SPTQuestingBots.Models;

public enum SoftStuckStatus
{
    None,
    Vaulting,
    Jumping,
    Failed,
}

public class SoftStuckDetector
{
    // Bots moving at half the walk speed (3.5 m/s) are expected to be stuck
    private const float SpeedThreshold = 3.5f / 2f;
    private const float StaleThreshold = 0.2f;

    /// <summary>
    /// Timer multiplier when BotMover reports not moving but speed > 0 (faster escalation).
    /// </summary>
    private const float NotMovingMultiplier = 2.0f;

    private readonly float _vaultDelay;
    private readonly float _jumpDelay;
    private readonly float _failDelay;

    private Vector3 _lastPosition;
    private float _lastSpeed;
    private float _lastUpdateTime;
    private float _timer;
    private bool _initialized;

    public SoftStuckStatus Status { get; private set; }
    public float Timer
    {
        get { return _timer; }
    }

    public SoftStuckDetector(float vaultDelay = 1.5f, float jumpDelay = 3f, float failDelay = 6f)
    {
        _vaultDelay = vaultDelay;
        _jumpDelay = jumpDelay;
        _failDelay = failDelay;
    }

    /// <summary>
    /// Returns true if a state transition occurred (caller should check Status for the action).
    /// </summary>
    /// <param name="currentPosition">Bot's current world position.</param>
    /// <param name="currentMoveSpeed">Bot's current movement speed.</param>
    /// <param name="currentTime">Current game time (Time.time).</param>
    /// <param name="isMoving">Optional BotMover.IsMoving value. When false and speed > 0, stuck escalation is faster.</param>
    public bool Update(Vector3 currentPosition, float currentMoveSpeed, float currentTime, bool? isMoving = null)
    {
        if (!_initialized)
        {
            _lastPosition = currentPosition;
            _lastSpeed = currentMoveSpeed;
            _lastUpdateTime = currentTime;
            _initialized = true;
            return false;
        }

        var deltaTime = currentTime - _lastUpdateTime;
        _lastUpdateTime = currentTime;

        var lastPos = _lastPosition;
        _lastPosition = currentPosition;

        // Asymmetric speed buffering (from Phobos):
        // Slower -> use current speed (avoid overestimating required distance)
        // Faster -> EWMA with alpha=0.9 (give bot time to build distance)
        var moveSpeed = currentMoveSpeed <= _lastSpeed ? currentMoveSpeed : 0.9f * _lastSpeed + 0.1f * currentMoveSpeed;
        _lastSpeed = moveSpeed;

        // Don't check if basically stationary
        if (moveSpeed <= 0.01f)
        {
            Reset();
            return false;
        }

        // Discard measurements after long periods of dormancy
        if (deltaTime > StaleThreshold)
        {
            Reset();
            return false;
        }

        // Calculate expected movement distance based on current speed
        var stuckThreshold = SpeedThreshold * moveSpeed * deltaTime;

        // Check actual horizontal movement (ignore Y to filter jumps)
        var moveVector = currentPosition - lastPos;
        moveVector.y = 0f;
        var distanceMoved = moveVector.magnitude;

        if (distanceMoved > stuckThreshold)
        {
            Reset();
            return false;
        }

        // Bot appears stuck -- increment timer.
        // If BotMover reports not moving while we expect movement, escalate faster.
        var timerMultiplier = isMoving == false && moveSpeed > 0.01f ? NotMovingMultiplier : 1.0f;
        _timer += deltaTime * timerMultiplier;
        var previousStatus = Status;

        switch (Status)
        {
            case SoftStuckStatus.None when _timer >= _vaultDelay:
                Status = SoftStuckStatus.Vaulting;
                break;
            case SoftStuckStatus.Vaulting when _timer >= _jumpDelay:
                Status = SoftStuckStatus.Jumping;
                break;
            case SoftStuckStatus.Jumping when _timer >= _failDelay:
                Status = SoftStuckStatus.Failed;
                break;
        }

        return Status != previousStatus;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        Status = SoftStuckStatus.None;
        _timer = 0f;
        _initialized = false;
    }
}

using System;
using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Models.Pathing;

/// <summary>
/// Status of the path-following engine.
/// </summary>
public enum PathFollowerStatus : byte
{
    /// <summary>No path loaded.</summary>
    Idle = 0,

    /// <summary>Actively following a path.</summary>
    Following = 1,

    /// <summary>Destination reached.</summary>
    Reached = 2,

    /// <summary>Path invalid or exhausted retries.</summary>
    Failed = 3,
}

/// <summary>
/// Pure-logic path-following engine inspired by Phobos's MovementSystem.
/// Handles corner-reaching with walk/sprint epsilon, path-deviation spring force,
/// sprint gating via angle jitter, and corner-cutting optimization.
///
/// This class contains NO Unity runtime calls (no Player.Move, no NavMesh).
/// The companion <c>CustomMoverController</c> handles Unity execution.
///
/// All distance comparisons use squared magnitudes to avoid sqrt.
/// All direction math operates in the XZ plane.
/// </summary>
public class CustomPathFollower
{
    private readonly CustomMoverConfig _config;

    private Vector3[] _corners;
    private int _currentCorner;
    private Vector3 _target;
    private PathFollowerStatus _status;
    private int _retryCount;
    private bool _partialPathDetected;

    // Squared epsilons (precomputed from config)
    private readonly float _walkEpsSqr;
    private readonly float _sprintEpsSqr;
    private readonly float _destinationEpsSqr;

    // Inertia state (mirrors BSG's GClass497 algorithm)
    private Vector3 _prevDirection;
    private float _accumulatedAngle;
    private Vector3 _driftOffset;

    public CustomPathFollower(CustomMoverConfig config)
    {
        _config = config;
        _walkEpsSqr = config.WalkCornerEpsilon * config.WalkCornerEpsilon;
        _sprintEpsSqr = config.SprintCornerEpsilon * config.SprintCornerEpsilon;
        _destinationEpsSqr = config.DestinationEpsilon * config.DestinationEpsilon;
        _status = PathFollowerStatus.Idle;
    }

    /// <summary>Current path corners (null if no path).</summary>
    public Vector3[] Corners
    {
        get { return _corners; }
    }

    /// <summary>Index of the corner currently being navigated toward.</summary>
    public int CurrentCorner
    {
        get { return _currentCorner; }
    }

    /// <summary>Total number of corners in the current path.</summary>
    public int TotalCorners
    {
        get { return _corners?.Length ?? 0; }
    }

    /// <summary>Current movement target position.</summary>
    public Vector3 Target
    {
        get { return _target; }
    }

    /// <summary>Current status of the path follower.</summary>
    public PathFollowerStatus Status
    {
        get { return _status; }
    }

    /// <summary>Number of path retry attempts.</summary>
    public int RetryCount
    {
        get { return _retryCount; }
    }

    /// <summary>Configuration in use.</summary>
    public CustomMoverConfig Config
    {
        get { return _config; }
    }

    /// <summary>Whether a path is loaded and active.</summary>
    public bool HasPath
    {
        get { return _corners != null && _corners.Length > 0; }
    }

    /// <summary>Whether we're on the last corner of the path.</summary>
    public bool IsOnLastCorner
    {
        get { return _corners != null && _currentCorner >= _corners.Length - 1; }
    }

    // ── Path Lifecycle ────────────────────────────────────────

    /// <summary>
    /// Load a new path and begin following it.
    /// </summary>
    /// <param name="corners">NavMesh path corners (already smoothed if desired).</param>
    /// <param name="target">The ultimate destination position.</param>
    public void SetPath(Vector3[] corners, Vector3 target)
    {
        _target = target;

        if (corners == null || corners.Length == 0)
        {
            LoggingController.LogWarning("[CustomPathFollower] SetPath called with null/empty corners");
            _corners = null;
            _status = PathFollowerStatus.Failed;
            return;
        }

        _corners = corners;
        _currentCorner = 0;
        _status = PathFollowerStatus.Following;
        _partialPathDetected = false;
        ResetInertia();
        LoggingController.LogInfo("[CustomPathFollower] Path started: " + corners.Length + " corners");
    }

    /// <summary>
    /// Clear the current path and reset to idle.
    /// </summary>
    public void ResetPath()
    {
        _corners = null;
        _currentCorner = 0;
        _status = PathFollowerStatus.Idle;
        _retryCount = 0;
        _partialPathDetected = false;
        ResetInertia();
    }

    /// <summary>
    /// Mark path as failed (e.g., from stuck detection).
    /// </summary>
    public void FailPath()
    {
        _status = PathFollowerStatus.Failed;
    }

    /// <summary>
    /// Increment retry count. Returns true if retries are exhausted.
    /// </summary>
    public bool IncrementRetry()
    {
        _retryCount++;
        return _retryCount >= _config.MaxRetries;
    }

    // ── Corner Reaching ───────────────────────────────────────

    /// <summary>
    /// Check if the bot has reached the current corner.
    /// Uses sprint epsilon when sprinting (wider to maintain momentum).
    /// </summary>
    /// <param name="position">Current bot position.</param>
    /// <param name="isSprinting">Whether the bot is currently sprinting.</param>
    /// <returns>True if the current corner is reached.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasReachedCorner(Vector3 position, bool isSprinting)
    {
        if (_corners == null || _currentCorner >= _corners.Length)
        {
            return false;
        }

        float sqrDist = SqrDistanceXZ(position, _corners[_currentCorner]);
        float eps = isSprinting ? _sprintEpsSqr : _walkEpsSqr;
        return sqrDist <= eps;
    }

    /// <summary>
    /// Check if the bot is close enough to the current corner that it could
    /// potentially skip ahead (for NavMesh.Raycast corner-cutting).
    /// Returns true if within 1m of the current corner.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCloseEnoughForCornerCut(Vector3 position)
    {
        if (_corners == null || _currentCorner >= _corners.Length)
        {
            return false;
        }

        return SqrDistanceXZ(position, _corners[_currentCorner]) < 1f;
    }

    /// <summary>
    /// Attempt to skip the current corner via NavMesh.Raycast corner-cutting.
    /// Called by CustomMoverController after Tick() when close to a corner.
    /// The canSeeNextCorner parameter is the result of NavMesh.Raycast (done by the caller).
    /// Returns true if the corner was skipped.
    /// </summary>
    /// <param name="position">Current bot position.</param>
    /// <param name="canSeeNextCorner">Whether NavMesh.Raycast confirms clear line-of-sight to the next corner.</param>
    /// <returns>True if the corner was successfully skipped.</returns>
    public bool TryCornerCut(Vector3 position, bool canSeeNextCorner)
    {
        if (_status != PathFollowerStatus.Following || _corners == null)
        {
            return false;
        }

        if (IsOnLastCorner)
        {
            return false;
        }

        if (!IsCloseEnoughForCornerCut(position))
        {
            return false;
        }

        if (!canSeeNextCorner)
        {
            return false;
        }

        int skippedIndex = _currentCorner;
        AdvanceCorner();
        LoggingController.LogDebug("[CustomPathFollower] Corner cut: skipped corner " + skippedIndex + " via NavMesh.Raycast");
        return true;
    }

    /// <summary>
    /// Advance to the next corner. Returns true if there are more corners to follow.
    /// </summary>
    public bool AdvanceCorner()
    {
        if (_corners == null)
        {
            return false;
        }

        _currentCorner++;

        if (_currentCorner >= _corners.Length)
        {
            // Past the last corner
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if the bot has reached the final destination.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasReachedDestination(Vector3 position)
    {
        return SqrDistanceXZ(position, _target) <= _destinationEpsSqr;
    }

    /// <summary>
    /// Check if the path's last corner is close enough to the target.
    /// If not, the path may need to be recalculated (partial path).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool DoesPathReachTarget()
    {
        if (_corners == null || _corners.Length == 0)
        {
            return false;
        }

        return SqrDistanceXZ(_corners[_corners.Length - 1], _target) <= _destinationEpsSqr;
    }

    /// <summary>
    /// Check if the given destination matches the current target within epsilon.
    /// Used to avoid re-issuing move orders for the same destination.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTargetCurrent(Vector3 destination)
    {
        return SqrDistanceXZ(destination, _target) <= _destinationEpsSqr;
    }

    // ── Move Direction ────────────────────────────────────────

    /// <summary>
    /// Compute the move direction vector for the current frame.
    /// Blends the raw direction toward the current corner with a path-deviation
    /// spring force and angular inertia (matching BSG's GClass497 algorithm).
    ///
    /// Returns a normalized Vector3 in the XZ plane (Y=0).
    /// </summary>
    /// <param name="position">Current bot position.</param>
    /// <param name="deltaTime">Frame delta time (seconds). Pass 0 to skip inertia.</param>
    /// <returns>Normalized movement direction (XZ plane).</returns>
    public Vector3 ComputeMoveDirection(Vector3 position, float deltaTime = 0f)
    {
        if (_corners == null || _currentCorner >= _corners.Length)
        {
            return Vector3.zero;
        }

        // Raw direction toward current corner
        Vector3 cornerDir = _corners[_currentCorner] - position;

        // Compute path-deviation spring force (XZ plane)
        Vector3 prevCorner = _currentCorner > 0 ? _corners[_currentCorner - 1] : position;

        Vector3 deviation = PathDeviationForce.ComputeDeviation(position, prevCorner, _corners[_currentCorner]);

        // Blend: normalize cornerDir, add spring, re-normalize
        Vector3 blended = PathDeviationForce.BlendWithDeviation(cornerDir, deviation);

        // Apply angular inertia (mirrors BSG GClass497.AddToInertion)
        if (deltaTime > 0f && _config.MaxInertia > 0f)
        {
            blended = ApplyInertia(blended, deltaTime);
        }

        return blended;
    }

    /// <summary>
    /// Compute the raw (unblended) direction toward the current corner.
    /// Used for SetSteerDirection which wants the raw direction, not the spring-blended one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 ComputeRawDirection(Vector3 position)
    {
        if (_corners == null || _currentCorner >= _corners.Length)
        {
            return Vector3.zero;
        }

        return _corners[_currentCorner] - position;
    }

    // ── Sprint Decision ───────────────────────────────────────

    /// <summary>
    /// Compute the angle jitter for upcoming path corners from the current position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ComputeSprintAngleJitter()
    {
        return SprintAngleJitter.ComputeAngleJitter(_corners, _currentCorner, _config.SprintLookaheadDistance);
    }

    /// <summary>
    /// Determine if the bot can sprint given the current path smoothness
    /// and the specified urgency level.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanSprint(SprintUrgency urgency)
    {
        float jitter = ComputeSprintAngleJitter();
        return SprintAngleJitter.CanSprint(jitter, urgency, _config.SprintJitterHigh, _config.SprintJitterMedium, _config.SprintJitterLow);
    }

    // ── Tick (combined corner-reaching + status update) ───────

    /// <summary>
    /// Process one movement tick. Checks corner reaching, advances corners,
    /// and updates status. Call this once per frame before ComputeMoveDirection.
    ///
    /// Returns the current status after processing.
    /// </summary>
    /// <param name="position">Current bot position.</param>
    /// <param name="isSprinting">Whether the bot is currently sprinting.</param>
    /// <returns>Updated path follower status.</returns>
    public PathFollowerStatus Tick(Vector3 position, bool isSprinting)
    {
        if (_status != PathFollowerStatus.Following || _corners == null)
        {
            return _status;
        }

        bool isLastCorner = _currentCorner >= _corners.Length - 1;

        if (!isLastCorner)
        {
            // Check if we've reached the current corner
            if (HasReachedCorner(position, isSprinting))
            {
                LoggingController.LogDebug("[CustomPathFollower] Corner " + _currentCorner + "/" + _corners.Length + " reached");
                AdvanceCorner();
            }
        }
        else
        {
            // On the last corner — check for destination arrival or path insufficiency
            if (!DoesPathReachTarget())
            {
                // Only count one retry per partial path detection.
                // Without this guard, IncrementRetry fires every tick and exhausts
                // MaxRetries (10) in ~167ms at 60fps, never giving the bot time to
                // walk the available partial path segment.
                if (!_partialPathDetected)
                {
                    _partialPathDetected = true;
                    if (IncrementRetry())
                    {
                        LoggingController.LogWarning("[CustomPathFollower] Path failed: retries exhausted (" + _retryCount + ")");
                        _status = PathFollowerStatus.Failed;
                        return _status;
                    }
                }
                // Caller should recalculate the path
                return _status;
            }

            if (HasReachedDestination(position))
            {
                LoggingController.LogInfo("[CustomPathFollower] Path completed: destination reached");
                _status = PathFollowerStatus.Reached;
                return _status;
            }
        }

        return _status;
    }

    // ── Inertia ───────────────────────────────────────────────

    /// <summary>Current accumulated drift offset (for testing/debugging).</summary>
    public Vector3 DriftOffset
    {
        get { return _driftOffset; }
    }

    /// <summary>Current accumulated turn angle in degrees (for testing/debugging).</summary>
    public float AccumulatedAngle
    {
        get { return _accumulatedAngle; }
    }

    /// <summary>
    /// Apply angular momentum / inertia to a movement direction.
    /// Mirrors BSG's GClass497.AddToInertion algorithm:
    /// 1. Compute angle delta between previous and current direction
    /// 2. Accumulate turn angle (clamped to ±ClampAngle)
    /// 3. Scale forward movement by cos(turnAngle)
    /// 4. Add perpendicular drift (side-slip from momentum)
    /// 5. Decay drift over time, cap at MaxInertia
    /// </summary>
    internal Vector3 ApplyInertia(Vector3 direction, float deltaTime)
    {
        // Flatten to XZ plane
        Vector3 dirXZ = new Vector3(direction.x, 0f, direction.z);
        if (dirXZ.sqrMagnitude < 0.0001f)
            return direction;

        Vector3 dirNorm = Normalize(dirXZ);

        // First frame: initialize previous direction
        if (_prevDirection.sqrMagnitude < 0.0001f)
        {
            _prevDirection = dirNorm;
            return direction;
        }

        // Compute angle between previous and current direction
        float dot = _prevDirection.x * dirNorm.x + _prevDirection.z * dirNorm.z;
        dot = Clamp(dot, -1f, 1f);
        float angleDeg = (float)(Math.Acos(dot) * (180.0 / Math.PI));

        // Determine turn sign via cross product (Y component of prev × current)
        float cross = _prevDirection.x * dirNorm.z - _prevDirection.z * dirNorm.x;
        bool isLeft = cross > 0f;

        // Accumulate turn angle
        if (angleDeg > 0f)
        {
            _accumulatedAngle += isLeft ? angleDeg : -angleDeg;
            _accumulatedAngle = Clamp(_accumulatedAngle, -_config.InertiaClampAngle, _config.InertiaClampAngle);
        }

        // Decay accumulated angle toward zero
        float absAngle = Math.Abs(_accumulatedAngle);
        if (absAngle > 0f)
        {
            float decay = _config.InertiaDecaySpeed * deltaTime;
            if (decay >= absAngle)
                _accumulatedAngle = 0f;
            else
                _accumulatedAngle -= Math.Sign(_accumulatedAngle) * decay;
        }

        // Apply forward scaling and perpendicular drift
        absAngle = Math.Abs(_accumulatedAngle);
        Vector3 result = dirNorm;
        if (absAngle > 0f)
        {
            float rad = absAngle * (float)(Math.PI / 180.0);
            float cosA = (float)Math.Cos(rad);
            result = new Vector3(dirNorm.x * cosA, 0f, dirNorm.z * cosA);

            // Perpendicular vector (rotate 90 degrees based on turn direction)
            Vector3 perp =
                _accumulatedAngle > 0f
                    ? new Vector3(-dirNorm.z, 0f, dirNorm.x) // left turn → drift right
                    : new Vector3(dirNorm.z, 0f, -dirNorm.x); // right turn → drift left

            _driftOffset += perp * (1f - cosA) * deltaTime;
        }

        // Decay drift offset
        float driftMag = (float)Math.Sqrt(_driftOffset.x * _driftOffset.x + _driftOffset.z * _driftOffset.z);
        if (driftMag > 0f)
        {
            float driftDecay = _config.InertiaDecaySpeed * deltaTime;
            driftDecay = driftDecay > driftMag ? driftMag : driftDecay;
            float invMag = 1f / driftMag;
            _driftOffset -= new Vector3(_driftOffset.x * invMag, 0f, _driftOffset.z * invMag) * driftDecay;
        }

        // Cap drift magnitude
        driftMag = (float)Math.Sqrt(_driftOffset.x * _driftOffset.x + _driftOffset.z * _driftOffset.z);
        if (driftMag > _config.MaxInertia)
        {
            float scale = _config.MaxInertia / driftMag;
            _driftOffset = new Vector3(_driftOffset.x * scale, 0f, _driftOffset.z * scale);
        }

        _prevDirection = dirNorm;

        // Re-normalize the result
        Vector3 final = new Vector3(result.x + _driftOffset.x, 0f, result.z + _driftOffset.z);
        float finalMag = (float)Math.Sqrt(final.x * final.x + final.z * final.z);
        if (finalMag > 0.0001f)
        {
            float inv = 1f / finalMag;
            return new Vector3(final.x * inv, 0f, final.z * inv);
        }

        return direction;
    }

    /// <summary>Reset inertia state (on path change or reset).</summary>
    internal void ResetInertia()
    {
        _prevDirection = Vector3.zero;
        _accumulatedAngle = 0f;
        _driftOffset = Vector3.zero;
    }

    // ── Helpers ───────────────────────────────────────────────

    /// <summary>
    /// Squared distance between two points in the XZ plane (ignoring Y).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SqrDistanceXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 Normalize(Vector3 v)
    {
        float mag = (float)Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        if (mag < 0.0001f)
            return Vector3.zero;
        float inv = 1f / mag;
        return new Vector3(v.x * inv, v.y * inv, v.z * inv);
    }
}

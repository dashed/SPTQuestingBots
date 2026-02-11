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

    // Squared epsilons (precomputed from config)
    private readonly float _walkEpsSqr;
    private readonly float _sprintEpsSqr;
    private readonly float _destinationEpsSqr;

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
    /// spring force that pulls the bot back toward the ideal path line.
    ///
    /// Returns a normalized Vector3 in the XZ plane (Y=0).
    /// </summary>
    /// <param name="position">Current bot position.</param>
    /// <returns>Normalized movement direction (XZ plane).</returns>
    public Vector3 ComputeMoveDirection(Vector3 position)
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
        return PathDeviationForce.BlendWithDeviation(cornerDir, deviation);
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
                // Path doesn't reach the target — needs retry
                if (IncrementRetry())
                {
                    LoggingController.LogWarning("[CustomPathFollower] Path failed: retries exhausted (" + _retryCount + ")");
                    _status = PathFollowerStatus.Failed;
                    return _status;
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
}

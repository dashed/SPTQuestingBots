using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace SPTQuestingBots.Models.Pathing
{
    /// <summary>
    /// Immutable-style path data representing a precomputed NavMesh path between two points.
    /// Supports cloning, reversing, appending, and prepending for path composition.
    /// </summary>
    /// <remarks>
    /// Used as the base class for <see cref="BotPathData"/> and as standalone cached paths
    /// for static quest routes that can be shared across bots.
    /// </remarks>
    public class StaticPathData : ICloneable
    {
        /// <summary>World position where the path begins.</summary>
        public Vector3 StartPosition { get; protected set; } = Vector3.negativeInfinity;

        /// <summary>World position where the path is intended to end.</summary>
        public Vector3 TargetPosition { get; protected set; } = Vector3.positiveInfinity;

        /// <summary>NavMesh path calculation status (Complete, Partial, or Invalid).</summary>
        public NavMeshPathStatus Status { get; protected set; } = NavMeshPathStatus.PathInvalid;

        /// <summary>Distance threshold for considering the target as reached.</summary>
        public float ReachDistance { get; protected set; } = float.NaN;

        /// <summary>Ordered array of path corner positions from start to end.</summary>
        public Vector3[] Corners { get; protected set; } = new Vector3[0];

        /// <summary>Total path length calculated from corner-to-corner distances.</summary>
        public float PathLength { get; protected set; } = float.NaN;

        /// <summary>Time.time when the corners were last set.</summary>
        public float LastSetTime { get; protected set; } = 0;

        /// <summary>Whether a target position has been assigned.</summary>
        public bool IsInitialized => TargetPosition != Vector3.positiveInfinity;

        /// <summary>Whether the path contains any corners.</summary>
        public bool HasPath => Corners.Length > 0;

        /// <summary>Seconds elapsed since the corners were last updated.</summary>
        public float TimeSinceLastSet => Time.time - LastSetTime;

        /// <summary>
        /// Creates an uninitialized path data instance.
        /// </summary>
        public StaticPathData() { }

        /// <summary>
        /// Creates a path data instance and immediately calculates a NavMesh path.
        /// </summary>
        public StaticPathData(Vector3 start, Vector3 target, float reachDistance)
        {
            StartPosition = start;
            TargetPosition = target;
            ReachDistance = reachDistance;

            Status = CreatePathSegment(start, target, out Vector3[] corners);
            SetCorners(corners);
        }

        /// <summary>
        /// Creates a shallow clone of this path data.
        /// </summary>
        public object Clone()
        {
            StaticPathData clone = new StaticPathData();
            clone.StartPosition = StartPosition;
            clone.TargetPosition = TargetPosition;
            clone.Status = Status;
            clone.ReachDistance = ReachDistance;
            clone.Corners = Corners;
            clone.PathLength = PathLength;
            clone.LastSetTime = LastSetTime;

            return clone;
        }

        /// <summary>
        /// Creates a reversed copy of this path with start and target swapped.
        /// </summary>
        public StaticPathData GetReverse()
        {
            StaticPathData reverse = new StaticPathData();
            reverse.StartPosition = TargetPosition;
            reverse.TargetPosition = StartPosition;
            reverse.Status = Status;
            reverse.ReachDistance = ReachDistance;
            reverse.Corners = Corners.Reverse().ToArray();
            reverse.PathLength = PathLength;
            reverse.LastSetTime = LastSetTime;

            return reverse;
        }

        /// <summary>
        /// Creates a new path by appending another path's corners after this path's corners.
        /// Deduplicates the junction point if both paths share it.
        /// </summary>
        public StaticPathData Append(StaticPathData pathToAppend)
        {
            if (pathToAppend.Corners.Length == 0)
            {
                return this;
            }

            StaticPathData newPath = (StaticPathData)Clone();

            Vector3[] newCorners;
            if (Corners.Last() == pathToAppend.Corners.First())
            {
                newCorners = Corners.AddRangeToArray(pathToAppend.Corners.Skip(1).ToArray());
            }
            else
            {
                newCorners = Corners.AddRangeToArray(pathToAppend.Corners);
            }

            newPath.TargetPosition = pathToAppend.TargetPosition;
            newPath.CombineWithPath(pathToAppend, newCorners);

            return newPath;
        }

        /// <summary>
        /// Creates a new path by prepending another path's corners before this path's corners.
        /// Deduplicates the junction point if both paths share it.
        /// </summary>
        public StaticPathData Prepend(StaticPathData pathToPrepend)
        {
            if (pathToPrepend.Corners.Length == 0)
            {
                return this;
            }

            StaticPathData newPath = (StaticPathData)Clone();

            Vector3[] newCorners;
            if (Corners.First() == pathToPrepend.Corners.Last())
            {
                newCorners = pathToPrepend.Corners.AddRangeToArray(Corners.Skip(1).ToArray());
            }
            else
            {
                newCorners = pathToPrepend.Corners.AddRangeToArray(Corners);
            }

            newPath.StartPosition = pathToPrepend.StartPosition;
            newPath.CombineWithPath(pathToPrepend, newCorners);

            return newPath;
        }

        /// <summary>
        /// Returns true if the path's last corner is within reach distance of the target.
        /// </summary>
        public bool IsComplete()
        {
            if (Corners.Length == 0)
            {
                return false;
            }

            if (Vector3.Distance(TargetPosition, Corners.Last()) > ReachDistance)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the distance between the path's last corner and the target position.
        /// Returns NaN if no corners exist.
        /// </summary>
        public float GetMissingDistanceToTarget()
        {
            if (Corners.Length == 0)
            {
                return float.NaN;
            }

            return Vector3.Distance(TargetPosition, Corners.Last());
        }

        /// <summary>
        /// Calculates a NavMesh path between two points and returns the resulting corners.
        /// </summary>
        protected NavMeshPathStatus CreatePathSegment(Vector3 start, Vector3 end, out Vector3[] pathCorners)
        {
            NavMeshPath navMeshPath = new NavMeshPath();
            NavMesh.CalculatePath(start, end, -1, navMeshPath);
            pathCorners = navMeshPath.corners;

            return navMeshPath.status;
        }

        /// <summary>
        /// Merges status and reach distance from another path, then updates corners.
        /// </summary>
        protected void CombineWithPath(StaticPathData pathToMerge, Vector3[] combinedCorners)
        {
            if (pathToMerge.ReachDistance < ReachDistance)
            {
                ReachDistance = pathToMerge.ReachDistance;
            }

            if (pathToMerge.Status == NavMeshPathStatus.PathInvalid)
            {
                Status = NavMeshPathStatus.PathInvalid;
            }
            else if (pathToMerge.Status == NavMeshPathStatus.PathPartial)
            {
                Status = NavMeshPathStatus.PathPartial;
            }

            SetCorners(combinedCorners);
        }

        /// <summary>
        /// Updates the path corners and recalculates path length and timestamp.
        /// </summary>
        protected void SetCorners(Vector3[] corners)
        {
            Corners = corners;
            PathLength = Corners.CalculatePathLength();
            LastSetTime = Time.time;
        }
    }
}

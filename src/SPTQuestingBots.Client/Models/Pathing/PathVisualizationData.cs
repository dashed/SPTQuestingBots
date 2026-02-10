using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SPTQuestingBots.Models.Pathing
{
    /// <summary>
    /// Manages a named 3D path visualization using Unity's LineRenderer.
    /// Used for debug display of bot paths, quest routes, and waypoints in the game world.
    /// </summary>
    public class PathVisualizationData
    {
        /// <summary>Display name for this path visualization.</summary>
        public string PathName { get; private set; }

        /// <summary>Color of the rendered line. Defaults to magenta.</summary>
        public Color LineColor { get; set; } = Color.magenta;

        /// <summary>Width of the rendered line in world units.</summary>
        public float LineThickness { get; set; } = 0.05f;

        private Vector3[] pathData = new Vector3[0];
        private LineRenderer lineRenderer;
        private static object lineRendererLockObj = new object();

        /// <summary>
        /// The path points as a read-only collection. Setting replaces all points.
        /// </summary>
        public IEnumerable<Vector3> PathData
        {
            get { return new ReadOnlyCollection<Vector3>(pathData); }
            set
            {
                lock (lineRendererLockObj)
                {
                    pathData = value.ToArray();
                }
            }
        }

        /// <summary>
        /// Creates a visualization with the given name and default appearance.
        /// </summary>
        public PathVisualizationData(string _pathName)
        {
            PathName = _pathName;
        }

        /// <summary>
        /// Creates a visualization with the given name and path points.
        /// </summary>
        public PathVisualizationData(string _pathName, Vector3[] _pathData)
            : this(_pathName)
        {
            pathData = _pathData;
        }

        /// <summary>
        /// Creates a visualization with the given name, path points, and line color.
        /// </summary>
        public PathVisualizationData(string _pathName, Vector3[] _pathData, Color _color)
            : this(_pathName, _pathData)
        {
            LineColor = _color;
        }

        /// <summary>
        /// Creates a visualization with the given name, path points, line color, and thickness.
        /// </summary>
        public PathVisualizationData(string _pathName, Vector3[] _pathData, Color _color, float _lineThickness)
            : this(_pathName, _pathData, _color)
        {
            LineThickness = _lineThickness;
        }

        /// <summary>
        /// Updates the LineRenderer with current path data, creating it if necessary.
        /// Clears the renderer if no path points are set.
        /// </summary>
        public void Update()
        {
            lock (lineRendererLockObj)
            {
                if (lineRenderer == null)
                {
                    lineRenderer = (new GameObject("Path_" + PathName)).GetOrAddComponent<LineRenderer>();
                    lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
                }

                // If there are no points, erase any that have been drawn previously
                if ((pathData == null) || (pathData.Length == 0))
                {
                    lineRenderer.positionCount = 0;
                    return;
                }

                lineRenderer.startColor = LineColor;
                lineRenderer.endColor = LineColor;
                lineRenderer.startWidth = LineThickness;
                lineRenderer.endWidth = LineThickness;

                lineRenderer.positionCount = pathData.Length;
                lineRenderer.SetPositions(pathData);
            }
        }

        /// <summary>
        /// Hides the rendered line by setting position count to zero without clearing path data.
        /// </summary>
        public void Erase()
        {
            lock (lineRendererLockObj)
            {
                if (lineRenderer != null)
                {
                    lineRenderer.positionCount = 0;
                }
            }
        }

        /// <summary>
        /// Erases the rendered line and clears all path data.
        /// </summary>
        public void Clear()
        {
            Erase();
            lock (lineRendererLockObj)
            {
                pathData = new Vector3[0];
            }
        }

        /// <summary>
        /// Replaces this visualization's path data, color, and thickness with values from another.
        /// </summary>
        public void Replace(PathVisualizationData other)
        {
            pathData = other.PathData.ToArray();
            LineColor = other.LineColor;
            LineThickness = other.LineThickness;
        }

        /// <summary>
        /// Changes the display name of this path visualization.
        /// </summary>
        public void ChangeName(string newName)
        {
            PathName = newName;
        }
    }
}

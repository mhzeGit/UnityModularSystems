using System.Collections.Generic;
using UnityEngine;

namespace ModularNPC
{
    /// <summary>
    /// A patrol loop built from plain scene transforms. Every child of <see cref="_pointsRoot"/> is a
    /// waypoint, in hierarchy order, and the list is re-read on use, so duplicating a waypoint adds it
    /// to the loop with no wiring. Place the waypoints under a static parent, never under the NPC.
    /// </summary>
    [AddComponentMenu("MHZE/NPC/Patrol Route")]
    public sealed class NpcPatrolRoute : MonoBehaviour
    {
        [Tooltip("Parent whose child transforms are the waypoints, in hierarchy order.")]
        public Transform pointsRoot;

        [Tooltip("Draws the loop in the scene view.")]
        public bool drawGizmos = true;

        private readonly List<Transform> _points = new List<Transform>(8);
        private int _index = -1;

        public int Count
        {
            get
            {
                Refresh();
                return _points.Count;
            }
        }

        public IReadOnlyList<Transform> Points
        {
            get
            {
                Refresh();
                return _points;
            }
        }

        /// <summary>
        /// Index of the waypoint last returned by <see cref="NextPoint"/>, or -1 before the first
        /// one (and again after <see cref="SetCurrentIndex"/> is given -1).
        /// </summary>
        public int CurrentIndex
        {
            get
            {
                Refresh();
                return _index;
            }
        }

        /// <summary>
        /// Makes <paramref name="index"/> the waypoint the NPC last targeted, so the next call to
        /// <see cref="NextPoint"/> returns the one after it. Pass -1 to restart the loop from the
        /// first waypoint. Intended for save/restore and for debug tools that jump an NPC through
        /// its route; the value is clamped to the waypoints that currently exist.
        /// </summary>
        public void SetCurrentIndex(int index)
        {
            Refresh();
            _index = Mathf.Clamp(index, -1, _points.Count - 1);
        }

        /// <summary>Returns the next waypoint in the loop, wrapping back to the first one.</summary>
        public Transform NextPoint()
        {
            Refresh();
            if (_points.Count == 0)
            {
                return null;
            }

            _index = (_index + 1) % _points.Count;
            return _points[_index];
        }

        /// <summary>Re-reads the waypoints from <see cref="pointsRoot"/>.</summary>
        public void Refresh()
        {
            _points.Clear();
            if (pointsRoot == null)
            {
                _index = -1;
                return;
            }

            for (int i = 0; i < pointsRoot.childCount; i++)
            {
                Transform child = pointsRoot.GetChild(i);
                if (child != null && child.gameObject.activeInHierarchy)
                {
                    _points.Add(child);
                }
            }

            if (_index >= _points.Count)
            {
                _index = -1;
            }
        }

        private void Awake()
        {
            Refresh();
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos)
            {
                return;
            }

            Refresh();
            if (_points.Count == 0)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
            for (int i = 0; i < _points.Count; i++)
            {
                if (_points[i] == null)
                {
                    continue;
                }

                Gizmos.DrawWireSphere(_points[i].position, 0.15f);
                if (_points.Count > 1)
                {
                    Transform next = _points[(i + 1) % _points.Count];
                    if (next != null)
                    {
                        Gizmos.DrawLine(_points[i].position, next.position);
                    }
                }
            }
        }
    }
}

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using MHZE.AirParticlePhysics;

namespace MHZE.AirParticlePhysics.Editor
{
    [InitializeOnLoad]
    internal static class AirParticleViewportDebug
    {
        private const int MaxProjectionVerts = 1024;

        private static readonly Dictionary<AirParticleEmitter, MeshFilter> _meshFilterCache = new Dictionary<AirParticleEmitter, MeshFilter>();
        private static readonly Dictionary<int, Vector3[]> _vertexCache = new Dictionary<int, Vector3[]>();
        private static readonly List<Vector2> _projected = new List<Vector2>();
        private static readonly List<Vector2> _hull = new List<Vector2>();
        private static Vector3[] _hull3D = new Vector3[64];

        static AirParticleViewportDebug()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView view)
        {
            var emitters = Object.FindObjectsByType<AirParticleEmitter>(FindObjectsSortMode.None);
            foreach (var emitter in emitters)
            {
                if (!emitter.debugDraw) continue;

                Vector3 origin = emitter.transform.position;
                Vector3 direction = emitter.VelocityDirection;

                if (direction.sqrMagnitude < 0.001f) continue;

                Vector3 end = emitter.ProjectionPosition;
                float length = Vector3.Distance(origin, end);

                Handles.color = Color.green;
                Handles.DrawLine(origin, end);

                Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
                if (right.sqrMagnitude < 0.001f)
                    right = Vector3.Cross(direction, Vector3.forward).normalized;
                Vector3 up = Vector3.Cross(right, direction).normalized;

                float arrowSize = length * 0.2f;
                float arrowAngle = 30f * Mathf.Deg2Rad;

                Vector3 arrowLeft = end - direction * arrowSize * Mathf.Cos(arrowAngle)
                                      + right * arrowSize * Mathf.Sin(arrowAngle);
                Vector3 arrowRight = end - direction * arrowSize * Mathf.Cos(arrowAngle)
                                       - right * arrowSize * Mathf.Sin(arrowAngle);
                Vector3 arrowUp = end - direction * arrowSize * Mathf.Cos(arrowAngle)
                                    - up * arrowSize * Mathf.Sin(arrowAngle);
                Vector3 arrowDown = end - direction * arrowSize * Mathf.Cos(arrowAngle)
                                      + up * arrowSize * Mathf.Sin(arrowAngle);

                Handles.DrawLine(end, arrowLeft);
                Handles.DrawLine(end, arrowRight);
                Handles.DrawLine(end, arrowUp);
                Handles.DrawLine(end, arrowDown);

                DrawProjectedSurface(emitter, end, direction, right, up);
            }
        }

        private static void DrawProjectedSurface(AirParticleEmitter emitter, Vector3 arrowTip, Vector3 viewDir, Vector3 right, Vector3 up)
        {
            if (!_meshFilterCache.TryGetValue(emitter, out var meshFilter) || meshFilter == null)
            {
                meshFilter = emitter.GetComponentInChildren<MeshFilter>();
                _meshFilterCache[emitter] = meshFilter;
            }

            if (meshFilter == null || meshFilter.sharedMesh == null) return;

            Mesh mesh = meshFilter.sharedMesh;
            int meshID = mesh.GetInstanceID();

            if (!_vertexCache.TryGetValue(meshID, out var verts))
            {
                verts = mesh.vertices;
                _vertexCache[meshID] = verts;
            }

            if (verts.Length < 3) return;

            Vector3 normal = viewDir.normalized;
            Matrix4x4 localToWorld = emitter.transform.localToWorldMatrix;

            _projected.Clear();

            int step = 1;
            if (verts.Length > MaxProjectionVerts)
                step = Mathf.CeilToInt((float)verts.Length / MaxProjectionVerts);

            for (int i = 0; i < verts.Length; i += step)
            {
                Vector3 worldVert = localToWorld.MultiplyPoint3x4(verts[i]);
                Vector3 onPlane = worldVert - Vector3.Project(worldVert - arrowTip, normal);
                _projected.Add(new Vector2(
                    Vector3.Dot(onPlane - arrowTip, right),
                    Vector3.Dot(onPlane - arrowTip, up)));
            }

            ComputeConvexHull(_projected, _hull);
            if (_hull.Count < 3) return;

            if (_hull3D == null || _hull3D.Length < _hull.Count)
                _hull3D = new Vector3[_hull.Count];

            for (int i = 0; i < _hull.Count; i++)
                _hull3D[i] = arrowTip + right * _hull[i].x + up * _hull[i].y;

            Handles.color = new Color(0f, 1f, 0f, 0.12f);
            Handles.DrawAAConvexPolygon(_hull3D);

            Handles.color = new Color(0f, 1f, 0f, 0.5f);
            for (int i = 0; i < _hull.Count; i++)
            {
                int next = (i + 1) % _hull.Count;
                Handles.DrawLine(_hull3D[i], _hull3D[next]);
            }
        }

        private static void ComputeConvexHull(List<Vector2> points, List<Vector2> hull)
        {
            hull.Clear();
            if (points.Count < 3)
            {
                hull.AddRange(points);
                return;
            }

            points.Sort((a, b) =>
                a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            hull.Clear();

            for (int i = 0; i < points.Count; i++)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(points[i]);
            }

            int lowerCount = hull.Count;
            for (int i = points.Count - 2; i >= 0; i--)
            {
                while (hull.Count > lowerCount && Cross(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(points[i]);
            }

            if (hull.Count > 1)
                hull.RemoveAt(hull.Count - 1);
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }


    }
}

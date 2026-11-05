using System.Collections.Generic;
using UnityEngine;

namespace MHZE.WallBreak
{
    /// <summary>
    /// Draws the wall's crack as thin ribbons along the lines where its pieces meet, just above the wall's faces, so the wall
    /// later breaks exactly where it cracked. It is a separate transparent layer, so it works on any texture. Every vertex
    /// stores a "birth" value in uv1.x (how far along the crack's growth it appears), which the shader compares with its
    /// progress. Ribbons are grouped per edge so the ones around a piece that has fallen out can be hidden.
    /// </summary>
    internal sealed class WallCrackMesh
    {
        public Mesh mesh;
        readonly List<int> firstIndex = new List<int>();
        readonly List<int> indexCount = new List<int>();
        readonly List<int> cellA = new List<int>();
        readonly List<int> cellB = new List<int>();
        readonly List<int> allTris = new List<int>();

        /// <param name="edges">The lines between pieces.</param>
        /// <param name="origin">Crack centre in mesh space.</param>
        /// <param name="radius">Distance in metres at which the crack is fully grown.</param>
        /// <param name="width">Ribbon width in metres at the centre.</param>
        /// <param name="tAxis">Wall thickness axis.</param>
        /// <param name="offset">How far off the face to float, in mesh units along the thickness axis.</param>
        public static WallCrackMesh Build(List<FractureEdge> edges, Vector3 origin, float radius, float width, Vector3 scale, int tAxis, float offset, Color color, bool bothSides, int seed)
        {
            var rng = new System.Random(seed);
            var result = new WallCrackMesh();
            var verts = new List<Vector3>(edges.Count * 16);
            var cols = new List<Color>(edges.Count * 16);
            var uv0 = new List<Vector2>(edges.Count * 16);
            var uv1 = new List<Vector2>(edges.Count * 16);
            var points = new List<Vector3>(16);
            var next = new List<Vector3>(16);

            for (int e = 0; e < edges.Count; e++)
            {
                FractureEdge edge = edges[e];
                if (!edge.frontFace && !bothSides) continue;

                Vector3 mid = (edge.p0 + edge.p1) * 0.5f;
                float d = Vector3.Scale(mid - origin, scale).magnitude;
                float spread = (float)rng.NextDouble();
                float birth = d / radius * 0.82f + spread * 0.18f;
                if (birth >= 1f) continue;
                // Some lines only show late, so the early crack reads as a crack and not a net.
                if (spread > 0.62f) birth = Mathf.Min(0.99f, birth + 0.25f);

                // A straight line between pieces, broken up so it wanders like a real crack.
                points.Clear();
                points.Add(edge.p0); points.Add(edge.p1);
                float wander = Vector3.Scale(edge.p1 - edge.p0, scale).magnitude * 0.13f;
                for (int level = 0; level < 3; level++)
                {
                    next.Clear();
                    for (int i = 0; i + 1 < points.Count; i++)
                    {
                        next.Add(points[i]);
                        Vector3 m = (points[i] + points[i + 1]) * 0.5f;
                        Vector3 along = Vector3.Scale(points[i + 1] - points[i], scale);
                        Vector3 side = new Vector3(along.y * Axis(tAxis, 2) - along.z * Axis(tAxis, 1), along.z * Axis(tAxis, 0) - along.x * Axis(tAxis, 2), along.x * Axis(tAxis, 1) - along.y * Axis(tAxis, 0));
                        if (side.sqrMagnitude > 1e-8f)
                        {
                            side.Normalize();
                            Vector3 push = side * (((float)rng.NextDouble() * 2f - 1f) * wander);
                            m += new Vector3(push.x / scale.x, push.y / scale.y, push.z / scale.z);
                        }
                        next.Add(m);
                    }
                    next.Add(points[points.Count - 1]);
                    points.Clear();
                    points.AddRange(next);
                    wander *= 0.55f;
                }

                result.firstIndex.Add(result.allTris.Count);
                float w = width * Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(d / radius));
                float shade = 0.8f + (float)rng.NextDouble() * 0.2f;
                Color c = new Color(color.r * shade, color.g * shade, color.b * shade, color.a);
                Vector3 lift = Vector3.zero;
                lift[tAxis] = edge.frontFace ? offset : -offset;

                for (int i = 0; i + 1 < points.Count; i++)
                {
                    Vector3 a = points[i] + lift, b = points[i + 1] + lift;
                    Vector3 along = Vector3.Scale(b - a, scale);
                    Vector3 side = new Vector3(along.y * Axis(tAxis, 2) - along.z * Axis(tAxis, 1), along.z * Axis(tAxis, 0) - along.x * Axis(tAxis, 2), along.x * Axis(tAxis, 1) - along.y * Axis(tAxis, 0));
                    if (side.sqrMagnitude < 1e-10f) continue;
                    side = side.normalized * (w * 0.5f);
                    Vector3 sideLocal = new Vector3(side.x / scale.x, side.y / scale.y, side.z / scale.z);
                    Vector3 ext = new Vector3(along.x / scale.x, along.y / scale.y, along.z / scale.z).normalized * 0f;
                    int b0 = verts.Count;
                    verts.Add(a + sideLocal - ext); uv0.Add(new Vector2(0f, 0f)); uv1.Add(new Vector2(birth, 0f));
                    verts.Add(a - sideLocal - ext); uv0.Add(new Vector2(1f, 0f)); uv1.Add(new Vector2(birth, 0f));
                    verts.Add(b - sideLocal + ext); uv0.Add(new Vector2(1f, 1f)); uv1.Add(new Vector2(birth, 0f));
                    verts.Add(b + sideLocal + ext); uv0.Add(new Vector2(0f, 1f)); uv1.Add(new Vector2(birth, 0f));
                    for (int k = 0; k < 4; k++) cols.Add(c);
                    result.allTris.Add(b0); result.allTris.Add(b0 + 1); result.allTris.Add(b0 + 2);
                    result.allTris.Add(b0); result.allTris.Add(b0 + 2); result.allTris.Add(b0 + 3);
                }
                result.indexCount.Add(result.allTris.Count - result.firstIndex[result.firstIndex.Count - 1]);
                result.cellA.Add(edge.a);
                result.cellB.Add(edge.b);
            }

            var mesh = new Mesh { name = "WallCrack" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetTriangles(result.allTris, 0, true);
            result.mesh = mesh;
            return result;
        }

        static float Axis(int axis, int component) { return axis == component ? 1f : 0f; }

        public void ShowAll() { mesh.SetTriangles(allTris, 0, true); }

        /// <summary>Hides the ribbons that touch a piece that has fallen out, so nothing floats over the hole.</summary>
        public void HideAround(FractureCell[] cells)
        {
            var tris = new List<int>(allTris.Count);
            for (int e = 0; e < firstIndex.Count; e++)
            {
                if (cells[cellA[e]].released || cells[cellB[e]].released) continue;
                for (int i = 0; i < indexCount[e]; i++) tris.Add(allTris[firstIndex[e] + i]);
            }
            mesh.SetTriangles(tris, 0, true);
        }
    }
}

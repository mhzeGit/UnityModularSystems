using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MHZE.WallBreak
{
    /// <summary>One vertex of a wall mesh while it is being cut: position, normal, tangent and both UV sets.</summary>
    internal struct WbVertex
    {
        public Vector3 p;
        public Vector3 n;
        public Vector4 t;
        public Vector2 uv0;
        public Vector2 uv1;

        public static WbVertex Lerp(in WbVertex a, in WbVertex b, float f)
        {
            WbVertex r;
            r.p = Vector3.LerpUnclamped(a.p, b.p, f);
            r.n = Vector3.LerpUnclamped(a.n, b.n, f);
            float nl = r.n.sqrMagnitude;
            if (nl > 1e-12f) r.n /= Mathf.Sqrt(nl);
            r.t = Vector4.LerpUnclamped(a.t, b.t, f);
            r.t.w = a.t.w;
            r.uv0 = Vector2.LerpUnclamped(a.uv0, b.uv0, f);
            r.uv1 = Vector2.LerpUnclamped(a.uv1, b.uv1, f);
            return r;
        }
    }

    /// <summary>
    /// A readable copy of a wall mesh, grouped by sub-mesh, with the numbers the slicer needs: how many UV units
    /// one metre of surface covers (so a cut face can continue the wall's texture) and which sub-mesh holds the
    /// most surface (the one cut faces borrow their material from).
    /// </summary>
    public sealed class WallMeshData
    {
        internal WbVertex[] verts;
        internal int[][] subTris;
        public int SubmeshCount { get; private set; }
        public int LargestSubmesh { get; private set; }
        public Bounds Bounds { get; private set; }
        /// <summary>UV units per metre of world surface (roughly; assumes the texture is not stretched).</summary>
        public float UvPerUnit { get; private set; }
        public bool HasLightmapUv { get; private set; }

        /// <summary>Returns null when the mesh cannot be read (Read/Write disabled).</summary>
        public static WallMeshData FromMesh(Mesh mesh, Vector3 scale)
        {
            if (mesh == null || !mesh.isReadable) return null;

            Vector3[] pos = mesh.vertices;
            Vector3[] nor = mesh.normals;
            Vector4[] tan = mesh.tangents;
            Vector2[] uv0 = mesh.uv;
            Vector2[] uv1 = mesh.uv2;

            var data = new WallMeshData();
            data.verts = new WbVertex[pos.Length];
            for (int i = 0; i < pos.Length; i++)
            {
                WbVertex v;
                v.p = pos[i];
                v.n = nor.Length == pos.Length ? nor[i] : Vector3.forward;
                v.t = tan.Length == pos.Length ? tan[i] : new Vector4(1, 0, 0, 1);
                v.uv0 = uv0.Length == pos.Length ? uv0[i] : Vector2.zero;
                v.uv1 = uv1.Length == pos.Length ? uv1[i] : Vector2.zero;
                data.verts[i] = v;
            }
            data.HasLightmapUv = uv1.Length == pos.Length;

            data.SubmeshCount = Mathf.Max(1, mesh.subMeshCount);
            data.subTris = new int[data.SubmeshCount][];
            float bestArea = -1f;
            double uvArea = 0, worldArea = 0;
            for (int s = 0; s < data.SubmeshCount; s++)
            {
                int[] tris = mesh.subMeshCount > 0 ? mesh.GetTriangles(s) : new int[0];
                data.subTris[s] = tris;
                float area = 0f;
                for (int k = 0; k + 2 < tris.Length; k += 3)
                {
                    WbVertex a = data.verts[tris[k]], b = data.verts[tris[k + 1]], c = data.verts[tris[k + 2]];
                    float wa = Vector3.Cross(Vector3.Scale(b.p - a.p, scale), Vector3.Scale(c.p - a.p, scale)).magnitude * 0.5f;
                    area += wa;
                    Vector2 e1 = b.uv0 - a.uv0, e2 = c.uv0 - a.uv0;
                    float ua = Mathf.Abs(e1.x * e2.y - e1.y * e2.x) * 0.5f;
                    if (wa > 1e-6f) { worldArea += wa; uvArea += ua; }
                }
                if (area > bestArea) { bestArea = area; data.LargestSubmesh = s; }
            }
            data.UvPerUnit = worldArea > 1e-6 && uvArea > 1e-9 ? Mathf.Sqrt((float)(uvArea / worldArea)) : 1f;
            data.Bounds = mesh.bounds;
            return data;
        }
    }

    /// <summary>Collects cut geometry per sub-mesh and turns it into a Mesh.</summary>
    internal sealed class WbMeshBuilder
    {
        public readonly List<WbVertex> verts = new List<WbVertex>();
        public readonly List<int>[] tris;
        public bool lightmapUv;

        public WbMeshBuilder(int submeshes)
        {
            tris = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) tris[i] = new List<int>();
        }

        public bool IsEmpty => verts.Count == 0;

        public void AddPolygon(int sub, List<WbVertex> poly)
        {
            if (poly.Count < 3) return;
            int b = verts.Count;
            verts.AddRange(poly);
            List<int> t = tris[sub];
            for (int i = 1; i < poly.Count - 1; i++) { t.Add(b); t.Add(b + i); t.Add(b + i + 1); }
        }

        public void Append(WbMeshBuilder other)
        {
            int b = verts.Count;
            verts.AddRange(other.verts);
            for (int s = 0; s < tris.Length && s < other.tris.Length; s++)
            {
                List<int> src = other.tris[s];
                for (int i = 0; i < src.Count; i++) tris[s].Add(src[i] + b);
            }
        }

        /// <summary>Vertical range (along one axis) covered by the geometry.</summary>
        public bool Range(int axis, out float min, out float max)
        {
            min = float.MaxValue; max = float.MinValue;
            for (int i = 0; i < verts.Count; i++)
            {
                float c = WbSlicer.C(verts[i].p, axis);
                if (c < min) min = c;
                if (c > max) max = c;
            }
            return verts.Count > 0;
        }

        public Mesh ToMesh(string name)
        {
            int n = verts.Count;
            var pos = new Vector3[n];
            var nor = new Vector3[n];
            var tan = new Vector4[n];
            var uv0 = new Vector2[n];
            var uv1 = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                WbVertex v = verts[i];
                pos[i] = v.p; nor[i] = v.n; tan[i] = v.t; uv0[i] = v.uv0; uv1[i] = v.uv1;
            }
            var m = new Mesh { name = name };
            if (n > 65000) m.indexFormat = IndexFormat.UInt32;
            m.vertices = pos;
            m.normals = nor;
            m.tangents = tan;
            m.uv = uv0;
            if (lightmapUv) m.uv2 = uv1;
            m.subMeshCount = tris.Length;
            for (int s = 0; s < tris.Length; s++) m.SetTriangles(tris[s], s, false);
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>Cuts wall geometry with axis-aligned boxes and builds the cut faces ("caps") that close the solid.</summary>
    internal static class WbSlicer
    {
        const float Eps = 1e-5f;
        static readonly List<WbVertex> PolyA = new List<WbVertex>(16);
        static readonly List<WbVertex> PolyB = new List<WbVertex>(16);

        public static float C(Vector3 v, int axis) { return axis == 0 ? v.x : axis == 1 ? v.y : v.z; }

        public static Vector3 V(int a0, float c0, int a1, float c1, int a2, float c2)
        {
            Vector3 r = Vector3.zero;
            r[a0] = c0; r[a1] = c1; r[a2] = c2;
            return r;
        }

        /// <summary>Adds the part of the source mesh inside the box to dest. Use ±infinity for an unbounded side.</summary>
        public static void ClipBox(WallMeshData src, Vector3 min, Vector3 max, WbMeshBuilder dest)
        {
            dest.lightmapUv = src.HasLightmapUv;
            for (int s = 0; s < src.SubmeshCount; s++)
            {
                int[] tris = src.subTris[s];
                for (int k = 0; k + 2 < tris.Length; k += 3)
                {
                    WbVertex a = src.verts[tris[k]], b = src.verts[tris[k + 1]], c = src.verts[tris[k + 2]];
                    Vector3 lo = Vector3.Min(a.p, Vector3.Min(b.p, c.p));
                    Vector3 hi = Vector3.Max(a.p, Vector3.Max(b.p, c.p));
                    if (hi.x < min.x - Eps || lo.x > max.x + Eps || hi.y < min.y - Eps || lo.y > max.y + Eps || hi.z < min.z - Eps || lo.z > max.z + Eps)
                        continue;

                    PolyA.Clear();
                    PolyA.Add(a); PolyA.Add(b); PolyA.Add(c);
                    List<WbVertex> cur = PolyA, other = PolyB;
                    for (int axis = 0; axis < 3 && cur.Count >= 3; axis++)
                    {
                        float lo1 = C(min, axis), hi1 = C(max, axis);
                        if (!float.IsInfinity(lo1) && C(lo, axis) < lo1 - Eps)
                        {
                            ClipPlane(cur, other, axis, lo1, 1f);
                            var t = cur; cur = other; other = t;
                        }
                        if (!float.IsInfinity(hi1) && C(hi, axis) > hi1 + Eps && cur.Count >= 3)
                        {
                            ClipPlane(cur, other, axis, hi1, -1f);
                            var t = cur; cur = other; other = t;
                        }
                    }
                    if (cur.Count >= 3) dest.AddPolygon(s, cur);
                }
            }
        }

        static void ClipPlane(List<WbVertex> inp, List<WbVertex> outp, int axis, float val, float sign)
        {
            outp.Clear();
            int cnt = inp.Count;
            for (int i = 0; i < cnt; i++)
            {
                WbVertex cur = inp[i];
                WbVertex prev = inp[(i + cnt - 1) % cnt];
                float dc = sign * (C(cur.p, axis) - val);
                float dp = sign * (C(prev.p, axis) - val);
                bool cin = dc >= -Eps, pin = dp >= -Eps;
                if (cin)
                {
                    if (!pin) outp.Add(WbVertex.Lerp(prev, cur, dp / (dp - dc)));
                    outp.Add(cur);
                }
                else if (pin)
                {
                    outp.Add(WbVertex.Lerp(prev, cur, dp / (dp - dc)));
                }
            }
        }

        /// <summary>Keeps the part of a polygon where dot(a, p) - d is at most zero.</summary>
        public static void ClipHalfSpace(List<WbVertex> inp, List<WbVertex> outp, Vector3 a, float d)
        {
            outp.Clear();
            int cnt = inp.Count;
            for (int i = 0; i < cnt; i++)
            {
                WbVertex cur = inp[i];
                WbVertex prev = inp[(i + cnt - 1) % cnt];
                float dc = Vector3.Dot(a, cur.p) - d;
                float dp = Vector3.Dot(a, prev.p) - d;
                bool cin = dc <= 0f, pin = dp <= 0f;
                if (cin)
                {
                    if (!pin) outp.Add(WbVertex.Lerp(prev, cur, dp / (dp - dc)));
                    outp.Add(cur);
                }
                else if (pin)
                {
                    outp.Add(WbVertex.Lerp(prev, cur, dp / (dp - dc)));
                }
            }
        }

        /// <summary>
        /// A flat rectangular face facing outwards along (faceAxis, sign), spanning [a0,a1] on spanAxis and [t0,t1]
        /// on thickAxis, at plane value 'plane'. Its UVs continue the wall's texture using the given per-axis density.
        /// </summary>
        public static void AddCap(WbMeshBuilder b, int sub, int faceAxis, float sign, float plane, int spanAxis, float a0, float a1,
            int thickAxis, float t0, float t1, Vector3 uvPerAxis)
        {
            if (a1 - a0 < 1e-5f || t1 - t0 < 1e-5f) return;
            Vector3 p00 = V(faceAxis, plane, spanAxis, a0, thickAxis, t0);
            Vector3 p10 = V(faceAxis, plane, spanAxis, a1, thickAxis, t0);
            Vector3 p11 = V(faceAxis, plane, spanAxis, a1, thickAxis, t1);
            Vector3 p01 = V(faceAxis, plane, spanAxis, a0, thickAxis, t1);
            Vector3 normal = V(faceAxis, sign, spanAxis, 0f, thickAxis, 0f);
            Vector4 tangent = new Vector4(0, 0, 0, 1);
            Vector3 tdir = V(spanAxis, 1f, faceAxis, 0f, thickAxis, 0f);
            tangent.x = tdir.x; tangent.y = tdir.y; tangent.z = tdir.z;

            WbVertex v0 = Make(p00, normal, tangent, spanAxis, thickAxis, uvPerAxis);
            WbVertex v1 = Make(p10, normal, tangent, spanAxis, thickAxis, uvPerAxis);
            WbVertex v2 = Make(p11, normal, tangent, spanAxis, thickAxis, uvPerAxis);
            WbVertex v3 = Make(p01, normal, tangent, spanAxis, thickAxis, uvPerAxis);

            // Unity front faces: cross(b - a, c - a) points at the viewer.
            bool flip = Vector3.Dot(Vector3.Cross(p10 - p00, p11 - p00), normal) < 0f;
            var poly = PolyA;
            poly.Clear();
            if (!flip) { poly.Add(v0); poly.Add(v1); poly.Add(v2); poly.Add(v3); }
            else { poly.Add(v0); poly.Add(v3); poly.Add(v2); poly.Add(v1); }
            b.AddPolygon(sub, poly);
        }

        static WbVertex Make(Vector3 p, Vector3 n, Vector4 t, int a1, int a2, Vector3 uvPerAxis)
        {
            WbVertex v;
            v.p = p; v.n = n; v.t = t;
            v.uv0 = new Vector2(C(p, a1) * C(uvPerAxis, a1), C(p, a2) * C(uvPerAxis, a2));
            v.uv1 = Vector2.zero;
            return v;
        }
    }
}

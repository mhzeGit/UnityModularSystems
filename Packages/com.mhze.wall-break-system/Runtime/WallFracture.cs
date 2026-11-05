using System.Collections.Generic;
using UnityEngine;

namespace MHZE.WallBreak
{
    /// <summary>One closed face of a piece, where it meets a neighbouring piece.</summary>
    internal struct FractureCap
    {
        public int neighbor;
        public List<WbVertex> poly;
    }

    /// <summary>One piece of the wall: its share of the wall's own surface, and the faces where it meets its neighbours.</summary>
    internal sealed class FractureCell
    {
        public Vector3 site;
        public bool inHole;
        public bool released;
        public WbMeshBuilder surface;
        public readonly List<FractureCap> caps = new List<FractureCap>();
    }

    /// <summary>A line on a wall face where two pieces meet. Cracks are drawn along these, so the wall later breaks where it cracked.</summary>
    internal struct FractureEdge
    {
        public int a, b;
        public Vector3 p0, p1;
        public bool frontFace;
    }

    /// <summary>What the fracture needs to know about a wall. Lengths with M are metres; the rest are mesh units.</summary>
    internal struct FractureSettings
    {
        public int u, v, t;
        public Vector3 scale, min, max;
        public Vector2 centre;
        public float halfU, halfV;
        public float chunkSize, roughness;
        public int seed, insideSubmesh, totalSubmeshes;
        public bool holeAtFloor, floorIsMax;
    }

    /// <summary>
    /// Divides a wall into irregular pieces with a 3D Voronoi pattern: small shards near the middle of the hole, bigger
    /// slabs further out, and cut faces that lean a little so the front and back outlines differ. Every piece gets the part
    /// of the wall's own surface that falls inside it, plus closed faces where it meets its neighbours, so a piece can
    /// either stay in the wall or fall away as a solid object. Works in metres, so a stretched wall still breaks evenly.
    /// </summary>
    internal sealed class WallFracture
    {
        public FractureCell[] cells;
        public readonly List<FractureEdge> edges = new List<FractureEdge>();

        static readonly List<WbVertex> PolyA = new List<WbVertex>(32);
        static readonly List<WbVertex> PolyB = new List<WbVertex>(32);

        public static WallFracture Build(WallMeshData data, FractureSettings s)
        {
            var result = new WallFracture();
            var rng = new System.Random(s.seed);
            List<Vector3> sites = MakeSites(rng, s);
            int n = sites.Count;
            result.cells = new FractureCell[n];

            int u = s.u, v = s.v, t = s.t;
            Vector3 sc = s.scale;
            float cuM = s.centre.x * sc[u], cvM = s.centre.y * sc[v];
            float huM = s.halfU * sc[u], hvM = s.halfV * sc[v];
            float tMax = s.max[t] * sc[t], tMin = s.min[t] * sc[t];
            float faceTol = Mathf.Max(0.004f, (tMax - tMin) * 0.04f);

            var order = new int[n];
            var dist = new float[n];
            int limit = Mathf.Min(n, 64);
            var planesA = new Vector3[limit];
            var planesD = new float[limit];

            for (int i = 0; i < n; i++)
            {
                Vector3 si = sites[i];
                for (int j = 0; j < n; j++) { order[j] = j; dist[j] = (sites[j] - si).sqrMagnitude; }
                System.Array.Sort(dist, order);

                // Half-spaces in metric form: dot(a, p) <= d keeps a mesh-space point p on this site's side.
                for (int k = 0; k < limit; k++)
                {
                    int j = order[k];
                    if (j == i) { planesA[k] = Vector3.zero; continue; }
                    Vector3 dir = sites[j] - si;
                    Vector3 mid = (sites[j] + si) * 0.5f;
                    dir.Normalize();
                    planesA[k] = Vector3.Scale(dir, sc);
                    planesD[k] = Vector3.Dot(dir, mid);
                }

                var cell = new FractureCell { site = si, surface = new WbMeshBuilder(s.totalSubmeshes) { lightmapUv = data.HasLightmapUv } };
                result.cells[i] = cell;
                ClipSurface(data, planesA, planesD, limit, cell.surface);
                if (cell.surface.IsEmpty) continue;

                // Faces between pieces: the convex outline of the surface points that lie on each shared plane.
                for (int k = 0; k < limit; k++)
                {
                    if (planesA[k] == Vector3.zero) continue;
                    List<WbVertex> poly = BuildCap(cell.surface, planesA[k], planesD[k], sc, data.UvPerUnit);
                    if (poly == null) continue;
                    int neighbor = order[k];
                    cell.caps.Add(new FractureCap { neighbor = neighbor, poly = poly });
                    if (i < neighbor) AddEdges(result.edges, poly, i, neighbor, t, s.max[t], s.min[t], faceTol / sc[t]);
                }

                // Which pieces fall out: inside the ragged outline, or touching the always-open doorway.
                float nx = (si[u] - cuM) / huM, ny = (si[v] - cvM) / hvM;
                float r = Mathf.Pow(Mathf.Pow(Mathf.Abs(nx), 3f) + Mathf.Pow(Mathf.Abs(ny), 3f), 1f / 3f);
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.4f * s.roughness;
                bool hole = r + noise <= 1f && r <= 1f + 0.3f * s.roughness;
                if (!hole) hole = TouchesDoorway(cell.surface, s, cuM, cvM, huM, hvM);
                cell.inHole = hole;
            }
            return result;
        }

        static List<Vector3> MakeSites(System.Random rng, FractureSettings s)
        {
            int u = s.u, v = s.v, t = s.t;
            Vector3 sc = s.scale;
            float minU = s.min[u] * sc[u], maxU = s.max[u] * sc[u];
            float minV = s.min[v] * sc[v], maxV = s.max[v] * sc[v];
            float tMid = (s.min[t] + s.max[t]) * 0.5f * sc[t];
            float thick = (s.max[t] - s.min[t]) * sc[t];
            float cu = s.centre.x * sc[u], cv = s.centre.y * sc[v];
            float hu = s.halfU * sc[u], hv = s.halfV * sc[v];
            float reach = Mathf.Max(hu, hv);
            float margin = s.chunkSize * 2f;
            float rMin = Mathf.Max(s.chunkSize * 0.5f, thick * 0.4f);

            float x0 = Mathf.Max(minU, cu - hu - margin), x1 = Mathf.Min(maxU, cu + hu + margin);
            float y0 = Mathf.Max(minV, cv - hv - margin), y1 = Mathf.Min(maxV, cv + hv + margin);

            var sites = new List<Vector3>(256);
            for (int tries = 0; tries < 16000 && sites.Count < 480; tries++)
            {
                float x = Mathf.Lerp(x0, x1, (float)rng.NextDouble());
                float y = Mathf.Lerp(y0, y1, (float)rng.NextDouble());
                float dx = x - cu, dy = y - cv;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float r = Mathf.Max(rMin, s.chunkSize * Mathf.Lerp(0.55f, 1.45f, Mathf.Clamp01(d / reach)));
                if (TooClose(sites, x, y, u, v, r)) continue;
                sites.Add(Site(u, x, v, y, t, tMid + ((float)rng.NextDouble() * 2f - 1f) * 0.32f * Mathf.Min(r, thick * 0.5f)));
            }

            // Coarse pieces for the rest of the wall, so a piece at the edge of the hole cannot reach across the whole wall.
            float area = (maxU - minU) * (maxV - minV);
            float far = Mathf.Max(s.chunkSize * 2.6f, Mathf.Sqrt(area / 180f));
            for (float x = minU + far * 0.5f; x < maxU; x += far)
            {
                for (float y = minV + far * 0.5f; y < maxV; y += far)
                {
                    float jx = x + ((float)rng.NextDouble() - 0.5f) * far * 0.5f;
                    float jy = y + ((float)rng.NextDouble() - 0.5f) * far * 0.5f;
                    if (jx > x0 - far * 0.3f && jx < x1 + far * 0.3f && jy > y0 - far * 0.3f && jy < y1 + far * 0.3f) continue;
                    if (TooClose(sites, jx, jy, u, v, far * 0.5f)) continue;
                    sites.Add(Site(u, jx, v, jy, t, tMid));
                }
            }
            if (sites.Count == 0) sites.Add(Site(u, cu, v, cv, t, tMid));
            return sites;
        }

        static Vector3 Site(int u, float x, int v, float y, int t, float z)
        {
            Vector3 m = Vector3.zero;
            m[u] = x; m[v] = y; m[t] = z;
            return m;
        }

        static bool TooClose(List<Vector3> sites, float x, float y, int u, int v, float r)
        {
            float rr = r * r;
            for (int i = 0; i < sites.Count; i++)
            {
                float ax = sites[i][u] - x, ay = sites[i][v] - y;
                if (ax * ax + ay * ay < rr) return true;
            }
            return false;
        }

        static void ClipSurface(WallMeshData data, Vector3[] a, float[] d, int count, WbMeshBuilder dest)
        {
            for (int s = 0; s < data.SubmeshCount; s++)
            {
                int[] tris = data.subTris[s];
                for (int k = 0; k + 2 < tris.Length; k += 3)
                {
                    PolyA.Clear();
                    PolyA.Add(data.verts[tris[k]]); PolyA.Add(data.verts[tris[k + 1]]); PolyA.Add(data.verts[tris[k + 2]]);
                    List<WbVertex> cur = PolyA, other = PolyB;
                    bool empty = false;
                    for (int p = 0; p < count; p++)
                    {
                        Vector3 pa = a[p];
                        if (pa == Vector3.zero) continue;
                        bool anyOut = false, anyIn = false;
                        for (int q = 0; q < cur.Count; q++)
                        {
                            if (Vector3.Dot(pa, cur[q].p) - d[p] > 0f) anyOut = true; else anyIn = true;
                        }
                        if (!anyOut) continue;
                        if (!anyIn) { empty = true; break; }
                        WbSlicer.ClipHalfSpace(cur, other, pa, d[p]);
                        var swap = cur; cur = other; other = swap;
                        if (cur.Count < 3) { empty = true; break; }
                    }
                    if (!empty) dest.AddPolygon(s, cur);
                }
            }
        }

        static List<WbVertex> BuildCap(WbMeshBuilder surface, Vector3 a, float d, Vector3 sc, float uvPerMeter)
        {
            const float onPlane = 0.0015f;
            List<WbVertex> pts = null;
            List<WbVertex> verts = surface.verts;
            for (int i = 0; i < verts.Count; i++)
            {
                if (Mathf.Abs(Vector3.Dot(a, verts[i].p) - d) > onPlane) continue;
                if (pts == null) pts = new List<WbVertex>(8);
                pts.Add(verts[i]);
            }
            if (pts == null || pts.Count < 3) return null;

            // Two directions along the plane, in metres.
            Vector3 normalM = new Vector3(a.x / sc.x, a.y / sc.y, a.z / sc.z).normalized;
            Vector3 e1 = Vector3.Cross(normalM, Mathf.Abs(normalM.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 e2 = Vector3.Cross(normalM, e1);

            // Convex hull of the points, in those directions (Andrew's monotone chain).
            var flat = new List<KeyValuePair<Vector2, WbVertex>>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 m = Vector3.Scale(pts[i].p, sc);
                flat.Add(new KeyValuePair<Vector2, WbVertex>(new Vector2(Vector3.Dot(m, e1), Vector3.Dot(m, e2)), pts[i]));
            }
            flat.Sort((x, y) => x.Key.x != y.Key.x ? x.Key.x.CompareTo(y.Key.x) : x.Key.y.CompareTo(y.Key.y));
            var hull = new List<KeyValuePair<Vector2, WbVertex>>(flat.Count + 1);
            for (int i = 0; i < flat.Count; i++)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2].Key, hull[hull.Count - 1].Key, flat[i].Key) <= 1e-7f) hull.RemoveAt(hull.Count - 1);
                hull.Add(flat[i]);
            }
            int lower = hull.Count + 1;
            for (int i = flat.Count - 2; i >= 0; i--)
            {
                while (hull.Count >= lower && Cross(hull[hull.Count - 2].Key, hull[hull.Count - 1].Key, flat[i].Key) <= 1e-7f) hull.RemoveAt(hull.Count - 1);
                hull.Add(flat[i]);
            }
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
            if (hull.Count < 3) return null;

            float area = 0f;
            for (int i = 1; i < hull.Count - 1; i++) area += Cross(hull[0].Key, hull[i].Key, hull[i + 1].Key) * 0.5f;
            if (Mathf.Abs(area) < 1e-5f) return null;

            Vector3 normalLocal = a.normalized;
            Vector3 tangentLocal = new Vector3(e1.x / sc.x, e1.y / sc.y, e1.z / sc.z).normalized;
            var poly = new List<WbVertex>(hull.Count);
            for (int i = 0; i < hull.Count; i++)
            {
                WbVertex w = hull[i].Value;
                w.n = normalLocal;
                w.t = new Vector4(tangentLocal.x, tangentLocal.y, tangentLocal.z, 1f);
                w.uv0 = hull[i].Key * uvPerMeter;
                poly.Add(w);
            }
            // Unity front faces: cross(b - a, c - a) points at the viewer.
            Vector3 g = Vector3.Cross(Vector3.Scale(poly[1].p - poly[0].p, sc), Vector3.Scale(poly[2].p - poly[0].p, sc));
            if (Vector3.Dot(g, normalM) < 0f) poly.Reverse();
            return poly;
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }

        static void AddEdges(List<FractureEdge> edges, List<WbVertex> poly, int a, int b, int t, float tHi, float tLo, float tol)
        {
            AddEdge(edges, poly, a, b, t, tHi, tol, true);
            AddEdge(edges, poly, a, b, t, tLo, tol, false);
        }

        static void AddEdge(List<FractureEdge> edges, List<WbVertex> poly, int a, int b, int t, float face, float tol, bool front)
        {
            Vector3 p0 = Vector3.zero, p1 = Vector3.zero;
            bool have0 = false, have1 = false;
            float best = -1f;
            for (int i = 0; i < poly.Count; i++)
            {
                if (Mathf.Abs(poly[i].p[t] - face) > tol) continue;
                if (!have0) { p0 = poly[i].p; have0 = true; continue; }
                float d = (poly[i].p - p0).sqrMagnitude;
                if (d > best) { best = d; p1 = poly[i].p; have1 = true; }
            }
            if (have0 && have1) edges.Add(new FractureEdge { a = a, b = b, p0 = p0, p1 = p1, frontFace = front });
        }

        static bool TouchesDoorway(WbMeshBuilder surface, FractureSettings s, float cuM, float cvM, float huM, float hvM)
        {
            List<WbVertex> verts = surface.verts;
            for (int i = 0; i < verts.Count; i++)
            {
                float nx = (verts[i].p[s.u] * s.scale[s.u] - cuM) / huM;
                float ny = (verts[i].p[s.v] * s.scale[s.v] - cvM) / hvM;
                if (Mathf.Abs(nx) > 0.66f) continue;
                float lo = -0.8f, hi = 0.8f;
                if (s.holeAtFloor) { if (s.floorIsMax) hi = 2f; else lo = -2f; }
                if (ny >= lo && ny <= hi) return true;
            }
            return false;
        }
    }
}

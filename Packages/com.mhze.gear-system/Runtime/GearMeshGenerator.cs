using UnityEngine;

namespace MHZE.GearSystem
{
    [AddComponentMenu("Mechanical/Gear Mesh Generator")]
    public class GearMeshGenerator : MonoBehaviour
    {
        [Header("Gear Geometry")]
        [Min(3)]
        public int toothCount = 12;
        [Min(0.01f)]
        public float pitchRadius = 0.5f;
        [Min(0.001f)]
        public float toothHeight = 0.1f;
        [Min(0.001f)]
        public float toothWidth = 0.13f;
        [Min(0.001f)]
        public float thickness = 0.2f;
        public GearAxis axis = GearAxis.Y;
        [Range(0f, 0.85f)]
        public float centerHoleRadiusFraction = 0f;
        [Range(0f, 360f)]
        public float rotationOffset;

        [Header("Mesh Quality")]
        [Range(3, 32)]
        public int segmentsPerTooth = 8;

        [Header("Auto Generation")]
        public bool generateOnAwake;

        [Header("Output")]
        public Mesh generatedMesh;
        [HideInInspector]
        public string m_GeneratedMeshAssetPath;

        public float CenterHoleRadius => pitchRadius * centerHoleRadiusFraction;

        private void Awake()
        {
            if (generateOnAwake)
                Generate();
        }

        public void Generate()
        {
            if (generatedMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(generatedMesh);
                else
                    DestroyImmediate(generatedMesh);
                generatedMesh = null;
            }

            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null)
                mf = gameObject.AddComponent<MeshFilter>();

            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr == null)
                mr = gameObject.AddComponent<MeshRenderer>();

            Mesh mesh = BuildGearMesh();
            mesh.name = $"Gear_{toothCount}t_{pitchRadius:F2}r";

            mf.sharedMesh = mesh;
            generatedMesh = mesh;
        }

        private static Vector3 GearVertex(GearAxis a, Vector3 radial, float axialOffset)
        {
            switch (a)
            {
                case GearAxis.X: return new Vector3(axialOffset, radial.x, radial.z);
                case GearAxis.Y: return new Vector3(radial.x, axialOffset, radial.z);
                case GearAxis.Z: return new Vector3(radial.x, radial.z, axialOffset);
                default: return Vector3.zero;
            }
        }

        // ── Step 1: Build the base cylinder ──────────────────────────────
        private Mesh BuildBaseCylinder()
        {
            int segs = toothCount * segmentsPerTooth;
            float halfThick = thickness * 0.5f;
            float holeRadius = CenterHoleRadius;
            bool hasHole = holeRadius > 0.001f;

            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();

            // Each face ring is a separate vertex copy so RecalculateNormals
            // never blends normals across different face types.

            // Wall front ring – vertices at pitchRadius, y = -halfThick
            int wallFront = verts.Count;
            for (int i = 0; i < segs; i++)
            {
                float a = i * 2f * Mathf.PI / segs;
                Vector3 r = new Vector3(Mathf.Cos(a) * pitchRadius, 0, Mathf.Sin(a) * pitchRadius);
                verts.Add(GearVertex(axis, r, -halfThick));
            }
            // Wall back ring – vertices at pitchRadius, y = +halfThick
            int wallBack = verts.Count;
            for (int i = 0; i < segs; i++)
            {
                float a = i * 2f * Mathf.PI / segs;
                Vector3 r = new Vector3(Mathf.Cos(a) * pitchRadius, 0, Mathf.Sin(a) * pitchRadius);
                verts.Add(GearVertex(axis, r, halfThick));
            }
            // Wall quads (all segments)
            for (int i = 0; i < segs; i++)
            {
                int n = (i + 1) % segs;
                int fi = wallFront + i, fn = wallFront + n;
                int bi = wallBack + i, bn = wallBack + n;
                tris.Add(fi); tris.Add(bi); tris.Add(bn);
                tris.Add(fi); tris.Add(bn); tris.Add(fn);
            }

            // Cap outer ring – separate copies so wall↔cap edge stays hard
            int capOuterFront = verts.Count;
            for (int i = 0; i < segs; i++)
            {
                float a = i * 2f * Mathf.PI / segs;
                Vector3 r = new Vector3(Mathf.Cos(a) * pitchRadius, 0, Mathf.Sin(a) * pitchRadius);
                verts.Add(GearVertex(axis, r, -halfThick));
            }
            int capOuterBack = verts.Count;
            for (int i = 0; i < segs; i++)
            {
                float a = i * 2f * Mathf.PI / segs;
                Vector3 r = new Vector3(Mathf.Cos(a) * pitchRadius, 0, Mathf.Sin(a) * pitchRadius);
                verts.Add(GearVertex(axis, r, halfThick));
            }

            // Inner wall (hole cylinder) – separate vertices
            int holeVertFrontStart = -1;
            if (hasHole)
            {
                holeVertFrontStart = verts.Count;
                for (int i = 0; i < segs; i++)
                {
                    float a = i * 2f * Mathf.PI / segs;
                    Vector3 r = new Vector3(Mathf.Cos(a) * holeRadius, 0, Mathf.Sin(a) * holeRadius);
                    verts.Add(GearVertex(axis, r, -halfThick));
                    verts.Add(GearVertex(axis, r, halfThick));
                }
                for (int i = 0; i < segs; i++)
                {
                    int n = (i + 1) % segs;
                    int fi0 = holeVertFrontStart + i * 2, fi1 = holeVertFrontStart + n * 2;
                    int bi0 = holeVertFrontStart + i * 2 + 1, bi1 = holeVertFrontStart + n * 2 + 1;
                    tris.Add(fi0); tris.Add(fi1); tris.Add(bi0);
                    tris.Add(fi1); tris.Add(bi1); tris.Add(bi0);
                }
            }

            // Cap inner ring – separate from inner wall for sharp normals
            if (hasHole)
            {
                int capHoleFront = verts.Count;
                for (int i = 0; i < segs; i++)
                {
                    float a = i * 2f * Mathf.PI / segs;
                    Vector3 r = new Vector3(Mathf.Cos(a) * holeRadius, 0, Mathf.Sin(a) * holeRadius);
                    verts.Add(GearVertex(axis, r, -halfThick));
                }
                int capHoleBack = verts.Count;
                for (int i = 0; i < segs; i++)
                {
                    float a = i * 2f * Mathf.PI / segs;
                    Vector3 r = new Vector3(Mathf.Cos(a) * holeRadius, 0, Mathf.Sin(a) * holeRadius);
                    verts.Add(GearVertex(axis, r, halfThick));
                }

                for (int i = 0; i < segs; i++)
                {
                    int n = (i + 1) % segs;
                    int oi = capOuterFront + i, on = capOuterFront + n;
                    int ii = capHoleFront + i, inn = capHoleFront + n;
                    tris.Add(oi); tris.Add(on); tris.Add(ii);
                    tris.Add(on); tris.Add(inn); tris.Add(ii);
                }
                for (int i = 0; i < segs; i++)
                {
                    int n = (i + 1) % segs;
                    int oi = capOuterBack + i, on = capOuterBack + n;
                    int ii = capHoleBack + i, inn = capHoleBack + n;
                    tris.Add(oi); tris.Add(ii); tris.Add(on);
                    tris.Add(on); tris.Add(ii); tris.Add(inn);
                }
            }
            else
            {
                int centerFront = verts.Count;
                verts.Add(GearVertex(axis, Vector3.zero, -halfThick));
                for (int i = 0; i < segs; i++)
                {
                    int n = (i + 1) % segs;
                    tris.Add(centerFront);
                    tris.Add(capOuterFront + i);
                    tris.Add(capOuterFront + n);
                }
                int centerBack = verts.Count;
                verts.Add(GearVertex(axis, Vector3.zero, halfThick));
                for (int i = 0; i < segs; i++)
                {
                    int n = (i + 1) % segs;
                    tris.Add(centerBack);
                    tris.Add(capOuterBack + n);
                    tris.Add(capOuterBack + i);
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            return mesh;
        }

        // ── Step 2: Build a single tooth ─────────────────────────────────
        private Mesh BuildToothMesh()
        {
            float halfTooth = toothWidth * 0.5f / pitchRadius;
            float halfTip = halfTooth * 0.65f;
            float halfThick = thickness * 0.5f;
            float r = pitchRadius;
            float rt = pitchRadius + toothHeight;

            float cosHT = Mathf.Cos(halfTooth);
            float sinHT = Mathf.Sin(halfTooth);
            float cosHt = Mathf.Cos(halfTip);
            float sinHt = Mathf.Sin(halfTip);

            Vector3 rl_rad = new Vector3(r * cosHT, 0, -r * sinHT);
            Vector3 rr_rad = new Vector3(r * cosHT, 0, r * sinHT);
            Vector3 tl_rad = new Vector3(rt * cosHt, 0, -rt * sinHt);
            Vector3 tr_rad = new Vector3(rt * cosHt, 0, rt * sinHt);

            // 5 faces × 4 vertices each = 20 non-shared vertices.
            // Each face gets its own vertices so normals stay sharp across edges.
            Vector3[] verts = new Vector3[20];

            // Front face (0-3): rl, tl, tr, rr  (CCW from -axis)
            verts[0] = GearVertex(axis, rl_rad, -halfThick);
            verts[1] = GearVertex(axis, tl_rad, -halfThick);
            verts[2] = GearVertex(axis, tr_rad, -halfThick);
            verts[3] = GearVertex(axis, rr_rad, -halfThick);

            // Back face (4-7): rl, rr, tr, tl  (CCW from +axis)
            verts[4] = GearVertex(axis, rl_rad, halfThick);
            verts[5] = GearVertex(axis, rr_rad, halfThick);
            verts[6] = GearVertex(axis, tr_rad, halfThick);
            verts[7] = GearVertex(axis, tl_rad, halfThick);

            // Left flank (8-11): rl_f, rl_b, tl_b, tl_f  (CCW from outside)
            verts[8] = GearVertex(axis, rl_rad, -halfThick);
            verts[9] = GearVertex(axis, rl_rad, halfThick);
            verts[10] = GearVertex(axis, tl_rad, halfThick);
            verts[11] = GearVertex(axis, tl_rad, -halfThick);

            // Right flank (12-15): rr_f, tr_f, tr_b, rr_b  (CCW from outside)
            verts[12] = GearVertex(axis, rr_rad, -halfThick);
            verts[13] = GearVertex(axis, tr_rad, -halfThick);
            verts[14] = GearVertex(axis, tr_rad, halfThick);
            verts[15] = GearVertex(axis, rr_rad, halfThick);

            // Top face (16-19): tl_f, tl_b, tr_b, tr_f  (CCW from outside)
            verts[16] = GearVertex(axis, tl_rad, -halfThick);
            verts[17] = GearVertex(axis, tl_rad, halfThick);
            verts[18] = GearVertex(axis, tr_rad, halfThick);
            verts[19] = GearVertex(axis, tr_rad, -halfThick);

            // Each quad → 2 triangles: (v0,v1,v2), (v0,v2,v3)
            int[] tris = new int[30];
            int t = 0;

            // Front
            tris[t++] = 0; tris[t++] = 1; tris[t++] = 2;
            tris[t++] = 0; tris[t++] = 2; tris[t++] = 3;

            // Back
            tris[t++] = 4; tris[t++] = 5; tris[t++] = 6;
            tris[t++] = 4; tris[t++] = 6; tris[t++] = 7;

            // Left
            tris[t++] = 8; tris[t++] = 9; tris[t++] = 10;
            tris[t++] = 8; tris[t++] = 10; tris[t++] = 11;

            // Right
            tris[t++] = 12; tris[t++] = 13; tris[t++] = 14;
            tris[t++] = 12; tris[t++] = 14; tris[t++] = 15;

            // Top
            tris[t++] = 16; tris[t++] = 17; tris[t++] = 18;
            tris[t++] = 16; tris[t++] = 18; tris[t++] = 19;

            Mesh mesh = new Mesh();
            mesh.vertices = verts;
            mesh.triangles = tris;
            return mesh;
        }

        // ── Step 3: Combine base cylinder + tooth copies ─────────────────
        private Mesh BuildGearMesh()
        {
            Mesh baseMesh = BuildBaseCylinder();
            Mesh toothMesh = BuildToothMesh();

            int combineCount = 1 + toothCount;
            var combine = new CombineInstance[combineCount];

            // Base cylinder: identity transform
            combine[0].mesh = baseMesh;
            combine[0].transform = Matrix4x4.identity;

            // Place teeth around the gear
            float periodDeg = 360f / toothCount;

            for (int i = 0; i < toothCount; i++)
            {
                Quaternion rot = GetToothRotation(i * periodDeg + rotationOffset);
                Vector3 pos = Vector3.zero;
                combine[1 + i].mesh = toothMesh;
                combine[1 + i].transform = Matrix4x4.TRS(pos, rot, Vector3.one);
            }

            Mesh result = new Mesh();
            result.CombineMeshes(combine, true, true);
            result.RecalculateNormals();
            result.RecalculateBounds();
            return result;
        }

        private Quaternion GetToothRotation(float angleDeg)
        {
            switch (axis)
            {
                case GearAxis.X: return Quaternion.Euler(angleDeg, 0, 0);
                case GearAxis.Y: return Quaternion.Euler(0, angleDeg, 0);
                case GearAxis.Z: return Quaternion.Euler(0, 0, angleDeg);
                default: return Quaternion.identity;
            }
        }

        public string GetGeometryHash()
        {
            string canonical = $"{toothCount}|{pitchRadius:F6}|{toothHeight:F6}|{toothWidth:F6}|{thickness:F6}|{axis}|{centerHoleRadiusFraction:F6}|{rotationOffset:F6}|{segmentsPerTooth}";
            return Hash128.Compute(canonical).ToString();
        }

        private void OnDestroy()
        {
            if (generatedMesh != null && Application.isPlaying)
                Destroy(generatedMesh);
        }

        private void OnValidate()
        {
            toothCount = Mathf.Max(3, toothCount);
            toothWidth = Mathf.Max(0.001f, toothWidth);
            thickness = Mathf.Max(0.001f, thickness);
            pitchRadius = Mathf.Max(0.01f, pitchRadius);
            toothHeight = Mathf.Max(0.001f, toothHeight);
            segmentsPerTooth = Mathf.Max(3, segmentsPerTooth);
        }
    }
}

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
        [Range(1f, 179f)]
        public float toothWidthAngle = 15f;
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
        public bool assignMeshCollider;

        [Header("Output")]
        public Mesh generatedMesh;

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

            if (assignMeshCollider)
            {
                MeshCollider mc = GetComponent<MeshCollider>();
                if (mc == null)
                    mc = gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
            }
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
            float period = 360f / toothCount;
            float halfThick = thickness * 0.5f;
            float halfTooth = toothWidthAngle * 0.5f;
            float holeRadius = CenterHoleRadius;
            bool hasHole = holeRadius > 0.001f;

            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();

            // Outer wall – iterate over each gap region between teeth explicitly.
            // For each tooth gap [i*period+halfTooth, (i+1)*period-halfTooth],
            // create wall vertex pairs at regular intervals covering the full gap.
            for (int ti = 0; ti < toothCount; ti++)
            {
                float gapStartDeg = ti * period + halfTooth;
                float gapEndDeg = (ti + 1) * period - halfTooth;
                float gapWidthDeg = gapEndDeg - gapStartDeg;

                if (gapWidthDeg <= 0f) continue;

                int numGapSegs = Mathf.Max(1, Mathf.RoundToInt(gapWidthDeg / (period / segmentsPerTooth)));
                int prevFront = -1;
                int prevBack = -1;

                for (int j = 0; j <= numGapSegs; j++)
                {
                    float t = (float)j / numGapSegs;
                    float angleRad = (gapStartDeg + t * gapWidthDeg) * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angleRad);
                    float sin = Mathf.Sin(angleRad);
                    Vector3 radial = new Vector3(cos * pitchRadius, 0, sin * pitchRadius);

                    int idxFront = verts.Count;
                    verts.Add(GearVertex(axis, radial, -halfThick));
                    int idxBack = verts.Count;
                    verts.Add(GearVertex(axis, radial, halfThick));

                    if (j > 0)
                    {
                        tris.Add(prevFront);
                        tris.Add(prevBack);
                        tris.Add(idxFront);
                        tris.Add(idxFront);
                        tris.Add(prevBack);
                        tris.Add(idxBack);
                    }

                    prevFront = idxFront;
                    prevBack = idxBack;
                }
            }

            // Inner wall (hole) – full circle
            int holeVertFrontStart = -1;
            int holeVertBackStart = -1;
            if (hasHole)
            {
                holeVertFrontStart = verts.Count;
                for (int i = 0; i < segs; i++)
                {
                    float angleRad = i * 2f * Mathf.PI / segs;
                    Vector3 radial = new Vector3(Mathf.Cos(angleRad) * holeRadius, 0, Mathf.Sin(angleRad) * holeRadius);
                    verts.Add(GearVertex(axis, radial, -halfThick));
                    verts.Add(GearVertex(axis, radial, halfThick));
                }

                for (int i = 0; i < segs; i++)
                {
                    int next = (i + 1) % segs;
                    int fi0 = holeVertFrontStart + i * 2;
                    int fi1 = holeVertFrontStart + next * 2;
                    int bi0 = holeVertFrontStart + i * 2 + 1;
                    int bi1 = holeVertFrontStart + next * 2 + 1;
                    tris.Add(fi0);
                    tris.Add(fi1);
                    tris.Add(bi0);
                    tris.Add(fi1);
                    tris.Add(bi1);
                    tris.Add(bi0);
                }
            }

            // Front cap – full disc/annulus from center/hole to pitchRadius
            // Build continuous outer ring for the caps (full circle, all segments)
            int capOuterFront = verts.Count;
            for (int i = 0; i < segs; i++)
            {
                float angleRad = i * 2f * Mathf.PI / segs;
                Vector3 radial = new Vector3(Mathf.Cos(angleRad) * pitchRadius, 0, Mathf.Sin(angleRad) * pitchRadius);
                verts.Add(GearVertex(axis, radial, -halfThick));
            }
            int capOuterBack = verts.Count;
            for (int i = 0; i < segs; i++)
            {
                float angleRad = i * 2f * Mathf.PI / segs;
                Vector3 radial = new Vector3(Mathf.Cos(angleRad) * pitchRadius, 0, Mathf.Sin(angleRad) * pitchRadius);
                verts.Add(GearVertex(axis, radial, halfThick));
            }

            if (hasHole)
            {
                // Separate inner ring for caps (not shared with inner wall)
                // so normals stay sharp at the hole edge.
                int capHoleFront = verts.Count;
                for (int i = 0; i < segs; i++)
                {
                    float angleRad = i * 2f * Mathf.PI / segs;
                    Vector3 radial = new Vector3(Mathf.Cos(angleRad) * holeRadius, 0, Mathf.Sin(angleRad) * holeRadius);
                    verts.Add(GearVertex(axis, radial, -halfThick));
                }
                int capHoleBack = verts.Count;
                for (int i = 0; i < segs; i++)
                {
                    float angleRad = i * 2f * Mathf.PI / segs;
                    Vector3 radial = new Vector3(Mathf.Cos(angleRad) * holeRadius, 0, Mathf.Sin(angleRad) * holeRadius);
                    verts.Add(GearVertex(axis, radial, halfThick));
                }

                // Front cap: annulus between capHoleFront and capOuterFront
                for (int i = 0; i < segs; i++)
                {
                    int next = (i + 1) % segs;
                    int oi = capOuterFront + i;
                    int on = capOuterFront + next;
                    int ii = capHoleFront + i;
                    int inn = capHoleFront + next;
                    tris.Add(oi);
                    tris.Add(on);
                    tris.Add(ii);
                    tris.Add(on);
                    tris.Add(inn);
                    tris.Add(ii);
                }
                // Back cap: annulus between capHoleBack and capOuterBack (reversed)
                for (int i = 0; i < segs; i++)
                {
                    int next = (i + 1) % segs;
                    int oi = capOuterBack + i;
                    int on = capOuterBack + next;
                    int ii = capHoleBack + i;
                    int inn = capHoleBack + next;
                    tris.Add(oi);
                    tris.Add(ii);
                    tris.Add(on);
                    tris.Add(on);
                    tris.Add(ii);
                    tris.Add(inn);
                }
            }
            else
            {
                // Front cap: triangle fan from center
                int centerFront = verts.Count;
                verts.Add(GearVertex(axis, Vector3.zero, -halfThick));
                for (int i = 0; i < segs; i++)
                {
                    int next = (i + 1) % segs;
                    tris.Add(centerFront);
                    tris.Add(capOuterFront + i);
                    tris.Add(capOuterFront + next);
                }
                // Back cap: triangle fan from center (reversed)
                int centerBack = verts.Count;
                verts.Add(GearVertex(axis, Vector3.zero, halfThick));
                for (int i = 0; i < segs; i++)
                {
                    int next = (i + 1) % segs;
                    tris.Add(centerBack);
                    tris.Add(capOuterBack + next);
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
            float halfTooth = toothWidthAngle * 0.5f * Mathf.Deg2Rad;
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

        private void OnDestroy()
        {
            if (generatedMesh != null && Application.isPlaying)
                Destroy(generatedMesh);
        }

        private void OnValidate()
        {
            toothCount = Mathf.Max(3, toothCount);
            toothWidthAngle = Mathf.Clamp(toothWidthAngle, 1f, 179f);
            thickness = Mathf.Max(0.001f, thickness);
            pitchRadius = Mathf.Max(0.01f, pitchRadius);
            toothHeight = Mathf.Max(0.001f, toothHeight);
            segmentsPerTooth = Mathf.Max(3, segmentsPerTooth);
        }
    }
}

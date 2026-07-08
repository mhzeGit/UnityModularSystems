using System.Collections.Generic;
using UnityEngine;

namespace MHZE.ChainDrive
{
    public enum ChainAxis { X, Y, Z }

    [System.Serializable]
    public struct ChainGearDef
    {
        public Transform transform;
        public float radius;
        public ChainAxis axis;
        public float gearDensity;
        public Transform meshTransform;
        [Range(0f, 1f)] public float sphereRadiusOffset;
        [Range(0f, 1f)] public float meshOffset;

        public ChainGearDef(
            Transform transform = null,
            float radius = 0.5f,
            ChainAxis axis = ChainAxis.Y,
            float gearDensity = 24f,
            Transform meshTransform = null,
            float sphereRadiusOffset = 0.5f,
            float meshOffset = 0f)
        {
            this.transform = transform;
            this.radius = radius;
            this.axis = axis;
            this.gearDensity = gearDensity;
            this.meshTransform = meshTransform;
            this.sphereRadiusOffset = sphereRadiusOffset;
            this.meshOffset = meshOffset;
        }
    }

    [System.Serializable]
    public struct ChainSegment
    {
        public int type;
        public Vector2 start;
        public Vector2 end;
        public Vector2 center;
        public float radius;
        public float startAngle;
        public float endAngle;
        public bool clockwise;
        public float length;
        public float cumulativeLength;
    }

    [System.Serializable]
    internal struct GearEngagement
    {
        public int linkIndex;
        public int toothIndex;
        public ConfigurableJoint joint;
        public bool active;
    }

    [ExecuteAlways]
    [AddComponentMenu("Mechanical/Chain Drive Constraint")]
    public class ChainDriveConstraint : MonoBehaviour
    {
        public ChainGearDef[] gears = new ChainGearDef[0];
        public ChainAxis axis = ChainAxis.Y;

        [Header("Chain Links")]
        public float chainBallRadius = 0.05f;
        public float chainBallMass = 0.5f;
        public int chainLinkCount = 48;

        [Header("Joint Physics")]
        public float jointSpring = 1500f;
        public float jointDamper = 30f;
        public float jointMaxForce = 1000f;

        [Header("Gear Teeth Overlap")]
        public bool createGearJoints = true;
        public float toothHeight = 0.1f;
        public float toothWidth = 0.1f;
        public float overlapSphereRadius = 0.08f;
        public int overlapCheckInterval = 2;

        [Header("Debug")]
        public bool debugDraw;

        private List<GameObject> m_ChainLinks = new List<GameObject>();
        private List<ConfigurableJoint> m_InterLinkJoints = new List<ConfigurableJoint>();
        private List<ChainSegment> m_Segments = new List<ChainSegment>();
        private float m_TotalPathLength;
        private bool m_NeedsRebuild = true;
        private int m_FrameCounter;

        private GearEngagement[] m_GearEngagements;
        private Vector3[] m_PreviousGearPositions;
        private HashSet<int> m_PreviousEngaged = new HashSet<int>();

        private void OnEnable()
        {
            m_NeedsRebuild = true;
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void OnValidate()
        {
            m_NeedsRebuild = true;
        }

        private void Update()
        {
            if (gears != null)
            {
                bool gearMoved = false;
                int n = gears.Length;
                if (m_PreviousGearPositions == null || m_PreviousGearPositions.Length != n)
                    gearMoved = true;
                else
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (gears[i].transform != null &&
                            Vector3.Distance(gears[i].transform.position, m_PreviousGearPositions[i]) > 0.0005f)
                        {
                            gearMoved = true;
                            break;
                        }
                    }
                }
                if (gearMoved) m_NeedsRebuild = true;
            }

            if (m_NeedsRebuild)
                BuildChain();

            if (createGearJoints && Application.isPlaying && m_ChainLinks.Count > 0)
            {
                m_FrameCounter++;
                if (m_FrameCounter >= overlapCheckInterval)
                {
                    m_FrameCounter = 0;
                    CheckGearOverlaps();
                    UpdateLinkColors();
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugDraw) return;
            DrawBeltPath();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        [ContextMenu("Build Chain")]
        public void BuildChain()
        {
            Cleanup();
            if (!ValidateInputs()) return;

            if (gears != null)
            {
                m_PreviousGearPositions = new Vector3[gears.Length];
                for (int i = 0; i < gears.Length; i++)
                {
                    if (gears[i].transform != null)
                        m_PreviousGearPositions[i] = gears[i].transform.position;
                }
            }

            m_GearEngagements = new GearEngagement[gears.Length];

            ComputeSegments();
            if (m_Segments.Count == 0) return;

            Vector3[] pathPoints = ComputeChainLinkPositions();
            SpawnChainLinks(pathPoints);
            ConnectChainLinks();

            m_NeedsRebuild = false;
        }

        public void Cleanup()
        {
            DestroyAllGearChainJoints();

            for (int i = m_InterLinkJoints.Count - 1; i >= 0; i--)
            {
                if (m_InterLinkJoints[i] != null)
                {
                    if (Application.isPlaying)
                        Destroy(m_InterLinkJoints[i]);
                    else
                        DestroyImmediate(m_InterLinkJoints[i]);
                }
            }
            m_InterLinkJoints.Clear();

            for (int i = m_ChainLinks.Count - 1; i >= 0; i--)
            {
                if (m_ChainLinks[i] != null)
                {
                    if (Application.isPlaying)
                        Destroy(m_ChainLinks[i]);
                    else
                        DestroyImmediate(m_ChainLinks[i]);
                }
            }
            m_ChainLinks.Clear();

            m_Segments.Clear();
            m_TotalPathLength = 0f;
            m_PreviousEngaged.Clear();
        }

        public List<GameObject> ChainLinks => m_ChainLinks;

        public void DestroyAllGearChainJoints()
        {
            if (m_GearEngagements != null)
            {
                for (int i = 0; i < m_GearEngagements.Length; i++)
                {
                    if (m_GearEngagements[i].joint != null)
                    {
                        if (Application.isPlaying)
                            Destroy(m_GearEngagements[i].joint);
                        else
                            DestroyImmediate(m_GearEngagements[i].joint);
                    }
                }
            }
        }

        private bool ValidateInputs()
        {
            if (gears == null || gears.Length < 2)
            {
                Debug.LogWarning("ChainDriveConstraint requires at least 2 gears.");
                return false;
            }
            for (int i = 0; i < gears.Length; i++)
            {
                if (gears[i].transform == null)
                {
                    Debug.LogWarning($"ChainDriveConstraint: gear {i} has no transform assigned.");
                    return false;
                }
                if (gears[i].radius <= 0f)
                {
                    Debug.LogWarning($"ChainDriveConstraint: gear {i} radius must be > 0.");
                    return false;
                }
            }
            return true;
        }

        // ── Belt path computation ─────────────────────────────────────────

        private void ComputeSegments()
        {
            m_Segments.Clear();
            m_TotalPathLength = 0f;

            int n = gears.Length;

            Vector2[] centers2D = new Vector2[n];
            float[] radii = new float[n];
            for (int i = 0; i < n; i++)
            {
                centers2D[i] = To2D(gears[i].transform.position);
                radii[i] = gears[i].radius;
            }

            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < n; i++)
                centroid += centers2D[i];
            centroid /= n;

            float signedArea = 0f;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                signedArea += centers2D[i].x * centers2D[j].y - centers2D[j].x * centers2D[i].y;
            }
            signedArea *= 0.5f;
            bool ccw = signedArea > 0f;

            Vector2[] exitPoints = new Vector2[n];
            Vector2[] entryPoints = new Vector2[n];

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                GetOuterTangent(centers2D[i], radii[i], centers2D[j], radii[j], ccw,
                    out Vector2 exit, out Vector2 entry);
                exitPoints[i] = exit;
                entryPoints[j] = entry;
            }

            for (int i = 0; i < n; i++)
            {
                Vector2 entry = entryPoints[i];
                Vector2 exit = exitPoints[i];

                float entryAngle = Mathf.Atan2(entry.y - centers2D[i].y, entry.x - centers2D[i].x);
                float exitAngle = Mathf.Atan2(exit.y - centers2D[i].y, exit.x - centers2D[i].x);

                float outsideAngle = Mathf.Atan2(centers2D[i].y - centroid.y, centers2D[i].x - centroid.x);

                float arcCW = ClockwiseArcLength(entryAngle, exitAngle);
                float arcCCW = (2f * Mathf.PI) - arcCW;

                bool arcIsCW;
                if (AngleInArc(entryAngle, exitAngle, true, outsideAngle))
                    arcIsCW = true;
                else if (AngleInArc(entryAngle, exitAngle, false, outsideAngle))
                    arcIsCW = false;
                else
                    arcIsCW = arcCW >= arcCCW;

                float arcLenRad = arcIsCW ? arcCW : arcCCW;
                float endA = entryAngle + (arcIsCW ? -arcCW : arcCCW);

                m_Segments.Add(new ChainSegment
                {
                    type = 0,
                    center = centers2D[i],
                    radius = radii[i],
                    startAngle = entryAngle,
                    endAngle = endA,
                    clockwise = arcIsCW,
                    start = entry,
                    end = exit,
                    length = arcLenRad * radii[i],
                    cumulativeLength = m_TotalPathLength
                });
                m_TotalPathLength += arcLenRad * radii[i];

                int next = (i + 1) % n;
                float lineLen = Vector2.Distance(exitPoints[i], entryPoints[next]);

                if (lineLen > 0.001f)
                {
                    m_Segments.Add(new ChainSegment
                    {
                        type = 1,
                        start = exitPoints[i],
                        end = entryPoints[next],
                        length = lineLen,
                        cumulativeLength = m_TotalPathLength
                    });
                    m_TotalPathLength += lineLen;
                }
            }
        }

        private Vector3[] ComputeChainLinkPositions()
        {
            if (m_Segments.Count == 0) return System.Array.Empty<Vector3>();

            int count = Mathf.Max(4, chainLinkCount);
            float spacing = m_TotalPathLength / count;
            Vector3[] positions = new Vector3[count];

            int segIndex = 0;

            for (int i = 0; i < count; i++)
            {
                float dist = i * spacing;

                while (segIndex < m_Segments.Count - 1 &&
                       dist >= m_Segments[segIndex].cumulativeLength + m_Segments[segIndex].length)
                {
                    segIndex++;
                }

                if (segIndex >= m_Segments.Count)
                    segIndex = m_Segments.Count - 1;

                ChainSegment seg = m_Segments[segIndex];
                float segT = (dist - seg.cumulativeLength) / Mathf.Max(0.001f, seg.length);
                segT = Mathf.Clamp01(segT);

                Vector2 pos2D;
                if (seg.type == 0)
                {
                    float arcLenRad = seg.clockwise
                        ? ClockwiseArcLength(seg.startAngle, seg.endAngle)
                        : (2f * Mathf.PI - ClockwiseArcLength(seg.startAngle, seg.endAngle));

                    float angle = seg.startAngle + (seg.clockwise ? -1f : 1f) * arcLenRad * segT;
                    angle = NormalizeAngle(angle);
                    pos2D = seg.center + seg.radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
                else
                {
                    pos2D = Vector2.Lerp(seg.start, seg.end, segT);
                }

                positions[i] = To3D(pos2D);
            }

            return positions;
        }

        private void SpawnChainLinks(Vector3[] positions)
        {
            if (positions.Length < 2) return;

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject link = new GameObject($"ChainLink_{i:D4}");
                link.transform.position = positions[i];
                link.transform.SetParent(null);
                link.hideFlags = HideFlags.DontSave;

                Rigidbody rb = link.AddComponent<Rigidbody>();
                rb.mass = chainBallMass;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.solverIterations = 12;
                rb.solverVelocityIterations = 4;

                SphereCollider collider = link.AddComponent<SphereCollider>();
                collider.radius = chainBallRadius;

                ChainLink cl = link.AddComponent<ChainLink>();
                cl.index = i;
                cl.chainConstraint = this;

                m_ChainLinks.Add(link);
            }
        }

        private void ConnectChainLinks()
        {
            int count = m_ChainLinks.Count;
            if (count < 2) return;

            float spacing = m_TotalPathLength / count;

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;

                Rigidbody rbA = m_ChainLinks[i].GetComponent<Rigidbody>();
                Rigidbody rbB = m_ChainLinks[next].GetComponent<Rigidbody>();
                if (rbA == null || rbB == null) continue;

                ConfigurableJoint joint = m_ChainLinks[i].AddComponent<ConfigurableJoint>();
                joint.connectedBody = rbB;
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector3.zero;
                joint.connectedAnchor = Vector3.zero;

                joint.xMotion = ConfigurableJointMotion.Limited;
                joint.yMotion = ConfigurableJointMotion.Limited;
                joint.zMotion = ConfigurableJointMotion.Limited;
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Free;

                SoftJointLimit linearLimit = joint.linearLimit;
                linearLimit.limit = spacing * 1.05f;
                linearLimit.bounciness = 0f;
                joint.linearLimit = linearLimit;

                SoftJointLimitSpring limitSpring = joint.linearLimitSpring;
                limitSpring.spring = jointSpring;
                limitSpring.damper = jointDamper;
                joint.linearLimitSpring = limitSpring;

                m_InterLinkJoints.Add(joint);
            }
        }

        // ── Gear tooth overlap detection ──────────────────────────────────

        private Vector3[] GetGearToothPositions(int gearIndex)
        {
            ChainGearDef gear = gears[gearIndex];
            Transform t = gear.transform;
            if (t == null) return System.Array.Empty<Vector3>();

            Vector3 center = t.position;
            Vector3 nml = AxisToVector(gear.axis, t);
            Vector3 tan = Vector3.ProjectOnPlane(t.right, nml).normalized;
            if (tan.sqrMagnitude < 0.001f)
                tan = Vector3.ProjectOnPlane(t.forward, nml).normalized;

            float count = Mathf.Max(1f, Mathf.Round(gear.gearDensity * gear.radius));
            int toothCount = Mathf.RoundToInt(count);
            float angleStep = 360f / toothCount;
            float offsetAngle = (toothWidth / gear.radius) * Mathf.Rad2Deg + gear.meshOffset * angleStep;
            float radialOffset = gear.sphereRadiusOffset * toothHeight;

            Vector3[] positions = new Vector3[toothCount];
            for (int i = 0; i < toothCount; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(i * angleStep + offsetAngle, nml) * tan;
                positions[i] = center + dir * (gear.radius + radialOffset);
            }
            return positions;
        }

        private void CheckGearOverlaps()
        {
            for (int g = 0; g < gears.Length; g++)
            {
                Rigidbody gearRB = gears[g].transform.GetComponent<Rigidbody>();
                if (gearRB == null) continue;

                Vector3[] toothPositions = GetGearToothPositions(g);
                if (toothPositions.Length == 0) continue;

                int bestLink = -1;
                int bestTooth = -1;
                float bestDistSq = float.MaxValue;

                for (int t = 0; t < toothPositions.Length; t++)
                {
                    Vector3 toothPos = toothPositions[t];
                    for (int l = 0; l < m_ChainLinks.Count; l++)
                    {
                        if (m_ChainLinks[l] == null) continue;
                        float dSq = (m_ChainLinks[l].transform.position - toothPos).sqrMagnitude;
                        if (dSq < bestDistSq)
                        {
                            bestDistSq = dSq;
                            bestLink = l;
                            bestTooth = t;
                        }
                    }
                }

                float minDistSq = (overlapSphereRadius + overlapSphereRadius)
                                * (overlapSphereRadius + overlapSphereRadius);

                bool shouldEngage = bestLink >= 0 && bestDistSq < minDistSq;

                ref GearEngagement eng = ref m_GearEngagements[g];

                if (shouldEngage)
                {
                    if (eng.active && eng.linkIndex == bestLink && eng.toothIndex == bestTooth)
                    {
                        UpdateGearChainJoint(eng.joint, g, bestLink, bestTooth);
                    }
                    else
                    {
                        if (eng.active)
                        {
                            if (Application.isPlaying) Destroy(eng.joint);
                        }

                        ConfigurableJoint joint = CreateGearChainJoint(g, bestTooth, bestLink);
                        eng = new GearEngagement
                        {
                            linkIndex = bestLink,
                            toothIndex = bestTooth,
                            joint = joint,
                            active = true
                        };
                    }
                }
                else
                {
                    if (eng.active)
                    {
                        if (Application.isPlaying) Destroy(eng.joint);
                        eng = default;
                    }
                }
            }
        }

        private void UpdateLinkColors()
        {
            var cur = new HashSet<int>();
            if (m_GearEngagements != null)
            {
                for (int g = 0; g < m_GearEngagements.Length; g++)
                {
                    if (m_GearEngagements[g].active)
                        cur.Add(m_GearEngagements[g].linkIndex);
                }
            }
            m_PreviousEngaged = cur;
        }

        private ConfigurableJoint CreateGearChainJoint(int gearIdx, int toothIdx, int linkIdx)
        {
            GameObject linkGO = m_ChainLinks[linkIdx];
            Rigidbody linkRB = linkGO.GetComponent<Rigidbody>();
            Rigidbody gearRB = gears[gearIdx].transform.GetComponent<Rigidbody>();
            if (linkRB == null || gearRB == null) return null;

            Vector3[] teeth = GetGearToothPositions(gearIdx);
            Vector3 toothPos = teeth[toothIdx];
            Vector3 linkPos = linkGO.transform.position;
            Vector3 overlapPoint = (linkPos + toothPos) * 0.5f;

            ConfigurableJoint joint = linkGO.AddComponent<ConfigurableJoint>();
            joint.connectedBody = gearRB;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = linkRB.transform.InverseTransformPoint(overlapPoint);
            joint.connectedAnchor = gearRB.transform.InverseTransformPoint(overlapPoint);

            Vector3 axis = (toothPos - gears[gearIdx].transform.position).normalized;
            joint.axis = linkRB.transform.InverseTransformDirection(axis);
            joint.secondaryAxis = Vector3.zero;

            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

            joint.xDrive = new JointDrive
            {
                positionSpring = jointSpring,
                positionDamper = jointDamper,
                maximumForce = jointMaxForce
            };
            joint.targetPosition = Vector3.zero;

            return joint;
        }

        private void UpdateGearChainJoint(ConfigurableJoint joint, int gearIdx, int linkIdx, int toothIdx)
        {
            if (joint == null) return;

            Rigidbody linkRB = joint.GetComponent<Rigidbody>();
            Rigidbody gearRB = joint.connectedBody;
            if (linkRB == null || gearRB == null) return;

            Vector3[] teeth = GetGearToothPositions(gearIdx);
            Vector3 toothPos = teeth[toothIdx];
            Vector3 linkPos = m_ChainLinks[linkIdx].transform.position;
            Vector3 overlapPoint = (linkPos + toothPos) * 0.5f;

            joint.anchor = linkRB.transform.InverseTransformPoint(overlapPoint);
            joint.connectedAnchor = gearRB.transform.InverseTransformPoint(overlapPoint);

            Vector3 axis = (toothPos - gears[gearIdx].transform.position).normalized;
            joint.axis = linkRB.transform.InverseTransformDirection(axis);
        }

        // ── 2D belt math helpers ──────────────────────────────────────────

        private static void GetOuterTangent(
            Vector2 c1, float r1,
            Vector2 c2, float r2,
            bool ccwPolygon,
            out Vector2 exit1, out Vector2 entry2)
        {
            Vector2 diff = c2 - c1;
            float len = diff.magnitude;
            if (len < 0.0001f)
            {
                exit1 = c1 + new Vector2(0, r1);
                entry2 = c2 + new Vector2(0, r2);
                return;
            }

            Vector2 dir = diff / len;
            float alpha = Mathf.Atan2(dir.y, dir.x);
            float psi = (r2 - r1) / len;
            psi = Mathf.Clamp(psi, -1f, 1f);
            float theta = Mathf.Asin(psi);

            float normalAngle;
            if (ccwPolygon)
                normalAngle = alpha - Mathf.PI * 0.5f - theta;
            else
                normalAngle = alpha + Mathf.PI * 0.5f + theta;

            exit1 = c1 + r1 * new Vector2(Mathf.Cos(normalAngle), Mathf.Sin(normalAngle));
            entry2 = c2 + r2 * new Vector2(Mathf.Cos(normalAngle), Mathf.Sin(normalAngle));
        }

        private static float ClockwiseArcLength(float from, float to)
        {
            float angle = from - to;
            if (angle < 0f) angle += 2f * Mathf.PI;
            return angle;
        }

        private static float NormalizeAngle(float a)
        {
            while (a < 0f) a += 2f * Mathf.PI;
            while (a >= 2f * Mathf.PI) a -= 2f * Mathf.PI;
            return a;
        }

        private static bool AngleInArc(float arcStart, float arcEnd, bool clockwise, float testAngle)
        {
            if (clockwise)
            {
                if (arcStart >= arcEnd)
                    return testAngle <= arcStart && testAngle >= arcEnd;
                else
                    return testAngle <= arcStart || testAngle >= arcEnd;
            }
            else
            {
                if (arcStart <= arcEnd)
                    return testAngle >= arcStart && testAngle <= arcEnd;
                else
                    return testAngle >= arcStart || testAngle <= arcEnd;
            }
        }

        // ── 3D ↔ 2D coordinate transforms ─────────────────────────────────

        private void GetPlaneBasis(out Vector3 tan1, out Vector3 tan2)
        {
            switch (axis)
            {
                case ChainAxis.X:
                    tan1 = transform.up; tan2 = transform.forward;
                    break;
                case ChainAxis.Y:
                    tan1 = transform.right; tan2 = transform.forward;
                    break;
                case ChainAxis.Z:
                    tan1 = transform.right; tan2 = transform.up;
                    break;
                default:
                    tan1 = transform.right; tan2 = transform.forward;
                    break;
            }
        }

        private Vector2 To2D(Vector3 worldPos)
        {
            GetPlaneBasis(out Vector3 tan1, out Vector3 tan2);
            Vector3 offset = worldPos - transform.position;
            return new Vector2(Vector3.Dot(offset, tan1), Vector3.Dot(offset, tan2));
        }

        private Vector3 To3D(Vector2 pos2D)
        {
            GetPlaneBasis(out Vector3 tan1, out Vector3 tan2);
            return transform.position + pos2D.x * tan1 + pos2D.y * tan2;
        }

        private static Vector3 AxisToVector(ChainAxis axis, Transform t)
        {
            switch (axis)
            {
                case ChainAxis.X: return t.right;
                case ChainAxis.Y: return t.up;
                case ChainAxis.Z: return t.forward;
                default: return t.up;
            }
        }

        // ── Debug drawing ──────────────────────────────────────────────────

        private void DrawBeltPath()
        {
            if (!debugDraw || gears == null || gears.Length < 2) return;

            ComputeSegments();
            if (m_Segments.Count == 0) return;

            Gizmos.color = Color.yellow;

            foreach (var seg in m_Segments)
            {
                if (seg.type == 0)
                {
                    float arcLenRad = seg.clockwise
                        ? ClockwiseArcLength(seg.startAngle, seg.endAngle)
                        : (2f * Mathf.PI - ClockwiseArcLength(seg.startAngle, seg.endAngle));

                    int arcSteps = Mathf.Max(8, Mathf.RoundToInt(seg.radius * 20f));
                    Vector3 prev = To3D(seg.start);

                    for (int i = 1; i <= arcSteps; i++)
                    {
                        float t = (float)i / arcSteps;
                        float angle = seg.startAngle + (seg.clockwise ? -1f : 1f) * arcLenRad * t;
                        angle = NormalizeAngle(angle);

                        Vector2 p2D = seg.center + seg.radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        Vector3 p3D = To3D(p2D);
                        Gizmos.DrawLine(prev, p3D);
                        prev = p3D;
                    }
                }
                else
                {
                    Vector3 start = To3D(seg.start);
                    Vector3 end = To3D(seg.end);
                    Gizmos.DrawLine(start, end);
                }
            }

            foreach (var gear in gears)
            {
                if (gear.transform == null) continue;
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(gear.transform.position, gear.radius);
            }

            for (int g = 0; g < gears.Length; g++)
            {
                Vector3[] teeth = GetGearToothPositions(g);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
                foreach (Vector3 pos in teeth)
                    Gizmos.DrawWireSphere(pos, overlapSphereRadius);
            }

            if (m_ChainLinks != null)
            {
                for (int i = 0; i < m_ChainLinks.Count; i++)
                {
                    if (m_ChainLinks[i] == null) continue;
                    Gizmos.color = m_PreviousEngaged.Contains(i)
                        ? new Color(0.15f, 0.85f, 0.15f, 0.7f)
                        : new Color(0.3f, 0.8f, 1f, 0.4f);
                    Gizmos.DrawWireSphere(m_ChainLinks[i].transform.position, chainBallRadius);
                }
            }
        }
    }
}

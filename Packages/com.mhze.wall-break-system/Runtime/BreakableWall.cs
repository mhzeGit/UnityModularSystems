using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace MHZE.WallBreak
{
    /// <summary>
    /// Put this on any wall (a mesh with a renderer and a collider) to give it a crack that grows with every strike
    /// and breaks into a walkable, jagged hole on the last one. It never needs the wall's texture or a special mesh:
    /// at the moment of breaking it slices the wall's OWN mesh into chunks (keeping the material, UVs and baked
    /// lightmap UVs), flings the chunks that fill the hole, and leaves the rest of the wall as one mesh with a rim.
    /// The crack itself is a separate transparent layer, so it looks right on every texture.
    /// Nothing here knows about tools or input: call <see cref="Strike"/> from anything (the Use System target in
    /// <c>Integrations/Use</c> does it for a held axe), and listen to <see cref="Struck"/> / <see cref="Broken"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MHZE/Wall Break/Breakable Wall")]
    public sealed class BreakableWall : MonoBehaviour
    {
        [Serializable] public sealed class StruckEvent : UnityEvent<int, int> { }

        [Header("Wall")]
        [Tooltip("The wall's mesh. Found on this object or a child when empty. Its mesh needs Read/Write enabled (see the inspector button).")]
        [SerializeField] private MeshFilter wallMesh;

        [Tooltip("Colliders that stop being solid when the wall breaks. Found under this object when empty.")]
        [SerializeField] private Collider[] wallColliders;

        [Header("Striking")]
        [Min(1)] [SerializeField] private int hitsToBreak = 6;

        [Tooltip("When on, only strikes near the crack count; strikes elsewhere on the wall just thud (event Missed).")]
        [SerializeField] private bool requireWeakSpot;
        [Min(0.05f)] [SerializeField] private float weakSpotRadius = 0.9f;

        [Header("Hole")]
        [Tooltip("Where the hole is centred (and where the crack starts). Move this marker to place it. Empty = the middle of the wall.")]
        [SerializeField] private Transform holeCenter;
        [Tooltip("Width and height of the hole in metres (roughly: its edge is jagged).")]
        [SerializeField] private Vector2 holeSize = new Vector2(1.3f, 2.0f);
        [Tooltip("Slide the hole down so it opens at the floor and can be walked through.")]
        [SerializeField] private bool holeStartsAtFloor = true;
        [Tooltip("Typical size in metres of the pieces the wall breaks into (smaller near the middle, larger further out).")]
        [Range(0.08f, 0.8f)] [SerializeField] private float chunkSize = 0.2f;
        [Tooltip("How ragged the edge of the hole is: 0 = a clean rounded rectangle.")]
        [Range(0f, 1f)] [SerializeField] private float edgeRoughness = 0.6f;
        [Tooltip("0 = a different pattern per wall; any other number gives the same crack and hole every time.")]
        [SerializeField] private int seed;
        [Tooltip("Optional material for the faces cut open inside the wall (plaster, brick core). Empty = continue the wall's own material.")]
        [SerializeField] private Material insideMaterial;

        [Header("Crack")]
        [SerializeField] private Color crackColor = new Color(0.03f, 0.02f, 0.02f, 1f);
        [Min(0.002f)] [SerializeField] private float crackWidth = 0.04f;
        [Tooltip("Show the crack on both faces of the wall.")]
        [SerializeField] private bool crackBothSides = true;
        [Range(0f, 1f)] [SerializeField] private float startingCrack = 0.22f;

        [Header("Debris")]
        [Tooltip("How hard the pieces are pushed away from the player when the wall gives way (m/s). They mostly just fall.")]
        [Min(0f)] [SerializeField] private float debrisSpeed = 2.4f;
        [Tooltip("Seconds the rubble stays before it shrinks away.")]
        [Min(2f)] [SerializeField] private float debrisLifetime = 45f;
        [Tooltip("Layer of the falling pieces. -1 = the wall's own layer.")]
        [SerializeField] private int debrisLayer = -1;
        [Tooltip("Off: once a piece has settled it stops blocking the player, so rubble in the doorway can be walked through.")]
        [SerializeField] private bool solidRubble;
        [Tooltip("Loose chips that pop off the surface on every blow.")]
        [Range(0, 12)] [SerializeField] private int chipsPerHit = 4;
        [Tooltip("How much of the hole's pieces come loose before the last blow (0 = none).")]
        [Range(0f, 0.6f)] [SerializeField] private float loosenBeforeBreak = 0.3f;
        [SerializeField] private bool dustOnHit = true;

        [Header("Audio")]
        [SerializeField] private AudioClip[] hitClips;
        [SerializeField] private AudioClip[] breakClips;
        [Tooltip("Played when a falling piece lands. Empty = uses the hit clips.")]
        [SerializeField] private AudioClip[] impactClips;
        [Range(0f, 1f)] [SerializeField] private float volume = 1f;

        [Header("For other systems")]
        [Tooltip("How loud a strike is (for noise/hearing logic in the game). Not used here.")]
        [Min(0f)] [SerializeField] private float hitLoudness = 1f;
        [Min(0f)] [SerializeField] private float breakLoudness = 2f;

        [Header("Events")]
        [Tooltip("Hits taken, hits needed.")]
        public StruckEvent OnStruck = new StruckEvent();
        public UnityEvent OnMissed = new UnityEvent();
        public UnityEvent OnBroken = new UnityEvent();

        /// <summary>(hits taken, hits needed, world point). Raised before OnStruck listeners.</summary>
        public event Action<int, int, Vector3> Struck;
        public event Action<Vector3> Broken;

        public int HitsTaken { get; private set; }
        public int HitsToBreak => hitsToBreak;
        public bool IsBroken { get; private set; }
        public float HitLoudness => hitLoudness;
        public float BreakLoudness => breakLoudness;
        public Transform MeshTransform => wallMesh != null ? wallMesh.transform : transform;

        // Frame of the wall: which mesh axes are width (u), height (v) and thickness (t).
        struct Frame
        {
            public int u, v, t;
            public Vector3 min, max, scale;
            public bool floorIsMax;
        }

        MeshRenderer wallRenderer;
        WallMeshData meshData;
        Frame frame;
        bool frameReady;
        bool warnedUnreadable;

        WallFracture fracture;
        WallCrackMesh crack;
        int[] holeOrder = new int[0];
        Vector3 centreMesh;
        Material[] pieceMats;
        int pieceSubs, capSub;
        Vector3 uvPerAxis;

        GameObject crackObject;
        Material crackMaterial;
        float crackProgressShown;
        float crackProgressTarget;
        GameObject remainder;
        MeshFilter remainderFilter;
        MeshCollider remainderCollider;
        readonly List<GameObject> debris = new List<GameObject>();
        bool[] colliderWasEnabled;
        bool fractured;
        int looseReleased;
        Coroutine collapse;
        float lastPuffTime, lastThudTime;

        static Texture2D crackMask, puffMask;

        /// <summary>Whether settled pieces keep blocking the player (see Solid Rubble).</summary>
        internal bool SolidRubble => solidRubble;
        internal float DebrisLifetime => debrisLifetime;

        void Reset() { CacheComponents(); }

        void Awake()
        {
            CacheComponents();
            SetCrackProgress(0, true);
            BuildFracture();
        }

        void Update()
        {
            if (crackObject != null && !Mathf.Approximately(crackProgressShown, crackProgressTarget))
            {
                crackProgressShown = Mathf.MoveTowards(crackProgressShown, crackProgressTarget, Time.deltaTime * 2.5f);
                crackMaterial.SetFloat("_Progress", crackProgressShown);
            }
        }

        void OnDestroy()
        {
            if (crackMaterial != null) Destroy(crackMaterial);
            for (int i = 0; i < debris.Count; i++) if (debris[i] != null) Destroy(debris[i]);
        }

        void CacheComponents()
        {
            if (wallMesh == null) wallMesh = GetComponentInChildren<MeshFilter>();
            if (wallMesh != null && wallRenderer == null) wallRenderer = wallMesh.GetComponent<MeshRenderer>();
            if (wallColliders == null || wallColliders.Length == 0) wallColliders = GetComponentsInChildren<Collider>();
        }

        // ---------------------------------------------------------------- public API

        /// <summary>
        /// One blow. 'point' and 'normal' are in world space (a RaycastHit's point and normal). Returns true when
        /// it counted. A blow outside the weak spot (when required) only thuds.
        /// </summary>
        public bool Strike(Vector3 point, Vector3 normal, float power = 1f, GameObject by = null)
        {
            if (IsBroken) return false;
            CacheComponents();
            if (!BuildFracture()) { BreakNow(point, normal); return true; }

            if (requireWeakSpot && DistanceToCrack(point) > weakSpotRadius)
            {
                PlayClips(hitClips, point, 0.5f);
                SpawnChips(point, normal, Mathf.Max(1, chipsPerHit / 2));
                OnMissed.Invoke();
                return false;
            }

            HitsTaken += Mathf.Max(1, Mathf.RoundToInt(power));
            float progress = Mathf.Clamp01((float)HitsTaken / hitsToBreak);
            SetCrackProgress(Mathf.Lerp(startingCrack, 1f, progress), false);
            PlayClips(hitClips, point, 1f);
            SpawnChips(point, normal, chipsPerHit);
            if (dustOnHit) SpawnDust(point, normal, 8, 0.18f);

            Struck?.Invoke(HitsTaken, hitsToBreak, point);
            OnStruck.Invoke(HitsTaken, hitsToBreak);

            if (HitsTaken >= hitsToBreak) BreakNow(point, normal);
            else Loosen(point, normal, progress);
            return true;
        }

        /// <summary>Breaks the wall now: the pieces of the hole give way in a wave from the point, and fall.</summary>
        public void BreakNow(Vector3 point, Vector3 normal)
        {
            if (IsBroken) return;
            CacheComponents();
            if (!BuildFracture())
            {
                // Mesh not readable: the best we can do is make the wall vanish.
                IsBroken = true;
                if (wallRenderer != null) wallRenderer.enabled = false;
                SetCollidersEnabled(false);
                if (crackObject != null) crackObject.SetActive(false);
                Broken?.Invoke(point);
                OnBroken.Invoke();
                return;
            }
            IsBroken = true;
            HitsTaken = Mathf.Max(HitsTaken, hitsToBreak);
            EnterFractured();
            PlayClips(breakClips, point, 1f);
            if (dustOnHit) SpawnDust(point, normal, 30, 0.45f);
            if (collapse != null) StopCoroutine(collapse);
            collapse = StartCoroutine(Collapse(point, normal));
            Broken?.Invoke(point);
            OnBroken.Invoke();
        }

        /// <summary>Puts the wall straight into its broken state with no debris (for skipping a chapter).</summary>
        public void SetBroken()
        {
            if (IsBroken) return;
            CacheComponents();
            if (!BuildFracture()) { IsBroken = true; if (wallRenderer) wallRenderer.enabled = false; SetCollidersEnabled(false); return; }
            IsBroken = true;
            HitsTaken = hitsToBreak;
            EnterFractured();
            for (int k = 0; k < holeOrder.Length; k++) fracture.cells[holeOrder[k]].released = true;
            RebuildRemainder();
            RememberAndDisableColliders();
            remainderCollider.enabled = true;
            remainderCollider.sharedMesh = null;
            remainderCollider.sharedMesh = remainderFilter.sharedMesh;
        }

        /// <summary>Puts the wall back as it was, intact, with its crack at the starting size (for rewinding).</summary>
        public void ResetWall()
        {
            CacheComponents();
            if (collapse != null) { StopCoroutine(collapse); collapse = null; }
            for (int i = 0; i < debris.Count; i++) if (debris[i] != null) Destroy(debris[i]);
            debris.Clear();
            if (remainder != null) { Destroy(remainder); remainder = null; remainderFilter = null; remainderCollider = null; }
            if (wallRenderer != null) wallRenderer.enabled = true;
            if (colliderWasEnabled != null) RestoreColliders();
            if (fracture != null) for (int i = 0; i < fracture.cells.Length; i++) fracture.cells[i].released = false;
            if (crack != null) crack.ShowAll();
            fractured = false;
            looseReleased = 0;
            IsBroken = false;
            HitsTaken = 0;
            if (crackObject != null) crackObject.SetActive(true);
            SetCrackProgress(0f, true);
        }

        /// <summary>Distance in metres from a world point to the crack's centre, measured along the wall.</summary>
        public float DistanceToCrack(Vector3 worldPoint)
        {
            if (!EnsureFrame()) return 0f;
            Vector3 p = MeshTransform.InverseTransformPoint(worldPoint);
            Vector2 c = HoleCenterLocal(out _, out _);
            float du = (WbSlicer.C(p, frame.u) - c.x) * frame.scale[frame.u];
            float dv = (WbSlicer.C(p, frame.v) - c.y) * frame.scale[frame.v];
            return Mathf.Sqrt(du * du + dv * dv);
        }

        /// <summary>World position of the crack's centre on the wall's middle plane.</summary>
        public Vector3 CrackCenterWorld()
        {
            if (!EnsureFrame()) return transform.position;
            Vector2 c = HoleCenterLocal(out _, out _);
            float tMid = (WbSlicer.C(frame.min, frame.t) + WbSlicer.C(frame.max, frame.t)) * 0.5f;
            return MeshTransform.TransformPoint(WbSlicer.V(frame.u, c.x, frame.v, c.y, frame.t, tMid));
        }

        // ---------------------------------------------------------------- frame and hole geometry

        bool EnsureFrame()
        {
            if (frameReady) return true;
            if (wallMesh == null || wallMesh.sharedMesh == null) return false;
            Vector3 meshScale = wallMesh.transform.lossyScale;
            meshScale = new Vector3(Mathf.Max(1e-4f, Mathf.Abs(meshScale.x)), Mathf.Max(1e-4f, Mathf.Abs(meshScale.y)), Mathf.Max(1e-4f, Mathf.Abs(meshScale.z)));
            if (meshData == null)
            {
                meshData = WallMeshData.FromMesh(wallMesh.sharedMesh, meshScale);
                if (meshData == null)
                {
                    if (!warnedUnreadable)
                    {
                        warnedUnreadable = true;
                        Debug.LogError("[BreakableWall] The mesh '" + wallMesh.sharedMesh.name + "' on '" + name +
                                       "' is not readable. Enable Read/Write on its import settings (the inspector has a button). The wall will vanish instead of breaking.", this);
                    }
                    return false;
                }
            }

            Bounds b = meshData.Bounds;
            Transform mt = wallMesh.transform;
            Vector3 ls = mt.lossyScale;
            frame.scale = new Vector3(Mathf.Max(1e-4f, Mathf.Abs(ls.x)), Mathf.Max(1e-4f, Mathf.Abs(ls.y)), Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
            frame.min = b.min; frame.max = b.max;

            // Thickness = the axis along which the wall is thinnest in the world.
            Vector3 world = new Vector3(b.size.x * frame.scale.x, b.size.y * frame.scale.y, b.size.z * frame.scale.z);
            int t = 0;
            if (world.y < world[t]) t = 1;
            if (world.z < world[t]) t = 2;
            frame.t = t;
            // Height = of the other two, the one that points most up in the world.
            int a = (t + 1) % 3, c = (t + 2) % 3;
            float upA = Mathf.Abs(Vector3.Dot(mt.TransformDirection(AxisVector(a)), Vector3.up));
            float upC = Mathf.Abs(Vector3.Dot(mt.TransformDirection(AxisVector(c)), Vector3.up));
            frame.v = upA >= upC ? a : c;
            frame.u = upA >= upC ? c : a;
            frame.floorIsMax = Vector3.Dot(mt.TransformDirection(AxisVector(frame.v)), Vector3.up) < 0f;
            frameReady = true;
            return true;
        }

        static Vector3 AxisVector(int axis) { return axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward; }

        /// <summary>The hole centre in mesh space (along u, v), after fitting the hole into the wall. Also returns its half sizes in mesh units.</summary>
        Vector2 HoleCenterLocal(out float halfU, out float halfV)
        {
            float minU = WbSlicer.C(frame.min, frame.u), maxU = WbSlicer.C(frame.max, frame.u);
            float minV = WbSlicer.C(frame.min, frame.v), maxV = WbSlicer.C(frame.max, frame.v);
            float margin = chunkSize * 0.6f;
            halfU = Mathf.Min(holeSize.x * 0.5f / frame.scale[frame.u], (maxU - minU) * 0.5f - margin / frame.scale[frame.u]);
            halfV = Mathf.Min(holeSize.y * 0.5f / frame.scale[frame.v], (maxV - minV) * 0.5f - margin / frame.scale[frame.v]);
            halfU = Mathf.Max(halfU, 0.02f);
            halfV = Mathf.Max(halfV, 0.02f);

            float cu = (minU + maxU) * 0.5f, cv = (minV + maxV) * 0.5f;
            if (holeCenter != null)
            {
                Vector3 p = MeshTransform.InverseTransformPoint(holeCenter.position);
                cu = WbSlicer.C(p, frame.u);
                cv = WbSlicer.C(p, frame.v);
            }
            if (holeStartsAtFloor)
            {
                cv = frame.floorIsMax ? maxV - halfV : minV + halfV;
                // Keep the opening at the floor but never let it leave the wall above.
            }
            cu = Mathf.Clamp(cu, minU + halfU + margin / frame.scale[frame.u] * 0.5f, maxU - halfU - margin / frame.scale[frame.u] * 0.5f);
            cv = Mathf.Clamp(cv, minV + halfV, maxV - halfV);
            return new Vector2(cu, cv);
        }

        // ---------------------------------------------------------------- the shatter pattern and the crack

        /// <summary>Works out, once, how the wall shatters (irregular pieces along the lines where the crack shows) and draws the crack.</summary>
        bool BuildFracture()
        {
            if (fracture != null) return true;
            CacheComponents();
            if (!EnsureFrame()) return false;

            int u = frame.u, v = frame.v, t = frame.t;
            Vector2 centre = HoleCenterLocal(out float halfU, out float halfV);
            pieceSubs = meshData.SubmeshCount + (insideMaterial != null ? 1 : 0);
            capSub = insideMaterial != null ? meshData.SubmeshCount : meshData.LargestSubmesh;

            var s = new FractureSettings
            {
                u = u, v = v, t = t,
                scale = frame.scale, min = frame.min, max = frame.max,
                centre = centre, halfU = halfU, halfV = halfV,
                chunkSize = chunkSize, roughness = edgeRoughness,
                seed = EffectiveSeed(),
                insideSubmesh = capSub, totalSubmeshes = pieceSubs,
                holeAtFloor = holeStartsAtFloor, floorIsMax = frame.floorIsMax
            };
            fracture = WallFracture.Build(meshData, s);

            float tMid = (WbSlicer.C(frame.min, t) + WbSlicer.C(frame.max, t)) * 0.5f;
            centreMesh = WbSlicer.V(u, centre.x, v, centre.y, t, tMid);

            // The pieces that will fall, nearest to the crack first.
            Vector3 centreM = Vector3.Scale(centreMesh, frame.scale);
            var list = new List<int>();
            for (int i = 0; i < fracture.cells.Length; i++)
            {
                FractureCell c = fracture.cells[i];
                if (c != null && c.inHole && !c.surface.IsEmpty) list.Add(i);
            }
            list.Sort((a, b) => (fracture.cells[a].site - centreM).sqrMagnitude.CompareTo((fracture.cells[b].site - centreM).sqrMagnitude));
            holeOrder = list.ToArray();

            Material[] src = wallRenderer != null ? wallRenderer.sharedMaterials : new Material[0];
            pieceMats = new Material[pieceSubs];
            for (int i = 0; i < pieceSubs; i++)
                pieceMats[i] = i < src.Length ? src[i] : (insideMaterial != null && i == pieceSubs - 1 ? insideMaterial : (src.Length > 0 ? src[0] : null));

            BuildCrackVisual(halfU, halfV);
            return true;
        }

        void BuildCrackVisual(float halfU, float halfV)
        {
            if (crackObject != null) return;
            float radius = Mathf.Max(halfU * frame.scale[frame.u], halfV * frame.scale[frame.v]) * 1.25f;
            float off = 0.004f / frame.scale[frame.t];
            crack = WallCrackMesh.Build(fracture.edges, centreMesh, radius, crackWidth, frame.scale, frame.t, off, crackColor, crackBothSides, EffectiveSeed() + 17);

            crackObject = new GameObject("WallCrack");
            crackObject.transform.SetParent(MeshTransform, false);
            crackObject.layer = gameObject.layer;
            crackObject.AddComponent<MeshFilter>().sharedMesh = crack.mesh;
            var mr = crackObject.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            crackMaterial = MakeSoftMaterial(GetCrackMask(), Color.white);
            mr.sharedMaterial = crackMaterial;
            crackMaterial.SetFloat("_Progress", crackProgressShown);
        }

        void SetCrackProgress(float progress, bool instant)
        {
            crackProgressTarget = progress < 0.0001f ? startingCrack : progress;
            if (instant) crackProgressShown = crackProgressTarget;
            if (crackMaterial != null && instant) crackMaterial.SetFloat("_Progress", crackProgressShown);
        }

        // ---------------------------------------------------------------- the break

        /// <summary>The first piece to give way swaps the wall for its pieces: the remainder is every piece still in the wall.</summary>
        void EnterFractured()
        {
            if (fractured) return;
            fractured = true;
            if (wallRenderer != null) wallRenderer.enabled = false;

            Transform mt = MeshTransform;
            remainder = new GameObject(name + "_Remainder");
            remainder.transform.SetParent(mt, false);
            remainder.layer = mt.gameObject.layer;
            remainderFilter = remainder.AddComponent<MeshFilter>();
            var rr = remainder.AddComponent<MeshRenderer>();
            rr.sharedMaterials = pieceMats;
            CopyRenderSettings(wallRenderer, rr);
            remainderCollider = remainder.AddComponent<MeshCollider>();
            remainderCollider.enabled = false;
            RebuildRemainder();
        }

        /// <summary>Builds the wall again from every piece that has not fallen, with a rim wherever a neighbour has gone.</summary>
        void RebuildRemainder()
        {
            var keep = new WbMeshBuilder(pieceSubs) { lightmapUv = meshData.HasLightmapUv };
            FractureCell[] cells = fracture.cells;
            for (int i = 0; i < cells.Length; i++)
            {
                FractureCell c = cells[i];
                if (c == null || c.released || c.surface.IsEmpty) continue;
                keep.Append(c.surface);
                for (int k = 0; k < c.caps.Count; k++)
                {
                    FractureCap cap = c.caps[k];
                    if (cells[cap.neighbor].released) keep.AddPolygon(capSub, cap.poly);
                }
            }
            Mesh old = remainderFilter.sharedMesh;
            Mesh mesh = keep.ToMesh("WallRemainder");
            remainderFilter.sharedMesh = mesh;
            if (remainderCollider.enabled)
            {
                remainderCollider.sharedMesh = null;
                remainderCollider.sharedMesh = mesh;
            }
            else remainderCollider.sharedMesh = mesh;
            if (old != null) Destroy(old);
            if (crack != null) crack.HideAround(cells);
        }

        /// <summary>Between blows a few pieces near the blow work loose and drop out of the wall.</summary>
        void Loosen(Vector3 point, Vector3 normal, float progress)
        {
            int target = Mathf.RoundToInt(holeOrder.Length * loosenBeforeBreak * Mathf.Pow(progress, 1.4f));
            int need = target - looseReleased;
            if (need <= 0) return;
            EnterFractured();

            Vector3 pm = Vector3.Scale(MeshTransform.InverseTransformPoint(point), frame.scale);
            var picks = new List<int>(holeOrder.Length);
            for (int k = 0; k < holeOrder.Length; k++) if (!fracture.cells[holeOrder[k]].released) picks.Add(holeOrder[k]);
            picks.Sort((a, b) => (fracture.cells[a].site - pm).sqrMagnitude.CompareTo((fracture.cells[b].site - pm).sqrMagnitude));

            Vector3 away = AwayFrom(normal);
            for (int k = 0; k < need && k < picks.Count; k++)
            {
                ReleaseCell(picks[k], point, away, 0.35f);
                looseReleased++;
            }
            RebuildRemainder();
        }

        /// <summary>The last blow: the rest of the hole's pieces give way in a wave outward from the blow, and fall.</summary>
        IEnumerator Collapse(Vector3 point, Vector3 normal)
        {
            // From now on the wall is its pieces: the original colliders go, the remainder's takes over.
            RememberAndDisableColliders();
            remainderCollider.enabled = true;
            remainderCollider.sharedMesh = null;
            remainderCollider.sharedMesh = remainderFilter.sharedMesh;
            if (crackObject != null) crackObject.SetActive(true);

            Vector3 pm = Vector3.Scale(MeshTransform.InverseTransformPoint(point), frame.scale);
            var order = new List<int>(holeOrder.Length);
            for (int k = 0; k < holeOrder.Length; k++) if (!fracture.cells[holeOrder[k]].released) order.Add(holeOrder[k]);
            order.Sort((a, b) => (fracture.cells[a].site - pm).sqrMagnitude.CompareTo((fracture.cells[b].site - pm).sqrMagnitude));
            float maxD = 0.01f;
            for (int k = 0; k < order.Count; k++) maxD = Mathf.Max(maxD, (fracture.cells[order[k]].site - pm).magnitude);

            Vector3 away = AwayFrom(normal);
            const int waves = 5;
            const float duration = 0.42f;
            int idx = 0;
            for (int w = 0; w < waves; w++)
            {
                float limit = (w + 1f) / waves;
                bool any = false;
                while (idx < order.Count && (fracture.cells[order[idx]].site - pm).magnitude / maxD <= limit + 1e-4f)
                {
                    float d = (fracture.cells[order[idx]].site - pm).magnitude / maxD;
                    ReleaseCell(order[idx], point, away, Mathf.Lerp(1f, 0.55f, d));
                    idx++;
                    any = true;
                }
                if (any) RebuildRemainder();
                yield return new WaitForSeconds(duration / waves);
            }
            collapse = null;
        }

        Vector3 AwayFrom(Vector3 normal)
        {
            return normal.sqrMagnitude > 0.01f ? -normal.normalized : -MeshTransform.TransformDirection(AxisVector(frame.t));
        }

        /// <summary>Turns one piece of the wall into a solid object that falls.</summary>
        void ReleaseCell(int index, Vector3 hitPoint, Vector3 away, float push)
        {
            FractureCell c = fracture.cells[index];
            c.released = true;

            var b = new WbMeshBuilder(pieceSubs) { lightmapUv = meshData.HasLightmapUv };
            b.Append(c.surface);
            for (int k = 0; k < c.caps.Count; k++) b.AddPolygon(capSub, c.caps[k].poly);

            GameObject go = SpawnPiece(b, pieceMats, "WallPiece", false, debrisLifetime, out Rigidbody rb);
            if (go == null) return;
            Vector3 outward = go.transform.position - hitPoint;
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.up;
            float p = debrisSpeed * push;
            SetVelocity(rb, away * (p * UnityEngine.Random.Range(0.4f, 1.1f)) + outward * (p * 0.35f) + UnityEngine.Random.insideUnitSphere * (p * 0.25f));
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * (1.5f + p * 1.5f);
        }

        /// <summary>Makes a free-standing piece from cut geometry: its own mesh, collider and rigidbody, centred so it tumbles about its middle.</summary>
        GameObject SpawnPiece(WbMeshBuilder b, Material[] mats, string pieceName, bool boxCollider, float life, out Rigidbody body)
        {
            body = null;
            if (b == null || b.IsEmpty) return null;
            Transform mt = MeshTransform;
            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            for (int i = 0; i < b.verts.Count; i++) { lo = Vector3.Min(lo, b.verts[i].p); hi = Vector3.Max(hi, b.verts[i].p); }
            Vector3 mid = (lo + hi) * 0.5f;
            for (int i = 0; i < b.verts.Count; i++) { WbVertex vtx = b.verts[i]; vtx.p -= mid; b.verts[i] = vtx; }
            Mesh mesh = b.ToMesh(pieceName);

            var go = new GameObject(pieceName);
            if (gameObject.scene.IsValid() && go.scene != gameObject.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            go.transform.SetPositionAndRotation(mt.TransformPoint(mid), mt.rotation);
            go.transform.localScale = mt.lossyScale;
            go.layer = debrisLayer >= 0 ? debrisLayer : mt.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.On;

            Vector3 sizeM = Vector3.Scale(hi - lo, frame.scale);
            Collider col;
            if (boxCollider || Mathf.Min(sizeM.x, Mathf.Min(sizeM.y, sizeM.z)) < 0.015f)
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = Vector3.Max(hi - lo, Vector3.one * 0.01f);
                col = box;
            }
            else
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.convex = true;
                mc.sharedMesh = mesh;
                col = mc;
            }

            body = go.AddComponent<Rigidbody>();
            body.mass = Mathf.Clamp(Mathf.Abs(sizeM.x * sizeM.y * sizeM.z) * 0.6f * 1800f, 0.2f, 80f);
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // The wall's own colliders are still switched on until the last blow; a piece starts inside them.
            for (int i = 0; i < wallColliders.Length; i++) if (wallColliders[i] != null) Physics.IgnoreCollision(col, wallColliders[i]);

            var d = go.AddComponent<WallDebris>();
            d.owner = this;
            d.lifetime = life;
            debris.Add(go);
            return go;
        }

        /// <summary>A falling piece has landed hard: a puff of dust and a thud, a few at a time so a collapse does not turn to noise.</summary>
        internal void NotePieceImpact(Vector3 point, Vector3 normal, float speed)
        {
            if (Time.time - lastPuffTime > 0.04f)
            {
                lastPuffTime = Time.time;
                if (dustOnHit) SpawnDust(point, normal, Mathf.RoundToInt(Mathf.Lerp(3f, 10f, Mathf.Clamp01(speed / 6f))), 0.15f);
            }
            if (Time.time - lastThudTime > 0.07f)
            {
                lastThudTime = Time.time;
                PlayClips(impactClips != null && impactClips.Length > 0 ? impactClips : hitClips, point, Mathf.Clamp01(speed / 7f) * 0.7f);
            }
        }

        // ---------------------------------------------------------------- hit feedback

        void SpawnChips(Vector3 worldPoint, Vector3 worldNormal, int count)
        {
            if (count <= 0 || meshData == null || !EnsureFrame()) return;
            Transform mt = MeshTransform;
            Vector3 p = mt.InverseTransformPoint(worldPoint);
            int u = frame.u, v = frame.v, t = frame.t;
            float sign = Vector3.Dot(worldNormal, mt.TransformDirection(AxisVector(t))) >= 0f ? 1f : -1f;
            float tFace = sign > 0f ? WbSlicer.C(frame.max, t) : WbSlicer.C(frame.min, t);
            Vector3 uvPerAxis = new Vector3(meshData.UvPerUnit * frame.scale.x, meshData.UvPerUnit * frame.scale.y, meshData.UvPerUnit * frame.scale.z);
            int subs = meshData.SubmeshCount;
            Material[] src = wallRenderer != null ? wallRenderer.sharedMaterials : new Material[0];
            Material[] mats = new Material[subs];
            for (int i = 0; i < subs; i++) mats[i] = i < src.Length ? src[i] : (src.Length > 0 ? src[0] : null);
            Vector3 away = -worldNormal.normalized;

            for (int c = 0; c < count; c++)
            {
                float size = UnityEngine.Random.Range(0.035f, 0.085f);
                float depth = UnityEngine.Random.Range(0.02f, 0.05f);
                float cu = WbSlicer.C(p, u) + UnityEngine.Random.Range(-0.1f, 0.1f) / frame.scale[u];
                float cv = WbSlicer.C(p, v) + UnityEngine.Random.Range(-0.1f, 0.1f) / frame.scale[v];
                float hu = size * 0.5f / frame.scale[u], hv = size * 0.5f / frame.scale[v];
                float tIn = tFace - sign * depth / frame.scale[t];
                float tLo = Mathf.Min(tFace, tIn), tHi = Mathf.Max(tFace, tIn);

                var b = new WbMeshBuilder(subs) { lightmapUv = meshData.HasLightmapUv };
                WbSlicer.ClipBox(meshData, WbSlicer.V(u, cu - hu, v, cv - hv, t, tLo), WbSlicer.V(u, cu + hu, v, cv + hv, t, tHi), b);
                if (b.IsEmpty) continue;
                int capSub = meshData.LargestSubmesh;
                WbSlicer.AddCap(b, capSub, u, -1f, cu - hu, v, cv - hv, cv + hv, t, tLo, tHi, uvPerAxis);
                WbSlicer.AddCap(b, capSub, u, 1f, cu + hu, v, cv - hv, cv + hv, t, tLo, tHi, uvPerAxis);
                WbSlicer.AddCap(b, capSub, v, -1f, cv - hv, u, cu - hu, cu + hu, t, tLo, tHi, uvPerAxis);
                WbSlicer.AddCap(b, capSub, v, 1f, cv + hv, u, cu - hu, cu + hu, t, tLo, tHi, uvPerAxis);
                WbSlicer.AddCap(b, capSub, t, -sign, tIn, u, cu - hu, cu + hu, v, cv - hv, cv + hv, uvPerAxis);

                GameObject chip = SpawnPiece(b, mats, "WallChip", true, 1.4f, out Rigidbody rb);
                if (chip == null) continue;
                SetVelocity(rb, (away * UnityEngine.Random.Range(0.3f, 1.2f)) + (worldNormal.normalized * UnityEngine.Random.Range(0.4f, 1.4f)) + UnityEngine.Random.insideUnitSphere * 0.8f + Vector3.up * 0.8f);
                rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 8f;
                Destroy(chip, 3.5f);
            }
        }

                void SpawnDust(Vector3 point, Vector3 normal, int count, float size)
        {
            var go = new GameObject("WallDust");
            if (gameObject.scene.IsValid() && go.scene != gameObject.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            go.transform.position = point + normal.normalized * 0.05f;
            go.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.01f ? normal : Vector3.up);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.02f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.startColor = DustColor();

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = 0.06f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.12f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.6f));

            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = MakeSoftMaterial(GetPuffMask(), Color.white);
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            ps.Play();
        }

        Color DustColor()
        {
            Color c = new Color(0.72f, 0.68f, 0.62f, 1f);
            if (wallRenderer != null && wallRenderer.sharedMaterial != null)
            {
                Material m = wallRenderer.sharedMaterial;
                if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
                else if (m.HasProperty("_Color")) c = m.GetColor("_Color");
            }
            // Pale and desaturated, like plaster dust.
            c = Color.Lerp(c, new Color(0.78f, 0.75f, 0.7f), 0.6f);
            c.a = 1f;
            return c;
        }

        void PlayClips(AudioClip[] clips, Vector3 point, float gain)
        {
            if (clips == null || clips.Length == 0) return;
            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip != null) AudioSource.PlayClipAtPoint(clip, point, volume * gain);
        }

        // ---------------------------------------------------------------- colliders and render settings

        void RememberAndDisableColliders()
        {
            colliderWasEnabled = new bool[wallColliders.Length];
            for (int i = 0; i < wallColliders.Length; i++)
            {
                if (wallColliders[i] == null) continue;
                colliderWasEnabled[i] = wallColliders[i].enabled;
                wallColliders[i].enabled = false;
            }
        }

        void SetCollidersEnabled(bool on)
        {
            for (int i = 0; i < wallColliders.Length; i++) if (wallColliders[i] != null) wallColliders[i].enabled = on;
        }

        void RestoreColliders()
        {
            for (int i = 0; i < wallColliders.Length && i < colliderWasEnabled.Length; i++)
                if (wallColliders[i] != null) wallColliders[i].enabled = colliderWasEnabled[i];
            colliderWasEnabled = null;
        }

        static void CopyRenderSettings(MeshRenderer from, MeshRenderer to)
        {
            if (from == null) return;
            // Keep the baked lighting: the remainder's lightmap UVs are the wall's own.
            to.lightmapIndex = from.lightmapIndex;
            to.lightmapScaleOffset = from.lightmapScaleOffset;
            to.realtimeLightmapIndex = from.realtimeLightmapIndex;
            to.realtimeLightmapScaleOffset = from.realtimeLightmapScaleOffset;
#if UNITY_EDITOR
            to.receiveGI = from.receiveGI; // editor-only property: it would break every player build
#endif
            to.shadowCastingMode = from.shadowCastingMode;
            to.receiveShadows = from.receiveShadows;
            to.lightProbeUsage = from.lightProbeUsage;
            to.reflectionProbeUsage = from.reflectionProbeUsage;
            to.renderingLayerMask = from.renderingLayerMask;
            to.staticShadowCaster = from.staticShadowCaster;
        }

        // ---------------------------------------------------------------- small helpers

        /// <summary>The seed field, or (when 0) a number made from the wall's name and place, so every wall differs but each is repeatable.</summary>
        int EffectiveSeed()
        {
            if (seed != 0) return seed;
            unchecked
            {
                int h = 17;
                string n = name;
                for (int i = 0; i < n.Length; i++) h = h * 31 + n[i];
                Vector3 p = transform.position;
                h = h * 31 + Mathf.RoundToInt(p.x * 10f);
                h = h * 31 + Mathf.RoundToInt(p.y * 10f);
                h = h * 31 + Mathf.RoundToInt(p.z * 10f);
                return h == 0 ? 1 : h;
            }
        }

        static void SetVelocity(Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(s * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static Material MakeSoftMaterial(Texture2D mask, Color tint)
        {
            Shader sh = Shader.Find("MHZE/WallBreak/Soft");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(sh) { name = "WallBreakSoft", hideFlags = HideFlags.HideAndDontSave };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", mask);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            m.renderQueue = 3000;
            return m;
        }

        static Texture2D GetCrackMask()
        {
            if (crackMask != null) return crackMask;
            const int w = 32;
            crackMask = new Texture2D(w, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int x = 0; x < w; x++)
            {
                float f = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(f * 1.6f));
                for (int y = 0; y < 2; y++) crackMask.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            crackMask.Apply(false, true);
            return crackMask;
        }

        static Texture2D GetPuffMask()
        {
            if (puffMask != null) return puffMask;
            const int n = 32;
            puffMask = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(1f, 0f, d);
                    puffMask.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
            puffMask.Apply(false, true);
            return puffMask;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            CacheComponents();
            if (wallMesh == null || wallMesh.sharedMesh == null) return;
            // Cheap frame for the gizmo only (does not need readable mesh data).
            Bounds b = wallMesh.sharedMesh.bounds;
            Transform mt = wallMesh.transform;
            Vector3 ls = mt.lossyScale;
            Vector3 sc = new Vector3(Mathf.Max(1e-4f, Mathf.Abs(ls.x)), Mathf.Max(1e-4f, Mathf.Abs(ls.y)), Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
            Vector3 world = new Vector3(b.size.x * sc.x, b.size.y * sc.y, b.size.z * sc.z);
            int t = 0; if (world.y < world[t]) t = 1; if (world.z < world[t]) t = 2;
            int a = (t + 1) % 3, c = (t + 2) % 3;
            float upA = Mathf.Abs(Vector3.Dot(mt.TransformDirection(AxisVector(a)), Vector3.up));
            float upC = Mathf.Abs(Vector3.Dot(mt.TransformDirection(AxisVector(c)), Vector3.up));
            int v = upA >= upC ? a : c, u = upA >= upC ? c : a;
            bool floorMax = Vector3.Dot(mt.TransformDirection(AxisVector(v)), Vector3.up) < 0f;
            float hu = holeSize.x * 0.5f / sc[u], hv = holeSize.y * 0.5f / sc[v];
            float cu = b.center[u], cv = b.center[v];
            if (holeCenter != null) { Vector3 p = mt.InverseTransformPoint(holeCenter.position); cu = p[u]; cv = p[v]; }
            if (holeStartsAtFloor) cv = floorMax ? b.max[v] - hv : b.min[v] + hv;
            Gizmos.matrix = mt.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 1f);
            Vector3 centre = WbSlicer.V(u, cu, v, cv, t, b.center[t]);
            Vector3 size = WbSlicer.V(u, hu * 2f, v, hv * 2f, t, b.size[t] + 0.02f / sc[t]);
            Gizmos.DrawWireCube(centre, size);
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.15f);
            Gizmos.DrawCube(centre, size);
        }
#endif
    }
}

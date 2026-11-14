using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MHZE.DrawerSystem
{
    /// <summary>
    /// Slides a drawer on its local axis, plays open/close sounds, and carries loose items inside it.
    /// The contents volume is a physics overlap query, not a collider, so it never blocks the player's
    /// interaction ray or an item's own pickup collider.
    /// </summary>
    public class DrawerController : MonoBehaviour
    {
        [Header("Motion")]
        [Tooltip("Local axis the drawer slides along when it opens.")]
        [SerializeField] private Vector3 slideAxis = Vector3.forward;
        [Tooltip("Distance the drawer slides out, in the parent's units. 0 fits it automatically: the drawer comes out until Keep Inside Fraction of its depth is still inside the furniture.")]
        [SerializeField] private float openDistance = 0f;
        [Tooltip("Automatic fit only: the fraction of the drawer's depth that always stays inside the furniture when fully open.")]
        [SerializeField, Range(0f, 0.9f)] private float keepInsideFraction = 0.1f;
        [Tooltip("Duration of the open/close move in seconds.")]
        [SerializeField, Min(0.05f)] private float duration = 0.45f;
        [Tooltip("Curve sampled over the duration; 0 is closed and 1 is open.")]
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("A toggle during a move reverses from the current position. Off makes the move finish first.")]
        [SerializeField] private bool allowInterruption = true;
        [Tooltip("Start the drawer already slid open (the closed position is still the starting local position).")]
        [SerializeField] private bool startOpen = false;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Played when the drawer starts opening.")]
        [SerializeField] private AudioClip openSound;
        [Tooltip("Played when the drawer starts closing.")]
        [SerializeField] private AudioClip closeSound;
        [SerializeField, Range(0f, 0.5f)] private float pitchVariation = 0.04f;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

        [Header("Contents")]
        [Tooltip("Carry loose items inside the drawer while it moves.")]
        [SerializeField] private bool carryContents = true;
        [Tooltip("Layers always treated as contents. Colliders with a Rigidbody are carried whatever their layer.")]
        [SerializeField] private LayerMask contentLayers = 1 << 8;
        [Tooltip("Width and depth of the measured drawer box as a fraction of its size.")]
        [SerializeField, Range(0.1f, 1f)] private float contentWidthDepthScale = 0.9f;
        [Tooltip("Height multiplier of the measured drawer box, so tall items standing in the drawer are caught.")]
        [SerializeField, Range(1f, 5f)] private float contentHeightScale = 2.5f;
        [Tooltip("Contents farther than this from the drawer are dropped from the carry list.")]
        [SerializeField, Min(0.5f)] private float releaseDistance = 2f;
        [SerializeField] private bool drawDetectionGizmo = true;

        private const int OverlapCapacity = 32;

        private readonly Collider[] overlapBuffer = new Collider[OverlapCapacity];
        private readonly List<CarriedContent> carriedContents = new List<CarriedContent>();
        private readonly HashSet<Collider> ignoredColliders = new HashSet<Collider>();

        private Vector3 closedLocalPosition;
        private Coroutine moveRoutine;

        /// <summary>True while the drawer is open or heading to open.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>True while a move is running.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>Raised when the drawer starts opening (true) or closing (false).</summary>
        public event Action<bool> StateChanged;

        /// <summary>Raised when a move finishes. The value is the state it finished in.</summary>
        public event Action<bool> MovementCompleted;

        /// <summary>Local axis the drawer slides along.</summary>
        public Vector3 SlideAxis => slideAxis;

        /// <summary>How far the drawer slides out, in the parent's units. Fitted automatically when the field is 0.</summary>
        public float OpenDistance => openDistance;

        /// <summary>The source used for the open and close sounds.</summary>
        public AudioSource AudioSource => audioSource;

        /// <summary>Clip played when the drawer starts opening.</summary>
        public AudioClip OpenSound => openSound;

        /// <summary>Clip played when the drawer starts closing.</summary>
        public AudioClip CloseSound => closeSound;

        private void Awake()
        {
            closedLocalPosition = transform.localPosition;

            if (openDistance <= 0f)
                openDistance = FitOpenDistance();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (startOpen)
            {
                IsOpen = true;
                transform.localPosition = OpenLocalPosition;
            }
        }

        private void OnDisable()
        {
            StopMove();
            ReleaseContents();
        }

        /// <summary>Slides the drawer open.</summary>
        public void Open() => MoveTo(true);

        /// <summary>Slides the drawer closed.</summary>
        public void Close() => MoveTo(false);

        /// <summary>Opens a closed drawer and closes an open or opening one.</summary>
        public void Toggle() => MoveTo(!IsOpen);

        private Vector3 OpenLocalPosition => closedLocalPosition + slideAxis.normalized * openDistance;

        private void MoveTo(bool open)
        {
            if (IsMoving)
            {
                if (open == IsOpen)
                    return;
                if (!allowInterruption)
                    return;
            }
            else if (open == IsOpen)
            {
                return;
            }

            StopMove();

            IsOpen = open;
            StateChanged?.Invoke(IsOpen);

            PlaySound(open ? openSound : closeSound);
            CollectContents();
            BeginCarry();

            Vector3 target = open ? OpenLocalPosition : closedLocalPosition;
            moveRoutine = StartCoroutine(AnimateMove(target));
        }

        private IEnumerator AnimateMove(Vector3 targetLocalPosition)
        {
            IsMoving = true;

            Vector3 startLocalPosition = transform.localPosition;
            Vector3 previousWorld = transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localPosition = Vector3.LerpUnclamped(startLocalPosition, targetLocalPosition, movementCurve.Evaluate(t));

                Vector3 currentWorld = transform.position;
                CarryContents(currentWorld - previousWorld);
                previousWorld = currentWorld;

                yield return null;
            }

            transform.localPosition = targetLocalPosition;
            CarryContents(transform.position - previousWorld);
            EndCarry();

            moveRoutine = null;
            IsMoving = false;
            MovementCompleted?.Invoke(IsOpen);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null)
                return;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                    audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
            audioSource.PlayOneShot(clip, soundVolume);
        }

        private void StopMove()
        {
            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
                moveRoutine = null;
                IsMoving = false;
            }

            EndCarry();
        }

        private void CollectContents()
        {
            carriedContents.Clear();
            if (!carryContents)
                return;

            BuildIgnoredColliders();

            GetContentBounds(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, overlapBuffer, rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider hit = overlapBuffer[i];
                if (hit == null || ignoredColliders.Contains(hit))
                    continue;
                if (hit.CompareTag("Player"))
                    continue;
                // Drawer bodies sit on the interaction layer: a neighbouring drawer is furniture, never contents.
                if (hit.GetComponentInParent<DrawerController>() != null)
                    continue;

                Rigidbody body = hit.attachedRigidbody;
                bool onContentLayer = (contentLayers.value & (1 << hit.gameObject.layer)) != 0;
                if (body == null && !onContentLayer)
                    continue;

                Transform target = body != null ? body.transform : hit.transform;
                if (target == null || target.IsChildOf(transform))
                    continue;
                if (ContainsCarried(target))
                    continue;

                Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
                if (!HasEnabledCollider(colliders))
                    continue;

                carriedContents.Add(new CarriedContent(target, body, colliders));
            }
        }

        private void BuildIgnoredColliders()
        {
            ignoredColliders.Clear();

            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
                ignoredColliders.Add(collider);

            Transform ancestor = transform.parent;
            while (ancestor != null)
            {
                Collider ancestorCollider = ancestor.GetComponent<Collider>();
                if (ancestorCollider != null)
                    ignoredColliders.Add(ancestorCollider);
                ancestor = ancestor.parent;
            }
        }

        private bool ContainsCarried(Transform target)
        {
            for (int i = 0; i < carriedContents.Count; i++)
            {
                if (carriedContents[i].Transform == target)
                    return true;
            }
            return false;
        }

        private void CarryContents(Vector3 delta)
        {
            for (int i = carriedContents.Count - 1; i >= 0; i--)
            {
                CarriedContent content = carriedContents[i];
                if (!content.IsValid(transform, releaseDistance) || !HasEnabledCollider(content.Colliders))
                {
                    carriedContents.RemoveAt(i);
                    continue;
                }

                if (content.Body != null)
                {
                    content.Body.position += delta;
                    content.Body.WakeUp();
                }
                else
                {
                    content.Transform.position += delta;
                }
            }
        }

        private void BeginCarry()
        {
            for (int i = 0; i < carriedContents.Count; i++)
            {
                Rigidbody body = carriedContents[i].Body;
                if (body == null)
                    continue;

                ZeroVelocity(body);
                body.useGravity = false;
                body.isKinematic = true;
            }
        }

        private void EndCarry()
        {
            for (int i = 0; i < carriedContents.Count; i++)
            {
                CarriedContent content = carriedContents[i];
                if (content.Body == null)
                    continue;
                if (!HasEnabledCollider(content.Colliders))
                    continue;

                content.Body.isKinematic = content.WasKinematic;
                content.Body.useGravity = content.WasGravity;
                ZeroVelocity(content.Body);
            }

            carriedContents.Clear();
        }

        private static void ZeroVelocity(Rigidbody body)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = Vector3.zero;
#else
            body.velocity = Vector3.zero;
#endif
            body.angularVelocity = Vector3.zero;
        }

        private static bool HasEnabledCollider(Collider[] colliders)
        {
            if (colliders == null)
                return false;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled && colliders[i].gameObject.activeInHierarchy)
                    return true;
            }

            return false;
        }

        private void ReleaseContents() => carriedContents.Clear();

        /// <summary>
        /// Works out how far the drawer can come out: until only <c>keepInside</c> of its back is left inside the
        /// furniture, so it is fully out but never slides off. Everything is measured along the slide axis in the
        /// parent's space, which is the space the slide itself happens in: the drawer's own colliders give its depth,
        /// the nearest ancestor with colliders (the furniture body) gives the front face it slides out of.
        /// </summary>
        private float FitOpenDistance()
        {
            Transform space = transform.parent;
            Vector3 axis = slideAxis.normalized;

            bool haveDrawer = false;
            float drawerMin = float.MaxValue;
            float drawerMax = float.MinValue;
            foreach (Collider collider in GetComponents<Collider>())
            {
                if (collider.isTrigger || !TryGetAxisRange(collider, space, axis, out float low, out float high))
                    continue;
                haveDrawer = true;
                drawerMin = Mathf.Min(drawerMin, low);
                drawerMax = Mathf.Max(drawerMax, high);
            }

            if (!haveDrawer)
                return FallbackOpenDistance;

            float depth = drawerMax - drawerMin;
            float keepInside = depth * keepInsideFraction;

            // The drawer is fully out when its back edge reaches the furniture's front face, minus what stays inside.
            float travel = depth - keepInside;
            if (TryGetCarcassFront(space, axis, out float carcassFront))
                travel = Mathf.Min(carcassFront - keepInside - drawerMin, travel);

            return Mathf.Max(travel, depth * 0.3f);
        }

        private const float FallbackOpenDistance = 0.36f;

        private bool TryGetCarcassFront(Transform space, Vector3 axis, out float front)
        {
            front = 0f;
            for (Transform ancestor = transform.parent; ancestor != null; ancestor = ancestor.parent)
            {
                bool found = false;
                float max = float.MinValue;
                foreach (Collider collider in ancestor.GetComponents<Collider>())
                {
                    if (collider.isTrigger || !collider.enabled || !TryGetAxisRange(collider, space, axis, out _, out float high))
                        continue;
                    found = true;
                    max = Mathf.Max(max, high);
                }

                if (found)
                {
                    front = max;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Extent of a collider along an axis of <paramref name="space"/>, from its true (rotated, scaled) shape.</summary>
        private static bool TryGetAxisRange(Collider collider, Transform space, Vector3 axis, out float low, out float high)
        {
            low = float.MaxValue;
            high = float.MinValue;

            Transform source = collider.transform;
            Vector3 center;
            Vector3 size;
            if (collider is BoxCollider box)
            {
                center = box.center;
                size = box.size;
            }
            else if (collider is MeshCollider mesh && mesh.sharedMesh != null)
            {
                center = mesh.sharedMesh.bounds.center;
                size = mesh.sharedMesh.bounds.size;
            }
            else
            {
                // Spheres, capsules and anything else: the world bounds are the best available shape.
                source = null;
                center = collider.bounds.center;
                size = collider.bounds.size;
            }

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = center + Vector3.Scale(size * 0.5f, new Vector3(
                    (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 world = source != null ? source.TransformPoint(corner) : corner;
                Vector3 local = space != null ? space.InverseTransformPoint(world) : world;
                float d = Vector3.Dot(local, axis);
                low = Mathf.Min(low, d);
                high = Mathf.Max(high, d);
            }

            return high >= low;
        }

        private void GetContentBounds(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
        {
            center = transform.position;
            Vector3 size = Vector3.one * 0.4f;
            Vector3 localCenter = Vector3.zero;

            // A batched MeshFilter points at the whole scene's combined mesh, not the drawer. The
            // collider keeps the drawer's own mesh, so it is the reliable source after batching.
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                localCenter = boxCollider.center;
                size = boxCollider.size;
            }
            else
            {
                MeshCollider meshCollider = GetComponent<MeshCollider>();
                if (meshCollider != null && meshCollider.sharedMesh != null)
                {
                    localCenter = meshCollider.sharedMesh.bounds.center;
                    size = meshCollider.sharedMesh.bounds.size;
                }
                else
                {
                    MeshFilter meshFilter = GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        localCenter = meshFilter.sharedMesh.bounds.center;
                        size = meshFilter.sharedMesh.bounds.size;
                    }
                }
            }

            Vector3 scaledSize = Vector3.Scale(size, Abs(transform.lossyScale));
            scaledSize.x *= contentWidthDepthScale;
            scaledSize.z = Mathf.Max(scaledSize.z * contentWidthDepthScale, 0.3f);
            scaledSize.y *= contentHeightScale;

            center = transform.TransformPoint(localCenter);
            halfExtents = scaledSize * 0.5f;
            rotation = transform.rotation;
        }

        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        private void OnDrawGizmosSelected()
        {
            if (!drawDetectionGizmo)
                return;

            GetContentBounds(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
            Gizmos.matrix = previous;

            DrawOpenPoseGizmo();
        }

        // Shows where the drawer's collider ends up when fully open, so a bad fit is visible without playing.
        private void DrawOpenPoseGizmo()
        {
            Collider own = GetComponent<Collider>();
            if (own == null)
                return;

            float distance = openDistance > 0f ? openDistance : FitOpenDistance();
            Vector3 closed = Application.isPlaying ? closedLocalPosition : transform.localPosition;
            Vector3 localMove = closed + slideAxis.normalized * distance - transform.localPosition;
            Vector3 worldMove = transform.parent != null ? transform.parent.TransformVector(localMove) : localMove;

            Bounds bounds = own.bounds;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.6f);
            Gizmos.DrawWireCube(bounds.center + worldMove, bounds.size);
        }

        private struct CarriedContent
        {
            public readonly Transform Transform;
            public readonly Rigidbody Body;
            public readonly Collider[] Colliders;
            public readonly bool WasKinematic;
            public readonly bool WasGravity;

            public CarriedContent(Transform transform, Rigidbody body, Collider[] colliders)
            {
                Transform = transform;
                Body = body;
                Colliders = colliders;
                WasKinematic = body != null && body.isKinematic;
                WasGravity = body == null || body.useGravity;
            }

            public bool IsValid(Transform drawer, float releaseDistance)
            {
                if (Transform == null || !Transform.gameObject.activeInHierarchy)
                    return false;
                if ((Transform.position - drawer.position).sqrMagnitude > releaseDistance * releaseDistance)
                    return false;
                return true;
            }
        }
    }
}

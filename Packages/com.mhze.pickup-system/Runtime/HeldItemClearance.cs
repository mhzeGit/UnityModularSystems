using UnityEngine;

namespace MHZE.PickupSystem
{
    /// <summary>
    /// Decides whether the held item can safely be let go of. A hand that reaches over a wall (or through a thin
    /// one) would drop the item on the far side or inside the wall. Two checks, both on the physics scene:
    /// <list type="bullet">
    /// <item>an oriented box around every mesh of the item, so an item touching or piercing anything solid is blocked;</item>
    /// <item>a line from the camera to the item's centre, so an item that sits wholly behind a thin collider
    /// (nothing overlaps it, but something stands between the player and it) is blocked too.</item>
    /// </list>
    /// Colliders of the item itself, of the player, triggers, free-moving rigid bodies and other pickable items
    /// are never obstacles: only walls, doors, furniture and the like.
    /// </summary>
    public static class HeldItemClearance
    {
        static readonly Collider[] OverlapBuffer = new Collider[32];
        static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

        /// <summary>
        /// The first solid collider in the way of letting go of <paramref name="item"/>, or null when the way is clear.
        /// </summary>
        /// <param name="item">The held item (its colliders are switched off while held, so its meshes are measured).</param>
        /// <param name="lineOrigin">Where the player looks from (the camera). Null skips the line check.</param>
        /// <param name="playerRoot">Root of the player; nothing under it is an obstacle. May be null.</param>
        /// <param name="layerMask">Layers that can obstruct.</param>
        /// <param name="margin">Metres added around the item's boxes, so an item resting hard against a wall counts as touching it.</param>
        public static Collider FindBlocker(GameObject item, Transform lineOrigin, Transform playerRoot, int layerMask, float margin)
        {
            if (item == null) return null;

            Physics.SyncTransforms();

            bool measured = false;
            Bounds total = default;
            Renderer[] renderers = item.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;

                if (!measured) { total = r.bounds; measured = true; }
                else total.Encapsulate(r.bounds);

                Collider blocker = FindOverlap(r, item.transform, playerRoot, layerMask, margin);
                if (blocker != null) return blocker;
            }

            if (!measured)
            {
                // An item with no mesh: a small ball at its pivot.
                return FindBallOverlap(item.transform.position, 0.1f + margin, item.transform, playerRoot, layerMask);
            }

            return lineOrigin != null ? FindOnLine(lineOrigin.position, total.center, item.transform, playerRoot, layerMask) : null;
        }

        static Collider FindOverlap(Renderer r, Transform item, Transform playerRoot, int layerMask, float margin)
        {
            Vector3 center;
            Vector3 half;
            Quaternion rotation;
            if (r is SkinnedMeshRenderer)
            {
                // Its local bounds follow the root bone, so the world box is the honest measure.
                center = r.bounds.center;
                half = r.bounds.extents;
                rotation = Quaternion.identity;
            }
            else
            {
                Transform t = r.transform;
                Bounds local = r.localBounds;
                Vector3 scale = t.lossyScale;
                center = t.TransformPoint(local.center);
                half = new Vector3(Mathf.Abs(local.extents.x * scale.x), Mathf.Abs(local.extents.y * scale.y), Mathf.Abs(local.extents.z * scale.z));
                rotation = t.rotation;
            }

            half += Vector3.one * margin;
            int count = Physics.OverlapBoxNonAlloc(center, half, OverlapBuffer, rotation, layerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (IsObstacle(OverlapBuffer[i], item, playerRoot)) return OverlapBuffer[i];
            }
            return null;
        }

        static Collider FindBallOverlap(Vector3 center, float radius, Transform item, Transform playerRoot, int layerMask)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, OverlapBuffer, layerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (IsObstacle(OverlapBuffer[i], item, playerRoot)) return OverlapBuffer[i];
            }
            return null;
        }

        static Collider FindOnLine(Vector3 from, Vector3 to, Transform item, Transform playerRoot, int layerMask)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.001f) return null;

            int count = Physics.RaycastNonAlloc(from, delta / distance, HitBuffer, distance, layerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (IsObstacle(HitBuffer[i].collider, item, playerRoot)) return HitBuffer[i].collider;
            }
            return null;
        }

        static bool IsObstacle(Collider c, Transform item, Transform playerRoot)
        {
            if (c == null || c.isTrigger) return false;
            if (c.transform.IsChildOf(item)) return false;
            if (playerRoot != null && c.transform.IsChildOf(playerRoot)) return false;
            if (c is CharacterController) return false;

            // Things that move freely get pushed out of the way; only fixed or animated geometry stands in the way.
            Rigidbody body = c.attachedRigidbody;
            if (body != null && !body.isKinematic) return false;

            // Other loose items lie on tables and in bowls; they are not walls.
            if (c.GetComponentInParent<IPickable>() != null) return false;
            return true;
        }
    }
}

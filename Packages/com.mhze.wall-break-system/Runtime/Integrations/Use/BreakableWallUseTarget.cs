using System.Collections.Generic;
using UnityEngine;
using MHZE.UseSystem;

namespace MHZE.WallBreak
{
    /// <summary>
    /// Lets the Use System aim a held tool at a <see cref="BreakableWall"/>. While the wall is intact and the player is
    /// close enough to swing at the spot they are looking at, the target is usable (so the "Press To Chop" prompt
    /// shows); when the tool lands, one blow is struck at the exact point the player was aiming at.
    /// Put this on the object that carries the wall's collider (the same object as, or under, the BreakableWall).
    /// </summary>
    [AddComponentMenu("MHZE/Wall Break/Breakable Wall Use Target")]
    public sealed class BreakableWallUseTarget : UseTargetBase
    {
        [Tooltip("How close the player's view must be to the wall for the swing to be offered (metres).")]
        [Min(0.5f)] [SerializeField] private float reach = 2.1f;
        [Tooltip("How hard a blow from the tool hits: the wall counts this many strikes.")]
        [Min(1)] [SerializeField] private int power = 1;

        private BreakableWall wall;
        private Collider[] colliders;
        private Camera cam;

        private void Reset()
        {
            targetId.value = "BreakableWall";
            toolData = new List<ToolData>
            {
                new ToolData { acceptedToolId = new ToolId { value = "Axe" }, usePromptPrefix = "Press", usePromptSuffix = "To Chop!" }
            };
        }

        private void Awake()
        {
            wall = GetComponentInParent<BreakableWall>();
            colliders = GetComponents<Collider>();
        }

        private void Update()
        {
            if (wall == null) return;
            if (wall.IsBroken) { SetCanUseAtTarget(false); return; }
            if (cam == null) cam = Camera.main;
            if (cam == null || colliders == null) return;

            // Only offer the swing when the view is aimed at this wall within reach.
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            bool close = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled && colliders[i].Raycast(ray, out RaycastHit hit, reach)) { close = true; break; }
            }
            SetCanUseAtTarget(close);
        }

        public override void Used(GameObject usedBy, string usedToolId, RaycastHit hitResults)
        {
            base.Used(usedBy, usedToolId, hitResults);
            if (wall == null || wall.IsBroken) return;
            wall.Strike(hitResults.point, hitResults.normal, power, usedBy);
        }
    }
}

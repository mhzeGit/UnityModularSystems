using System;
using UnityEngine;

namespace MHZE.InventorySystem
{
    /// <summary>
    /// Sits on the stash object and reports, in the very moment it happens, that a put-away item was moved out
    /// of the stash by someone else (a story reset, a rewind). A switched-off item gets no message of its own, so
    /// the stash, which is switched on, is told instead.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class InventoryStash : MonoBehaviour
    {
        public Action ChildrenChanged;

        void OnTransformChildrenChanged()
        {
            ChildrenChanged?.Invoke();
        }
    }
}

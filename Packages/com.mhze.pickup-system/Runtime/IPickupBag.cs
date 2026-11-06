using UnityEngine;

namespace MHZE.PickupSystem
{
    /// <summary>
    /// An optional bag behind the hand, implemented by an inventory and set on <see cref="PickupSystem.Bag"/>.
    /// With a bag the hand puts the held item away instead of dropping it when another one is picked up or
    /// the player switches item, and a pickup is refused when the bag has no room. Without a bag the pickup
    /// system behaves exactly as it always did: one held item, a pickup while occupied waits or drops.
    /// </summary>
    public interface IPickupBag
    {
        /// <summary>
        /// Where stowed items wait, switched off. Give it a unit scale and no rotation so an item keeps its
        /// size and pose through the stash.
        /// </summary>
        Transform StashRoot { get; }

        /// <summary>The bag already keeps this item: it waits in the stash or it is the one in the hand.</summary>
        bool Contains(GameObject item);

        /// <summary>The bag has room for this item, on a stack with space or in a free slot.</summary>
        bool CanKeep(GameObject item, IPickable pickable);

        /// <summary>
        /// The held item was put in the hand by the story (or a script), not picked up by the player, and has to
        /// stay there until it is released. The player's own pickups are refused meanwhile instead of putting it
        /// away. The story itself may still replace it.
        /// </summary>
        bool IsHandLocked { get; }
    }
}

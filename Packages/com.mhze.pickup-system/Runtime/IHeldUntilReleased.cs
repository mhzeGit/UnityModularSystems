namespace MHZE.PickupSystem
{
    /// <summary>
    /// Optional. A component on a pickable object that the player may not drop or throw while it says so (a gift the
    /// story gave them that has to be handed over first). Only the player's own input is refused, silently:
    /// <see cref="PickupSystem.DropCurrentHeldItem"/> is code and always works, so the story can still take the item.
    /// </summary>
    public interface IHeldUntilReleased
    {
        bool BlocksPlayerRelease { get; }
    }
}

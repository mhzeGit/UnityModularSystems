namespace MHZE.InventorySystem
{
    /// <summary>
    /// Optional. A component on a pickable object that must stay in the player's hand while it says so: the bar will
    /// not put it away (keys and wheel do nothing, the selected slot gives a small start) and will not take another
    /// pickup, until the item is dropped, handed over or stops asking. For things that cannot be pocketed, such as a
    /// full bowl of soup, or whose game treats "no longer in the hand" as "dropped".
    /// </summary>
    public interface IKeepInHand
    {
        bool MustStayInHand { get; }
    }
}
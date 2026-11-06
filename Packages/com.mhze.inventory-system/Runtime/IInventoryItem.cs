namespace MHZE.InventorySystem
{
    /// <summary>
    /// Optional. A component on a pickable object that decides how the inventory shows and stacks it, for
    /// items that are not <c>PickableItemBase</c> with a <c>PickableItemData</c> asset. Without it, an item
    /// with item data is shown by that data and an item without is a unique, unstackable object.
    /// </summary>
    public interface IInventoryItem
    {
        InventoryItemInfo GetInventoryInfo();
    }
}

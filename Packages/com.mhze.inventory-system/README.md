# Inventory System

A minimal hotbar for a first-person game with hand-held pickups. It sits on top of the
[Pickup System](../com.mhze.pickup-system/README.md): the item in the **selected slot** is the item in the
player's hand, and everything the player picks up takes a slot.

- a configurable number of slots (6 by default, 1 to 30, changeable while playing)
- **number keys** 1-9 and 0 and the **scroll wheel** (wrapping) change the selection
- scrolling or pressing a key for an **empty slot** is allowed, and leaves the hand empty
- a picked-up item takes the next free slot, or the selected slot if it is empty
- the same kind of item **stacks** when its item data allows it, and the stack size is shown
- a full bar refuses the pickup (the item stays where it is) and shows "Inventory full"
- the bar is built in code on its own screen-space canvas: a row of squares, the selected one lit

## Setup

1. Have a working `PickupSystem` on the player (see the pickup package).
2. Add **MHZE ▸ Inventory ▸ Player Inventory** to the same object (or any object in the scene).
   That is all: it finds the pickup system, builds the bar and registers itself as the pickup system's bag.
3. Set `Slot Count` on the component. It can be edited while the game runs.
4. On each **PickableItemData** asset set what the bar shows and whether the item stacks:
   `ItemName` (shown until the item has an icon), `ItemIcon` (a sprite, shown instead of the name) and
   `MaxStack` (1 = does not stack). Items stack only when they share the **same item data asset**.

## How it behaves

| Player does | Result |
| --- | --- |
| picks up an item with a free slot | item takes the selected slot if empty, else the first free one (or joins a stack of its kind); the slot is selected and the item is in the hand; the item that was in the hand is put away |
| picks up an item when every slot is taken | refused; nothing moves; `PickupRefused` is raised |
| presses 1-9 / 0, or scrolls | that slot is selected; the hand puts its item away and draws the slot's item (nothing for an empty slot) |
| a story or script puts an item in the hand (`PickupSystem.PickupObject`) | it takes a slot and is **locked in the hand**: the keys and the wheel do nothing (the selected slot gives a small start), and the player's own pickups are refused, until it is released (drop key, hand-over, the story). This keeps a brush, a torch or a cat where the chapter expects it |
| drops or throws the held item | it leaves the bar; if its slot is a stack the next one is drawn |
| gives the held item away, uses it up, or the story takes it (`DropCurrentHeldItem`) | it leaves the bar |

Put-away items are **kept**, not dropped: they are reparented under an `Inventory Stash` object (a root object of
the player's scene, so the story's objects are never destroyed or moved with the player) and switched off, with
physics left in the held state. Drawing one switches it back on in the hand. Neither putting away nor drawing runs
the item's `Picked`/`Dropped` callbacks; those still mean "taken from / put back in the world".

If the world takes a put-away item back (a story reset reparents it, or it is destroyed) the bar notices on the
next frame and forgets it; a reparented item that was left switched off is switched back on.

## Input gating

The keys and wheel work only while the `PickupSystem` is enabled (pausing a game by disabling it also stops the
bar). `PlayerInventory.InputEnabled` is a master switch and `PlayerInventory.InputGate` takes a `Func<bool>`
asked every frame, for example "the player has control":

```csharp
inventory.InputGate = () => controller.ControlsEnabled;
```

## Scripting

```csharp
PlayerInventory bag = PlayerInventory.Instance;
bag.SetSlotCount(8);          // cut-off slots move their items to free slots, or drop them
bag.Select(2);                // the hand follows within the frame
bag.SelectNext();             // wraps by default
bag.Holds(someObject);        // in the hand or put away
bag.IsHandLocked;             // the story put the held item there; it stays until released
bag.ReleaseAll();             // everything back into the world in front of the player
bag.Inventory[0].Count;       // slot content: Info.Name, Info.Icon, Count, items
bag.SlotChanged += i => { };  // also SelectionChanged, SlotCountChanged, PickupRefused, SelectionBlocked
```

Items that are not `PickableItemBase` can implement `IInventoryItem` to say how they are named, shown and stacked.

## Notes and limits

- The bar is not saved. It starts empty in every scene.
- Story code that calls `PickupSystem.PickupObject` while the bar is full drops the held item to make room
  (the classic behaviour); it never loses an item.
- `InventoryBarView` is optional. Turn off `Create Bar` and draw your own UI from the events.
- Tests: `Tests/Editor`. Add `com.mhze.inventory-system` to the project's manifest `testables` to run them.

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
- the bar is built in code on its own screen-space canvas: a row of separate dark stone slabs, the selected one lighter with a thin ochre edge; on the InventoryBarView it can sit at the bottom, a bottom corner or the top (`Anchor` and `Margin`), and its size, colours and font are settings
- **the bar keeps out of the way**: it slides off the edge of the screen when nothing has changed for a few seconds and comes back, for a few seconds, whenever the player switches slot (also pressing the key of the slot already held), picks something up, drops something or is refused (`Auto Hide`, `Visible Seconds`); `InventoryBarAvoider` lets other UI (the subtitles) use the space while the bar is away

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

## The bar slides away

With **Auto Hide** on (the default) the bar starts off the screen and slides in, on its own eased slide (about 0.2 s in,
0.4 s out), whenever something happens to the inventory: a pickup, a drop, a switch of slot, the key of the slot that is
already selected, a refused pickup or switch, a different number of slots. It stays **Visible Seconds** (3.5) after the
last such thing and slides away again; a change while it is leaving turns it round without a jerk. Time is unscaled, so
it also works while the game is paused. Nothing is drawn while it is away.

```csharp
InventoryBarView bar = ...;
bar.AutoHide = false;          // keep it on screen all the time
bar.VisibleSeconds = 5f;
bar.Show();                    // bring it in for VisibleSeconds (also Show(seconds))
bar.Hide();                    // send it away now (only with AutoHide on)
bar.Reveal;                    // 0 away .. 1 fully there
bar.ClearanceHeight;           // screen pixels from the screen edge to the top of the bar and its notice
```

**InventoryBarAvoider** (`MHZE ▸ Inventory ▸ Inventory Bar Avoider`) goes on a UI element at the bottom of the screen that
the bar would cover (the subtitles, a prompt). It lifts the element just enough to clear the bar and its notice (plus
**Gap**), by the same slide as the bar, so the two rise and fall together, and the element settles back into the freed
space when the bar is away. It never moves an element that already sits above the bar, puts the element back when it
is switched off, and finds the bar in the scene by itself.

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
bag.SlotChanged += i => { };  // also SelectionChanged, SelectionRepeated, SlotCountChanged, PickupRefused, SelectionBlocked
```

Items that are not `PickableItemBase` can implement `IInventoryItem` to say how they are named, shown and stacked.
An item that cannot be pocketed (a full bowl of soup) or whose game reads "not in the hand" as "dropped" implements `IKeepInHand`: while it says `MustStayInHand` the bar will not put it away and takes no other pickup, until it is dropped or handed over.

## Notes and limits

- The bar is not saved. It starts empty in every scene.
- Story code that calls `PickupSystem.PickupObject` while the bar is full drops the held item to make room
  (the classic behaviour); it never loses an item.
- `InventoryBarView` is optional. Turn off `Create Bar` and draw your own UI from the events.
- Tests: `Tests/Editor` (the slots, the controller on a real pickup system) and `Tests/PlayMode` (the real update loop with
  virtual keyboard and mouse: scenarios with several kinds of items, the bar sliding away and the subtitles riding above it, and a randomized run that checks after every step that
  the bar, the hand and the world still agree). Add `com.mhze.inventory-system` to the project's manifest `testables` to run them.

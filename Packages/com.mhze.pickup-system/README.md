# Pickup System

`PickupSystem` owns the one held object, attachment, physics callbacks and release.
`PlayerArmAnimator` presents that state on an animated first-person rig. It never owns inventory,
consumption, throwing physics or story progression. An inventory can sit behind the hand through the
small `IPickupBag` contract (below); the `com.mhze.inventory-system` package implements it.

## A bag behind the hand (IPickupBag)

Without a bag the hand holds one item: a pickup while occupied waits (`OnAttemptedPickWhileOccupied`) and
the story drops the held item to take another. Set `PickupSystem.Bag` to an `IPickupBag` and the hand can
carry more than the one item:

- `CheckIfPickable` (the player's interaction) refuses with `OnPickRefused` when the bag has no room
  (`CanKeep`), otherwise the pickup goes ahead even while the hand is full.
- Whenever the hand has to give up an item for another (a pickup, `PickupObject` from the story) it
  **stows** the held item instead of dropping it, provided the bag `Contains` it and has room for the
  newcomer. Otherwise it drops it exactly as before.
- `StowHeldItem()` parents the held item under `Bag.StashRoot` and switches it off. No `Dropped()`
  callback runs and the physics state stays "held". Listeners get `OnPickedItemChanged(null)` with
  `IsStowing` set, so they can tell a stow from a drop.
- `PickupObject` on an item the bag already `Contains` draws it from the stash: it is switched on and put
  in the hand without `Picked()` or `OnItemPicked`.
- `IsHandLocked` lets the bag say the held item came from the story and has to stay: the player's pickups are then
  refused with `OnAttemptedPickWhileOccupied`, as in a hand that is simply full. `IsPlayerPickup` is true while the
  player's own interaction picks something up, so listeners can tell it from a story calling `PickupObject`.
- `PickableItemData.MaxStack` (1 by default) is the stack size an inventory uses for that item.

Without a bag nothing changes.

## First-person holding

1. Put an Animator and PlayerArmAnimator on a camera-child arm rig. Assign the Animator,
   PickupSystem and Right Hand Socket explicitly. Assign PickupSystem's PlayerArmBase to the rig
   and PickableObjectHolder to the animated socket. ConfigureHoldingRig is the runtime equivalent.
2. Align the rig in camera coordinates **before** tuning items. The hand must be in front of the
   camera near plane. Preserve the neutral pose; pickup adds hand offsets to that pose, including
   when a story gives an item before Start.
3. The Animator contract is IsHolding (bool), HoldType (int: 0 idle, 1 palm, 2 stick, 3 pinch),
   HoldPressure (float, 0 open–1 closed), Attack and Throw (optional triggers). HoldPressure needs
   an actual finger-only animation layer or blend tree; setting the parameter alone cannot bend
   a mesh. Use the authored skeleton, correct skin bindings and finger curves.
4. Create an Item Hold Profile (MHZE/Pickup). Choose Grip, FingerClosure and optional ArmOffset.
   Assign it to PickableItemData.HoldProfile. UseTrigger is opt-in; blank prevents generic use
   actions such as eating or adding ingredients from playing a melee attack.
5. Keep **model-specific** ItemLocationOffset/ItemRotationOffset on PickableItemData. They are
   relative to the animated socket, not the camera or world. Do not correct a grip by moving the
   whole rig. Duplicate item data for differently shaped models even when they share a profile.
6. Add PickupArmUseBridge / PickupArmThrowBridge to the arm when those packages are installed.
   They are separate optional assemblies, not dependencies of the pickup core. The use bridge
   points UseSystem.playerHand at the same socket; the throw bridge animates the released item
   even if a release listener executes first.

IPickable remains unchanged for custom implementations. Without HoldProfile the existing hand
fields still work. The migrated PlayerArmAnimator preserves its script GUID and uses MovedFrom
for its former BlahBlahFamily / Assembly-CSharp identity.

## Reaching with the held item (PlayerArmReach)

Put `PlayerArmReach` on the same object as the arm's Animator. While it is on, the arm reaches out
and puts the **tip of the held item** on a world point, over the animated hold pose, then eases back:

```csharp
reach.Begin(heldItemTip);              // a Transform at the end of the item (bristles, probe, nozzle)
reach.SetTarget(tipWorld, towardHand); // every frame: where the tip goes, and the way the item
                                       // points from the tip back toward the hand
reach.End();                           // ease back to the animation (EndNow() snaps)
```

It solves the shoulder-elbow-wrist chain with two bones, turns the hand so the item points the
right way (keeping the animated roll of the grip), lengthens the arm up to `maxStretch` (the hand
and item keep their size) and slides the whole arm only when that is not enough, so the open end of
the arm mesh stays below the screen edge. The other arm is scaled away while it reaches. The arm
root's neutral position is whatever `PickupSystem` last set; the reach adds to it and gives it back.
`TipReached` says whether the tip is on its target. Rig bones are found by the same names as
`PlayerArmAnimator`, or assign them. Tune `reachShift`, `elbowPole`, `restStretch` and `maxStretch`
per rig, with the real first-person view.

For a shoulder that must stay attached to the camera/body, assign `shoulderAnchor` to a separate
Transform (outside the arm's own hierarchy). The shoulder stays on that point at full reach,
and any distance left after the two-bone solve is covered by moving the wrist, stretching the
forearm without scaling the hand or held item. The usual `maxStretch` still shapes the elbow;
it does not limit this final wrist correction. Ending/ disabling reach restores the neutral root
and bone positions. Leaving the anchor empty preserves the normal sliding behavior.

## Hiding the hand you are not using (PlayerArmVisibility)

`PlayerArmAnimator` adds `PlayerArmVisibility` to its rig (add it by hand only to change its settings).
It decides every frame, per hand, whether that hand is drawn:

- **Right hand (Auto)**: visible while `PickupSystem` holds an item, while `PlayerArmReach` reaches, and
  while the Animator is not in its `Idle` state or is in a transition (the `idleState` field names the
  state), so a pickup blends up and a release or throw is seen all the way down. Hidden at rest.
- **Left hand (Auto)**: hidden; it has no use of its own.
- **Force a hand** under an owner: `PlayerArmVisibility.Current.Show(PlayerHand.Left, owner)`,
  `Hide(hand, owner)`, then `Clear(owner)`. Hide beats Show beats Auto; a destroyed owner is dropped
  automatically. The per-hand Inspector setting (Auto / Show / Hide) is the permanent base.

The arm model draws both arms as one skinned mesh, so a hidden hand has its arm bone scaled to zero
(anything held in that hand goes with it). A zero-scale parent would zero the scale of an item
reparented through it, so `PickupSystem` restores the true bone scale before it picks up, drops or
force-releases an item; do not reparent held items to or from the hand bones yourself while a hand is
forced hidden. `Refresh()` applies the hands immediately instead of at the end of the frame.

## Authoring and validation

Test pickup, carrying while looking up/down, drop, throw, consumption, handoff, object destruction,
disable/re-enable and repeated play sessions. Assert only one item is parented to the socket.
Inspect actual first-person screenshots, the contact point and the transition; a state parameter
or a correctly parented floating object is not visual validation. Check custom IPickable callers
and story-given items too. Keep editor previews temporary and explicitly invoked; no automatic
scene/prefab generators or asset-saving hooks.

EditMode tests cover additive neutral offsets before Start, direct replacement and legacy/profile
data resolution. Add `com.mhze.pickup-system` to the test project's manifest `testables` to expose
package tests when testing a Git dependency.

## Items the player cannot drop (IHeldUntilReleased)

A component on a pickable item can implement `IHeldUntilReleased` and return `BlocksPlayerRelease = true` while the
player must keep it (a gift to hand over). The drop input then does nothing, silently, and without a bag a pickup of
something else is refused like a full hand. `PickupSystem.HeldItemBlocksPlayerRelease` reports it so a throw system can
refuse too. `DropCurrentHeldItem()` ignores it, so the story can always take the item. Items without it are unchanged.

## Drop safety (walls)

The hand reaches ahead of the camera, so standing close to a wall and pressing the drop key used to let go of the
item on the far side of it, or inside it. Now `PickupSystem.TryPlayerDrop()` (what the drop key calls) first asks
`IsHeldItemObstructed(out Collider blocker)`:

- **Contact**: an oriented box around each mesh of the held item (its colliders are off while held) overlaps
  a solid collider, widened by `dropClearanceMargin` (2 cm).
- **Line**: a line from the camera (`DropCheckOrigin`, else `Camera.main`) to the item's centre crosses a
  solid collider, which catches an item wholly behind a thin wall that nothing overlaps.

When it is obstructed the item is slid back toward the camera, `dropPullBackStep` (5 cm) at a time, to the nearest
spot where both checks pass, and dropped there: on the player's side of the wall. It never comes closer to the camera
than `dropPullBackLimit` (30 cm); when no clear spot exists the drop is refused (the item stays in the hand and
`OnDropBlocked` fires). Triggers, the item's own and the player's colliders, free-moving rigid bodies and other
pickable items never count. `dropObstacleLayers` limits the layers; `CheckDropClearance = false` turns the check
off. `DropCurrentHeldItem()` is code and ignores all of it, like `IHeldUntilReleased`.

A throw is not relocated: a throw system should refuse while `IsHeldItemObstructed` (the game's
`PickupThrowMediator` returns `false` from `ThrowingSystem.CanThrowFilter` and calls `NotifyReleaseBlocked()`).
Queries run only when the player presses drop or throw, never per frame. The logic lives in `HeldItemClearance`.

## Camera-follow smoothing

Add PlayerArmMotionLag to the arm rig root (the camera-child object with PlayerArmAnimator) and assign its camera. If left blank, it uses the rig's parent.
`rotationResponse` controls how quickly it catches up in seconds; larger values create more visible follow-through. The camera remains immediate, while the arm and held item lag behind its rotation.
`settleResponse` controls the softer return after a turn; larger values let the arm drift back instead of snapping to rest.

The drag is progressive, never hard-clamped. `maxLagAngle` is the knee where the drag starts to
stiffen hard (default 24 degrees) and `limitStiffness` controls how strong that stiffening is
(default 5). The arm can pass the knee slightly, and at extreme turn speeds it even catches up a
little, so it keeps moving instead of stopping dead at a fixed angle. Lower `maxLagAngle` or raise
`limitStiffness` if a long held item clips the edge of the view during a fast turn; raise
`settleResponse` if the return still feels abrupt.

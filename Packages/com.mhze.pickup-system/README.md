# Pickup System

`PickupSystem` owns the one held object, attachment, physics callbacks and release.
`PlayerArmAnimator` presents that state on an animated first-person rig. It never owns inventory,
consumption, throwing physics or story progression.

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

## Camera-follow smoothing

Add PlayerArmMotionLag to the arm rig root (the camera-child object with PlayerArmAnimator) and assign its camera. If left blank, it uses the rig's parent.
`rotationResponse` controls how quickly it catches up in seconds; larger values create more visible follow-through. The camera remains immediate, while the arm and held item lag behind its rotation.
`settleResponse` controls the softer return after a turn; larger values let the arm drift back instead of snapping to rest.

The lag is soft-limited so a fast turn cannot leave the arm outside the view. `maxLagAngle` is the
largest angle the arm may trail (default 24 degrees) and `limitStiffness` controls how quickly it
stiffens as it gets there (default 2). Near the limit the excess is pulled in over a short time
rather than clamped in one frame, so the arm keeps a little give and eases against the limit
instead of stopping dead on it. Lower `maxLagAngle` if a long held item clips the edge of the view
during a fast turn, and raise `settleResponse` if the release still feels abrupt.
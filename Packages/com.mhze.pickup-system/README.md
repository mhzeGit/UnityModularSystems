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

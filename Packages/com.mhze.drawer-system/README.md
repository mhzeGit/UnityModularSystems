# Drawer System

A drawer that slides on its own local axis, plays open/close sounds, and carries the loose items inside with it.

## Drawer Controller

Add `DrawerController` to the drawer GameObject itself (the moving part, not the furniture root). It records
the closed local position on Awake, slides along the slide axis by `Open Distance`, and exposes `Open()`,
`Close()` and `Toggle()`.

- **Smart open distance**: leave `Open Distance` at 0 and the drawer fits itself. It measures its own colliders
  (depth along the slide axis, from their true rotated shape) and the nearest ancestor with colliders (the
  furniture body, which gives the front face it slides out of). The drawer comes out until only `Keep Inside`
  (default 4 cm, capped at `Keep Inside Max Fraction` of its depth) of its back is still inside the furniture:
  fully out, never off the rails. The slide axis is in the parent's space, and so is the measurement, so rotated
  and scaled imports work. Type a value into `Open Distance` only to override the fit.
- **Gizmo**: selecting a drawer also draws its collider at the fitted fully-open pose (orange).

- **Motion**: local slide axis, duration and an editable `AnimationCurve`. Interrupting mid-move reverses from
  the current position; `Allow Interruption` can be turned off.
- **Sound**: an `AudioSource` on the drawer plus open/close clips. The controller creates a source if none is
  assigned, so a bare prefab still plays.
- **Contents**: a `Physics.OverlapBox` volume measured from the drawer mesh or collider. No collider is added,
  so it never blocks the player's interaction ray or the item's own pickup collider. Objects with a Rigidbody
  are carried whatever their layer; other colliders on `Content Layers` (default `Interactable`) are carried
  too. Items are moved by the same world delta as the drawer, using `Rigidbody.MovePosition` in `FixedUpdate`
  so physical items keep their colliders and can still be picked up. Items that leave the drawer volume, or
  whose colliders are disabled because the player is holding them, are dropped from the carry list.
- **Gizmo**: `Draw Detection Gizmo` draws the measured contents box when the drawer is selected. Colliders that
  belong to another `DrawerController` are never carried as contents.

`DrawerController` has no dependency on an interaction package. The game adds its own thin
`InteractableItemBase` adapter for prompts and input.

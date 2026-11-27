# First Person Controller

A compact first-person controller (CharacterController or Rigidbody driver) with walking, running,
crouching, jumping, head bob, camera effects and scripted look and move. `FirstPersonController`
is the entry point; `FPCSettings` holds the tuning values.

## Scripted control

- `ForceLookAt`, `StopForceLook`: turn the view toward a point or transform.
- `ForceMoveTo`, `StopForceMove`: walk the player to a position.
- `SetPosition` moves the player and leaves the view alone (safe every frame while they look around);
  `SetRotation` and `SetPositionAndRotation` also set where they face.
- `HoldBody(owner)` / `ReleaseBody(owner)` stop walking, jumping, gravity and motion effects for a scripted
  moment while looking keeps working. Prefer this to `DisableControls` for scripted moments.
- `HoldLook(owner)` / `ReleaseLook(owner)` hold the view still while the player uses the mouse cursor on UI
  (answer buttons, a picker): look input is ignored and a click does not recapture the cursor; walking keeps
  working. The caller shows the cursor. The dialog package does this for its questions by itself.
- `TurnView(yaw, pitch)` turns the view by a delta with the player's own look input added on top;
  `CameraRoll` banks it. Together with `HoldBody` they drive a camera ride the player can still look around in.
- `EnableControls` / `DisableControls` switch every input off or on (looking too).
- `EnableMove`, `EnableRun`, `EnableJump`, `EnableCrouch`, `EnableLook` switch one input.
- `CameraHeightOffset` raises or lowers the eyes (metres, negative lowers).

## Soft camera lock

Call `controller.TryBeginSoftLookLock(owner, new Vector2(6f, 4f), .12f)` to anchor a temporary
look limit at the current yaw and pitch. Mouse sensitivity and invert-Y still apply. Input moves the
view with progressively stronger resistance toward the limits, with smooth camera response. Turning
back responds immediately, even after repeatedly pushing against a limit. Zero locks an axis fully.
Limits must be finite, nonnegative and below 180 degrees; smoothing must be finite and nonnegative.

Call `controller.EndSoftLookLock(owner)` on completion and cancellation. It keeps the displayed
orientation, discards pending rotation, and restores unrestricted looking without a snap.
`IsSoftLookLocked` reports whether a limit is active. A second owner cannot replace or release an
active lock (`TryBeginSoftLookLock` returns false); repeating the same owner leaves its anchor intact.
Forced look, teleport/synchronization and disabling the controller clear the lock so an old anchor
cannot constrain a new scene position. A lock cannot start during forced look or its return.
Control disabling (for example pause) preserves the lock, and zero-time frames do not accumulate input.

## Seated posture

`FirstPersonSeat` sits the player down: walking, running, jumping and crouching are switched off,
looking and every other system (interaction, pickup) keep working, and the eye drops to a seated
height. The controller, its collider and its settings are not modified.

```csharp
FirstPersonSeat seat = FirstPersonSeat.For(controller);   // adds the component when missing
seat.SitDown(stool);                                       // stool: a transform on top of the seat
// ...
seat.StandUp(exitPoint);                                   // feet on exitPoint, inputs restored
seat.Cancel(exitPoint);                                    // the same, instantly (resets, rewinds)
```

- The seat transform's **position** is the top of the seat, where the feet would rest if the
  player stood on it. Give the seat a collider so the capsule is held there. Its **forward** is
  the way the player faces when they sit; `SitDown(seat, false)` moves them without turning the view.
- `Seated` and `StoodUp` fire when the movement has finished; `IsSeated` is true from the moment
  `SitDown` is called until `StandUp` is called.
- The eyes drop through `FirstPersonController.CameraHeightOffset` (added to the crouch camera height
  every frame), so interaction rays and look tests start from the seated eye. `Eye Height Above Seat`
  and `Standing Eye Height` set how far they drop.

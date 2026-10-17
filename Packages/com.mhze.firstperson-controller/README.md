# First Person Controller

A compact first-person controller (CharacterController or Rigidbody driver) with walking, running,
crouching, jumping, head bob, camera effects and scripted look and move. `FirstPersonController`
is the entry point; `FPCSettings` holds the tuning values.

## Scripted control

- `ForceLookAt`, `StopForceLook`: turn the view toward a point or transform.
- `ForceMoveTo`, `StopForceMove`: walk the player to a position.
- `SetPosition`, `SetRotation`, `SetPositionAndRotation`: place the player.
- `EnableControls` / `DisableControls` switch every input off or on (looking too).
- `EnableMove`, `EnableRun`, `EnableJump`, `EnableCrouch`, `EnableLook` switch one input.

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
  the way the player faces when they sit.
- `Seated` and `StoodUp` fire when the movement has finished; `IsSeated` is true from the moment
  `SitDown` is called until `StandUp` is called.
- The camera drop is taken off at the start of every frame and put back in `LateUpdate`, so the
  controller's own head bob and landing effects are unaffected. `Eye Height Above Seat` and
  `Standing Eye Height` set how far the eye drops.

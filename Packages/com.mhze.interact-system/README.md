# Interact System

Raycast interaction and input prompts for player and NPC interactors.

`IInteractable.HoldTime` requires one continuous press on the same available target.
Releasing the input, looking away, switching targets, disabling the interactor, or the
target becoming unavailable cancels the attempt and ends its hold feedback. After a
cancelled attempt the player must release and press again; elapsed time never transfers
to another object or resumes when looking back.

An instant interaction uses `HoldTime = 0`. Both styles retain the existing
`IInteractable` and `IInteractor` public contracts.

## Reach and obstacles (PlayerInteractor)

The player interacts with what is under the crosshair, within `Max Distance` of the camera
(default 2.5 m, about arm's length plus a step).

A wall or any other solid collider between the camera and an interactable stops the
interaction, even when that collider is not on `Interactable Layer` (the usual setup lists only
the interactable layer). `Obstacle Layers` chooses what can obstruct (everything except Ignore
Raycast by default); trigger colliders and the player's own colliders, such as a held item, never
obstruct. `Blocked By Obstacles` turns the check off.

Play Mode regression tests cover uninterrupted completion, release, loss of aim,
target switching and target availability.

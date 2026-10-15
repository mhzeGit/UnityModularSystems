# Interact System

Raycast interaction and input prompts for player and NPC interactors.

`IInteractable.HoldTime` requires one continuous press on the same available target.
Releasing the input, looking away, switching targets, disabling the interactor, or the
target becoming unavailable cancels the attempt and ends its hold feedback. After a
cancelled attempt the player must release and press again; elapsed time never transfers
to another object or resumes when looking back.

An instant interaction uses `HoldTime = 0`. Both styles retain the existing
`IInteractable` and `IInteractor` public contracts.

Play Mode regression tests cover uninterrupted completion, release, loss of aim,
target switching and target availability.

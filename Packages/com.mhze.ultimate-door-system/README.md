# Ultimate Door System

Configurable door: procedural / Animation / Animator / none, rotating or sliding, password lock, auto open/close, open/close sounds.

## Motion and who did it (1.5.0)

`State` only changes when a swing ends: a door opening still says `Closed`, a door closing still says `Open`. `IsAnimating`, `IsOpening` and `IsClosing` tell a door on its way from one standing still. `LastInteractor` is the transform last passed to `Open(interactor)`, `CloseBy(interactor)` or `Toggle(interactor)` (null when a script or an auto-close did it), so a listener can tell the player's hand from an NPC's. `CloseBy` is a separate name from `Close()` on purpose: event bindings look methods up by name, and an overload would make `Close` ambiguous.

## Locked rattle (1.4.0)

Trying a locked door (`Toggle` or `Open`) fires `OnAttemptOpenLocked`, plays the door's **Locked Sound** (pitch/volume varied, with a cooldown so click-spam does not stack) and, for Procedural / None doors, plays a small damped shake (default three swings, ~2 degrees, 0.5 s; a sliding door nudges a few cm). Numbers and the curve live in a `DoorRattleProfile` asset (Create > MHZE > Ultimate Door System > Door Rattle Profile); a door with none uses `DoorRattleProfile.Default`. `Rattle Amplitude` on the door scales or disables the shake. Every click restarts the shake from the stored pose; the door always ends exactly where it started, and Unlock, Open, Close or disabling cancels it and restores the pose.

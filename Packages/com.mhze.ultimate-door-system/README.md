# Ultimate Door System

Configurable door: procedural / Animation / Animator / none, rotating or sliding, password lock, auto open/close, open/close sounds.

## Locked rattle (1.4.0)

Trying a locked door (`Toggle` or `Open`) fires `OnAttemptOpenLocked`, plays the door's **Locked Sound** (pitch/volume varied, with a cooldown so click-spam does not stack) and, for Procedural / None doors, plays a small damped shake (default three swings, ~2 degrees, 0.5 s; a sliding door nudges a few cm). Numbers and the curve live in a `DoorRattleProfile` asset (Create > MHZE > Ultimate Door System > Door Rattle Profile); a door with none uses `DoorRattleProfile.Default`. `Rattle Amplitude` on the door scales or disables the shake. Every click restarts the shake from the stored pose; the door always ends exactly where it started, and Unlock, Open, Close or disabling cancels it and restores the pose.

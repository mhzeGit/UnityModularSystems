# NPC System

Feature-based NPC navigation, perception, targeting and presentation. Public contracts are exposed
through `Npc.Features.TryGetOperational<T>(out capability)`; NPCs keep a single `Npc` component.

## Standalone perception targets

Add `NpcPerceptionTarget` to the root containing an object's query collider. At authoring time,
assign Aim Transform or Local Aim Offset to choose the point a vision sensor checks.

`target.SetAimTransform(visibleTransform)` binds that aim point at runtime. For example, bind a
character's camera or head so a sensor looks toward its eyes rather than its feet. Root identity,
position, category, team, filtering and query colliders remain unchanged. Passing `null` restores
the authored root-local offset. The caller owns the referenced transform's lifetime.

Version 1.6.1 adds this backward-compatible binding API; existing serialized targets are unchanged.

## Checking a known visible target

`vision.TryObserve(target, out observation)` checks an operational vision feature's range, field of
view, target filters and line of sight directly. It avoids candidate overlap saturation in dense
scenes, so a known target does not disappear merely because scenery filled the discovery buffer.
It leaves continuous sensing commands and their observation events unchanged. Use `Scan` to
discover unknown targets; use `TryObserve` when the caller already owns the intended target.

Version 1.6.2 adds this backward-compatible query API.

## Facial expressions

`NpcExpressions` is an optional presentation feature. Assign an `NpcExpressionLibrary` with paths
relative to the character Animator, neutral local transforms and matching pose arrays. Author
only face bones; exclude head/neck, hair and body. Bake poses from source clips in the consuming
project's explicit editor tools. Runtime never samples full-body clips or adds another Animator.
The feature applies reversible offsets relative to the authored neutral pose in LateUpdate. It removes
the previous frame's offset before applying the current one, so the face does not drift while the
Animator continues jaw and lip movement underneath expressions; keep lip-sync and blink animation on
the Animator.

Use `npc.Features.TryGetOperational(out NpcExpressions face)`, then
`face.Show("Happy", intensity: 0.5f, duration: 2f, blendSeconds: 0.3f, owner: this)`.
Unknown ids return false without replacing the active pose. Last request wins, without a queue.
Duration 0 explicitly holds. `face.Clear(owner: this)` releases only your request; unowned Clear
clears any request. `ResetImmediate()` is for checkpoint rewind/reset. Disable/shutdown restores
neutral and clears requests. Bind paths must resolve to the actual rig; no library means no work.

With Dialog System installed, `NpcDialogExpressions` adds explicit sequence + zero-based authored
line cues, intensity and a bounded duration. Blank lines do not shift authored indices. Each cue
has a 0.3-second blend; unmapped lines and stop/replacement/completion ease back to neutral. The
component never guesses emotion from text. Disabling detaches listeners. Gameplay callers should
use a distinct owner so a dialog ending cannot clear a more recent gameplay reaction.

`NpcTalking` raises the animator's `Talking` bool while voice audio plays under the NPC. A dialog line
without a voice clip plays no audio, so on its own it leaves the speaker's mouth still. With Dialog
System installed, add `NpcDialogTalking` to the NPC and list the speaker names its lines use (for
example `Girlfriend`): while one of those lines without a voice clip is on screen it holds the talking
state (`NpcTalking.Hold(owner, true)`) for about as long as the text takes to say (Characters Per Second,
default 14; at least Minimum Seconds; never past the line's own duration), and releases it when the line
changes or the dialog ends. Voiced lines are left to the audio. Any other code can hold the state the same
way with its own owner object.

Expression offsets are removed in the scheduler early Update/FixedUpdate pass, before an Animator
can write a new face pose, and applied again in LateUpdate. This keeps a held expression visible
when a body clip changes or animated jaw/lid channels evaluate, without accumulating offsets on
unanimated facial bones. Features needing this preparation can implement `INpcPreAnimationTickable`.

## Body turns during a look

Set NpcLookAtTarget.BodyTurnThresholdDegrees to a horizontal angle (for example 90) to let a stationary NPC turn its body fully toward a look target after it passes that limit. The head continues tracking throughout. Zero (the default) keeps head-only tracking. This uses INpcRotation, yields to existing rotation commands and active navigation, and cancels its own turn when looking stops, the target changes or the feature shuts down.

### Eye tracking with the head target

`NpcLookAtTarget` automatically binds `LeftEye` and `RightEye` descendants of its head bone.
Both eyeballs track the existing `INpcLook` target after the head's LateUpdate, without changing
lid bones. No second target or controller is needed. Gaze axes are calibrated from the neutral
head forward, preserving differently oriented eye bones. Each eye converges from its own pivot.
Eye yaw/pitch are limited to 30/20 degrees, with 720 degrees/second motion; configure the eye
bone names, angle limits, speed, or Disable Eye Tracking on the feature for other rigs.
Missing eye bones preserve head-only behavior. Zero speed/limits and empty names use defaults
for older SerializeReference instances. StopLooking, target destruction and deactivation release
the eyes; deactivation restores neutral immediately. Eye neutral is the bound rest pose, as with
the head: animate the lids for blinks/expressions, rather than also animating eyeball gaze.

### Head follow drag

`NpcLookAtTarget.DragDelay` (Inspector: **Drag Delay**) is the head follow smoothing time in
seconds: `0` snaps to the current target on every tick, while larger values trail it smoothly.
Try `0.15`–`0.3` seconds for gentle drag or `0.6` for a slower response. This is exponential
following, not a fixed wait: after one delay interval the head has covered about 63% of the
remaining angle toward a stationary target. Negative values clamp to zero. The accumulated
pose survives Animator writes, retargeting continues from the current pose, and the response
is independent of frame rate. Body-turn thresholds and eye tracking remain independent;
Degrees Per Second still controls the return to rest after StopLooking. Existing serialized
features default to zero drag until a value is set.

`NpcLookAtTarget.EyesFollowInstantly` (Inspector: **Eyes Follow Instantly**) makes the eyes land on
the target every tick while looking, ignoring Eye Degrees Per Second, so a smooth head drag never
makes the gaze lag. Eye angle limits still apply, so the eyes lead a slow head and saturate at their
limits until it catches up. Returning to rest after StopLooking still uses the normal eye speed. Off by default.

## Door open gesture

`NpcDoorOpenGesture` (optional presentation feature, in the `ModularNPC.Doors` integration) makes
`NpcDoorNavigation` play a gesture before it opens a door: once the NPC stands at the closed door it
plays the clip of an `NpcDoorOpenGestureProfile` (create one with **Create > Modular NPC > Door Open
Gesture Profile**), opens the door at the profile's **Contact Point** (normalized clip time the hand
touches the door), and walks on once the door is open and the clip has finished. The clip plays in
its own playable graph over the Animator controller (the controller asset is not changed); the
profile's **Weight** curve (x: normalized clip time, y: 0 = controller only, 1 = clip only) blends it
in and out. NPCs without the feature, or with no profile or clip, open doors straight away as before.
A new destination, a warp or a deactivated feature cancels the gesture at once. Any other
`INpcDoorOpenGesture` feature (for example a procedural one) works the same way.

Version 1.9.0 adds this backward-compatible feature.

## Bone jiggle

`NpcBoneJiggle` (presentation feature, **Bone Jiggle**) adds a soft, springy follow-through to fleshy
bones - buttocks, a belly, ears - on top of whatever the Animator plays. List the bones (for example
`LeftButt`, `RightButt`); each swings about its own pivot toward a sprung copy of its tip (its child bone,
or a named **Tip Bone Name**), which lags when the body speeds up, stops or is struck, overshoots, and
settles. A still NPC does not move, and a steady walk only bobs with the steps.

| Setting | Effect |
| --- | --- |
| Weight | Overall amount; 0 is off, 1 as configured, up to 2 to exaggerate. Settable from code (`Weight`). |
| Frequency | Wobbles per second. About 3-6 reads as buttocks; lower is heavy, higher is tight. |
| Damping Ratio | Low rings for longer. 0.3 gives about two bounces; 1 never overshoots. |
| Inertia | How much the flesh lags the body. Above 1 is goofier. |
| Max Angle Degrees | Hard cap on how far a bone leaves its animated pose. |
| Snap Distance | A warp or scene placement bigger than this restarts the wobble instead of whipping. |
| per bone: Frequency Scale, Strength | Give left and right slightly different values so they drift out of sync. |
| per bone: Max Angle Degrees, Damping Ratio | Override the feature's values for this bone (0 = use the feature's). Lets one bone swing wide and bounce while the rest of the body stays heavy. |
| per bone: Position Amount | 0 (default) = the bone only swings. Above 0 it also slides with the flesh by that share of the tip's lag (about 0.3-0.6 gives buttocks a soft bounce). |
| Max Position Offset | The furthest a bone slides from its animated place, in metres. |
| Preview Kick, Preview Interval, Preview Speed | Tuning aid, off by default. Jolts every spring on a timer (alternating up and down) so you can watch and adjust the jiggle on an NPC that is standing still. |

The feature runs in LateUpdate after the Animator and removes its own rotation again before the next
Animator pass, so it works on bones the clips animate and on bones they do not. `ResetMotion()` calms
the bones at once. The spring itself is `NpcJiggleSpring` (component-free, covered by edit-mode tests).

Version 1.10.0 adds this backward-compatible feature; 1.11.0 adds Position Amount and Max Position Offset (existing bones keep rotation only); 1.12.0 adds per-bone Max Angle Degrees and Damping Ratio (0 = feature value) and raises the per-bone Strength limit to 4; 1.13.0 reads per-bone settings live (Inspector edits in Play Mode apply at once, also adding or removing a bone) and adds the Preview Kick tuning aid; 1.13.1 treats a Max Position Offset or Preview value of 0 as "default", because Unity loads settings added after a prefab was saved as 0.

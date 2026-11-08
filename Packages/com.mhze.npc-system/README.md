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

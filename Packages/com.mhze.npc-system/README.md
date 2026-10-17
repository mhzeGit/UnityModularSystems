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
The feature owns these facial local transforms in LateUpdate, including neutral; it does not
provide lip sync. Do not assign another facial animation system to the same bones.

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

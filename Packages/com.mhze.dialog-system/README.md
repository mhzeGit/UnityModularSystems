# Dialog System

Simple, adjustable Canvas dialog system for Unity. No UI Toolkit, no TextMeshPro essentials
required — the built-in uGUI `Text` and font are used so it works out of the box.

## Install

```
https://github.com/mhzeGit/mhzeModularSystems.git?path=/Packages/com.mhze.dialog-system
```

## What it contains

| Type | Role |
| --- | --- |
| `DialogLine` | One spoken line: speaker, text, auto-advance duration, optional voice-over. |
| `DialogSequence` | ScriptableObject asset holding an ordered list of lines. |
| `DialogView` | Canvas UI: a speech bubble that sizes itself to its text and shows `{Name}:"Text"`, plus a continue hint at the bottom-right of the screen. |
| `DialogManager` | Plays sequences, typewriter reveal, advance input, auto-advance, events; asks questions (`Ask`). |
| `DialogChoiceView` / `DialogChoice` | The numbered answer buttons above the bubble, and one answer (label, enabled). |
| `DialogWorkbenchWindow` | Editor-only live and completed dialog history, with persistent sequence editing during Play Mode. |
| `DialogProximityTrigger` | Starts a sequence when the target (player) comes within range, optionally only while another actor is near the trigger. |

## Quick start

1. **Tools ▸ Dialog System ▸ Setup Dialog Canvas** builds the Canvas UI and a `DialogManager`
   in the open scene and wires everything up.
2. Create a sequence: **Assets ▸ Create ▸ MHZE ▸ Dialog System ▸ Dialog Sequence**, then fill in
   the lines: speaker, text and (optionally) a duration for auto-advance.
3. Put the sequence on an NPC:
   - Add a **DialogProximityTrigger** component next to the NPC.
   - Assign the **Sequence**.
   - Leave **Target** empty to find the object tagged `Player`, or assign the player transform.
   - Set the **Radius** (metres, measured on the horizontal plane).
4. Enter Play Mode and walk close to the NPC — the dialog appears.

### Location-gated dialog

To play a sequence only when a character is standing at a specific spot (for example a patrol
waypoint), put the `DialogProximityTrigger` on the spot and set **Required Actor** to that NPC:

- The actor must be within **Actor Radius** of the trigger, and
- the player must come within **Radius** of the actor.

Because both are measured from the actor, the trigger reads as "the NPC is here and the player got
close". Leaving and returning re-arms the trigger; use **Only Once** for a single play per session.

## Look and sizing

- The speaker and the line are one piece of text in the form `{Name}:"Text"` (the name is bold and
  tinted with **Speaker Color**). Set **Combine Speaker Into Text** off on the `DialogView` to fall
  back to the old separate speaker label.
- **Dynamic Size** keeps the bubble only as big as its text: it grows in width up to **Max Width**,
  wraps, then grows in height. **Min Width** stops very short lines from collapsing to a sliver.
- **Center Text** centres the line horizontally and vertically. **Padding** is the inner margin
  between the text and the bubble edge.
- The bubble is sized from the text currently on screen, so it keeps up with the typewriter reveal.
  It grows instantly to fit the text it already shows — never narrower, so the line cannot re-wrap
  mid-transition — and eases back down when a new line is smaller. Turn **Smooth Resize** off to
  resize instantly in both directions and tune **Resize Speed** for the shrink easing.

## Appearance

- **Text Color** tints the spoken line (the speaker name keeps **Speaker Color**), and
  **Background Color** tints the bubble itself. Both are applied every time a line changes, so they
  can be adjusted at runtime.

### Rounded corners (`RoundedCorners`)

Rounded corners are a standalone, reusable component rather than a dialog setting:

- Attach **RoundedCorners** to any GameObject that has a UI **Image** and it replaces the sprite with
  a procedural, 9-sliced rounded rectangle — no art asset, works at edit time and at runtime.
- **Radius** is in UI pixels and previews live as you drag it in the inspector. `0` restores the
  original sprite, as does disabling/removing the component (**Restore On Disable**).
- Add it from **Add Component ▸ UI ▸ Rounded Corners (Procedural)**, the **Regenerate Rounded
  Sprite** button in its inspector, **Tools ▸ Dialog System ▸ Add Rounded Corners To Selected UI
  Images**, or **GameObject ▸ UI ▸ Rounded Corners (Procedural)**.
- The dialog setup wizard adds the component to the dialog panel, so the bubble's roundness is tuned
  on that component in the inspector.

## Advancing

- `Space`, `Enter` or left mouse button advance by default. Assign an **Advance Action** on the
  `DialogManager` to use a custom Input System action instead.
- While text is typing out, advancing reveals the rest of the line; advancing again moves on.
- Set a line's **Duration** above `0` and the manager auto-advances it once the duration elapses.
- Set `DialogManager` **Characters Per Second** to `0` to show every line instantly.
- While a line is fully shown, a **ContinueHint** label appears in the bottom-right corner of the
  screen ("Click LMB to continue"). Edit the wording on that label — `DialogView` only shows and
  hides it, and it is independent of the bubble's size.

## Questions and answers (`DialogManager.Ask`)

```csharp
var answers = new[] { new DialogChoice("Sure"), new DialogChoice("Hell no", enabled: false) };
DialogManager.Instance.Ask("Niko", "Do you wanna play with me?", answers, index => { /* 0 = Sure */ });
```

- The question is typed out in the speech bubble like a line (speaker and text). Once it is fully shown, the
  answers appear as rounded buttons stacked above the bubble, numbered 1, 2, 3... (`DialogChoiceView`). Advancing
  (Space, Enter, click) while it types shows it at once; it never skips the question.
- The player picks with the mouse (hover highlights, click picks), the number keys, Up/Down or W/S plus Enter or
  Space, or a gamepad (d-pad or left stick, south button). Input is ignored for **Input Delay** (0.3 s) after the
  answers appear, so a key held to skip the question cannot pick an answer.
- A `DialogChoice` with `Enabled = false` is shown greyed out and struck through. Trying to pick it shakes the
  button (and plays **Choice Denied Sound**); it is never picked.
- The mouse cursor is freed and shown while the answers are up and put back afterwards (**Unlock Cursor** on the
  view). Turning off the player's own controls during the question is the caller's job, except the camera: with
  the First Person Controller package (1.7.0 or newer) installed, the view stops following the mouse while the
  answers are up, and a click beside them does not recapture the cursor (`Runtime/Integrations/FirstPerson`).
  `DialogChoiceView.ShownChanged` tells other camera or input systems to do the same.
- Picking an answer hides the bubble and the buttons, raises `ChoiceMade(index)` and calls the `Ask` callback.
  `IsChoosing`, `CurrentChoices`, `Choose(index)` (pick as if the player did; false for a disabled answer) and
  `CancelChoice()` are available for scripting and tests.
- Playing a sequence, `Stop()`, a new `Ask` or disabling the manager drops a waiting question without an answer and
  raises `ChoiceCancelled`: ask again if the answer is still needed.
- With no **Choice View** assigned on the `DialogManager`, one is built at runtime under the bubble's panel, in the
  line's font and size. Its colours, sizes and spacing are on the `DialogChoiceView` component. Optional
  **Choice Move / Pick / Denied** sounds on the manager play on a 2D source.

## Optional extras

### Live Dialog Workbench

Open **Tools ▸ BlahBlah Family ▸ Dialog ▸ Live Dialog Workbench** while the game is running. It
records the line currently on screen and completed lines in separate lists. Select an authored
sequence line, edit its speaker or text, then choose **Save & Update Live Dialog** for the current
line or **Save to Dialog Asset** for a line that has already played. Changes are written to the
sequence asset and saved immediately; editing the current line also refreshes the visible dialog.
Runtime-only lines created with `DialogManager.Play(speaker, ...)` are still captured, but cannot be
saved because they have no authored asset.

The history is editor-session memory (up to 500 lines); it does not write a transcript asset.

- `DialogLine.voiceOver` plays through the `DialogManager`'s **Voice Source** (an `AudioSource`).
  For a positional NPC voice, put that `AudioSource` on the character's head bone (spatial blend
  `1`, short min/max distances) and assign it to the manager — the line is then heard from the head.
- `DialogManager.Play("Girlfriend", "Hello!", "Welcome!")` plays quick runtime lines with no asset.
- `DialogManager.Instance`, `IsPlaying`, `CurrentSequence`, `DialogStarted`, `DialogFinished`
  and `LineChanged` are available for scripting.
- `DialogProximityTrigger` supports one-shot or repeat-with-cooldown playback, an optional facing
  cone, an optional required-actor gate (see above), and `Stop` when the target walks away.
  `ResetState()` makes a trigger that has already played fire again, and `HasPlayed` reports whether
  it has, which debug tools use to jump backwards through a story.

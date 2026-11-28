# Input Prompt System

On-screen input prompts ("Press [E] to open") that follow the player's device: keyboard and mouse or
gamepad, with binding icons from an `InputBindingIconLibrary`. Prompts are `InputPromptDefinition`
assets gathered in an `InputPromptCollection`, shown by `InputPromptManager` at the left, centre or
right anchor of an `InputPromptUI` canvas.

## Hold progress ring (1.1.0)

For hold-to-interact actions, a ring around the centre of the screen fills while the key is held and
shows how much of the hold is left. Keep the normal prompt (with its key icon) on screen and drive the
ring from the hold:

```csharp
promptManager.ShowHoldProgress(holdSeconds); // hold started: ring pops in and fills over holdSeconds
promptManager.CompleteHoldProgress();        // hold finished: ring bursts outward and fades
promptManager.CancelHoldProgress();          // let go early: ring drains, shrinks and fades
promptManager.SetHoldProgress(0.4f);         // optional: drive the fill yourself (stops the auto fill)
```

The fill advances with game time (`Time.deltaTime`), like a hold timer; the pop, burst and drain use
real time so they finish even if the completed interaction pauses the game.

`InputPromptUI` creates the ring (`InputPromptHoldRing`) at the centre of its root canvas the first
time it is needed, or uses the one assigned to its `Hold Ring` field. The ring generates its own
anti-aliased textures (dark outline, track, radial fill, rounded caps), so it needs no art.

### Tuning: `InputPromptHoldRingProfile`

Size, colours and the three motions live in an `InputPromptHoldRingProfile` asset
(`Create > Input Prompts > Hold Ring Profile`). Assign it on the ring, or name it
`InputPromptHoldRingProfile` and put it in any `Resources/` folder; without one, built-in defaults are
used. The profile is read when a hold starts, so edits apply from the next hold, also in Play Mode.

| Field | Meaning |
| --- | --- |
| `diameter`, `thickness`, `outlineWidth`, `offset` | Ring size in canvas units (1920x1080 reference), offset from screen centre |
| `outlineColor`, `trackColor`, `fillColor` | Dark outline, empty track, fill colour by progress (gradient, left = start) |
| `appearDuration` + `appearCurve`, `appearStartScale` | Pop-in. y: 0 = invisible at the start scale, 1 = full size and opaque; above 1 overshoots |
| `completeDuration` + `completeCurve`, `completeScale` | Burst. y: 0 = full ring at rest, 1 = grown to the complete scale and faded out |
| `cancelDuration` + `cancelCurve`, `cancelScale` | Drain. y: 0 = as it was when let go, 1 = drained, shrunk and faded out |

The fill itself is never eased: it always shows the true share of the hold.

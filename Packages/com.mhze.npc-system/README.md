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

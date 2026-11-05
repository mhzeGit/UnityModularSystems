# Wall Break System

Put a crack on **any** wall and break it with a tool. Nothing about the wall's texture, UVs or shader matters: when the last blow
lands, the wall's *own mesh* is sliced into irregular pieces (a 3D Voronoi pattern: its material, UVs and baked-lightmap UVs are kept, and
the faces cut open continue the wall's texture), the pieces that fill the hole tumble to the floor as rubble, and the rest of the wall stays as one mesh with a ragged rim and a
collider the player can walk through. The crack is drawn along the seams between the pieces, so the wall breaks where it cracked.

## Make a wall breakable
1. A wall = a mesh with a `MeshRenderer` and a collider. Its mesh needs **Read/Write enabled** (the inspector has a button).
   Any slab-like wall works (a scaled cube, a modular wall piece, a ProBuilder wall); the thin axis is found automatically.
2. Add **Breakable Wall** (`MHZE/Wall Break/Breakable Wall`). The orange box in the Scene view is the hole. Optional `Hole Center`
   marker: move it to place the hole and the crack's centre (empty = the middle of the wall, opening at the floor).
3. For a held tool, also add **Breakable Wall Use Target** on the collider object (needs the Use System package; `Reset` fills the
   tool id `Axe` and the prompt).

## Make a tool
A held axe is a pickable item (Pickup System) with a `UsableItemBase` whose tool id is `Axe`, `Use Impact Delay` = how far into the
swing the blade lands, plus an optional **Chop Tool** (swing sound). Give its Item Hold Profile `UseTrigger = Attack` to play the
arm's swing.

## From code and other systems
`wall.Strike(point, normal, power)` counts one blow (anything can strike: a trap, an explosion). Hooks: `Struck(hits, needed, point)`,
`Broken(point)`, the UnityEvents `OnStruck / OnMissed / OnBroken`, and `HitLoudness` / `BreakLoudness` (for noise logic).
`ResetWall()` rewinds, `SetBroken()` puts it in the broken state with no debris (skip).

## Notes
- Hole size, chunk size, edge roughness, seed, crack width, debris (speed, lifetime, solid rubble), loosen-before-break and audio are all inspector fields.
- The remainder keeps the original renderer's lightmap index and scale/offset, so baked lighting stays consistent.
- The shader `MHZE/WallBreak/Soft` is URP only (crack ribbons and dust puffs). It lives in `Runtime/Resources` so builds include it.

## How it works (for the next developer)

| File | Job |
| --- | --- |
| `Runtime/BreakableWall.cs` | The component: strike counting, the break sequence (`Loosen`, `Collapse`), remainder mesh, chips, dust, audio, events |
| `Runtime/WallFracture.cs` | Builds the Voronoi pieces once: sites, per-piece clipped surface, cut faces between neighbours, seam lines for the crack, which pieces fall |
| `Runtime/WallMeshSlicer.cs` | Readable mesh copy, mesh builder, polygon clipping against planes and boxes, axis-aligned cut faces |
| `Runtime/WallCrackMesh.cs` | The crack ribbons along the seams; each carries a "birth" value the shader compares with its progress, so the crack grows |
| `Runtime/WallDebris.cs` | On a fallen piece: dust and thud on first hard landing, stops blocking the player once settled, shrinks after its lifetime |
| `Runtime/Resources/MHZE_WallBreak_Soft.shader` | URP unlit transparent used for the crack and dust (in `Resources` so builds include it) |
| `Runtime/Integrations/Use/*` | Use System glue (`BreakableWallUseTarget`, `ChopTool`); compiled only when the use-system package is installed |
| `Editor/BreakableWallInspector.cs` | Warns about an unreadable mesh and offers the Read/Write button |
| `Documentation~/FilmLab/` | How to test and film a break in a scratch project (see its README) |

Things that bit during development: the wall mesh must be readable at runtime; use `Rigidbody.linearVelocity` on Unity 6;
`Object.GetInstanceID()` is an error in Unity 6.6 (the seed is made from the wall's name and position instead); the number of nearest
cutting planes per piece (64) must stay above the neighbours a piece can really have, or outer pieces overlap the doorway; pieces start
inside the wall's original colliders, so they ignore them until the last blow disables them.

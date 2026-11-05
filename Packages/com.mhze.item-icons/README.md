# Item Icons

An editor tool that draws an **icon for every pickable item** from the item's own model and puts it on the item data
(`PickableItemData.ItemIcon`). The picture goes through the project's own render pipeline, so the materials, shaders and
post effects look the way they do in the game, under a fixed studio light, upright, framed and centred, on a transparent
background. Run it from a menu, from code or from the command line; it is safe to run again whenever a model changes.

Needs `com.mhze.pickup-system` (the item data). Editor only: nothing of it is in a build.

## Run it

| How | What |
| --- | --- |
| Menu `Tools > MHZE > Item Icons > Generate All Icons` | every item data asset in the search folders |
| `... > Generate Missing Icons` | only items whose `ItemIcon` is empty |
| `... > Report (What Would Be Drawn)` | a dry run: the model each item would be drawn from and where the file would go |
| `... > Select Settings (Create If Missing)` | the settings asset (made in `Assets/ItemIcons` when the project has none) |
| Right click item data > `Item Icons > Generate Icon For Selected Items` | just those items |
| Code | `MHZE.ItemIcons.ItemIconGenerator.GenerateAll()`, `.GenerateAll(null, onlyMissing: true)`, `.Generate(items)`, `.Plan()`, `.Preview(data)`; each returns an `ItemIconReport` (`ToString()` is the text, `HasProblems` the verdict) |
| Command line | `Unity -batchmode -projectPath <project> -executeMethod MHZE.ItemIcons.ItemIconBatch.GenerateAll` (also `GenerateMissing`, `Plan`). **No `-nographics`**: the icons are drawn on the graphics card. The editor must not already have the project open. The report goes to the log and to `Logs/ItemIcons/report.txt` (or the file in `ITEM_ICONS_REPORT`); the exit code is 0, or 1 when an item failed |

An agent that can run code in the open editor (Unity MCP `execute_code`) calls
`return MHZE.ItemIcons.ItemIconGenerator.GenerateAll().ToString();` and reads the answer.

## What it does for each item

1. **Finds the model.** In this order: a prefab named in the settings' overrides, the item data's own `Item Prefab`, a prefab asset
   with a component that points at the item data (the item inside it, even when it is a child), then a scene that does (the
   scene is opened beside the open ones and closed again; nothing in it changes). An item with no model (a generic item data
   nothing uses) is reported as *without a model*, not as an error.
2. **Makes a clean copy** in a scene of its own (a small empty scene of the package, far from the world's origin, so nothing in the
   open scenes can show): scripts off, lights and text labels removed, particle systems run for a moment, standing as it
   stands in the world but with its turn about the vertical forgotten.
3. **Turns it to a good side.** Looking straight down, the long side of its footprint is turned across the picture. The camera then
   looks from the front right and above (`Yaw`, `Pitch`).
4. **Lays long things on the diagonal.** When the silhouette is at least 1.8 times longer than wide, the camera is rolled so that
   the item runs from the top left to the bottom right (a key, a carrot, a torch). Compact items stay upright.
5. **Frames it** by measuring the picture, not the mesh: centred, filling the picture less the padding.
6. **Lights it.** One key light from the upper left and a cool fill from the lower right, both fixed to the camera so every icon is
   lit from the same side, plus a flat ambient colour.
7. **Takes the picture twice, on black and on white**, and works the transparency out from the difference. That is exact whatever the
   render pipeline's post effects do. It draws at `Supersample` times the size and shrinks.
8. **Adds a contour** (a few pixels of a near-black line), writes the PNG (only when the bytes changed, so a second run changes
   nothing), imports it as a **Sprite** (clamped, mip maps on) and sets `ItemIcon` on the item data.

## Settings (`ItemIconSettings`)

Make one with `Assets > Create > MHZE > Item Icons > Settings`. Without one the tool uses the defaults and writes to `Assets/ItemIcons`.

| Group | Fields |
| --- | --- |
| Output | `Output Folder`, `File Prefix` (`Icon_`), `Remove From Name` (`PickableItemData_` is cut from the front of the data name), `Size` (256), `Supersample`, `Mip Maps` |
| Where to look | `Search Folders` (default `Assets`), `Search Scenes` |
| View | `Yaw`, `Pitch`, `Field Of View` (0 = orthographic), `Padding`, `Align Longest Side` |
| Tilt | `Tilt`, `Tilt From` (how long before it is tilted), `Tilt Angle` |
| Light | key and fill colour, intensity and direction, ambient colour |
| Contour | `Contour Pixels`, `Contour Color` |
| Studio | `Studio Position`, `Quality Level` (the quality level, so the render pipeline asset, used for drawing), `Particle Seconds` |
| Items | one **override** per item: `Skip`, `Source` (draw this prefab instead), `Extra Rotation` (turn the item before it is shown: the fix for a prefab that lies the wrong way), own `Yaw` and `Pitch` (`Override View`), `Tilt` (Auto, Off, Fixed with an angle), `Zoom` |

The file for an item is `Output Folder / File Prefix + name without the prefix + .png`.

## Good to know

- It draws through whatever render pipeline is active, so an outline or posterize pass the project adds to its camera shows in the
  icons, subject to that pass's own layer rules. The camera is made without multisampling (the supersampling smooths instead),
  because some post passes cannot take a multisampled target.
- Needs a graphics device. In batch mode that means no `-nographics`; the tests skip themselves without one.
- Unity cannot add a scene beside one that was never saved. If the only open scene is untitled and has no changes, the tool replaces it
  for the length of the run and opens a fresh untitled scene afterwards. If it is untitled **with** changes, the tool stops and says
  to save it.
- Items placed only at runtime (spawned by a script) have no model to find; name a prefab for them under `Items`.
- The report lists every item: `Generated`, `Unchanged`, `Skipped`, `NoSource`, `Failed` (and `Planned` in a dry run).
- A model with `TEMP_` placeholder art gets a placeholder icon. Run `Generate All` again once the art changes.

## Tests

`Tests/Editor/ItemIconTests.cs` draws real icons in a scratch folder: transparency, framing, centring, the diagonal, the light, the
second run changing nothing, the sprite import, items inside a prefab, items placed in a scene (opened and closed again), the
untitled scene, an override, a model with nothing to draw failing alone. Add the package to `testables` and run EditMode with the
graphics device on.

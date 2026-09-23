# Basic Menus

Generic, modular, art-free menu framework for Unity. Drop it into any project to get a main menu, pause menu, tabbed options menu (with input rebinding), info and confirmation screens — all driven by plain uGUI controls, with mouse/gamepad detection, cursor handling and automatic back-button routing included.

The package ships **no art assets**: every generated control is a solid-color uGUI element you can restyle or replace. Runtime code never references sprites, fonts or scenes.

## Features

- **`UIScreen` base class** — show/hide any panel, with open/close C# events and UnityEvents.
- **`UIBackRouter`** — Escape / gamepad B automatically closes the top-most screen (priority based). No wiring per screen.
- **`UIInputModeDetector`** — switches between Pointer (mouse) and Navigation (keyboard/gamepad) modes, keeps EventSystem selection clean and routes cursor state through `UICursorStateRouter`.
- **Main menu** — Play / Continue / Options / Info / Quit buttons with an animated slide to sub-panels. All buttons optional; gameplay hooks are UnityEvents.
- **Pause menu** — freezes time, Resume / Restart / Options / Main Menu / Quit (all optional), scene loading built in, safety restore of `Time.timeScale`.
- **Options menu** — any number of tabs, Gameplay (sensitivity, invert Y), Video (quality, window mode, resolution, VSync, frame rate), Audio (master/music/SFX/dialog), Controls (full input rebinding rows). Every field is optional; unassigned controls are skipped.
- **Info & Confirmation screens** — simple closeable panel and a blocking yes/no popup.
- **Button effects** — `UIButtonEffects` state machine with scale/color modules, plus custom effect base class.
- **UI sounds** — optional `UISoundProfile` + pooled `UIAudioManager` (bring your own clips).
- **Editor wizard** — builds complete, wired hierarchies in the open scene with one click (`Tools > Basic Menus`).

## Requirements

- Unity **2022.3** or newer.
- **Input System** package (`com.unity.inputsystem`, 1.8+). Project-wide actions are used automatically when no action references are assigned.
- **TextMeshPro** for labels and dropdowns. In Unity 6 it ships with uGUI; import `Window > TextMeshPro > Import TMP Essential Resources` if text does not render.

## Quick start (editor wizard)

1. Open the scene that should contain the menus.
2. Run one of:
   - `Tools > Basic Menus > Create Main Menu`
   - `Tools > Basic Menus > Create Pause Menu`
   - `Tools > Basic Menus > Create Options Panel`
3. The wizard creates the canvas, controls, and an always-alive `[Basic Menus System]` object (input mode detector + back router), then wires every reference.
4. Restyle the generated controls (they are plain Images, Buttons, Sliders, Toggles, TMP dropdowns) or replace them with your own.
5. Wire the main menu's `On Play Clicked` event to your scene loading, and the pause menu's scene names in the inspector.

> The system object persists across scenes (`DontDestroyOnLoad`), so it only needs to exist in the first scene of the game.

## How it works

### Screens

Every menu panel derives from `UIScreen`. A screen either toggles its own GameObject or an assigned **content panel** (used by the pause menu so its GameObject can stay active and keep listening for input).

| Class | Purpose | Back priority |
| --- | --- | --- |
| `MainMenuScreen` | Root menu with animated sub-panels | 40 |
| `PauseScreen` | In-game pause (time scale + scene changes) | 0 |
| `OptionsScreen` | Tabbed settings | 60 |
| `InfoScreen` | Static info panel | 60 |
| `ConfirmationScreen` | Blocking yes/no popup | 200 |

### Back routing

`UIBackRouter` listens for **UI/Cancel** (Escape / gamepad B). Screens register themselves automatically and are asked in descending `BackPriority`; the first one that returns `true` from `OnBack()` consumes the input. A screen is never wired manually. If no action reference is assigned, the router falls back to the project-wide `UI/Cancel` action.

You can also call `backRouter.HandleBack()` from a "Back" button, and `UIBackRouter.SuppressBackThisFrame()` right after opening a screen with the same key that is also Cancel.

`PauseScreen` resolves its pause input in this order: assigned Pause action → project-wide `Pause` action → project-wide `UI/Cancel`. So Escape / gamepad B opens and closes the pause menu out of the box; assign a dedicated Pause action to keep Cancel exclusively for back routing.

### Input mode & cursor

`UIInputModeDetector` (always-alive) tracks whether the player is using the mouse (`Pointer`) or keyboard/gamepad (`Navigation`):

- Pointer mode: hover highlights buttons, EventSystem selection is cleared.
- Navigation mode: the last focused control is re-selected so keyboard/gamepad navigation always has focus.
- Cursor visibility is owned by `UICursorStateRouter`: menus request a cursor state with a priority; when no request is left, the configured gameplay defaults (hidden + locked by default) are restored.

Read the mode from anywhere with `UIInputState.Current` and subscribe to `UIInputState.Changed`.

### Options & persistence

`OptionsScreen` applies changes immediately and stores them in `PlayerPrefs`:

| Setting | Key | Notes |
| --- | --- | --- |
| Sensitivity | `Opt_Sensitivity` | `OnSensitivityChanged` |
| Invert Y | `Opt_InvertY` | `OnInvertYChanged` |
| Quality level | `Opt_Quality` | `OnQualityChanged` |
| Window mode | `Opt_WindowMode` | `OnWindowModeChanged` |
| Resolution | `Opt_ResW` / `Opt_ResH` | `OnResolutionChanged` |
| VSync | `Opt_VSync` | `OnVSyncChanged` |
| Frame rate | `Opt_FrameRate` | `OnFrameRateChanged` (`-1` = unlimited) |
| Volumes | `Opt_VolMaster`, `Opt_VolMusic`, `Opt_VolSFX`, `Opt_VolDialog` | `OnVolumeChanged` |
| Input rebinds | `Opt_InputRebinds` | JSON of binding overrides per action asset |

Saved video settings are re-applied on startup by `BasicMenuSettings.ApplySavedVideoSettings()` (called automatically before the first scene loads). For audio, either assign an `AudioMixer` on the options screen (exposed parameters `MasterVolume`, `MusicVolume`, `SFXVolume`, `DialogVolume`) or call `BasicMenuSettings.ApplySavedAudioSettings(mixer)` from your audio bootstrap.

Gameplay systems react through the static events, e.g.:

```csharp
private void OnEnable() => OptionsScreen.OnSensitivityChanged += HandleSensitivity;
private void OnDisable() => OptionsScreen.OnSensitivityChanged -= HandleSensitivity;

private void HandleSensitivity(float value) => lookSensitivity = value;
```

### Input rebinding

Add one `InputRebindRowUI` per rebindable action inside the options screen (or duplicate the one the wizard creates). Rows are discovered automatically through `GetComponentsInChildren`; no list to maintain.

- Assign an `InputActionReference` (and optionally a `BindingIconLibrary` for button icons).
- Rebinding supports composite bindings, cancel keys (Escape), clearing a binding (Backspace/Delete), and optional duplicate prevention.
- Overrides are saved and reloaded per action asset through the options screen.

### Button effects & sounds

- `UIButtonEffects` tracks Normal / Hovered / Selected / Pressed / Disabled for any Selectable and forwards state to effect modules: `UIScaleEffect`, `UIColorEffect`, or your own subclass of `UIButtonEffect`.
- `UIButtonSFX` plays hover/click/select/press/release clips from a `UISoundProfile` through the pooled `UIAudioManager`. Both are optional and independent of the rest.

### Confirmation popup

```csharp
confirmScreen.Open("Quit game?", "All unsaved progress will be lost.");
confirmScreen.OnConfirmed.AddListener(Quit);
```

The popup has priority 200, so back input cancels it before anything else reacts.

## Creating a custom screen

```csharp
using MHZE.BasicMenus;
using UnityEngine;
using UnityEngine.UI;

public class InventoryScreen : UIScreen
{
    [SerializeField] private Button closeButton;

    private void Start() => closeButton.onClick.AddListener(Close);

    public override int BackPriority => 70;

    public override bool OnBack()
    {
        if (!IsOpen) return false;
        Close();
        return true;
    }
}
```

Open it with `inventoryScreen.Open()` / `Toggle()` / `Close()`. Escape and gamepad B work automatically.

## Folder layout

```
Runtime/
  Core/        UIScreen, UIBackRouter, UIInputModeDetector, UIInputState,
               UICursorStateRouter, BasicMenuSettings
  Screens/     MainMenuScreen, PauseScreen, OptionsScreen, InfoScreen,
               ConfirmationScreen
  Components/  UIButtonGroup, SliderHandler, ToggleHandler,
               SelectableCustomNavEvents, TabSelectHelper
  Effects/     UIButtonEffects, UIButtonEffect, UIScaleEffect, UIColorEffect,
               UIButtonSFX
  Audio/       UIAudioManager, UISoundProfile
  Rebinding/   InputRebindRowUI, BindingIconLibrary
Editor/
  BasicMenusSetupWizard
```

# mhzeModularSystems

Modular Unity packages for first-person controller, interaction systems, and more.

## Packages

### First Person Controller

```
https://github.com/mhzeGit/UnityModularSystems.git?path=/Packages/com.mhze.firstperson-controller
```

Modular, compact first person controller with smooth movement, camera, crouch, jump, force look, and force move.

### Freeform Camera

```
https://github.com/mhzeGit/UnityModularSystems.git?path=/Packages/com.mhze.freeform-camera
```

Free-fly camera controller with WASD movement, mouse look, speed control, and optional collision. Editor & development build only.

### Gear System

```
https://github.com/mhzeGit/UnityModularSystems.git?path=/Packages/com.mhze.gear-system
```

Modular gear system for rotating mechanisms, mechanical linkages, and drive train simulations.

### Input Prompt System

```
https://github.com/mhzeGit/UnityModularSystems.git?path=/Packages/com.mhze.input-prompt-system
```

Dynamic input prompt UI system that detects keyboard+mouse vs gamepad, looks up binding icons from an icon library, and displays contextual prompts with text and sprites on configurable screen anchors.

### Interact System

```
https://github.com/mhzeGit/UnityModularSystems.git?path=/Packages/com.mhze.interact-system
```

Modular interaction system with raycast-based detection, IInteractable/IInteractor interfaces, instant and hold-to-interact support, events for prompt UI integration.

### Mouse Physics Grab System

```
https://github.com/mhzeGit/UnityModularSystems.git?path=/Packages/com.mhze.mouse-physics-grab-system
```

Modular physics-based mouse grab/drag system for dragging Rigidbody objects with spring-damper forces, scroll depth adjustment, and configurable constraints.

### Pickup System

```
https://github.com/mhzeGit/UnityModularSystems.git?path=/Packages/com.mhze.pickup-system
```

Modular pickup system for picking up, holding, and dropping objects with configurable hand/item offsets, physics management, singleton access, and input-driven drop support.

### Use System

```
https://github.com/mhzeGit/UnityModularSystems.git?path=/Packages/com.mhze.use-system
```

Modular use system with raycast-based target detection, IUsable/IUsableTarget interfaces, configurable tools and targets, cooldown and impact delay support, and event-driven prompt integration.

### Throw System

```
https://github.com/mhzeGit/UnityModularSystems.git?path=/Packages/com.mhze.throw-system
```

Modular throw system with charge-based force, camera-forward direction, configurable exponential charge curve, input-driven throw and cancel, and event-driven integration.

### Ultimate Door System

```
https://github.com/mhzeGit/UnityModularSystems.git?path=/Packages/com.mhze.ultimate-door-system
```

Configurable door system with procedural, Animation component, Animator, and no-animation modes. Supports rotating and sliding doors, password-based locking with SHA256 hashing, auto open/close triggers with direction detection, configurable movement curves, and event-driven integration for open, close, lock, unlock, and lock-failed events.

### Ultimate Trigger System

```
https://github.com/mhzeGit/UnityModularSystems.git?path=/Packages/com.mhze.ultimate-trigger-system
```

Configurable trigger zone with tag-based filtering (selectable via dropdown), UnityEvent bindings, and built-in collider debug previews. Supports enter/stay/exit events with box/sphere/capsule/mesh collider visualisation.

### Roughness Detection

```
https://github.com/mhzeGit/UnityModularSystems.git?path=/Packages/com.mhze.roughness-detection
```

GPU-based surface roughness detector that raycasts forward from any GameObject and samples the exact per-pixel roughness from the material's metallic/specular gloss maps at the hit point. Features a custom inspector with live roughness preview bar, Scene view ray + colored label visualization, and a runtime GUI overlay.

### Cylinder Collider

```
https://github.com/mhzeGit/UnityModularSystems.git?path=/Packages/com.mhze.cylinder-collider
```

Procedural cylinder collider component with configurable radius, height, subdivision count (vertex quality), and axis direction. Uses a convex MeshCollider internally for optimal PhysX performance. Custom inspector with Scene view editing handles for height and radius.

## Mediator Scripts

Mediator scripts in `Assets/Scripts/` connect standalone packages together. Attach these to a GameObject in your scene and assign the relevant system references in the Inspector.

| Script | Purpose |
| --- | --- |
| **[PickupThrowMediator](./Assets/Scripts/PickupThrowMediator.cs)** | Connects PickupSystem and ThrowingSystem — forwards picked-up items to the throw system and drops items when thrown. |
| **[UseInputPromptMediator](./Assets/Scripts/UseInputPromptMediator.cs)** | Connects UseSystem to InputPromptManager — shows/hides use prompts when a usable target is found or lost. |
| **[InteractInputPromptMediator](./Assets/Scripts/InteractInputPromptMediator.cs)** | Connects InteractSystem to InputPromptManager — shows/hides instant and hold-to-interact prompts with dynamic prefixes/suffixes from the interactable. |

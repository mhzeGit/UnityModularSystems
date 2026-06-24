# Gear System — What It Does

The goal is a single component you can put on any object that already has a Rigidbody. This component turns that object into a gear that can connect to other gear objects in the scene. When you connect two gears together, rotating one makes the other rotate too — exactly like real mechanical gears.

---

## How It Works in Simple Terms

**The component itself** lives on each gear object. You can set how many teeth the gear has, what size it is (its "module" — the standard measurement that determines tooth size), and what type of gear it is. These settings automatically work out the gear's pitch diameter (the imaginary circle where teeth from two gears touch).

**Connecting gears** is done through a list on the component. You drag other gear objects into this list to say "this gear meshes with that one." The system automatically figures out the gear ratio from the tooth counts. So if gear A has 20 teeth and gear B has 40, the system knows B rotates at half the speed of A and in the opposite direction.

**Different gear shapes** are supported:
- Standard spur gears — normal flat gears with straight teeth, the most common type
- Helical gears — teeth are cut at an angle, making them run smoother and quieter, but they also push sideways
- Bevel gears — cone-shaped gears that let you change the axis of rotation, typically used to turn a corner (like in a hand drill)
- Rack-and-pinion — a gear that rolls along a flat toothed bar, converting rotation into straight-line movement

**The physics** works in three ways:
- **Kinematic mode** — driven gears follow the driver perfectly with no slip. Good for simple animations and decorative mechanisms.
- **Dynamic mode** — full physics simulation. Torque is transferred from gear to gear, friction slows things down, and if you apply too much torque the gear can slip (simulating stripped teeth). You can set the efficiency of each mesh (how much power is lost), the amount of backlash (looseness between teeth), and friction values.
- **Hybrid mode** — starts kinematic but switches to dynamic when torque gets high enough. A practical middle-ground.

**Driving the system** — you can apply a motor torque to any gear to make it spin, or set a target RPM and let the system work out the torque needed. Gears with no motor input are driven purely by their connections to other gears.

**What happens under the hood** — every physics step, the system looks at all the gear components in the scene, figures out which ones are connected to which, sorts them into the right order (power flows from drivers to driven gears), then calculates the torques and rotations and applies them through each gear's Rigidbody.

---

## What the User Sees and Does

**In the Inspector**, the component shows all the settings grouped into clear sections: geometry (teeth, size, type), physics (torque, friction, efficiency), connections (the list of linked gears), and debug options. There are buttons to automatically find and connect nearby gears that are close enough to mesh, to validate that the whole gear train makes sense (correct ratios, no interference), and to realign the teeth phase so everything starts in the right position.

**In the Scene view**, while the object is selected, you see visual helpers: circles showing each gear's pitch diameter, lines showing where teeth make contact, and optionally the tooth profile itself.

**A separate window** shows a graph of every gear in the scene and how they're connected. It displays the direction power is flowing, each gear's current RPM, and highlights any gear that's stalled or overloaded.

**An optional extra component** can generate a 3D mesh of the gear teeth automatically — so you don't need to model gear shapes yourself. It creates the right tooth shape for spur, helical, bevel, or rack gears based on the settings.

---

## What the System Handles

- Multiple gears connected in a chain (A drives B, B drives C)
- Gears with multiple connections (one gear driving two others)
- Branching gear trains that split and rejoin
- Cycle detection — if you accidentally connect gears in a loop, it warns you

**Edge cases it accounts for:**
- If no gear in a chain has motor power, everything holds still through the Rigidbody's built-in constraints
- If there are multiple disconnected groups of gears in the scene, each group is solved independently
- If the gear ratio is extreme (over 10:1), it warns about potential instability
- Prefabs and scene loading work correctly — gears register themselves when enabled and unregister when disabled

---

## A Typical Workflow

1. Create two cylinder objects, add a Rigidbody and the gear component to both
2. Set one to 20 teeth and the other to 40 teeth
3. Position them so their pitch circles just touch (the system shows you where these are)
4. In the first gear's connections list, drag in the second gear
5. Click "align phase" so the teeth line up properly
6. Apply a motor torque to the first gear
7. The second gear rotates at exactly half speed in the opposite direction, with realistic torque transfer, friction loss, and backlash if those are enabled

# Little Shape Engineer: Shapeonaut Rescue

**Build, Rotate, Test, and Fix!**

Little Shape Engineer: Shapeonaut Rescue is a child-friendly desktop Unity game for spatial learning. The current default build is the V2 prototype from `Assets/Game/Scripts`: Nova explores a soft diorama planet, enters glowing Build Zones, and repairs structures with six basic shapes:

- Cube
- Rectangular Prism
- Plate
- Ramp
- Triangular Prism
- Cylinder

Every build in the game is made from these simple shapes.

## Open the Project

1. Open this folder in Unity Hub.
2. Use Unity 2022.3 LTS or newer.
3. Open `Assets/Scenes/Main.unity`.
4. Press Play.

## Current V2 MVP Flow

- Start Screen
- Planet Exploration
- Build Zone Interaction
- Level Briefing
- Build Mode
- Run Test
- Repair Feedback
- Level Complete
- Data Export

The V2 experience includes T0 tutorial plus L1-L9 official repair levels.

## Older Prototype Screens

The older immediate-mode prototype scripts are still kept in `Assets/Scripts` for reference, but `GameBootstrap` now starts `ShapeonautRescueGame` from `Assets/Game/Scripts/Core`.

Older screens included:

- Choose Your Engineer
- Shape Library
- World Map
- Level Select
- Progress
- Pause / Help / Settings

## Child-Friendly Controls

- Drag a shape from the bottom shelf to the 3D grid.
- Drag a placed shape to move it.
- Click `Rotate` or press `R` to turn the selected shape.
- Click `Test` or press `Enter` to check the build.
- Click `Focus` to shrink the side panels, then click `Show` to bring them back.
- Click `3D View` or press `V` to cycle clear camera angles.
- Use the `-` / `+` buttons beside `Layer` or press `Q` / `E` to choose the build height.
- Right-drag to rotate the camera.
- Use the mouse wheel to zoom.
- Right-click a placed shape to remove it.

## V2 Controls

Exploration:

- Move Nova with `WASD` or arrow keys.
- Right-drag to rotate the camera.
- Use the mouse wheel to zoom.
- Press `E` near a glowing Build Zone.

Build Mode:

- Click a shape in the bottom shelf.
- Click the 3D grid to place it.
- Click a placed block to select it.
- Press `Q` / `E` to rotate.
- Press `R` / `F` to change height.
- Press `Delete` to remove.
- Press `Ctrl+Z` / `Ctrl+Y` for undo and redo.
- Press `1`, `2`, `3`, or `C` for camera views.
- Press `Space` or click `Run Test`.

## V2 Architecture

New game-owned code lives under `Assets/Game/Scripts`:

- `Core`: game coordinator, data definitions, level library, logger
- `World`: diorama planet, Nova/Pip, camera, primitive block rendering
- `BuildSystem`: grid placement, inventory, undo/redo
- `Testing`: blueprint and functional checks
- `UI`: child-friendly IMGUI screens

## UI Direction

The interface follows the Little Shape Engineer mockups:

- Blue-and-white card layout
- Nova helper robot
- Level 1-20 task cards
- Goal, progress, tip, and build-plan panels
- Feedback, reflection, teaching, and completion screens

## V2 Levels

The game includes 10 playable V2 nodes:

- T0 Training Pad
- L1 Step Bridge
- L2 Pip Workbench
- L3 Broken Bridge
- L4 Ramp to the High Pad
- L5 Signal Window
- L6 Windmill Core
- L7 Three-View Tower
- L8 Pip's Left
- L9 Final Planet Repair

Each level has a clear target operation, expected child errors, pass/fail testing, and child-friendly Pip feedback.

## Build Target

Use `File > Build Settings`, choose `PC, Mac & Linux Standalone`, then build for your desktop platform.

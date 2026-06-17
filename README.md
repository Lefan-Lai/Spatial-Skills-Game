# Little Shape Engineer

**Build, Rotate, Test, and Fix!**

Little Shape Engineer is a child-friendly desktop Unity game for spatial learning. Children use six basic shapes to repair a colorful floating island:

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

## Current MVP Flow

- Start Screen
- Choose Your Engineer
- Shape Library
- World Map
- Level Select
- Level Briefing
- Main Build Screen
- Run Test
- Level Complete

## Child-Friendly Controls

- Drag a shape from the bottom shelf to the 3D grid.
- Drag a placed shape to move it.
- Click `Rotate` or press `R` to turn the selected shape.
- Click `Run Test` or press `Enter` to check the build.
- Right-drag to rotate the camera.
- Use the mouse wheel to zoom.
- Right-click a placed shape to remove it.

## MVP Levels

The game includes 20 English MVP levels based on the design specification:

- Blueprint Build
- Functional Build
- Repair Task
- Memory Build
- Robot View
- Final Challenge

The older prototype scripts are still kept in `Assets/Scripts` for reference. `GameBootstrap` starts `LittleShapeEngineerGame`.

## Build Target

Use `File > Build Settings`, choose `PC, Mac & Linux Standalone`, then build for your desktop platform.

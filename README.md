# 小小形状工程师 / Little Shape Engineer

Desktop Unity prototype for a child-friendly 3D shape-building game. Children use six basic shapes to repair a colorful floating island: cube, rectangular prism, plate, ramp, triangular prism, and cylinder.

## Open the project

1. Open this folder in Unity Hub.
2. Use Unity 2022.3 LTS or newer.
3. Open `Assets/Scenes/Main.unity`.
4. Press Play.

## What changed

- The game now opens as `小小形状工程师`, with a fresh child-friendly island interface.
- The first screen is a bright island bridge task with ocean, islands, a lighthouse, a small robot, and a large 3D build board.
- The UI is simplified for children: left task card, right blueprint card, bottom shape palette, and only a few large top buttons.
- Text is short and visual feedback is emphasized through ghost outlines, color, stars, and robot hints.

## Gameplay

- Drag a shape from the bottom palette onto the 3D grid.
- Drag placed shapes directly to move them.
- Use the rotate button or `R` to rotate the selected shape.
- Use the test button or `Enter` to check the build.
- Right-drag the mouse to orbit the 3D camera.
- Use the mouse wheel to zoom.
- Right-click a placed shape to remove it.

## MVP Content

The prototype includes a 20-level mission set based on the new design:

- Blueprint build tasks
- Functional build tasks
- Repair tasks
- Memory build tasks
- Viewpoint tasks
- One integrated final challenge

The project keeps the older scripts in `Assets/Scripts` for reference, but `GameBootstrap` now starts `LittleShapeEngineerGame`.

## Build target

Use `File > Build Settings`, choose `PC, Mac & Linux Standalone`, then build for your desktop platform.

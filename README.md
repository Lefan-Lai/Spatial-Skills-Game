# Block Detective 2.0 / Spatial Skills Game

Desktop Unity prototype for a structured block-construction spatial skills training game. The player joins the Block Detective Agency, rebuilds voxel crime scenes from projection evidence, receives deterministic geometry diagnostics, and teaches the AI agent Cube a reusable spatial rule.

## Open the project

1. Open this folder in Unity Hub.
2. Use Unity 2022.3 LTS or newer.
3. Open `Assets/Scenes/Main.unity`.
4. Press Play.

## Gameplay

- Read the left-side evidence board: front, right, and top projections.
- Move the yellow cursor through a 4x4x4 voxel grid.
- Add or remove blocks to match the hidden target structure.
- Submit the model to the deterministic geometry engine.
- Read diagnostic feedback, then teach Cube a spatial rule in the right-side panel.
- The prototype includes five cases across copy, blueprint, shadow evidence, witness view, and teach-agent modes.

## Controls

- `WASD` or arrow keys: move on the grid.
- `Q` / `E`: move the cursor down/up.
- `Space`: add or remove a block.
- `Enter`: submit.
- `1` front view, `2` right view, `3` top view, `4` free view.
- `H`: show a hint.

The project is designed for desktop builds and uses Unity's built-in render pipeline and uGUI.

## Build target

Use `File > Build Settings`, choose `PC, Mac & Linux Standalone`, then build for your desktop platform.

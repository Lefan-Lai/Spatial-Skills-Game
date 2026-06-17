# Block Detective Deluxe / Spatial Skills Game

Desktop Unity prototype for a structured block-construction spatial skills training game. The player joins the Block Detective lab, rebuilds voxel case scenes from projection evidence, receives deterministic geometry diagnostics, can ask a GPT tutor for explanation, and can teach the AI agent Cube a reusable spatial rule.

## Open the project

1. Open this folder in Unity Hub.
2. Use Unity 2022.3 LTS or newer.
3. Open `Assets/Scenes/Main.unity`.
4. Press Play.

## Gameplay

- The game opens directly into a playable cartoon 3D block board.
- Use the mouse to paint blocks onto the grid, drag existing blocks to new cells, and right-click blocks to remove them.
- Compare visual projection panels for front, right, and top evidence. The current model grid turns green for matches, red for extra projection marks, and yellow for missing marks.
- Submit the model to the deterministic geometry engine. The engine decides correctness; GPT is only used to explain already-computed facts.
- Switch research conditions C1-C6 across visual feedback, GPT tutor feedback, teach-the-agent feedback, fixed difficulty, and adaptive difficulty.
- In GPT/Teach conditions, enter an OpenAI API key and use the default `gpt-5.4-mini` model or replace it with another Responses API model.
- Teach Cube a rule after a diagnostic to test the recursive feedback loop.

## Controls

- Left mouse on empty cell: paint a block.
- Left mouse on a block: drag and drop it.
- Right mouse on a block: remove it.
- Mouse wheel: change active layer.
- `Q` / `E`: change active layer.
- `Space`: add or remove the active cell.
- `Enter`: submit.
- `1` front view, `2` right view, `3` top view, `4` free view.
- `H`: show a hint.

The project is designed for desktop builds and uses Unity's built-in render pipeline and uGUI. The original prototype scripts are still in `Assets/Scripts`, but `GameBootstrap` now starts `BlockDetectiveDeluxeGame`.

## GPT setup

1. Press Play in Unity.
2. Choose C2, C3, C5, or C6.
3. Paste your OpenAI API key in the right-side tutor panel.
4. Keep the model as `gpt-5.4-mini` for a normal low-latency tutor, or type another model name.
5. Click `Ask GPT` after building or submitting.

For a real classroom or published build, route GPT calls through your own small server instead of shipping an API key inside the Unity client.

## Build target

Use `File > Build Settings`, choose `PC, Mac & Linux Standalone`, then build for your desktop platform.

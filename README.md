# AstroBrick Mission / Spatial Skills Game

Desktop Unity prototype for a LEGO-inspired 3D spatial construction training game. The player guides a little astronaut on a moon-base build board, selects basic brick types, rotates and places them, receives deterministic geometric diagnosis, answers reflective correction prompts, can ask GPT-powered Mission Control for constructive feedback, and can teach the robot Novi to rebuild the structure.

## Open the project

1. Open this folder in Unity Hub.
2. Use Unity 2022.3 LTS or newer.
3. Open `Assets/Scenes/Main.unity`.
4. Press Play.

## Gameplay

- The game opens directly into `AstroBrick Mission`, a cartoon moon-base scene.
- All scored structures use a controlled basic brick grammar: bricks, plates, tiles, slopes, wedges, and corner slopes.
- Use the palette to select a brick, click the grid to place it, rotate it, change layer, drag placed bricks, and orbit the 3D camera freely.
- Submit the model to a deterministic geometry engine. The engine diagnoses wrong part, wrong footprint, wrong position, wrong layer, wrong orientation, mirror error, support error, missing element, and extra element.
- Mission Control gives constructive feedback and reflective four-choice correction prompts.
- In LLM conditions, GPT explains the already-computed geometry facts; it does not score the answer.
- In Full System, teach Novi using spatial language such as layer, high edge, left/right, target front, robot view, and support.

## Controls

- Left mouse on empty cell: place the selected brick.
- Left mouse on a brick: drag and drop it.
- Hold right mouse and drag: rotate the 3D camera.
- Mouse wheel: zoom the camera.
- Short right-click on a brick: remove it.
- `Q` / `E`: change active layer.
- `R`: rotate the selected brick.
- `Enter`: submit.
- `H`: show a hint.

The project is designed for desktop builds and uses Unity's built-in render pipeline and uGUI. Earlier Block Detective prototype scripts are still in `Assets/Scripts`, but `GameBootstrap` now starts `AstroBrickMissionGame`.

## GPT setup

1. Press Play in Unity.
2. Choose `LLM + MCQ` or `Full System`.
3. Paste your OpenAI API key in the Mission Control panel.
4. Keep the model as `gpt-5.4-mini` for a normal low-latency tutor, or type another model name.
5. Click `Ask GPT` after building or submitting.

For a real classroom or published build, route GPT calls through your own small server instead of shipping an API key inside the Unity client.

## LEGO-inspired disclaimer

This is an independent LEGO-inspired spatial learning prototype. It is not affiliated with, sponsored by, or endorsed by the LEGO Group, and it does not use LEGO logos or special functional parts as core scored task materials.

## Build target

Use `File > Build Settings`, choose `PC, Mac & Linux Standalone`, then build for your desktop platform.

using System.Collections.Generic;
using UnityEngine;

namespace ShapeonautRescue
{
    public sealed class ShapeonautUI
    {
        private readonly List<Rect> hotRects = new List<Rect>();
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private Font font;
        private GUIStyle panel;
        private GUIStyle button;
        private GUIStyle primaryButton;
        private float scale = 1f;
        private Vector2 offset;

        public bool IsPointerOverUi()
        {
            Vector2 point = MouseDesignPosition();
            for (int i = 0; i < hotRects.Count; i++)
            {
                if (hotRects[i].Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        public void Draw(ShapeonautRescueGame game)
        {
            Prepare();
            hotRects.Clear();

            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1f));
            GUI.depth = 0;

            switch (game.Mode)
            {
                case RescueMode.MainMenu:
                    DrawMainMenu(game);
                    break;
                case RescueMode.Exploration:
                    DrawExplorationHud(game);
                    break;
                case RescueMode.LevelBriefing:
                    DrawBriefing(game);
                    break;
                case RescueMode.Build:
                    DrawBuildHud(game);
                    break;
                case RescueMode.TestFeedback:
                    DrawFeedback(game);
                    break;
                case RescueMode.Complete:
                    DrawComplete(game);
                    break;
                case RescueMode.ShapeLibrary:
                    DrawShapeLibrary(game);
                    break;
                case RescueMode.Settings:
                    DrawSettings(game);
                    break;
                case RescueMode.Pause:
                    DrawPause(game);
                    break;
                case RescueMode.Export:
                    DrawExport(game);
                    break;
            }

            GUI.matrix = old;
        }

        private void DrawMainMenu(ShapeonautRescueGame game)
        {
            DrawFullScreenShell("Little Shape Engineer", "Shapeonaut Rescue");
            DrawPanel(new Rect(1020f, 260f, 520f, 520f), new Color(0.06f, 0.12f, 0.18f, 0.66f));
            DrawFitted(new Rect(1070f, 304f, 420f, 74f), "Repair the little planet with simple shapes.", 30, 17, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Button(new Rect(1130f, 420f, 300f, 64f), "Start Adventure", primaryButton, delegate { game.StartAdventure(); });
            Button(new Rect(1130f, 502f, 300f, 58f), "Continue", button, delegate { game.StartAdventure(); });
            Button(new Rect(1130f, 580f, 300f, 58f), "Shape Library", button, delegate { game.Mode = RescueMode.ShapeLibrary; });
            Button(new Rect(1130f, 658f, 300f, 58f), "Settings", button, delegate { game.Mode = RescueMode.Settings; });
            Button(new Rect(1130f, 736f, 300f, 46f), "Export Data", button, delegate { game.ExportData(); });

            DrawPanel(new Rect(260f, 270f, 620f, 450f), new Color(0.92f, 0.98f, 1f, 0.84f));
            DrawPlanetPostcard(new Rect(300f, 310f, 540f, 370f));
        }

        private void DrawExplorationHud(ShapeonautRescueGame game)
        {
            DrawQuestToast("Explore the planet. Walk to a glowing Build Zone and press E.");
            DrawPanel(new Rect(34f, 760f, 440f, 210f), new Color(0.06f, 0.12f, 0.18f, 0.68f));
            DrawFitted(new Rect(64f, 790f, 380f, 42f), "Current Quest", 23, 14, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            DrawFitted(new Rect(64f, 842f, 380f, 74f), game.NearestZoneIndex >= 0 ? "Press E to repair: " + game.Levels[game.NearestZoneIndex].Title : "Find the next glowing repair zone.", 20, 13, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
            DrawFitted(new Rect(64f, 928f, 380f, 28f), "WASD move  |  Right-drag camera  |  Esc pause", 15, 10, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.78f, 0.9f, 1f));

            DrawPanel(new Rect(1500f, 760f, 360f, 210f), new Color(0.06f, 0.12f, 0.18f, 0.68f));
            DrawFitted(new Rect(1530f, 790f, 300f, 38f), "Planet Progress", 22, 14, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            for (int i = 0; i < game.Levels.Count; i++)
            {
                float x = 1538f + (i % 5) * 58f;
                float y = 850f + (i / 5) * 54f;
                DrawSolid(new Rect(x, y, 42f, 42f), game.CompletedLevels.Contains(i) ? new Color(0.42f, 0.82f, 0.48f, 1f) : new Color(0.35f, 0.52f, 0.7f, 1f));
                DrawFitted(new Rect(x, y, 42f, 42f), game.Levels[i].Id, 16, 9, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            }
        }

        private void DrawBriefing(ShapeonautRescueGame game)
        {
            LevelDefinition level = game.CurrentLevel;
            DrawFullScreenShell("Repair Briefing", level.Id + " - " + level.Title);
            DrawPanel(new Rect(360f, 230f, 1200f, 650f), new Color(1f, 1f, 1f, 0.9f));
            DrawTaskBadge(new Rect(410f, 270f, 210f, 44f), level);
            DrawFitted(new Rect(660f, 268f, 600f, 48f), level.Region, 22, 13, FontStyle.Bold, TextAnchor.MiddleCenter, DeepBlue());
            DrawPlanetPostcard(new Rect(430f, 360f, 470f, 300f));
            DrawPanel(new Rect(950f, 360f, 520f, 300f), new Color(0.94f, 0.98f, 1f, 0.92f));
            DrawFitted(new Rect(990f, 388f, 440f, 60f), level.Story, 21, 14, FontStyle.Bold, TextAnchor.MiddleCenter, DeepBlue());
            DrawFitted(new Rect(990f, 470f, 440f, 74f), "Goal: " + level.Goal, 20, 13, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.08f, 0.14f, 0.22f));
            DrawFitted(new Rect(990f, 560f, 440f, 62f), "Success: " + level.Success, 18, 12, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.1f, 0.35f, 0.18f));
            DrawShapeShelf(game, new Rect(470f, 700f, 980f, 116f), false);
            Button(new Rect(770f, 900f, 260f, 62f), "Start Repair", primaryButton, delegate { game.BeginBuildMode(); });
            Button(new Rect(1060f, 900f, 220f, 62f), "Back", button, delegate { game.Mode = RescueMode.Exploration; });
        }

        private void DrawBuildHud(ShapeonautRescueGame game)
        {
            hotRects.Add(new Rect(20f, 20f, 1510f, 84f));
            hotRects.Add(new Rect(30f, 132f, 310f, 510f));
            hotRects.Add(new Rect(1560f, 132f, 330f, 520f));
            hotRects.Add(new Rect(420f, 846f, 1080f, 182f));

            DrawToolbar(game);
            DrawGoalCard(game);
            DrawBlueprintCard(game);
            DrawShapeShelf(game, new Rect(420f, 846f, 1080f, 182f), true);
            DrawQuestToast(game.Toast);
        }

        private void DrawToolbar(ShapeonautRescueGame game)
        {
            DrawPanel(new Rect(20f, 20f, 1510f, 84f), new Color(0.06f, 0.12f, 0.18f, 0.66f));
            Button(new Rect(40f, 34f, 90f, 54f), "Back", button, delegate { game.Mode = RescueMode.LevelBriefing; });
            Button(new Rect(148f, 34f, 90f, 54f), "Undo", button, delegate { game.Undo(); });
            Button(new Rect(256f, 34f, 90f, 54f), "Redo", button, delegate { game.Redo(); });
            Button(new Rect(374f, 34f, 90f, 54f), "Top", button, delegate { game.SetBuildView(1); });
            Button(new Rect(482f, 34f, 90f, 54f), "Front", button, delegate { game.SetBuildView(2); });
            Button(new Rect(590f, 34f, 90f, 54f), "Side", button, delegate { game.SetBuildView(3); });
            Button(new Rect(708f, 34f, 90f, 54f), "Reset", button, delegate { game.SetBuildView(0); });
            DrawFitted(new Rect(830f, 34f, 330f, 54f), game.CurrentLevel.Id + "  " + game.CurrentLevel.Title, 22, 12, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Button(new Rect(1200f, 30f, 150f, 60f), "Run Test", primaryButton, delegate { game.RunTest(); });
            Button(new Rect(1370f, 34f, 64f, 54f), "Help", button, delegate { game.ShowHelp(); });
            Button(new Rect(1450f, 34f, 64f, 54f), "Esc", button, delegate { game.Mode = RescueMode.Pause; });
        }

        private void DrawGoalCard(ShapeonautRescueGame game)
        {
            LevelDefinition level = game.CurrentLevel;
            DrawPanel(new Rect(30f, 132f, 310f, 510f), new Color(1f, 0.97f, 0.86f, 0.92f));
            DrawHeader(new Rect(30f, 132f, 310f, 48f), "Goal");
            DrawFitted(new Rect(58f, 202f, 254f, 82f), level.Goal, 19, 12, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.08f, 0.12f, 0.18f));
            DrawPanel(new Rect(58f, 310f, 254f, 112f), new Color(0.9f, 0.97f, 1f, 0.9f));
            DrawFitted(new Rect(78f, 326f, 214f, 32f), "Pip says", 18, 12, FontStyle.Bold, TextAnchor.MiddleLeft, Blue());
            DrawFitted(new Rect(78f, 364f, 214f, 44f), game.PipLine, 16, 11, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.08f, 0.14f, 0.22f));
            DrawPanel(new Rect(58f, 450f, 254f, 150f), Color.white);
            DrawFitted(new Rect(78f, 464f, 214f, 28f), "Controls", 17, 11, FontStyle.Bold, TextAnchor.MiddleLeft, Blue());
            DrawFitted(new Rect(78f, 498f, 214f, 86f), "Q/E rotate\nR/F height\nDelete remove\nSpace test", 15, 10, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.08f, 0.14f, 0.22f));
        }

        private void DrawBlueprintCard(ShapeonautRescueGame game)
        {
            DrawPanel(new Rect(1560f, 132f, 330f, 520f), new Color(0.92f, 0.98f, 1f, 0.92f));
            DrawHeader(new Rect(1560f, 132f, 330f, 48f), "Build Plan");
            DrawProjection(game, new Rect(1588f, 210f, 274f, 112f), "Top View", 0);
            DrawProjection(game, new Rect(1588f, 344f, 274f, 112f), "Front View", 1);
            DrawProjection(game, new Rect(1588f, 478f, 274f, 112f), "Side View", 2);
            DrawFitted(new Rect(1588f, 604f, 274f, 30f), "Layer " + (game.Build.SelectedLayer + 1), 18, 11, FontStyle.Bold, TextAnchor.MiddleCenter, Blue());
        }

        private void DrawFeedback(ShapeonautRescueGame game)
        {
            DrawFullScreenShell(game.LastReport.Passed ? "Great Repair!" : "Almost There", "Test feedback");
            DrawPanel(new Rect(410f, 260f, 1100f, 520f), new Color(1f, 1f, 1f, 0.92f));
            DrawPlanetPostcard(new Rect(460f, 320f, 470f, 300f));
            DrawPanel(new Rect(980f, 320f, 460f, 300f), new Color(0.92f, 0.98f, 1f, 0.92f));
            DrawFitted(new Rect(1030f, 358f, 360f, 54f), game.LastReport.Message, 28, 16, FontStyle.Bold, TextAnchor.MiddleCenter, DeepBlue());
            DrawFitted(new Rect(1030f, 440f, 360f, 86f), game.LastReport.Hint, 22, 14, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.08f, 0.14f, 0.22f));
            if (game.LastReport.Passed)
            {
                Button(new Rect(1050f, 610f, 300f, 62f), "Complete Repair", primaryButton, delegate { game.Mode = RescueMode.Complete; });
            }
            else
            {
                Button(new Rect(1050f, 610f, 300f, 62f), "Try Fixing", primaryButton, delegate { game.Mode = RescueMode.Build; });
            }
        }

        private void DrawComplete(ShapeonautRescueGame game)
        {
            DrawFullScreenShell("Great Repair!", game.CurrentLevel.WorldChange);
            DrawPanel(new Rect(460f, 260f, 1000f, 520f), new Color(0.92f, 0.98f, 1f, 0.94f));
            DrawFitted(new Rect(560f, 314f, 800f, 80f), "***", 70, 34, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.68f, 0.12f));
            DrawPlanetPostcard(new Rect(700f, 420f, 520f, 270f));
            Button(new Rect(660f, 820f, 260f, 62f), "Next Repair", primaryButton, delegate { game.NextLevel(); });
            Button(new Rect(1000f, 820f, 260f, 62f), "Back to Planet", button, delegate { game.Mode = RescueMode.Exploration; });
        }

        private void DrawShapeLibrary(ShapeonautRescueGame game)
        {
            DrawFullScreenShell("Shape Library", "Six shapes build every repair");
            int i = 0;
            foreach (KeyValuePair<BlockKind, BlockDefinition> pair in game.Blocks)
            {
                int col = i % 3;
                int row = i / 3;
                Rect card = new Rect(360f + col * 390f, 260f + row * 250f, 320f, 200f);
                DrawPanel(card, Color.white);
                DrawBlockIcon(pair.Value, new Rect(card.x + 94f, card.y + 24f, 132f, 84f));
                DrawFitted(new Rect(card.x + 30f, card.y + 120f, card.width - 60f, 30f), pair.Value.Name, 20, 12, FontStyle.Bold, TextAnchor.MiddleCenter, DeepBlue());
                DrawFitted(new Rect(card.x + 30f, card.y + 154f, card.width - 60f, 26f), pair.Value.Directional ? "Turn it to change direction." : "Good for stacking.", 15, 10, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.08f, 0.14f, 0.22f));
                i++;
            }
            Button(new Rect(820f, 860f, 260f, 60f), "Back", button, delegate { game.Mode = RescueMode.MainMenu; });
        }

        private void DrawSettings(ShapeonautRescueGame game)
        {
            DrawFullScreenShell("Settings", "MVP options");
            DrawPanel(new Rect(610f, 300f, 700f, 420f), Color.white);
            DrawFitted(new Rect(690f, 360f, 540f, 190f), "Sound: On\nMusic: On\nLanguage: English\nDifficulty: Normal\nColor Blind Mode: Off", 24, 15, FontStyle.Bold, TextAnchor.UpperLeft, DeepBlue());
            Button(new Rect(820f, 640f, 280f, 60f), "Back", button, delegate { game.Mode = RescueMode.MainMenu; });
        }

        private void DrawPause(ShapeonautRescueGame game)
        {
            DrawFullScreenShell("Pause", "Take a breath");
            DrawPanel(new Rect(660f, 320f, 600f, 360f), Color.white);
            Button(new Rect(820f, 380f, 280f, 60f), "Continue", primaryButton, delegate { game.Mode = RescueMode.Build; });
            Button(new Rect(820f, 462f, 280f, 60f), "Restart Level", button, delegate { game.RestartLevel(); });
            Button(new Rect(820f, 544f, 280f, 60f), "Back to Planet", button, delegate { game.Mode = RescueMode.Exploration; });
        }

        private void DrawExport(ShapeonautRescueGame game)
        {
            DrawFullScreenShell("Export Data", "Saved to your app data folder");
            DrawPanel(new Rect(520f, 360f, 880f, 250f), Color.white);
            DrawFitted(new Rect(580f, 420f, 760f, 90f), game.ExportMessage, 22, 13, FontStyle.Bold, TextAnchor.MiddleCenter, DeepBlue());
            Button(new Rect(820f, 650f, 280f, 60f), "Back", button, delegate { game.Mode = RescueMode.MainMenu; });
        }

        private void DrawShapeShelf(ShapeonautRescueGame game, Rect rect, bool interactive)
        {
            DrawPanel(rect, new Color(1f, 0.97f, 0.86f, 0.94f));
            int i = 0;
            foreach (KeyValuePair<BlockKind, BlockDefinition> pair in game.Blocks)
            {
                float width = (rect.width - 98f) / 6f;
                Rect card = new Rect(rect.x + 18f + i * (width + 12f), rect.y + 18f, width, rect.height - 36f);
                DrawPanel(card, pair.Key == game.Build.SelectedKind ? new Color(0.84f, 0.94f, 1f, 0.96f) : Color.white);
                DrawBlockIcon(pair.Value, new Rect(card.x + card.width * 0.18f, card.y + 14f, card.width * 0.64f, 58f));
                DrawFitted(new Rect(card.x + 8f, card.y + 84f, card.width - 16f, 30f), pair.Value.ShortName, 14, 9, FontStyle.Bold, TextAnchor.MiddleCenter, DeepBlue());
                if (interactive)
                {
                    DrawFitted(new Rect(card.x + card.width * 0.5f - 24f, card.y + card.height - 28f, 48f, 24f), game.Build.Remaining(pair.Key).ToString(), 16, 10, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Blue());
                    Button(card, "", GUIStyle.none, delegate { game.SelectShape(pair.Key); });
                }
                i++;
            }
        }

        private void DrawProjection(ShapeonautRescueGame game, Rect rect, string label, int mode)
        {
            DrawPanel(rect, Color.white);
            DrawFitted(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 22f), label, 15, 10, FontStyle.Bold, TextAnchor.MiddleLeft, Blue());
            Rect grid = new Rect(rect.x + 18f, rect.y + 36f, rect.width - 36f, rect.height - 48f);
            DrawSolid(grid, new Color(0.9f, 0.96f, 1f, 0.65f));
            int cols = 9;
            int rows = 5;
            float cw = grid.width / cols;
            float ch = grid.height / rows;
            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    DrawSolid(new Rect(grid.x + x * cw + 1f, grid.y + y * ch + 1f, cw - 2f, ch - 2f), new Color(1f, 1f, 1f, 0.32f));
                }
            }

            for (int i = 0; i < game.CurrentLevel.Targets.Count; i++)
            {
                TargetBlock target = game.CurrentLevel.Targets[i];
                int col = mode == 2 ? target.Cell.z + 4 : target.Cell.x + 4;
                int row = mode == 0 ? target.Cell.z + 2 : target.Cell.y;
                if (col < 0 || col >= cols || row < 0 || row >= rows)
                {
                    continue;
                }

                Color color = game.Blocks[target.Kind].Color;
                DrawSolid(new Rect(grid.x + col * cw + 4f, grid.y + (rows - 1 - row) * ch + 4f, cw - 8f, ch - 8f), color);
            }
        }

        private void DrawPlanetPostcard(Rect rect)
        {
            DrawPanel(rect, new Color(0.92f, 0.98f, 1f, 0.96f));
            DrawSolid(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f), new Color(0.5f, 0.78f, 0.94f, 1f));
            DrawSolid(new Rect(rect.x + 10f, rect.y + rect.height * 0.62f, rect.width - 20f, rect.height * 0.22f), new Color(0.38f, 0.66f, 0.32f, 1f));
            DrawSolid(new Rect(rect.x + rect.width * 0.28f, rect.y + rect.height * 0.48f, rect.width * 0.44f, rect.height * 0.14f), new Color(0.45f, 0.72f, 0.34f, 1f));
            DrawSolid(new Rect(rect.x + rect.width * 0.42f, rect.y + rect.height * 0.37f, rect.width * 0.16f, rect.height * 0.18f), new Color(0.82f, 0.66f, 0.42f, 1f));
            GUI.Label(new Rect(rect.x + rect.width * 0.35f, rect.y + rect.height * 0.24f, rect.width * 0.3f, rect.height * 0.2f), "/\\", MakeStyle(Color.clear, TextAnchor.MiddleCenter, 62, FontStyle.Bold, new Color(0.86f, 0.24f, 0.18f)));
        }

        private void DrawFullScreenShell(string heading, string subheading)
        {
            DrawSolid(new Rect(0f, 0f, 1920f, 1080f), new Color(0.58f, 0.78f, 0.9f, 1f));
            DrawPanel(new Rect(24f, 24f, 1872f, 1032f), new Color(0.96f, 0.99f, 1f, 0.92f));
            DrawFitted(new Rect(230f, 44f, 1460f, 64f), heading, 50, 26, FontStyle.Bold, TextAnchor.MiddleCenter, DeepBlue());
            DrawFitted(new Rect(360f, 112f, 1200f, 36f), subheading, 24, 14, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.08f, 0.22f, 0.48f));
            DrawRobotBadge(new Rect(60f, 44f, 128f, 112f));
        }

        private void DrawQuestToast(string message)
        {
            DrawPanel(new Rect(520f, 30f, 880f, 58f), new Color(0.06f, 0.12f, 0.18f, 0.68f));
            DrawFitted(new Rect(548f, 40f, 824f, 36f), message, 22, 12, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        }

        private void DrawRobotBadge(Rect rect)
        {
            DrawPanel(rect, new Color(0.92f, 0.98f, 1f, 0.96f));
            DrawSolid(new Rect(rect.x + 34f, rect.y + 38f, 60f, 42f), new Color(0.92f, 0.96f, 1f, 1f));
            DrawSolid(new Rect(rect.x + 44f, rect.y + 50f, 40f, 18f), new Color(0.04f, 0.1f, 0.14f, 1f));
            DrawSolid(new Rect(rect.x + 20f, rect.y + 15f, 88f, 16f), new Color(1f, 0.76f, 0.18f, 1f));
        }

        private void DrawBlockIcon(BlockDefinition definition, Rect rect)
        {
            if (definition.Kind == BlockKind.Ramp || definition.Kind == BlockKind.TriangularPrism)
            {
                GUI.Label(rect, "/\\", MakeStyle(Color.clear, TextAnchor.MiddleCenter, 48, FontStyle.Bold, definition.Color));
            }
            else if (definition.Kind == BlockKind.Cylinder)
            {
                DrawSolid(new Rect(rect.x + rect.width * 0.35f, rect.y + 8f, rect.width * 0.3f, rect.height - 16f), definition.Color);
                DrawSolid(new Rect(rect.x + rect.width * 0.25f, rect.y + 4f, rect.width * 0.5f, 12f), Color.Lerp(definition.Color, Color.white, 0.28f));
            }
            else
            {
                DrawSolid(new Rect(rect.x + 10f, rect.y + 12f, rect.width - 20f, rect.height - 24f), definition.Color);
                DrawSolid(new Rect(rect.x + 10f, rect.y + 12f, rect.width - 20f, 10f), Color.Lerp(definition.Color, Color.white, 0.25f));
            }
        }

        private void DrawTaskBadge(Rect rect, LevelDefinition level)
        {
            Color color = level.Task == LevelTask.Ramp || level.Task == LevelTask.Path ? new Color(0.28f, 0.62f, 0.22f, 1f) : Blue();
            if (level.Task == LevelTask.Windmill || level.Task == LevelTask.Perspective)
            {
                color = new Color(0.54f, 0.3f, 0.75f, 1f);
            }
            DrawFitted(rect, level.Task.ToString(), 15, 9, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, color);
        }

        private void DrawHeader(Rect rect, string text)
        {
            DrawSolid(rect, Blue());
            DrawFitted(rect, text, 22, 13, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        }

        private void Button(Rect rect, string label, GUIStyle style, System.Action action)
        {
            hotRects.Add(rect);
            if (GUI.Button(rect, label, style))
            {
                action();
            }
        }

        private void DrawFitted(Rect rect, string text, int max, int min, FontStyle fontStyle, TextAnchor anchor, Color color)
        {
            DrawFitted(rect, text, max, min, fontStyle, anchor, color, Color.clear);
        }

        private void DrawFitted(Rect rect, string text, int max, int min, FontStyle fontStyle, TextAnchor anchor, Color color, Color background)
        {
            GUIStyle style = MakeStyle(background, anchor, max, fontStyle, color);
            GUIContent content = new GUIContent(text);
            int size = max;
            while (size > min && style.CalcHeight(content, rect.width) > rect.height)
            {
                size--;
                style.fontSize = size;
            }

            GUI.Label(rect, text, style);
        }

        private void DrawPanel(Rect rect, Color color)
        {
            DrawSolid(new Rect(rect.x + 4f, rect.y + 5f, rect.width, rect.height), new Color(0.03f, 0.08f, 0.12f, 0.18f));
            DrawSolid(rect, color);
            DrawSolid(new Rect(rect.x, rect.y, rect.width, 2f), new Color(1f, 1f, 1f, 0.48f));
        }

        private void DrawSolid(Rect rect, Color color)
        {
            if (color.a <= 0.01f)
            {
                return;
            }
            GUI.DrawTexture(rect, Texture(color));
        }

        private void Prepare()
        {
            scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            scale = Mathf.Max(0.1f, scale);
            offset = new Vector2((Screen.width - 1920f * scale) * 0.5f, (Screen.height - 1080f * scale) * 0.5f);
            if (panel != null)
            {
                return;
            }

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panel = MakeStyle(new Color(1f, 1f, 1f, 0.9f), TextAnchor.UpperLeft, 18, FontStyle.Normal, DeepBlue());
            button = MakeStyle(new Color(1f, 0.98f, 0.9f, 0.96f), TextAnchor.MiddleCenter, 17, FontStyle.Bold, DeepBlue());
            primaryButton = MakeStyle(new Color(0.46f, 0.84f, 0.3f, 0.98f), TextAnchor.MiddleCenter, 20, FontStyle.Bold, new Color(0.05f, 0.22f, 0.05f));
        }

        private GUIStyle MakeStyle(Color background, TextAnchor anchor, int fontSize, FontStyle fontStyle, Color textColor)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.background = background.a > 0.01f ? Texture(background) : null;
            style.hover.background = style.normal.background;
            style.active.background = style.normal.background;
            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.font = font;
            style.fontSize = fontSize;
            style.fontStyle = fontStyle;
            style.alignment = anchor;
            style.wordWrap = true;
            style.clipping = TextClipping.Clip;
            style.padding = new RectOffset(12, 12, 7, 7);
            return style;
        }

        private Texture2D Texture(Color color)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color);
            Texture2D texture;
            if (textures.TryGetValue(key, out texture))
            {
                return texture;
            }

            texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            textures[key] = texture;
            return texture;
        }

        private Vector2 MouseDesignPosition()
        {
            return new Vector2((Input.mousePosition.x - offset.x) / scale, (Screen.height - Input.mousePosition.y - offset.y) / scale);
        }

        private Color Blue()
        {
            return new Color(0.06f, 0.38f, 0.76f, 0.96f);
        }

        private Color DeepBlue()
        {
            return new Color(0.02f, 0.14f, 0.34f, 1f);
        }
    }
}

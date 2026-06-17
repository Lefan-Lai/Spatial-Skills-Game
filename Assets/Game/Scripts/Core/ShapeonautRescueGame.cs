using System.Collections.Generic;
using UnityEngine;

namespace ShapeonautRescue
{
    public sealed class ShapeonautRescueGame : MonoBehaviour
    {
        public readonly HashSet<int> CompletedLevels = new HashSet<int>();

        private Dictionary<BlockKind, BlockDefinition> blocks;
        private List<LevelDefinition> levels;
        private ShapeonautBuildModel build;
        private ShapeonautWorldView world;
        private ShapeonautTester tester;
        private ShapeonautUI ui;
        private RescueDataLogger logger;

        private RescueMode mode = RescueMode.MainMenu;
        private ErrorReport lastReport = ErrorReport.Pass();
        private int levelIndex;
        private int nearestZoneIndex = -1;
        private string toast = "Welcome, Nova.";
        private string pipLine = "I will help you test and fix.";
        private string exportMessage = "";

        public Dictionary<BlockKind, BlockDefinition> Blocks
        {
            get { return blocks; }
        }

        public List<LevelDefinition> Levels
        {
            get { return levels; }
        }

        public ShapeonautBuildModel Build
        {
            get { return build; }
        }

        public RescueMode Mode
        {
            get { return mode; }
            set { mode = value; }
        }

        public LevelDefinition CurrentLevel
        {
            get { return levels[Mathf.Clamp(levelIndex, 0, levels.Count - 1)]; }
        }

        public ErrorReport LastReport
        {
            get { return lastReport; }
        }

        public int NearestZoneIndex
        {
            get { return nearestZoneIndex; }
        }

        public string Toast
        {
            get { return toast; }
        }

        public string PipLine
        {
            get { return pipLine; }
        }

        public string ExportMessage
        {
            get { return exportMessage; }
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            blocks = ShapeonautLibrary.BuildBlockDefinitions();
            levels = ShapeonautLibrary.BuildLevels();
            build = new ShapeonautBuildModel();
            world = new ShapeonautWorldView(blocks);
            tester = new ShapeonautTester(blocks);
            ui = new ShapeonautUI();
            logger = new RescueDataLogger();

            world.Initialize(levels);
            LoadLevel(0);
            logger.Log(CurrentLevel.Id, "boot", "Shapeonaut Rescue V2");
        }

        private void Update()
        {
            if (mode == RescueMode.Exploration)
            {
                UpdateExploration();
            }
            else if (mode == RescueMode.Build)
            {
                UpdateBuild();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (mode == RescueMode.Build)
                {
                    mode = RescueMode.Pause;
                }
                else if (mode == RescueMode.Exploration)
                {
                    mode = RescueMode.MainMenu;
                }
            }
        }

        private void OnGUI()
        {
            ui.Draw(this);
        }

        public void StartAdventure()
        {
            mode = RescueMode.Exploration;
            toast = "Walk to a glowing Build Zone.";
            pipLine = "Press E near a repair zone.";
            world.ApplyExplorationCamera(true);
            logger.Log(CurrentLevel.Id, "start_adventure", "");
        }

        public void LoadLevel(int index)
        {
            levelIndex = Mathf.Clamp(index, 0, levels.Count - 1);
            build.Load(CurrentLevel);
            lastReport = ErrorReport.Pass();
            toast = CurrentLevel.Id + ": " + CurrentLevel.Title;
            pipLine = CurrentLevel.PipHint;
            world.RebuildBlocks(build.Blocks, new Vector3Int(999, 999, 999));
            world.RebuildGhosts(CurrentLevel, build.Blocks);
            world.ApplyBuildCamera(0);
            logger.Log(CurrentLevel.Id, "load_level", CurrentLevel.Title);
        }

        public void BeginBuildMode()
        {
            mode = RescueMode.Build;
            toast = "Choose a shape, place it on the grid, then run a test.";
            pipLine = CurrentLevel.PipHint;
            world.ApplyBuildCamera(0);
            world.RebuildBlocks(build.Blocks, new Vector3Int(999, 999, 999));
            world.RebuildGhosts(CurrentLevel, build.Blocks);
            logger.Log(CurrentLevel.Id, "begin_build", "");
        }

        public void RestartLevel()
        {
            LoadLevel(levelIndex);
            mode = RescueMode.LevelBriefing;
            logger.Log(CurrentLevel.Id, "restart", "");
        }

        public void NextLevel()
        {
            int next = levelIndex + 1;
            if (next >= levels.Count)
            {
                next = 0;
            }

            LoadLevel(next);
            mode = RescueMode.LevelBriefing;
        }

        public void SelectShape(BlockKind kind)
        {
            build.SelectKind(kind);
            toast = blocks[kind].Name + " selected.";
            pipLine = "Click the grid to place it.";
            logger.Log(CurrentLevel.Id, "select_shape", kind.ToString());
        }

        public void SetBuildView(int viewMode)
        {
            world.ApplyBuildCamera(viewMode);
            toast = viewMode == 1 ? "Top view" : viewMode == 2 ? "Front view" : viewMode == 3 ? "Side view" : "Free 3D view";
            logger.Log(CurrentLevel.Id, "camera_view", toast);
        }

        public void Undo()
        {
            if (build.Undo())
            {
                RefreshBuildViews(new Vector3Int(999, 999, 999));
                logger.Log(CurrentLevel.Id, "undo", "");
            }
        }

        public void Redo()
        {
            if (build.Redo())
            {
                RefreshBuildViews(new Vector3Int(999, 999, 999));
                logger.Log(CurrentLevel.Id, "redo", "");
            }
        }

        public void RunTest()
        {
            lastReport = tester.Evaluate(CurrentLevel, build.Blocks);
            toast = lastReport.Passed ? "Great repair!" : "Almost there.";
            pipLine = lastReport.Hint;
            RefreshBuildViews(lastReport.HighlightCell);
            mode = RescueMode.TestFeedback;
            logger.Log(CurrentLevel.Id, "run_test", lastReport.Type.ToString());

            if (lastReport.Passed)
            {
                CompletedLevels.Add(levelIndex);
                world.SetZoneRepaired(levelIndex, true);
            }
        }

        public void ShowHelp()
        {
            pipLine = "Q/E rotate, R/F height, Space tests the repair.";
            toast = "Build controls shown on the left.";
        }

        public void ExportData()
        {
            exportMessage = "Exported logs to:\n" + logger.Export();
            mode = RescueMode.Export;
        }

        private void UpdateExploration()
        {
            world.UpdateExploration(Time.deltaTime);
            nearestZoneIndex = world.GetNearestZoneIndex(levels);
            if (nearestZoneIndex >= 0)
            {
                toast = "Press E to repair " + levels[nearestZoneIndex].Title;
            }
            else
            {
                toast = "Explore the planet and find a glowing Build Zone.";
            }

            if ((Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)) && nearestZoneIndex >= 0)
            {
                LoadLevel(nearestZoneIndex);
                mode = RescueMode.LevelBriefing;
            }
        }

        private void UpdateBuild()
        {
            world.UpdateBuildCamera();

            if (Input.GetKeyDown(KeyCode.Q))
            {
                build.RotateSelected(-1);
                RefreshBuildViews(new Vector3Int(999, 999, 999));
                logger.Log(CurrentLevel.Id, "rotate", "-90");
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                build.RotateSelected(1);
                RefreshBuildViews(new Vector3Int(999, 999, 999));
                logger.Log(CurrentLevel.Id, "rotate", "90");
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                build.ChangeLayer(1);
                toast = "Layer " + (build.SelectedLayer + 1);
            }
            if (Input.GetKeyDown(KeyCode.F))
            {
                build.ChangeLayer(-1);
                toast = "Layer " + (build.SelectedLayer + 1);
            }
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                build.DeleteSelected();
                RefreshBuildViews(new Vector3Int(999, 999, 999));
                logger.Log(CurrentLevel.Id, "delete", "");
            }
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                RunTest();
            }
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SetBuildView(1);
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SetBuildView(2);
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SetBuildView(3);
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                SetBuildView(0);
            }
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.Z))
                {
                    Undo();
                }
                if (Input.GetKeyDown(KeyCode.Y))
                {
                    Redo();
                }
            }

            if (Input.GetMouseButtonDown(0) && !ui.IsPointerOverUi())
            {
                HandleBuildClick();
            }
        }

        private void HandleBuildClick()
        {
            string id = world.RaycastBlockId(Input.mousePosition);
            if (!string.IsNullOrEmpty(id))
            {
                build.SelectById(id);
                toast = blocks[build.SelectedKind].Name + " selected.";
                pipLine = "Q/E rotates it. Delete removes it.";
                logger.Log(CurrentLevel.Id, "select_block", build.SelectedKind.ToString());
                return;
            }

            Vector3Int cell;
            if (!world.TryRaycastGrid(Input.mousePosition, Mathf.Max(build.SelectedLayer, 0), out cell))
            {
                return;
            }

            cell.y = Mathf.Max(build.SelectedLayer, build.TopLayerAt(cell.x, cell.z));
            bool placed;
            if (build.SelectedBlock != null)
            {
                placed = build.TryMoveSelected(cell);
                logger.Log(CurrentLevel.Id, "move_block", cell.ToString());
            }
            else
            {
                placed = build.TryPlaceSelected(cell);
                logger.Log(CurrentLevel.Id, "place_block", build.SelectedKind + " " + cell);
            }

            if (placed)
            {
                toast = "Block placed.";
                pipLine = "Run a test when your repair is ready.";
                RefreshBuildViews(new Vector3Int(999, 999, 999));
            }
            else
            {
                toast = "That spot is not ready.";
                pipLine = "Try an empty grid cell or another layer.";
            }
        }

        private void RefreshBuildViews(Vector3Int highlight)
        {
            world.RebuildBlocks(build.Blocks, highlight);
            world.RebuildGhosts(CurrentLevel, build.Blocks);
        }
    }
}

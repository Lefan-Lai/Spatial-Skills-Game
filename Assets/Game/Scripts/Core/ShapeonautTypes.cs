using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ShapeonautRescue
{
    public enum RescueMode
    {
        MainMenu,
        Exploration,
        LevelBriefing,
        Build,
        TestFeedback,
        Complete,
        ShapeLibrary,
        Settings,
        Pause,
        Export
    }

    public enum BlockKind
    {
        Cube,
        RectangularPrism,
        Plate,
        Ramp,
        TriangularPrism,
        Cylinder
    }

    public enum LevelTask
    {
        Tutorial,
        Blueprint,
        Support,
        Path,
        Ramp,
        Window,
        Windmill,
        ThreeViews,
        Perspective,
        Final
    }

    public enum ErrorType
    {
        None,
        MissingBlock,
        ExtraBlock,
        WrongShapeType,
        WrongPosition,
        WrongHeight,
        WrongOrientation,
        SupportError,
        PathDisconnected,
        RampWrongDirection,
        CylinderAxisError,
        SymmetryError,
        PerspectiveError
    }

    [Serializable]
    public sealed class BlockDefinition
    {
        public BlockKind Kind;
        public string Name;
        public string ShortName;
        public Vector3 Size;
        public Color Color;
        public bool Directional;

        public BlockDefinition(BlockKind kind, string name, string shortName, Vector3 size, Color color, bool directional)
        {
            Kind = kind;
            Name = name;
            ShortName = shortName;
            Size = size;
            Color = color;
            Directional = directional;
        }
    }

    [Serializable]
    public sealed class TargetBlock
    {
        public BlockKind Kind;
        public Vector3Int Cell;
        public int Rotation;

        public TargetBlock(BlockKind kind, int x, int y, int z, int rotation)
        {
            Kind = kind;
            Cell = new Vector3Int(x, y, z);
            Rotation = ShapeonautUtil.NormalizeRotation(rotation);
        }
    }

    [Serializable]
    public sealed class PlacedBlock
    {
        public string Id;
        public BlockKind Kind;
        public Vector3Int Cell;
        public int Rotation;
        public GameObject View;

        public PlacedBlock CloneWithoutView()
        {
            return new PlacedBlock
            {
                Id = Id,
                Kind = Kind,
                Cell = Cell,
                Rotation = Rotation
            };
        }
    }

    [Serializable]
    public sealed class LevelDefinition
    {
        public string Id;
        public int Number;
        public string Title;
        public string Region;
        public LevelTask Task;
        public string Story;
        public string Goal;
        public string Success;
        public string PipHint;
        public string WorldChange;
        public readonly Dictionary<BlockKind, int> Inventory = new Dictionary<BlockKind, int>();
        public readonly List<TargetBlock> Targets = new List<TargetBlock>();
        public readonly List<TargetBlock> StartingBlocks = new List<TargetBlock>();
    }

    public sealed class ErrorReport
    {
        public bool Passed;
        public ErrorType Type;
        public string Message;
        public string Hint;
        public Vector3Int HighlightCell;
        public BlockKind ExpectedKind;

        public static ErrorReport Pass()
        {
            return new ErrorReport
            {
                Passed = true,
                Type = ErrorType.None,
                Message = "Great repair!",
                Hint = "The structure works now.",
                HighlightCell = new Vector3Int(999, 999, 999)
            };
        }
    }

    [Serializable]
    public sealed class RescueEvent
    {
        public float Time;
        public string LevelId;
        public string Action;
        public string Detail;

        public RescueEvent(float time, string levelId, string action, string detail)
        {
            Time = time;
            LevelId = levelId;
            Action = action;
            Detail = detail;
        }
    }

    public sealed class RescueDataLogger
    {
        private readonly List<RescueEvent> events = new List<RescueEvent>();
        private readonly string sessionId = Guid.NewGuid().ToString("N");

        public void Log(string levelId, string action, string detail)
        {
            events.Add(new RescueEvent(Time.timeSinceLevelLoad, levelId, action, detail));
        }

        public string Export()
        {
            string folder = Path.Combine(Application.persistentDataPath, "Exports");
            Directory.CreateDirectory(folder);
            string jsonPath = Path.Combine(folder, "shapeonaut_" + sessionId + ".json");
            string csvPath = Path.Combine(folder, "shapeonaut_" + sessionId + ".csv");

            StringBuilder json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"sessionId\": \"" + sessionId + "\",");
            json.AppendLine("  \"events\": [");
            for (int i = 0; i < events.Count; i++)
            {
                RescueEvent e = events[i];
                json.Append("    {\"time\":").Append(e.Time.ToString("0.000"))
                    .Append(",\"levelId\":\"").Append(Escape(e.LevelId))
                    .Append("\",\"action\":\"").Append(Escape(e.Action))
                    .Append("\",\"detail\":\"").Append(Escape(e.Detail)).Append("\"}");
                json.AppendLine(i == events.Count - 1 ? "" : ",");
            }

            json.AppendLine("  ]");
            json.AppendLine("}");
            File.WriteAllText(jsonPath, json.ToString());

            StringBuilder csv = new StringBuilder();
            csv.AppendLine("time,levelId,action,detail");
            for (int i = 0; i < events.Count; i++)
            {
                RescueEvent e = events[i];
                csv.Append(e.Time.ToString("0.000")).Append(',')
                    .Append(Csv(e.LevelId)).Append(',')
                    .Append(Csv(e.Action)).Append(',')
                    .Append(Csv(e.Detail)).AppendLine();
            }

            File.WriteAllText(csvPath, csv.ToString());
            return folder;
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }

    public static class ShapeonautUtil
    {
        public static int NormalizeRotation(int rotation)
        {
            int value = rotation % 4;
            if (value < 0)
            {
                value += 4;
            }

            return value;
        }
    }

    public static class ShapeonautLibrary
    {
        public static Dictionary<BlockKind, BlockDefinition> BuildBlockDefinitions()
        {
            Dictionary<BlockKind, BlockDefinition> blocks = new Dictionary<BlockKind, BlockDefinition>();
            blocks[BlockKind.Cube] = new BlockDefinition(BlockKind.Cube, "Cube", "Cube", new Vector3(1f, 1f, 1f), new Color(0.26f, 0.58f, 0.88f), false);
            blocks[BlockKind.RectangularPrism] = new BlockDefinition(BlockKind.RectangularPrism, "Rectangular Prism", "Prism", new Vector3(1f, 1f, 3f), new Color(0.22f, 0.48f, 0.86f), true);
            blocks[BlockKind.Plate] = new BlockDefinition(BlockKind.Plate, "Plate", "Plate", new Vector3(2f, 0.25f, 2f), new Color(0.95f, 0.68f, 0.18f), true);
            blocks[BlockKind.Ramp] = new BlockDefinition(BlockKind.Ramp, "Ramp", "Ramp", new Vector3(1f, 1f, 2f), new Color(0.9f, 0.28f, 0.22f), true);
            blocks[BlockKind.TriangularPrism] = new BlockDefinition(BlockKind.TriangularPrism, "Triangular Prism", "Tri Prism", new Vector3(1f, 1f, 2f), new Color(0.5f, 0.28f, 0.78f), true);
            blocks[BlockKind.Cylinder] = new BlockDefinition(BlockKind.Cylinder, "Cylinder", "Cylinder", new Vector3(1f, 1f, 1f), new Color(0.92f, 0.43f, 0.12f), true);
            return blocks;
        }

        public static List<LevelDefinition> BuildLevels()
        {
            List<LevelDefinition> levels = new List<LevelDefinition>();

            LevelDefinition t0 = Level("T0", 0, "Training Pad", "Landing Meadow", LevelTask.Tutorial, "Nova just landed. Learn to place, rotate, raise, and test one block.", "Place a cube in the glowing cell.", "The training pad lights up.", "Click Cube, then click the glowing grid.");
            AddInv(t0, BlockKind.Cube, 4);
            t0.Targets.Add(T(BlockKind.Cube, 0, 0, 0, 0));
            levels.Add(t0);

            LevelDefinition l1 = Level("L1", 1, "Step Bridge", "Landing Meadow", LevelTask.Blueprint, "A tiny ledge is broken.", "Build a 1-2-3 stair for Nova.", "Nova can climb to the upper pad.", "Use R to choose a higher layer.");
            AddInv(l1, BlockKind.Cube, 8);
            l1.Targets.Add(T(BlockKind.Cube, -1, 0, 0, 0));
            l1.Targets.Add(T(BlockKind.Cube, 0, 0, 0, 0));
            l1.Targets.Add(T(BlockKind.Cube, 0, 1, 0, 0));
            l1.Targets.Add(T(BlockKind.Cube, 1, 0, 0, 0));
            l1.Targets.Add(T(BlockKind.Cube, 1, 1, 0, 0));
            l1.Targets.Add(T(BlockKind.Cube, 1, 2, 0, 0));
            levels.Add(l1);

            LevelDefinition l2 = Level("L2", 2, "Pip Workbench", "Landing Meadow", LevelTask.Support, "Pip needs a stable table.", "Build a plate top with four supports.", "Pip's tool bench turns on.", "This part needs support underneath.");
            AddInv(l2, BlockKind.Cube, 8);
            AddInv(l2, BlockKind.Plate, 2);
            l2.Targets.Add(T(BlockKind.Cube, -1, 0, -1, 0));
            l2.Targets.Add(T(BlockKind.Cube, 1, 0, -1, 0));
            l2.Targets.Add(T(BlockKind.Cube, -1, 0, 1, 0));
            l2.Targets.Add(T(BlockKind.Cube, 1, 0, 1, 0));
            l2.Targets.Add(T(BlockKind.Plate, 0, 1, 0, 0));
            levels.Add(l2);

            LevelDefinition l3 = Level("L3", 3, "Broken Bridge", "River Path", LevelTask.Path, "The river path has a gap.", "Turn long prisms to cross the gap.", "The bridge connects both islands.", "The long side should face the gap.");
            AddInv(l3, BlockKind.RectangularPrism, 4);
            AddInv(l3, BlockKind.Cube, 4);
            l3.Targets.Add(T(BlockKind.Cube, -3, 0, 0, 0));
            l3.Targets.Add(T(BlockKind.Cube, 3, 0, 0, 0));
            l3.Targets.Add(T(BlockKind.RectangularPrism, -1, 1, 0, 1));
            l3.Targets.Add(T(BlockKind.RectangularPrism, 1, 1, 0, 1));
            levels.Add(l3);

            LevelDefinition l4 = Level("L4", 4, "Ramp to the High Pad", "River Path", LevelTask.Ramp, "The high pad is just out of reach.", "Turn the ramp so Nova can walk up.", "The high path glows.", "The ramp should face the platform.");
            AddInv(l4, BlockKind.Cube, 6);
            AddInv(l4, BlockKind.Ramp, 2);
            l4.Targets.Add(T(BlockKind.Cube, 2, 0, 0, 0));
            l4.Targets.Add(T(BlockKind.Cube, 2, 1, 0, 0));
            l4.Targets.Add(T(BlockKind.Ramp, 0, 0, 0, 1));
            levels.Add(l4);

            LevelDefinition l5 = Level("L5", 5, "Signal Window", "Signal Grove", LevelTask.Window, "The signal station is dark.", "Place the window one layer above the door.", "The station window lights up.", "Leave the lower space open for the door.");
            AddInv(l5, BlockKind.Cube, 8);
            AddInv(l5, BlockKind.Plate, 1);
            AddInv(l5, BlockKind.RectangularPrism, 2);
            l5.Targets.Add(T(BlockKind.Cube, -1, 0, 0, 0));
            l5.Targets.Add(T(BlockKind.Cube, 1, 0, 0, 0));
            l5.Targets.Add(T(BlockKind.Cube, -1, 1, 0, 0));
            l5.Targets.Add(T(BlockKind.Cube, 1, 1, 0, 0));
            l5.Targets.Add(T(BlockKind.Plate, 0, 1, 0, 0));
            l5.Targets.Add(T(BlockKind.RectangularPrism, 0, 2, 0, 1));
            levels.Add(l5);

            LevelDefinition l6 = Level("L6", 6, "Windmill Core", "Signal Grove", LevelTask.Windmill, "The energy windmill is stuck.", "Build four blades around the center cylinder.", "The windmill spins again.", "Find the center first, then build around it.");
            AddInv(l6, BlockKind.Cylinder, 1);
            AddInv(l6, BlockKind.RectangularPrism, 4);
            AddInv(l6, BlockKind.Cube, 4);
            l6.Targets.Add(T(BlockKind.Cube, 0, 0, 0, 0));
            l6.Targets.Add(T(BlockKind.Cylinder, 0, 1, 0, 0));
            l6.Targets.Add(T(BlockKind.RectangularPrism, 0, 2, -1, 0));
            l6.Targets.Add(T(BlockKind.RectangularPrism, 1, 2, 0, 1));
            l6.Targets.Add(T(BlockKind.RectangularPrism, 0, 2, 1, 0));
            l6.Targets.Add(T(BlockKind.RectangularPrism, -1, 2, 0, 1));
            levels.Add(l6);

            LevelDefinition l7 = Level("L7", 7, "Three-View Tower", "Observatory Hill", LevelTask.ThreeViews, "The observatory needs a signal tower.", "Build from top, front, and side views.", "The tower unfolds.", "Check the top view for depth.");
            AddInv(l7, BlockKind.Cube, 8);
            AddInv(l7, BlockKind.Plate, 2);
            AddInv(l7, BlockKind.Cylinder, 1);
            l7.Targets.Add(T(BlockKind.Cube, -1, 0, 0, 0));
            l7.Targets.Add(T(BlockKind.Cube, 0, 0, 0, 0));
            l7.Targets.Add(T(BlockKind.Cube, 1, 0, 0, 0));
            l7.Targets.Add(T(BlockKind.Plate, 0, 1, 0, 0));
            l7.Targets.Add(T(BlockKind.Cylinder, 0, 2, 0, 0));
            levels.Add(l7);

            LevelDefinition l8 = Level("L8", 8, "Pip's Left", "Observatory Hill", LevelTask.Perspective, "Pip is facing the control panel.", "Put the cube on Pip's left side.", "The control panel unlocks.", "Stand where Pip stands. Which side is left?");
            AddInv(l8, BlockKind.Cube, 2);
            AddInv(l8, BlockKind.Plate, 1);
            l8.Targets.Add(T(BlockKind.Plate, 0, 0, 0, 0));
            l8.Targets.Add(T(BlockKind.Cube, -1, 1, 0, 0));
            levels.Add(l8);

            LevelDefinition l9 = Level("L9", 9, "Final Planet Repair", "Observatory Hill", LevelTask.Final, "The planet core needs one full repair path.", "Connect bridge, ramp, signal, and windmill parts.", "The planet core lights up.", "Use everything you repaired before.");
            AddInv(l9, BlockKind.Cube, 12);
            AddInv(l9, BlockKind.RectangularPrism, 6);
            AddInv(l9, BlockKind.Plate, 4);
            AddInv(l9, BlockKind.Ramp, 2);
            AddInv(l9, BlockKind.Cylinder, 1);
            l9.Targets.Add(T(BlockKind.RectangularPrism, -2, 0, 0, 1));
            l9.Targets.Add(T(BlockKind.RectangularPrism, 0, 0, 0, 1));
            l9.Targets.Add(T(BlockKind.Ramp, 2, 0, 0, 1));
            l9.Targets.Add(T(BlockKind.Cube, 3, 1, 0, 0));
            l9.Targets.Add(T(BlockKind.Plate, 3, 2, 0, 0));
            l9.Targets.Add(T(BlockKind.Cylinder, 4, 3, 0, 0));
            levels.Add(l9);

            return levels;
        }

        private static LevelDefinition Level(string id, int number, string title, string region, LevelTask task, string story, string goal, string success, string hint)
        {
            return new LevelDefinition
            {
                Id = id,
                Number = number,
                Title = title,
                Region = region,
                Task = task,
                Story = story,
                Goal = goal,
                Success = success,
                PipHint = hint,
                WorldChange = success
            };
        }

        private static TargetBlock T(BlockKind kind, int x, int y, int z, int rotation)
        {
            return new TargetBlock(kind, x, y, z, rotation);
        }

        private static void AddInv(LevelDefinition level, BlockKind kind, int count)
        {
            level.Inventory[kind] = count;
        }
    }
}

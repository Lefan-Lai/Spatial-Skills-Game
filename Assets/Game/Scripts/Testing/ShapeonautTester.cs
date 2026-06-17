using System.Collections.Generic;
using UnityEngine;

namespace ShapeonautRescue
{
    public sealed class ShapeonautTester
    {
        private readonly Dictionary<BlockKind, BlockDefinition> definitions;

        public ShapeonautTester(Dictionary<BlockKind, BlockDefinition> definitions)
        {
            this.definitions = definitions;
        }

        public ErrorReport Evaluate(LevelDefinition level, List<PlacedBlock> placed)
        {
            ErrorReport report = EvaluateBlueprint(level, placed);
            if (!report.Passed)
            {
                return AddTaskLanguage(level, report);
            }

            ErrorReport functional = EvaluateFunctionalRule(level, placed);
            if (!functional.Passed)
            {
                return AddTaskLanguage(level, functional);
            }

            return ErrorReport.Pass();
        }

        private ErrorReport EvaluateBlueprint(LevelDefinition level, List<PlacedBlock> placed)
        {
            bool[] used = new bool[placed.Count];

            for (int i = 0; i < level.Targets.Count; i++)
            {
                TargetBlock target = level.Targets[i];
                int exact = FindExact(target, placed, used);
                if (exact >= 0)
                {
                    used[exact] = true;
                    continue;
                }

                int sameKind = FindSameKind(target, placed, used);
                if (sameKind >= 0)
                {
                    PlacedBlock block = placed[sameKind];
                    if (block.Cell.y != target.Cell.y)
                    {
                        return Report(ErrorType.WrongHeight, target, "This block should be one layer " + (target.Cell.y > block.Cell.y ? "higher." : "lower."), "Use R and F to choose the height.");
                    }

                    if (block.Cell.x != target.Cell.x || block.Cell.z != target.Cell.z)
                    {
                        return Report(ErrorType.WrongPosition, target, "This block is close. Move it one space.", "Look from the top to check its spot.");
                    }

                    if (definitions[target.Kind].Directional && ShapeonautUtil.NormalizeRotation(block.Rotation) != ShapeonautUtil.NormalizeRotation(target.Rotation))
                    {
                        return Report(ErrorType.WrongOrientation, target, "Try turning this block once.", "The long side should face the right way.");
                    }
                }

                int sameCell = FindSameCell(target, placed, used);
                if (sameCell >= 0)
                {
                    return Report(ErrorType.WrongShapeType, target, "This spot needs a different shape.", "Pick the shape shown in the build plan.");
                }

                return Report(ErrorType.MissingBlock, target, "A shape is missing here.", "Try filling the glowing space.");
            }

            for (int i = 0; i < placed.Count; i++)
            {
                if (!used[i])
                {
                    return new ErrorReport
                    {
                        Passed = false,
                        Type = ErrorType.ExtraBlock,
                        Message = "There is one extra shape.",
                        Hint = "Take away the extra shape, then run the test again.",
                        HighlightCell = placed[i].Cell,
                        ExpectedKind = placed[i].Kind
                    };
                }
            }

            return ErrorReport.Pass();
        }

        private ErrorReport EvaluateFunctionalRule(LevelDefinition level, List<PlacedBlock> placed)
        {
            if (level.Task == LevelTask.Support)
            {
                for (int i = 0; i < placed.Count; i++)
                {
                    PlacedBlock block = placed[i];
                    if (block.Kind == BlockKind.Plate && !HasSupportUnder(block, placed))
                    {
                        return new ErrorReport
                        {
                            Passed = false,
                            Type = ErrorType.SupportError,
                            Message = "This part needs support underneath.",
                            Hint = "Put cubes under the plate corners.",
                            HighlightCell = block.Cell,
                            ExpectedKind = BlockKind.Cube
                        };
                    }
                }
            }

            if (level.Task == LevelTask.Ramp)
            {
                PlacedBlock ramp = FindKind(placed, BlockKind.Ramp);
                if (ramp != null && ShapeonautUtil.NormalizeRotation(ramp.Rotation) != 1)
                {
                    return new ErrorReport
                    {
                        Passed = false,
                        Type = ErrorType.RampWrongDirection,
                        Message = "The ramp points away from Nova.",
                        Hint = "Turn the ramp so Nova can walk up it.",
                        HighlightCell = ramp.Cell,
                        ExpectedKind = BlockKind.Ramp
                    };
                }
            }

            if (level.Task == LevelTask.Windmill)
            {
                PlacedBlock center = FindKind(placed, BlockKind.Cylinder);
                if (center == null || center.Cell != new Vector3Int(0, 1, 0))
                {
                    return new ErrorReport
                    {
                        Passed = false,
                        Type = ErrorType.CylinderAxisError,
                        Message = "The center cylinder is not in place.",
                        Hint = "Find the center first, then build around it.",
                        HighlightCell = new Vector3Int(0, 1, 0),
                        ExpectedKind = BlockKind.Cylinder
                    };
                }
            }

            if (level.Task == LevelTask.Perspective)
            {
                bool found = false;
                for (int i = 0; i < placed.Count; i++)
                {
                    if (placed[i].Kind == BlockKind.Cube && placed[i].Cell == new Vector3Int(-1, 1, 0))
                    {
                        found = true;
                    }
                }

                if (!found)
                {
                    return new ErrorReport
                    {
                        Passed = false,
                        Type = ErrorType.PerspectiveError,
                        Message = "Use Pip's left, not your screen's left.",
                        Hint = "Stand where Pip stands, then choose left.",
                        HighlightCell = new Vector3Int(-1, 1, 0),
                        ExpectedKind = BlockKind.Cube
                    };
                }
            }

            return ErrorReport.Pass();
        }

        private ErrorReport AddTaskLanguage(LevelDefinition level, ErrorReport report)
        {
            if (level.Task == LevelTask.Path && report.Type == ErrorType.WrongOrientation)
            {
                report.Type = ErrorType.PathDisconnected;
                report.Message = "The path stops here.";
                report.Hint = "Turn the long block so it reaches the gap.";
            }
            else if (level.Task == LevelTask.Windmill && report.Type == ErrorType.WrongPosition)
            {
                report.Type = ErrorType.SymmetryError;
                report.Message = "One blade does not match the others.";
                report.Hint = "Build one side, then mirror it.";
            }

            return report;
        }

        private ErrorReport Report(ErrorType type, TargetBlock target, string message, string hint)
        {
            return new ErrorReport
            {
                Passed = false,
                Type = type,
                Message = message,
                Hint = hint,
                HighlightCell = target.Cell,
                ExpectedKind = target.Kind
            };
        }

        private int FindExact(TargetBlock target, List<PlacedBlock> placed, bool[] used)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if (used[i])
                {
                    continue;
                }

                PlacedBlock block = placed[i];
                if (block.Kind != target.Kind || block.Cell != target.Cell)
                {
                    continue;
                }

                if (definitions[target.Kind].Directional && ShapeonautUtil.NormalizeRotation(block.Rotation) != ShapeonautUtil.NormalizeRotation(target.Rotation))
                {
                    continue;
                }

                return i;
            }

            return -1;
        }

        private int FindSameKind(TargetBlock target, List<PlacedBlock> placed, bool[] used)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if (!used[i] && placed[i].Kind == target.Kind)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindSameCell(TargetBlock target, List<PlacedBlock> placed, bool[] used)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if (!used[i] && placed[i].Cell == target.Cell)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool HasSupportUnder(PlacedBlock plate, List<PlacedBlock> placed)
        {
            int supports = 0;
            for (int i = 0; i < placed.Count; i++)
            {
                PlacedBlock other = placed[i];
                if (other.Kind == BlockKind.Cube && other.Cell.y == plate.Cell.y - 1 && Mathf.Abs(other.Cell.x - plate.Cell.x) <= 1 && Mathf.Abs(other.Cell.z - plate.Cell.z) <= 1)
                {
                    supports++;
                }
            }

            return supports >= 4;
        }

        private PlacedBlock FindKind(List<PlacedBlock> placed, BlockKind kind)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i].Kind == kind)
                {
                    return placed[i];
                }
            }

            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShapeonautRescue
{
    public sealed class ShapeonautBuildModel
    {
        public const int GridMinX = -5;
        public const int GridMaxX = 5;
        public const int GridMinZ = -4;
        public const int GridMaxZ = 4;
        public const int GridMaxY = 5;

        public readonly List<PlacedBlock> Blocks = new List<PlacedBlock>();
        public readonly Dictionary<BlockKind, int> Inventory = new Dictionary<BlockKind, int>();

        private readonly Dictionary<BlockKind, int> baseInventory = new Dictionary<BlockKind, int>();
        private readonly Stack<List<PlacedBlock>> undoStack = new Stack<List<PlacedBlock>>();
        private readonly Stack<List<PlacedBlock>> redoStack = new Stack<List<PlacedBlock>>();

        public BlockKind SelectedKind = BlockKind.Cube;
        public PlacedBlock SelectedBlock;
        public int SelectedRotation;
        public int SelectedLayer;

        public bool CanUndo
        {
            get { return undoStack.Count > 0; }
        }

        public bool CanRedo
        {
            get { return redoStack.Count > 0; }
        }

        public void Load(LevelDefinition level)
        {
            Blocks.Clear();
            Inventory.Clear();
            baseInventory.Clear();
            undoStack.Clear();
            redoStack.Clear();
            SelectedBlock = null;
            SelectedRotation = 0;
            SelectedLayer = 0;

            foreach (KeyValuePair<BlockKind, int> pair in level.Inventory)
            {
                Inventory[pair.Key] = pair.Value;
                baseInventory[pair.Key] = pair.Value;
            }

            for (int i = 0; i < level.StartingBlocks.Count; i++)
            {
                TargetBlock target = level.StartingBlocks[i];
                AddBlock(target.Kind, target.Cell, target.Rotation, false);
            }

            SelectedKind = FirstAvailableKind();
        }

        public int Remaining(BlockKind kind)
        {
            int count;
            if (Inventory.TryGetValue(kind, out count))
            {
                return count;
            }

            return 0;
        }

        public void SelectKind(BlockKind kind)
        {
            SelectedKind = kind;
            SelectedBlock = null;
            SelectedRotation = 0;
        }

        public void RotateSelected(int delta)
        {
            if (SelectedBlock != null)
            {
                PushUndo();
                SelectedBlock.Rotation = ShapeonautUtil.NormalizeRotation(SelectedBlock.Rotation + delta);
                redoStack.Clear();
                return;
            }

            SelectedRotation = ShapeonautUtil.NormalizeRotation(SelectedRotation + delta);
        }

        public void ChangeLayer(int delta)
        {
            SelectedLayer = Mathf.Clamp(SelectedLayer + delta, 0, GridMaxY);
        }

        public bool TryPlaceSelected(Vector3Int cell)
        {
            cell.y = Mathf.Clamp(cell.y, 0, GridMaxY);
            if (!IsPlacementValid(cell, null) || Remaining(SelectedKind) <= 0)
            {
                return false;
            }

            PushUndo();
            AddBlock(SelectedKind, cell, SelectedRotation, true);
            redoStack.Clear();
            return true;
        }

        public bool TryMoveSelected(Vector3Int cell)
        {
            if (SelectedBlock == null)
            {
                return false;
            }

            cell.y = Mathf.Clamp(cell.y, 0, GridMaxY);
            if (!IsPlacementValid(cell, SelectedBlock))
            {
                return false;
            }

            PushUndo();
            SelectedBlock.Cell = cell;
            redoStack.Clear();
            return true;
        }

        public PlacedBlock SelectAt(Vector3Int cell)
        {
            for (int i = Blocks.Count - 1; i >= 0; i--)
            {
                if (Blocks[i].Cell == cell)
                {
                    SelectedBlock = Blocks[i];
                    SelectedKind = Blocks[i].Kind;
                    SelectedRotation = Blocks[i].Rotation;
                    SelectedLayer = Blocks[i].Cell.y;
                    return SelectedBlock;
                }
            }

            SelectedBlock = null;
            return null;
        }

        public PlacedBlock SelectById(string id)
        {
            for (int i = 0; i < Blocks.Count; i++)
            {
                if (Blocks[i].Id == id)
                {
                    SelectedBlock = Blocks[i];
                    SelectedKind = Blocks[i].Kind;
                    SelectedRotation = Blocks[i].Rotation;
                    SelectedLayer = Blocks[i].Cell.y;
                    return SelectedBlock;
                }
            }

            SelectedBlock = null;
            return null;
        }

        public void DeleteSelected()
        {
            if (SelectedBlock == null)
            {
                return;
            }

            PushUndo();
            Inventory[SelectedBlock.Kind] = Remaining(SelectedBlock.Kind) + 1;
            Blocks.Remove(SelectedBlock);
            SelectedBlock = null;
            redoStack.Clear();
        }

        public bool Undo()
        {
            if (undoStack.Count == 0)
            {
                return false;
            }

            redoStack.Push(Capture());
            Restore(undoStack.Pop());
            return true;
        }

        public bool Redo()
        {
            if (redoStack.Count == 0)
            {
                return false;
            }

            undoStack.Push(Capture());
            Restore(redoStack.Pop());
            return true;
        }

        public bool IsPlacementValid(Vector3Int cell, PlacedBlock ignore)
        {
            if (cell.x < GridMinX || cell.x > GridMaxX || cell.z < GridMinZ || cell.z > GridMaxZ || cell.y < 0 || cell.y > GridMaxY)
            {
                return false;
            }

            for (int i = 0; i < Blocks.Count; i++)
            {
                PlacedBlock other = Blocks[i];
                if (other == ignore)
                {
                    continue;
                }

                if (other.Cell == cell)
                {
                    return false;
                }
            }

            return true;
        }

        public int TopLayerAt(int x, int z)
        {
            int top = 0;
            for (int i = 0; i < Blocks.Count; i++)
            {
                PlacedBlock block = Blocks[i];
                if (block.Cell.x == x && block.Cell.z == z)
                {
                    top = Mathf.Max(top, block.Cell.y + 1);
                }
            }

            return Mathf.Clamp(top, 0, GridMaxY);
        }

        private void AddBlock(BlockKind kind, Vector3Int cell, int rotation, bool consumeInventory)
        {
            PlacedBlock block = new PlacedBlock
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = kind,
                Cell = cell,
                Rotation = ShapeonautUtil.NormalizeRotation(rotation)
            };
            Blocks.Add(block);

            if (consumeInventory)
            {
                Inventory[kind] = Remaining(kind) - 1;
            }
        }

        private BlockKind FirstAvailableKind()
        {
            foreach (BlockKind kind in Enum.GetValues(typeof(BlockKind)))
            {
                if (Remaining(kind) > 0)
                {
                    return kind;
                }
            }

            return BlockKind.Cube;
        }

        private void PushUndo()
        {
            undoStack.Push(Capture());
            while (undoStack.Count > 40)
            {
                List<PlacedBlock>[] stack = undoStack.ToArray();
                undoStack.Clear();
                for (int i = stack.Length - 2; i >= 0; i--)
                {
                    undoStack.Push(stack[i]);
                }
            }
        }

        private List<PlacedBlock> Capture()
        {
            List<PlacedBlock> snapshot = new List<PlacedBlock>();
            for (int i = 0; i < Blocks.Count; i++)
            {
                snapshot.Add(Blocks[i].CloneWithoutView());
            }

            return snapshot;
        }

        private void Restore(List<PlacedBlock> snapshot)
        {
            Blocks.Clear();
            for (int i = 0; i < snapshot.Count; i++)
            {
                Blocks.Add(snapshot[i].CloneWithoutView());
            }
            SelectedBlock = null;
            RecalculateInventory();
        }

        private void RecalculateInventory()
        {
            Inventory.Clear();
            foreach (KeyValuePair<BlockKind, int> pair in baseInventory)
            {
                Inventory[pair.Key] = pair.Value;
            }

            for (int i = 0; i < Blocks.Count; i++)
            {
                PlacedBlock block = Blocks[i];
                Inventory[block.Kind] = Remaining(block.Kind) - 1;
            }
        }
    }
}

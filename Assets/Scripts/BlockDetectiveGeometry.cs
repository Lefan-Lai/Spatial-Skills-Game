using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class BlockDetectiveGame
{
    private DiagnosticResult Diagnose(HashSet<Vector3Int> answer, CaseData data)
    {
        DiagnosticResult result = new DiagnosticResult();
        HashSet<Vector3Int> target = new HashSet<Vector3Int>(data.Target);
        result.Passed = target.SetEquals(answer);

        AddProjectionFact(result, "front", CompareBoolGrid(GetFrontProjection(answer), GetFrontProjection(target)));
        AddProjectionFact(result, "right", CompareBoolGrid(GetRightProjection(answer), GetRightProjection(target)));
        AddProjectionFact(result, "top", CompareBoolGrid(GetTopProjection(answer), GetTopProjection(target)));

        if (result.Passed)
        {
            result.ErrorType = ErrorType.None;
            result.EngineFacts.Add("exact voxel set matches target");
            return result;
        }

        if (target.SetEquals(MirrorX(answer)))
        {
            result.ErrorType = ErrorType.LeftRightMirror;
            result.EngineFacts.Add("user model becomes the target after x-axis mirror");
        }
        else if (target.SetEquals(MirrorZ(answer)))
        {
            result.ErrorType = ErrorType.FrontBackReverse;
            result.EngineFacts.Add("user model becomes the target after z-axis mirror");
        }
        else if (SameFootprint(answer, target))
        {
            result.ErrorType = ErrorType.HeightError;
            result.EngineFacts.Add("top footprint matches, but one or more column heights differ");
        }
        else if (answer.Count < target.Count)
        {
            result.ErrorType = ErrorType.MissingBlock;
            result.EngineFacts.Add("answer has fewer blocks than target");
        }
        else if (answer.Count > target.Count)
        {
            result.ErrorType = ErrorType.ExtraBlock;
            result.EngineFacts.Add("answer has more blocks than target");
        }
        else if (result.MatchedViews.Count >= 2)
        {
            result.ErrorType = ErrorType.ProjectionAmbiguity;
            result.EngineFacts.Add("multiple projections match, but exact 3D structure differs");
        }
        else
        {
            result.ErrorType = ErrorType.Unknown;
            result.EngineFacts.Add("no simple misconception pattern matched");
        }

        return result;
    }

    private void AddProjectionFact(DiagnosticResult result, string viewName, bool matches)
    {
        if (matches)
        {
            result.MatchedViews.Add(viewName);
            result.EngineFacts.Add(viewName + " projection matches target");
        }
        else
        {
            result.MismatchedViews.Add(viewName);
            result.EngineFacts.Add(viewName + " projection differs");
        }
    }

    private bool[,] GetFrontProjection(IEnumerable<Vector3Int> voxels)
    {
        bool[,] view = new bool[GridHeight, GridWidth];
        foreach (Vector3Int voxel in voxels)
        {
            if (IsInsideGrid(voxel))
            {
                view[GridHeight - 1 - voxel.y, voxel.x] = true;
            }
        }

        return view;
    }

    private bool[,] GetRightProjection(IEnumerable<Vector3Int> voxels)
    {
        bool[,] view = new bool[GridHeight, GridDepth];
        foreach (Vector3Int voxel in voxels)
        {
            if (IsInsideGrid(voxel))
            {
                view[GridHeight - 1 - voxel.y, voxel.z] = true;
            }
        }

        return view;
    }

    private bool[,] GetTopProjection(IEnumerable<Vector3Int> voxels)
    {
        bool[,] view = new bool[GridDepth, GridWidth];
        foreach (Vector3Int voxel in voxels)
        {
            if (IsInsideGrid(voxel))
            {
                view[voxel.z, voxel.x] = true;
            }
        }

        return view;
    }

    private bool CompareBoolGrid(bool[,] a, bool[,] b)
    {
        if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1))
        {
            return false;
        }

        for (int row = 0; row < a.GetLength(0); row++)
        {
            for (int col = 0; col < a.GetLength(1); col++)
            {
                if (a[row, col] != b[row, col])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private string ProjectionToString(bool[,] projection)
    {
        StringBuilder builder = new StringBuilder();
        for (int row = 0; row < projection.GetLength(0); row++)
        {
            for (int col = 0; col < projection.GetLength(1); col++)
            {
                builder.Append(projection[row, col] ? "# " : ". ");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private HashSet<Vector3Int> MirrorX(IEnumerable<Vector3Int> voxels)
    {
        HashSet<Vector3Int> mirrored = new HashSet<Vector3Int>();
        foreach (Vector3Int voxel in voxels)
        {
            mirrored.Add(new Vector3Int(GridWidth - 1 - voxel.x, voxel.y, voxel.z));
        }

        return mirrored;
    }

    private HashSet<Vector3Int> MirrorZ(IEnumerable<Vector3Int> voxels)
    {
        HashSet<Vector3Int> mirrored = new HashSet<Vector3Int>();
        foreach (Vector3Int voxel in voxels)
        {
            mirrored.Add(new Vector3Int(voxel.x, voxel.y, GridDepth - 1 - voxel.z));
        }

        return mirrored;
    }

    private bool SameFootprint(HashSet<Vector3Int> a, HashSet<Vector3Int> b)
    {
        HashSet<Vector2Int> footprintA = new HashSet<Vector2Int>();
        HashSet<Vector2Int> footprintB = new HashSet<Vector2Int>();

        foreach (Vector3Int voxel in a)
        {
            footprintA.Add(new Vector2Int(voxel.x, voxel.z));
        }

        foreach (Vector3Int voxel in b)
        {
            footprintB.Add(new Vector2Int(voxel.x, voxel.z));
        }

        return footprintA.SetEquals(footprintB);
    }

    private bool IsInsideGrid(Vector3Int cell)
    {
        return cell.x >= 0 && cell.x < GridWidth &&
               cell.y >= 0 && cell.y < GridHeight &&
               cell.z >= 0 && cell.z < GridDepth;
    }

    private static Vector3Int[] Cells(params int[] values)
    {
        Vector3Int[] cells = new Vector3Int[values.Length / 3];
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = new Vector3Int(values[i * 3], values[i * 3 + 1], values[i * 3 + 2]);
        }

        return cells;
    }

    private Vector3 GridToWorld(Vector3Int cell)
    {
        float x = (cell.x - (GridWidth - 1) * 0.5f) * CellSize;
        float y = cell.y * CellSize;
        float z = (cell.z - (GridDepth - 1) * 0.5f) * CellSize;
        return new Vector3(x, y, z);
    }

    private void CreateFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Builder Floor";
        floor.transform.SetParent(stageRoot, false);
        floor.transform.position = new Vector3(0f, -0.48f, 0f);
        floor.transform.localScale = new Vector3(4.8f, 0.08f, 4.8f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
    }

    private void CreateGridLines()
    {
        float half = (GridWidth - 1) * CellSize * 0.5f;
        float y = -0.41f;

        for (int i = 0; i <= GridWidth; i++)
        {
            float coordinate = (i - GridWidth * 0.5f) * CellSize;
            CreateGridLine("Grid X " + i, new Vector3(-half - CellSize * 0.5f, y, coordinate), new Vector3(half + CellSize * 0.5f, y, coordinate), gridLineMaterial);
            CreateGridLine("Grid Z " + i, new Vector3(coordinate, y, -half - CellSize * 0.5f), new Vector3(coordinate, y, half + CellSize * 0.5f), gridLineMaterial);
        }

        CreateGridLine("X Axis", new Vector3(-2.2f, y + 0.02f, -2.2f), new Vector3(2.2f, y + 0.02f, -2.2f), axisXMaterial);
        CreateGridLine("Z Axis", new Vector3(-2.2f, y + 0.02f, -2.2f), new Vector3(-2.2f, y + 0.02f, 2.2f), axisZMaterial);
    }

    private void CreateGridLine(string name, Vector3 start, Vector3 end, Material material)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(gridRoot, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = false;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = 0.014f;
        line.endWidth = 0.014f;
        line.material = material;
        line.startColor = material.color;
        line.endColor = material.color;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    private void CreateCursor()
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Voxel Cursor";
        cube.transform.SetParent(cursorRoot, false);
        cube.transform.localScale = Vector3.one * 0.8f;
        cube.GetComponent<Renderer>().sharedMaterial = cursorMaterial;
        UpdateCursorVisual();
    }

    private void UpdateCursorVisual()
    {
        if (cursorRoot != null)
        {
            cursorRoot.position = GridToWorld(cursorCell);
        }
    }

    private void ShowSolvedTargetPreview()
    {
        ClearChildren(solvedTargetRoot);
        Vector3 previewOffset = new Vector3(3.9f, 0f, 0f);
        Vector3 targetCenter = new Vector3((GridWidth - 1) * 0.5f, (GridHeight - 1) * 0.5f, (GridDepth - 1) * 0.5f);

        for (int i = 0; i < cases[caseIndex].Target.Length; i++)
        {
            Vector3Int cell = cases[caseIndex].Target[i];
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Solved Target Block " + i;
            cube.transform.SetParent(solvedTargetRoot, false);
            Vector3 centered = new Vector3(cell.x, cell.y, cell.z) - targetCenter;
            cube.transform.position = previewOffset + new Vector3(centered.x, centered.y + 1f, centered.z) * CellSize;
            cube.transform.localScale = Vector3.one * 0.68f;
            cube.GetComponent<Renderer>().sharedMaterial = targetGhostMaterial;
        }
    }

    private void ClearVoxelObjects()
    {
        foreach (KeyValuePair<Vector3Int, GameObject> entry in voxelObjects)
        {
            if (entry.Value != null)
            {
                Destroy(entry.Value);
            }
        }

        voxelObjects.Clear();
    }

    private void ClearChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private void ApplyMaterialToUserBlocks(Material material)
    {
        foreach (KeyValuePair<Vector3Int, GameObject> entry in voxelObjects)
        {
            if (entry.Value != null)
            {
                entry.Value.GetComponent<Renderer>().sharedMaterial = material;
            }
        }
    }

    private void CreateLight(string name, LightType type, Vector3 position, Quaternion rotation, float intensity)
    {
        GameObject lightObject = new GameObject(name);
        Light light = lightObject.AddComponent<Light>();
        light.type = type;
        light.intensity = intensity;
        light.range = 8f;
        light.shadows = type == LightType.Directional ? LightShadows.Soft : LightShadows.None;
        lightObject.transform.position = position;
        lightObject.transform.rotation = rotation;
    }
}


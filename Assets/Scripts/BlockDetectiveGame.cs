using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed partial class BlockDetectiveGame : MonoBehaviour
{
    private const int GridWidth = 4;
    private const int GridHeight = 4;
    private const int GridDepth = 4;
    private const float CellSize = 0.82f;

    private enum DetectiveView
    {
        Free,
        Front,
        Right,
        Top,
        Witness
    }

    private enum ErrorType
    {
        None,
        LeftRightMirror,
        FrontBackReverse,
        HeightError,
        MissingBlock,
        ExtraBlock,
        ProjectionAmbiguity,
        Unknown
    }

    private enum LoadState
    {
        Low,
        Optimal,
        High,
        Overload
    }

    private sealed class CaseData
    {
        public string Id;
        public string Chapter;
        public string Mode;
        public string Title;
        public string Brief;
        public string PredictionPrompt;
        public string Vocabulary;
        public string WitnessNote;
        public Vector3Int[] Target;
        public bool ShowFront = true;
        public bool ShowRight = true;
        public bool ShowTop = true;
    }

    private sealed class DiagnosticResult
    {
        public bool Passed;
        public ErrorType ErrorType;
        public readonly List<string> MatchedViews = new List<string>();
        public readonly List<string> MismatchedViews = new List<string>();
        public readonly List<string> EngineFacts = new List<string>();
    }

    private sealed class SessionEvent
    {
        public float Time;
        public string LevelId;
        public string Action;
        public string Detail;
    }

    private readonly List<CaseData> cases = new List<CaseData>();
    private readonly List<SessionEvent> sessionEvents = new List<SessionEvent>();
    private readonly HashSet<Vector3Int> userVoxels = new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, GameObject> voxelObjects = new Dictionary<Vector3Int, GameObject>();

    private Camera mainCamera;
    private Transform stageRoot;
    private Transform gridRoot;
    private Transform voxelRoot;
    private Transform cursorRoot;
    private Transform solvedTargetRoot;

    private Material blockMaterial;
    private Material cursorMaterial;
    private Material targetGhostMaterial;
    private Material floorMaterial;
    private Material solvedMaterial;
    private Material gridLineMaterial;
    private Material axisXMaterial;
    private Material axisZMaterial;
    private Font uiFont;

    private Text titleText;
    private Text metaText;
    private Text scoreText;
    private Text evidenceText;
    private Text builderText;
    private Text tutorText;
    private Text diagnosticText;
    private Text cubeText;
    private Text loadText;
    private Text statusText;
    private Text cursorText;
    private InputField teachInput;
    private Button nextButton;

    private int caseIndex;
    private int score;
    private int attempts;
    private int solvedCases;
    private int hintsUsed;
    private int actionsThisCase;
    private int viewSwitchesThisCase;
    private int mentalEffort = 2;
    private float caseStartTime;
    private bool currentCaseSolved;
    private Vector3Int cursorCell;
    private DiagnosticResult lastDiagnostic;

    private void Start()
    {
        Application.targetFrameRate = 60;
        BuildCases();
        SetupScene();
        BuildInterface();
        StartCase(0);
    }

    private void Update()
    {
        HandleKeyboardInput();
        UpdateHud();
    }

    private void BuildCases()
    {
        cases.Add(new CaseData
        {
            Id = "C0-1",
            Chapter = "Chapter 0: Detective Training Yard",
            Mode = "Copy the Scene",
            Title = "The Split Signal",
            Brief = "Mirror Master scattered a small signal tower. Rebuild it from orthographic evidence.",
            PredictionPrompt = "If the front view matches but top view fails, what relation is hidden?",
            Vocabulary = "above, below, adjacent, height, column",
            WitnessNote = "Cube watches from the front and often ignores depth.",
            Target = Cells(0, 0, 0, 1, 0, 0, 1, 1, 0, 2, 0, 1, 2, 1, 1)
        });

        cases.Add(new CaseData
        {
            Id = "C1-2",
            Chapter = "Chapter 1: Blueprint Room",
            Mode = "Blueprint Builder",
            Title = "Height Map Archive",
            Brief = "The blueprint survived. Each occupied position must become a column with the right height.",
            PredictionPrompt = "Top evidence gives position; front and right evidence check height.",
            Vocabulary = "height map, layer, stack, column",
            WitnessNote = "The witness above sees footprints, not full height.",
            Target = Cells(0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 0, 1, 2, 1, 1, 2, 2, 1, 3, 0, 2)
        });

        cases.Add(new CaseData
        {
            Id = "C2-3",
            Chapter = "Chapter 2: Shadow Lab",
            Mode = "Shadow Evidence",
            Title = "Two Shadows, One Structure",
            Brief = "Only the front and right shadows were recorded. Use them, then verify the hidden top view.",
            PredictionPrompt = "Two projections may still hide multiple 3D possibilities.",
            Vocabulary = "front projection, right projection, occlusion, depth",
            WitnessNote = "A side camera noticed the center column, but not every hidden block.",
            ShowTop = false,
            Target = Cells(0, 0, 0, 1, 0, 0, 2, 0, 0, 1, 1, 0, 1, 0, 1, 1, 1, 1)
        });

        cases.Add(new CaseData
        {
            Id = "C3-4",
            Chapter = "Chapter 3: Witness Alley",
            Mode = "Witness View",
            Title = "The Sidewalk Statement",
            Brief = "A witness saw the model from the right. Rebuild it and check the viewpoint.",
            PredictionPrompt = "A witness's left is not always your left.",
            Vocabulary = "viewpoint, egocentric, left-right, front-back",
            WitnessNote = "Witness view is from the positive X side, looking toward the board.",
            Target = Cells(0, 0, 1, 1, 0, 1, 2, 0, 1, 2, 1, 1, 2, 0, 2, 3, 0, 2)
        });

        cases.Add(new CaseData
        {
            Id = "C4-5",
            Chapter = "Chapter 4: Mirror Master Finale",
            Mode = "Teach Cube Challenge",
            Title = "Mirror Master's Decoy",
            Brief = "The decoy looks almost right from the front. Diagnose mirrors and teach Cube a rule.",
            PredictionPrompt = "When front is correct but right fails, depth is the prime suspect.",
            Vocabulary = "mirror, reverse, depth, strategy, explanation",
            WitnessNote = "Cube needs a portable rule after this case.",
            Target = Cells(0, 0, 0, 1, 0, 0, 2, 0, 0, 2, 1, 0, 0, 0, 1, 0, 1, 1, 3, 0, 2, 3, 1, 2)
        });
    }

    private void SetupScene()
    {
        blockMaterial = CreateMaterial("Detective Blocks", new Color(1f, 0.45f, 0.16f, 1f), false);
        cursorMaterial = CreateMaterial("Cursor", new Color(1f, 0.92f, 0.25f, 0.34f), true);
        targetGhostMaterial = CreateMaterial("Target Ghost", new Color(0.1f, 0.82f, 1f, 0.28f), true);
        floorMaterial = CreateMaterial("Investigation Floor", new Color(0.055f, 0.065f, 0.075f, 1f), false);
        solvedMaterial = CreateMaterial("Solved Blocks", new Color(0.2f, 1f, 0.5f, 1f), false);
        gridLineMaterial = CreateLineMaterial("Grid Lines", new Color(0.44f, 0.52f, 0.58f, 0.36f));
        axisXMaterial = CreateLineMaterial("X Axis", new Color(0.95f, 0.25f, 0.22f, 0.8f));
        axisZMaterial = CreateLineMaterial("Z Axis", new Color(0.25f, 0.6f, 1f, 0.8f));

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.025f, 0.032f, 0.04f, 1f);
        mainCamera.fieldOfView = 43f;

        if (mainCamera.GetComponent<AudioListener>() == null)
        {
            mainCamera.gameObject.AddComponent<AudioListener>();
        }

        CreateLight("Key Light", LightType.Directional, new Vector3(-2.5f, 6f, -3.4f), Quaternion.Euler(52f, -34f, 0f), 1.05f);
        CreateLight("Evidence Fill", LightType.Point, new Vector3(1.5f, 3.8f, -2.7f), Quaternion.identity, 1.8f);

        stageRoot = new GameObject("Block Detective Runtime Stage").transform;
        gridRoot = new GameObject("Builder Grid").transform;
        voxelRoot = new GameObject("Player Voxel Model").transform;
        solvedTargetRoot = new GameObject("Solved Target Preview").transform;
        cursorRoot = new GameObject("Builder Cursor").transform;
        gridRoot.SetParent(stageRoot, false);
        voxelRoot.SetParent(stageRoot, false);
        solvedTargetRoot.SetParent(stageRoot, false);
        cursorRoot.SetParent(stageRoot, false);

        CreateFloor();
        CreateGridLines();
        CreateCursor();
        ApplyCameraView(DetectiveView.Free);
    }

    private void StartCase(int requestedIndex)
    {
        caseIndex = requestedIndex % cases.Count;
        currentCaseSolved = false;
        attempts = 0;
        hintsUsed = 0;
        actionsThisCase = 0;
        viewSwitchesThisCase = 0;
        mentalEffort = 2;
        caseStartTime = Time.time;
        lastDiagnostic = null;
        cursorCell = Vector3Int.zero;
        userVoxels.Clear();
        ClearVoxelObjects();
        ClearChildren(solvedTargetRoot);

        if (teachInput != null)
        {
            teachInput.text = "";
        }

        if (nextButton != null)
        {
            nextButton.interactable = false;
        }

        diagnosticText.text = "";
        tutorText.text = GenerateOpeningBrief();
        cubeText.text = "Cube: I am ready to learn a rule after your first diagnostic.";
        ApplyCameraView(DetectiveView.Free);
        UpdateCursorVisual();
        UpdateEvidenceBoard();
        SetStatus("Inspect evidence, make a prediction, then build with the cursor.");
        LogEvent("start_case", cases[caseIndex].Id);
    }

    private void HandleKeyboardInput()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            MoveCursor(-1, 0, 0);
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            MoveCursor(1, 0, 0);
        }
        else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            MoveCursor(0, 0, 1);
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            MoveCursor(0, 0, -1);
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            MoveCursor(0, -1, 0);
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            MoveCursor(0, 1, 0);
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleBlockAtCursor();
        }
        else if (Input.GetKeyDown(KeyCode.Return))
        {
            SubmitAnswer();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ApplyCameraView(DetectiveView.Front);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ApplyCameraView(DetectiveView.Right);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ApplyCameraView(DetectiveView.Top);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            ApplyCameraView(DetectiveView.Free);
        }
        else if (Input.GetKeyDown(KeyCode.H))
        {
            ShowHint();
        }
        else if (Input.GetKeyDown(KeyCode.N) && currentCaseSolved)
        {
            NextCase();
        }
    }

    private void MoveCursor(int dx, int dy, int dz)
    {
        cursorCell = new Vector3Int(
            Mathf.Clamp(cursorCell.x + dx, 0, GridWidth - 1),
            Mathf.Clamp(cursorCell.y + dy, 0, GridHeight - 1),
            Mathf.Clamp(cursorCell.z + dz, 0, GridDepth - 1));

        actionsThisCase++;
        UpdateCursorVisual();
        UpdateEvidenceBoard();
        LogEvent("move_cursor", cursorCell.ToString());
    }

    private void ToggleBlockAtCursor()
    {
        if (userVoxels.Contains(cursorCell))
        {
            RemoveBlockAtCursor();
        }
        else
        {
            AddBlockAtCursor();
        }
    }

    private void AddBlockAtCursor()
    {
        if (userVoxels.Contains(cursorCell))
        {
            SetStatus("There is already a block at the cursor.");
            return;
        }

        userVoxels.Add(cursorCell);
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "User Block " + cursorCell;
        cube.transform.SetParent(voxelRoot, false);
        cube.transform.position = GridToWorld(cursorCell);
        cube.transform.localScale = Vector3.one * 0.74f;
        cube.GetComponent<Renderer>().sharedMaterial = blockMaterial;
        voxelObjects[cursorCell] = cube;

        actionsThisCase++;
        UpdateEvidenceBoard();
        SetStatus("Block added. Use projections to decide whether it belongs.");
        LogEvent("add_block", cursorCell.ToString());
    }

    private void RemoveBlockAtCursor()
    {
        if (!userVoxels.Contains(cursorCell))
        {
            SetStatus("No block at the cursor.");
            return;
        }

        userVoxels.Remove(cursorCell);
        GameObject cube;
        if (voxelObjects.TryGetValue(cursorCell, out cube))
        {
            Destroy(cube);
            voxelObjects.Remove(cursorCell);
        }

        actionsThisCase++;
        UpdateEvidenceBoard();
        SetStatus("Block removed.");
        LogEvent("remove_block", cursorCell.ToString());
    }

    private void SubmitAnswer()
    {
        attempts++;
        lastDiagnostic = Diagnose(userVoxels, cases[caseIndex]);
        diagnosticText.text = FormatDiagnostic(lastDiagnostic);
        tutorText.text = GenerateTutorFeedback(lastDiagnostic);
        cubeText.text = GenerateCubePrompt(lastDiagnostic);

        if (lastDiagnostic.Passed)
        {
            currentCaseSolved = true;
            solvedCases++;
            int points = Mathf.Max(50, 160 - Mathf.RoundToInt(Time.time - caseStartTime) - hintsUsed * 12 - attempts * 8);
            score += points;
            nextButton.interactable = true;
            ApplyMaterialToUserBlocks(solvedMaterial);
            ShowSolvedTargetPreview();
            SetStatus("Case solved. Teach Cube the strategy, then go to the next case.");
            LogEvent("submit_pass", "points=" + points);
        }
        else
        {
            score = Mathf.Max(0, score - 6);
            SetStatus("Diagnostic complete. Fix the model or teach Cube what went wrong.");
            LogEvent("submit_fail", lastDiagnostic.ErrorType.ToString());
        }

        UpdateEvidenceBoard();
    }

    private void TeachCube()
    {
        string rule = teachInput == null ? "" : teachInput.text.Trim();
        if (rule.Length < 8)
        {
            cubeText.text = "Cube: I need a fuller rule. Mention a view and a relation, such as depth, height, mirror, left, or right.";
            SetStatus("Teach Cube with a complete spatial rule.");
            return;
        }

        ErrorType targetError = lastDiagnostic == null ? ErrorType.Unknown : lastDiagnostic.ErrorType;
        int quality = ScoreRuleQuality(rule, targetError);
        bool cubeSucceeded = quality >= 60;

        cubeText.text =
            "Rule extracted\n" +
            "{\n" +
            "  condition: \"" + ExtractCondition(rule) + "\",\n" +
            "  strategy: \"" + ExtractStrategy(rule, targetError) + "\",\n" +
            "  quality: " + quality + "/100\n" +
            "}\n\n" +
            (cubeSucceeded
                ? "Cube test: success. Cube used your rule on a hidden case.\nRecursive feedback: you turned intuition into a transferable strategy."
                : "Cube test: partial. Add a clearer view cue or spatial relation.\nRecursive feedback: revise the rule with front/right/top plus depth/height/mirror language.");

        score += cubeSucceeded ? 18 : 4;
        LogEvent("teach_cube", "quality=" + quality + ", success=" + cubeSucceeded);
        SetStatus(cubeSucceeded ? "Cube learned the rule." : "Cube needs a clearer rule.");
    }

    private void ShowHint()
    {
        hintsUsed++;
        actionsThisCase++;

        if (lastDiagnostic == null)
        {
            tutorText.text = "Hint ladder\n" + cases[caseIndex].PredictionPrompt + "\n\nSpatial terms: " + cases[caseIndex].Vocabulary;
        }
        else
        {
            tutorText.text = "Hint ladder\n" + HintForError(lastDiagnostic.ErrorType) + "\n\nSpatial terms: " + cases[caseIndex].Vocabulary;
        }

        SetStatus("Hint used. The system recorded this as behavior telemetry.");
        LogEvent("hint", cases[caseIndex].Id);
    }

    private void ResetBuild()
    {
        userVoxels.Clear();
        ClearVoxelObjects();
        ClearChildren(solvedTargetRoot);
        currentCaseSolved = false;
        nextButton.interactable = false;
        lastDiagnostic = null;
        diagnosticText.text = "";
        UpdateEvidenceBoard();
        SetStatus("Build reset for this case.");
        LogEvent("reset_build", cases[caseIndex].Id);
    }

    private void NextCase()
    {
        StartCase(caseIndex + 1);
    }

    private void SetMentalEffort(int value)
    {
        mentalEffort = Mathf.Clamp(value, 1, 3);
        LogEvent("mental_effort", mentalEffort.ToString());
        SetStatus("Mental effort rating saved: " + mentalEffort);
    }

    private void ApplyCameraView(DetectiveView view)
    {
        Vector3 target = new Vector3(0f, 1.1f, 0f);
        switch (view)
        {
            case DetectiveView.Front:
                mainCamera.transform.position = new Vector3(0f, 1.8f, -7.2f);
                break;
            case DetectiveView.Right:
                mainCamera.transform.position = new Vector3(7.2f, 1.8f, 0f);
                break;
            case DetectiveView.Top:
                mainCamera.transform.position = new Vector3(0.01f, 8f, 0.01f);
                target = Vector3.zero;
                break;
            case DetectiveView.Witness:
                mainCamera.transform.position = new Vector3(5.7f, 2.4f, 3.5f);
                break;
            default:
                mainCamera.transform.position = new Vector3(4.7f, 4.8f, -6.7f);
                break;
        }

        mainCamera.transform.LookAt(target);
        viewSwitchesThisCase++;
        LogEvent("switch_view", view.ToString());
    }

    private void LogEvent(string action, string detail)
    {
        if (cases.Count == 0)
        {
            return;
        }

        SessionEvent evt = new SessionEvent
        {
            Time = Time.time,
            LevelId = cases[caseIndex].Id,
            Action = action,
            Detail = detail
        };

        sessionEvents.Add(evt);
        Debug.Log("[BlockDetective] " + evt.LevelId + " | " + action + " | " + detail);
    }
}


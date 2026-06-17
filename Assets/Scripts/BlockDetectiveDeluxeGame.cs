using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class BlockDetectiveDeluxeGame : MonoBehaviour
{
    private const int GridWidth = 5;
    private const int GridHeight = 4;
    private const int GridDepth = 5;
    private const float CellSize = 0.82f;
    private const string OpenAiResponsesUrl = "https://api.openai.com/v1/responses";
    private const string DefaultModel = "gpt-5.4-mini";

    private enum FeedbackMode
    {
        VisualDiagnostic,
        LlmTutor,
        TeachAgent
    }

    private enum DifficultyPolicy
    {
        Fixed,
        Adaptive
    }

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

    private enum LoadBand
    {
        Calm,
        Focused,
        Strained,
        Overloaded
    }

    private sealed class ConditionData
    {
        public string Label;
        public FeedbackMode Feedback;
        public DifficultyPolicy Difficulty;
        public string ResearchQuestion;
    }

    private sealed class CaseData
    {
        public string Id;
        public string Title;
        public string Mission;
        public string PredictionPrompt;
        public string Vocabulary;
        public string WitnessNote;
        public int Difficulty;
        public Vector3Int[] Target;
        public bool ShowFront = true;
        public bool ShowRight = true;
        public bool ShowTop = true;
    }

    private sealed class DiagnosticResult
    {
        public bool Passed;
        public ErrorType Error;
        public readonly List<string> MatchedViews = new List<string>();
        public readonly List<string> MismatchedViews = new List<string>();
        public readonly List<string> EngineFacts = new List<string>();
        public readonly List<Vector3Int> MissingCells = new List<Vector3Int>();
        public readonly List<Vector3Int> ExtraCells = new List<Vector3Int>();
    }

    private sealed class SessionEvent
    {
        public float Time;
        public string CaseId;
        public string Condition;
        public string Action;
        public string Detail;
    }

    private sealed class ProjectionGrid
    {
        public Text Header;
        public Text TargetLabel;
        public Text CurrentLabel;
        public Image[,] TargetCells;
        public Image[,] CurrentCells;
        public int Rows;
        public int Columns;
    }

    private readonly List<ConditionData> conditions = new List<ConditionData>();
    private readonly List<CaseData> cases = new List<CaseData>();
    private readonly List<SessionEvent> sessionEvents = new List<SessionEvent>();
    private readonly HashSet<Vector3Int> userVoxels = new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, GameObject> blockObjects = new Dictionary<Vector3Int, GameObject>();

    private Camera mainCamera;
    private Transform stageRoot;
    private Transform cellRoot;
    private Transform blockRoot;
    private Transform cursorRoot;
    private Transform diagnosticGhostRoot;
    private Transform agentRoot;

    private Material[] blockMaterials;
    private Material floorMaterial;
    private Material floorTileMaterialA;
    private Material floorTileMaterialB;
    private Material gridLineMaterial;
    private Material cursorMaterial;
    private Material invalidCursorMaterial;
    private Material blockShineMaterial;
    private Material correctMaterial;
    private Material extraMaterial;
    private Material missingGhostMaterial;
    private Material targetGhostMaterial;
    private Material robotBodyMaterial;
    private Material robotFaceMaterial;
    private Material robotEyeMaterial;
    private Material backdropMaterial;

    private Renderer cursorRenderer;
    private Font uiFont;

    private Text titleText;
    private Text metaText;
    private Text missionText;
    private Text statusText;
    private Text tutorText;
    private Text diagnosticText;
    private Text cubeText;
    private Text telemetryText;
    private Text loadText;
    private Text cursorText;
    private Text conditionText;
    private Image loadFill;
    private InputField apiKeyInput;
    private InputField modelInput;
    private InputField teachInput;
    private Button askGptButton;
    private Button teachButton;
    private Button nextButton;
    private readonly List<Button> conditionButtons = new List<Button>();
    private ProjectionGrid frontProjection;
    private ProjectionGrid rightProjection;
    private ProjectionGrid topProjection;

    private int conditionIndex;
    private int caseIndex;
    private int score;
    private int solvedCases;
    private int attempts;
    private int hintsUsed;
    private int actionsThisCase;
    private int viewSwitchesThisCase;
    private int mentalEffort = 2;
    private int activeLayer;
    private float caseStartTime;
    private bool currentCaseSolved;
    private bool isPainting;
    private bool isDragging;
    private bool gptRequestRunning;
    private Vector3Int hoverCell;
    private Vector3Int dragStartCell;
    private GameObject draggingObject;
    private bool hoverValid;
    private bool hoverIsBlock;
    private DiagnosticResult lastDiagnostic;
    private Color[] conditionButtonBaseColors;

    private void Start()
    {
        Application.targetFrameRate = 60;
        QualitySettings.antiAliasing = Mathf.Max(QualitySettings.antiAliasing, 4);
        QualitySettings.vSyncCount = 1;

        BuildConditions();
        BuildCases();
        SetupScene();
        BuildInterface();
        SelectCondition(0);
        StartCase(0);
    }

    private void Update()
    {
        UpdateHoverFromMouse();
        HandleMouseInput();
        HandleKeyboardInput();
        AnimateAgent();
        UpdateHud();
    }

    private void BuildConditions()
    {
        conditions.Add(new ConditionData
        {
            Label = "C1 Visual + Fixed",
            Feedback = FeedbackMode.VisualDiagnostic,
            Difficulty = DifficultyPolicy.Fixed,
            ResearchQuestion = "Can deterministic visual diagnosis train spatial reasoning without LLM feedback?"
        });

        conditions.Add(new ConditionData
        {
            Label = "C2 LLM + Fixed",
            Feedback = FeedbackMode.LlmTutor,
            Difficulty = DifficultyPolicy.Fixed,
            ResearchQuestion = "Does GPT explanation add value beyond visual diagnosis?"
        });

        conditions.Add(new ConditionData
        {
            Label = "C3 Teach + Fixed",
            Feedback = FeedbackMode.TeachAgent,
            Difficulty = DifficultyPolicy.Fixed,
            ResearchQuestion = "Does teaching Cube improve transfer and metacognition?"
        });

        conditions.Add(new ConditionData
        {
            Label = "C4 Visual + Adaptive",
            Feedback = FeedbackMode.VisualDiagnostic,
            Difficulty = DifficultyPolicy.Adaptive,
            ResearchQuestion = "Can adaptive difficulty improve the basic diagnostic condition?"
        });

        conditions.Add(new ConditionData
        {
            Label = "C5 LLM + Adaptive",
            Feedback = FeedbackMode.LlmTutor,
            Difficulty = DifficultyPolicy.Adaptive,
            ResearchQuestion = "Can GPT feedback stay concise when load is high?"
        });

        conditions.Add(new ConditionData
        {
            Label = "C6 Teach + Adaptive",
            Feedback = FeedbackMode.TeachAgent,
            Difficulty = DifficultyPolicy.Adaptive,
            ResearchQuestion = "Can recursive feedback and adaptive difficulty support transfer?"
        });
    }

    private void BuildCases()
    {
        cases.Add(new CaseData
        {
            Id = "BD-01",
            Title = "Training Yard: Split Signal",
            Mission = "Rebuild the small signal tower. Use the front, right, and top evidence before you submit.",
            PredictionPrompt = "Prediction: if front is right but top is wrong, the hidden issue is depth.",
            Vocabulary = "above, below, adjacent, column, footprint",
            WitnessNote = "Cube sees the front clearly but can miss blocks behind it.",
            Difficulty = 1,
            Target = Cells(0, 0, 0, 1, 0, 0, 1, 1, 0, 2, 0, 1, 2, 1, 1)
        });

        cases.Add(new CaseData
        {
            Id = "BD-02",
            Title = "Blueprint Room: Height Map",
            Mission = "Match the footprint and the column heights. The top view gives positions, not full height.",
            PredictionPrompt = "Prediction: a matching top view can still hide a height error.",
            Vocabulary = "height map, stack, layer, footprint, column",
            WitnessNote = "The overhead camera saw the footprint but not the full stack height.",
            Difficulty = 2,
            Target = Cells(0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 0, 1, 2, 1, 1, 2, 2, 1, 3, 0, 2)
        });

        cases.Add(new CaseData
        {
            Id = "BD-03",
            Title = "Shadow Lab: Two Views",
            Mission = "Only front and right evidence are public at first. Submit to reveal whether the hidden top footprint works.",
            PredictionPrompt = "Prediction: two shadows may have more than one possible 3D structure.",
            Vocabulary = "projection, occlusion, ambiguity, depth",
            WitnessNote = "This is a projection ambiguity trap. Check one view at a time.",
            Difficulty = 2,
            ShowTop = false,
            Target = Cells(0, 0, 0, 1, 0, 0, 2, 0, 0, 1, 1, 0, 1, 0, 1, 1, 1, 1)
        });

        cases.Add(new CaseData
        {
            Id = "BD-04",
            Title = "Witness Alley: Side Statement",
            Mission = "A witness viewed the model from the right. Convert that viewpoint back into your builder grid.",
            PredictionPrompt = "Prediction: a witness left-right statement depends on where the witness stood.",
            Vocabulary = "viewpoint, egocentric, side view, left-right",
            WitnessNote = "Witness position: positive X side, looking toward the center.",
            Difficulty = 3,
            Target = Cells(0, 0, 1, 1, 0, 1, 2, 0, 1, 2, 1, 1, 2, 0, 2, 3, 0, 2)
        });

        cases.Add(new CaseData
        {
            Id = "BD-05",
            Title = "Mirror Case: Friendly Decoy",
            Mission = "The front view is tempting. Use side/top evidence to avoid a mirrored answer.",
            PredictionPrompt = "Prediction: if the front view matches but right fails, front-back order is suspect.",
            Vocabulary = "mirror, reverse, depth, compare, strategy",
            WitnessNote = "Cube often thinks a correct front view proves the whole structure.",
            Difficulty = 3,
            Target = Cells(0, 0, 0, 1, 0, 0, 2, 0, 0, 2, 1, 0, 0, 0, 1, 0, 1, 1, 3, 0, 2, 3, 1, 2)
        });

        cases.Add(new CaseData
        {
            Id = "BD-06",
            Title = "Transfer Vault: Offset Tower",
            Mission = "Solve a new arrangement using a rule, not trial and error. Teach Cube after the diagnostic.",
            PredictionPrompt = "Prediction: compare top footprint first, then column heights.",
            Vocabulary = "transfer, rule, height, footprint, front-back",
            WitnessNote = "This case tests whether your rule works beyond the example you just saw.",
            Difficulty = 4,
            Target = Cells(1, 0, 0, 1, 1, 0, 2, 0, 0, 3, 0, 1, 3, 1, 1, 3, 2, 1, 0, 0, 3, 0, 1, 3, 4, 0, 4)
        });
    }

    private void SetupScene()
    {
        RenderSettings.ambientLight = new Color(0.78f, 0.86f, 0.92f, 1f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.72f, 0.89f, 0.96f, 1f);
        RenderSettings.fogDensity = 0.018f;

        CreateMaterials();
        SetupCamera();

        stageRoot = new GameObject("Block Detective Deluxe Runtime").transform;
        cellRoot = new GameObject("Mouse Paintable Cells").transform;
        blockRoot = new GameObject("Player Blocks").transform;
        cursorRoot = new GameObject("Mouse Cursor Ghost").transform;
        diagnosticGhostRoot = new GameObject("Diagnostic Missing Block Ghosts").transform;
        agentRoot = new GameObject("Cube Tutor Agent").transform;

        cellRoot.SetParent(stageRoot, false);
        blockRoot.SetParent(stageRoot, false);
        cursorRoot.SetParent(stageRoot, false);
        diagnosticGhostRoot.SetParent(stageRoot, false);
        agentRoot.SetParent(stageRoot, false);

        CreateLights();
        CreateBackdrop();
        CreateBuilderFloor();
        CreateGridLines();
        CreateCursor();
        CreateAgent();
        ApplyCameraView(DetectiveView.Free);
    }

    private void CreateMaterials()
    {
        blockMaterials = new[]
        {
            CreateMaterial("Candy Coral Block", new Color(1f, 0.39f, 0.35f, 1f), false),
            CreateMaterial("Bright Teal Block", new Color(0.0f, 0.74f, 0.82f, 1f), false),
            CreateMaterial("Sunny Yellow Block", new Color(1f, 0.78f, 0.18f, 1f), false),
            CreateMaterial("Mint Block", new Color(0.32f, 0.86f, 0.55f, 1f), false),
            CreateMaterial("Lavender Block", new Color(0.61f, 0.48f, 1f, 1f), false)
        };

        floorMaterial = CreateMaterial("Stage Floor", new Color(0.93f, 0.96f, 0.98f, 1f), false);
        floorTileMaterialA = CreateMaterial("Cell Tile A", new Color(0.88f, 0.95f, 1f, 1f), false);
        floorTileMaterialB = CreateMaterial("Cell Tile B", new Color(0.96f, 0.91f, 1f, 1f), false);
        gridLineMaterial = CreateLineMaterial("Grid Ink Lines", new Color(0.25f, 0.33f, 0.42f, 0.52f));
        cursorMaterial = CreateMaterial("Valid Cursor", new Color(0.1f, 0.92f, 1f, 0.24f), true);
        invalidCursorMaterial = CreateMaterial("Invalid Cursor", new Color(1f, 0.16f, 0.24f, 0.38f), true);
        blockShineMaterial = CreateMaterial("Block Top Shine", new Color(1f, 1f, 1f, 0.42f), true);
        correctMaterial = CreateMaterial("Correct Block", new Color(0.28f, 0.95f, 0.56f, 1f), false);
        extraMaterial = CreateMaterial("Extra Block", new Color(1f, 0.28f, 0.28f, 1f), false);
        missingGhostMaterial = CreateMaterial("Missing Block Ghost", new Color(1f, 0.9f, 0.12f, 0.34f), true);
        targetGhostMaterial = CreateMaterial("Target Hint Ghost", new Color(0.12f, 0.56f, 1f, 0.22f), true);
        robotBodyMaterial = CreateMaterial("Cube Agent Body", new Color(0.18f, 0.64f, 1f, 1f), false);
        robotFaceMaterial = CreateMaterial("Cube Agent Face", new Color(0.98f, 1f, 1f, 1f), false);
        robotEyeMaterial = CreateMaterial("Cube Agent Eyes", new Color(0.05f, 0.1f, 0.18f, 1f), false);
        backdropMaterial = CreateMaterial("Soft Studio Backdrop", new Color(0.72f, 0.9f, 1f, 1f), false);
    }

    private void SetupCamera()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.72f, 0.9f, 1f, 1f);
        mainCamera.fieldOfView = 42f;
        mainCamera.nearClipPlane = 0.05f;
        mainCamera.farClipPlane = 100f;

        if (mainCamera.GetComponent<AudioListener>() == null)
        {
            mainCamera.gameObject.AddComponent<AudioListener>();
        }
    }

    private void CreateLights()
    {
        CreateLight("Soft Sun", LightType.Directional, new Vector3(-3.5f, 8f, -5f), Quaternion.Euler(48f, -35f, 0f), 1.05f, true);
        CreateLight("Candy Fill", LightType.Point, new Vector3(3.2f, 3.4f, -3.1f), Quaternion.identity, 1.65f, false);
        CreateLight("Robot Rim", LightType.Point, new Vector3(3.4f, 2.5f, 2.5f), Quaternion.identity, 0.92f, false);
    }

    private void CreateBackdrop()
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Pastel Studio Backdrop";
        wall.transform.SetParent(stageRoot, false);
        wall.transform.position = new Vector3(0f, 2.2f, 4.3f);
        wall.transform.localScale = new Vector3(9f, 4.2f, 0.08f);
        wall.GetComponent<Renderer>().sharedMaterial = backdropMaterial;
        Destroy(wall.GetComponent<Collider>());

        GameObject sign = new GameObject("3D Title Sign", typeof(TextMesh));
        sign.transform.SetParent(stageRoot, false);
        sign.transform.position = new Vector3(-2.6f, 2.55f, 4.2f);
        sign.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        sign.transform.localScale = Vector3.one * 0.16f;
        TextMesh text = sign.GetComponent<TextMesh>();
        text.text = "Block Detective";
        text.fontSize = 72;
        text.anchor = TextAnchor.MiddleLeft;
        text.alignment = TextAlignment.Left;
        text.color = new Color(0.08f, 0.16f, 0.26f, 1f);
    }

    private void CreateBuilderFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Builder Stage Floor";
        floor.transform.SetParent(stageRoot, false);
        floor.transform.position = new Vector3(0f, -0.54f, 0f);
        floor.transform.localScale = new Vector3(GridWidth * CellSize + 0.8f, 0.12f, GridDepth * CellSize + 0.8f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

        for (int x = 0; x < GridWidth; x++)
        {
            for (int z = 0; z < GridDepth; z++)
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "Mouse Cell " + x + "," + z;
                tile.transform.SetParent(cellRoot, false);
                tile.transform.position = GridToWorld(new Vector3Int(x, 0, z)) + new Vector3(0f, -0.48f, 0f);
                tile.transform.localScale = new Vector3(CellSize * 0.88f, 0.045f, CellSize * 0.88f);
                tile.GetComponent<Renderer>().sharedMaterial = ((x + z) % 2 == 0) ? floorTileMaterialA : floorTileMaterialB;
                BlockDetectiveDeluxeCell marker = tile.AddComponent<BlockDetectiveDeluxeCell>();
                marker.X = x;
                marker.Z = z;
            }
        }
    }

    private void CreateGridLines()
    {
        float min = -GridWidth * CellSize * 0.5f;
        float max = GridWidth * CellSize * 0.5f;
        float y = -0.43f;

        for (int i = 0; i <= GridWidth; i++)
        {
            float coordinate = min + i * CellSize;
            CreateGridLine("Grid Z " + i, new Vector3(min, y, coordinate), new Vector3(max, y, coordinate));
            CreateGridLine("Grid X " + i, new Vector3(coordinate, y, min), new Vector3(coordinate, y, max));
        }
    }

    private void CreateGridLine(string name, Vector3 start, Vector3 end)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(stageRoot, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = false;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = 0.016f;
        line.endWidth = 0.016f;
        line.material = gridLineMaterial;
        line.startColor = gridLineMaterial.color;
        line.endColor = gridLineMaterial.color;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    private void CreateCursor()
    {
        GameObject cursor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cursor.name = "Mouse Hover Cursor";
        cursor.transform.SetParent(cursorRoot, false);
        cursor.transform.localScale = Vector3.one * 0.78f;
        cursorRenderer = cursor.GetComponent<Renderer>();
        cursorRenderer.sharedMaterial = cursorMaterial;
        Destroy(cursor.GetComponent<Collider>());
    }

    private void CreateAgent()
    {
        agentRoot.position = new Vector3(3.2f, 0.1f, 2.35f);
        agentRoot.rotation = Quaternion.Euler(0f, -28f, 0f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Cube Agent Body";
        body.transform.SetParent(agentRoot, false);
        body.transform.localPosition = new Vector3(0f, 0.52f, 0f);
        body.transform.localScale = new Vector3(0.72f, 0.72f, 0.72f);
        body.GetComponent<Renderer>().sharedMaterial = robotBodyMaterial;
        Destroy(body.GetComponent<Collider>());

        GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
        face.name = "Cube Agent Face";
        face.transform.SetParent(agentRoot, false);
        face.transform.localPosition = new Vector3(0f, 0.57f, -0.38f);
        face.transform.localScale = new Vector3(0.48f, 0.32f, 0.04f);
        face.GetComponent<Renderer>().sharedMaterial = robotFaceMaterial;
        Destroy(face.GetComponent<Collider>());

        CreateAgentEye(new Vector3(-0.14f, 0.61f, -0.43f));
        CreateAgentEye(new Vector3(0.14f, 0.61f, -0.43f));
        CreateAgentAntenna();
    }

    private void CreateAgentEye(Vector3 localPosition)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "Cube Agent Eye";
        eye.transform.SetParent(agentRoot, false);
        eye.transform.localPosition = localPosition;
        eye.transform.localScale = Vector3.one * 0.075f;
        eye.GetComponent<Renderer>().sharedMaterial = robotEyeMaterial;
        Destroy(eye.GetComponent<Collider>());
    }

    private void CreateAgentAntenna()
    {
        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.name = "Cube Agent Antenna Stem";
        stem.transform.SetParent(agentRoot, false);
        stem.transform.localPosition = new Vector3(0f, 0.99f, 0f);
        stem.transform.localScale = new Vector3(0.035f, 0.2f, 0.035f);
        stem.GetComponent<Renderer>().sharedMaterial = robotEyeMaterial;
        Destroy(stem.GetComponent<Collider>());

        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "Cube Agent Antenna Tip";
        tip.transform.SetParent(agentRoot, false);
        tip.transform.localPosition = new Vector3(0f, 1.24f, 0f);
        tip.transform.localScale = Vector3.one * 0.12f;
        tip.GetComponent<Renderer>().sharedMaterial = blockMaterials[2];
        Destroy(tip.GetComponent<Collider>());
    }

    private void BuildInterface()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        GameObject canvasObject = new GameObject("Block Detective Deluxe HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;

        Transform canvasRoot = canvasObject.transform;
        GameObject topBar = CreatePanelObject(canvasRoot, "Top Bar", new Color(0.98f, 1f, 1f, 0.94f));
        ConfigureRect(topBar.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 86f));
        AddHorizontalLayout(topBar, 18, 10, TextAnchor.MiddleLeft, true);

        GameObject leftPanel = CreatePanelObject(canvasRoot, "Evidence Panel", new Color(1f, 1f, 1f, 0.92f));
        ConfigureRect(leftPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, -28f), new Vector2(350f, -178f));
        AddVerticalLayout(leftPanel, 14, 8, TextAnchor.UpperCenter);

        GameObject rightPanel = CreatePanelObject(canvasRoot, "Tutor Panel", new Color(0.08f, 0.12f, 0.17f, 0.9f));
        ConfigureRect(rightPanel.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-18f, -28f), new Vector2(400f, -178f));
        AddVerticalLayout(rightPanel, 14, 8, TextAnchor.UpperCenter);

        GameObject bottomBar = CreatePanelObject(canvasRoot, "Bottom Bar", new Color(0.98f, 1f, 1f, 0.94f));
        ConfigureRect(bottomBar.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 154f));
        AddVerticalLayout(bottomBar, 12, 6, TextAnchor.UpperCenter);

        BuildTopBar(topBar.transform);
        BuildEvidencePanel(leftPanel.transform);
        BuildTutorPanel(rightPanel.transform);
        BuildBottomBar(bottomBar.transform);
    }

    private void BuildTopBar(Transform parent)
    {
        GameObject titleGroup = CreateLayoutGroup(parent, "Title Group", true, 0, 0, TextAnchor.MiddleLeft);
        AddLayoutSize(titleGroup, 300f, 70f, 0f, 0f);
        titleText = CreateText(titleGroup.transform, "Title", "Block Detective Deluxe", 28, TextAnchor.MiddleLeft, new Color(0.06f, 0.12f, 0.2f, 1f));
        AddLayoutSize(titleText.gameObject, 280f, 34f, 0f, 0f);

        GameObject centerGroup = CreateLayoutGroup(parent, "Condition Group", true, 6, 0, TextAnchor.MiddleCenter);
        AddLayoutSize(centerGroup, 780f, 70f, 1f, 0f);

        conditionButtons.Clear();
        for (int i = 0; i < conditions.Count; i++)
        {
            int capturedIndex = i;
            Button button = CreateButton(centerGroup.transform, conditions[i].Label, 118f, 34f, delegate { SelectCondition(capturedIndex); });
            conditionButtons.Add(button);
        }

        GameObject rightGroup = CreateLayoutGroup(parent, "Top Metrics", false, 2, 0, TextAnchor.MiddleRight);
        AddLayoutSize(rightGroup, 430f, 70f, 0f, 0f);
        metaText = CreateText(rightGroup.transform, "Meta", "", 14, TextAnchor.UpperRight, new Color(0.12f, 0.2f, 0.28f, 1f));
        AddLayoutSize(metaText.gameObject, 410f, 28f, 0f, 0f);
        loadText = CreateText(rightGroup.transform, "Load Text", "", 13, TextAnchor.UpperRight, new Color(0.12f, 0.2f, 0.28f, 1f));
        AddLayoutSize(loadText.gameObject, 410f, 24f, 0f, 0f);

        GameObject loadTrack = CreatePanelObject(rightGroup.transform, "Load Track", new Color(0.78f, 0.86f, 0.9f, 0.72f));
        AddLayoutSize(loadTrack, 410f, 10f, 0f, 0f);
        loadFill = CreatePanelObject(loadTrack.transform, "Load Fill", new Color(0.2f, 0.78f, 0.56f, 1f)).GetComponent<Image>();
        ConfigureRect(loadFill.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        loadFill.type = Image.Type.Filled;
        loadFill.fillMethod = Image.FillMethod.Horizontal;
        loadFill.fillOrigin = 0;
        loadFill.fillAmount = 0.3f;
    }

    private void BuildEvidencePanel(Transform parent)
    {
        Text header = CreateText(parent, "Evidence Header", "Evidence Board", 24, TextAnchor.MiddleLeft, new Color(0.08f, 0.14f, 0.22f, 1f));
        AddLayoutSize(header.gameObject, 0f, 30f, 1f, 0f);

        missionText = CreateText(parent, "Mission Text", "", 14, TextAnchor.UpperLeft, new Color(0.12f, 0.18f, 0.26f, 1f));
        AddLayoutSize(missionText.gameObject, 0f, 112f, 1f, 0f);

        frontProjection = CreateProjectionGrid(parent, "Front Projection", GridHeight, GridWidth);
        rightProjection = CreateProjectionGrid(parent, "Right Projection", GridHeight, GridDepth);
        topProjection = CreateProjectionGrid(parent, "Top Projection", GridDepth, GridWidth);

        telemetryText = CreateText(parent, "Telemetry Text", "", 12, TextAnchor.UpperLeft, new Color(0.22f, 0.3f, 0.38f, 1f));
        AddLayoutSize(telemetryText.gameObject, 0f, 106f, 1f, 0f);
    }

    private void BuildTutorPanel(Transform parent)
    {
        Text header = CreateText(parent, "Tutor Header", "Tutor Lab", 24, TextAnchor.MiddleLeft, Color.white);
        AddLayoutSize(header.gameObject, 0f, 30f, 1f, 0f);

        conditionText = CreateText(parent, "Condition Text", "", 13, TextAnchor.UpperLeft, new Color(0.74f, 0.9f, 1f, 1f));
        AddLayoutSize(conditionText.gameObject, 0f, 64f, 1f, 0f);

        tutorText = CreateText(parent, "Tutor Text", "", 14, TextAnchor.UpperLeft, new Color(0.94f, 0.98f, 1f, 1f));
        AddLayoutSize(tutorText.gameObject, 0f, 178f, 1f, 0f);

        diagnosticText = CreateText(parent, "Diagnostic Text", "", 13, TextAnchor.UpperLeft, new Color(1f, 0.9f, 0.55f, 1f));
        AddLayoutSize(diagnosticText.gameObject, 0f, 126f, 1f, 0f);

        cubeText = CreateText(parent, "Cube Text", "", 13, TextAnchor.UpperLeft, new Color(0.82f, 0.94f, 1f, 1f));
        AddLayoutSize(cubeText.gameObject, 0f, 112f, 1f, 0f);

        GameObject llmRow = CreateLayoutGroup(parent, "LLM Row", true, 6, 0, TextAnchor.MiddleLeft);
        AddLayoutSize(llmRow, 0f, 36f, 1f, 0f);
        apiKeyInput = CreateInputField(llmRow.transform, "OpenAI API Key", "OpenAI API key", 172f, 34f, true);
        modelInput = CreateInputField(llmRow.transform, "Model", DefaultModel, 122f, 34f, false);
        modelInput.text = DefaultModel;
        askGptButton = CreateButton(llmRow.transform, "Ask GPT", 78f, 34f, AskGptTutor);

        teachInput = CreateInputField(parent, "Teach Rule Input", "Teach Cube a spatial rule after feedback...", 0f, 54f, false);
        teachInput.lineType = InputField.LineType.MultiLineNewline;
        teachInput.characterLimit = 260;

        GameObject teachRow = CreateLayoutGroup(parent, "Teach Row", true, 6, 0, TextAnchor.MiddleLeft);
        AddLayoutSize(teachRow, 0f, 38f, 1f, 0f);
        teachButton = CreateButton(teachRow.transform, "Teach Cube", 120f, 34f, TeachCube);
        CreateButton(teachRow.transform, "Hint", 78f, 34f, ShowHint);
        CreateButton(teachRow.transform, "Target Ghost", 118f, 34f, ShowTargetGhost);
    }

    private void BuildBottomBar(Transform parent)
    {
        statusText = CreateText(parent, "Status Text", "", 15, TextAnchor.MiddleCenter, new Color(0.08f, 0.14f, 0.22f, 1f));
        AddLayoutSize(statusText.gameObject, 0f, 26f, 1f, 0f);

        cursorText = CreateText(parent, "Cursor Text", "", 13, TextAnchor.MiddleCenter, new Color(0.12f, 0.2f, 0.28f, 1f));
        AddLayoutSize(cursorText.gameObject, 0f, 22f, 1f, 0f);

        GameObject actionRow = CreateLayoutGroup(parent, "Action Row", true, 8, 0, TextAnchor.MiddleCenter);
        AddLayoutSize(actionRow, 0f, 38f, 1f, 0f);
        CreateButton(actionRow.transform, "Paint", 86f, 34f, delegate { AddBlockAtCell(GetActiveCell()); });
        CreateButton(actionRow.transform, "Remove", 92f, 34f, delegate { RemoveBlockAtCell(GetActiveCell()); });
        CreateButton(actionRow.transform, "Layer -", 86f, 34f, delegate { SetActiveLayer(activeLayer - 1); });
        CreateButton(actionRow.transform, "Layer +", 86f, 34f, delegate { SetActiveLayer(activeLayer + 1); });
        CreateButton(actionRow.transform, "Submit", 96f, 34f, SubmitAnswer);
        CreateButton(actionRow.transform, "Reset Build", 110f, 34f, ResetBuild);
        nextButton = CreateButton(actionRow.transform, "Next Case", 108f, 34f, NextCase);

        GameObject viewRow = CreateLayoutGroup(parent, "View Row", true, 8, 0, TextAnchor.MiddleCenter);
        AddLayoutSize(viewRow, 0f, 36f, 1f, 0f);
        CreateButton(viewRow.transform, "Free", 76f, 32f, delegate { ApplyCameraView(DetectiveView.Free); });
        CreateButton(viewRow.transform, "Front", 76f, 32f, delegate { ApplyCameraView(DetectiveView.Front); });
        CreateButton(viewRow.transform, "Right", 76f, 32f, delegate { ApplyCameraView(DetectiveView.Right); });
        CreateButton(viewRow.transform, "Top", 76f, 32f, delegate { ApplyCameraView(DetectiveView.Top); });
        CreateButton(viewRow.transform, "Witness", 92f, 32f, delegate { ApplyCameraView(DetectiveView.Witness); });
        CreateButton(viewRow.transform, "Effort 1", 90f, 32f, delegate { SetMentalEffort(1); });
        CreateButton(viewRow.transform, "Effort 2", 90f, 32f, delegate { SetMentalEffort(2); });
        CreateButton(viewRow.transform, "Effort 3", 90f, 32f, delegate { SetMentalEffort(3); });
    }

    private void SelectCondition(int requestedIndex)
    {
        conditionIndex = Mathf.Clamp(requestedIndex, 0, conditions.Count - 1);

        for (int i = 0; i < conditionButtons.Count; i++)
        {
            Image image = conditionButtons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = i == conditionIndex
                    ? new Color(0.12f, 0.54f, 0.95f, 1f)
                    : GetConditionButtonBaseColor(i);
            }
        }

        UpdateConditionControls();
        SetStatus("Condition selected: " + conditions[conditionIndex].Label);
        LogEvent("select_condition", conditions[conditionIndex].Label);
    }

    private Color GetConditionButtonBaseColor(int index)
    {
        if (conditionButtonBaseColors == null || conditionButtonBaseColors.Length != conditions.Count)
        {
            conditionButtonBaseColors = new Color[conditions.Count];
            for (int i = 0; i < conditionButtonBaseColors.Length; i++)
            {
                conditionButtonBaseColors[i] = new Color(0.18f, 0.25f, 0.32f, 1f);
            }
        }

        return conditionButtonBaseColors[index];
    }

    private void UpdateConditionControls()
    {
        if (conditions.Count == 0)
        {
            return;
        }

        ConditionData condition = conditions[conditionIndex];
        if (askGptButton != null)
        {
            askGptButton.interactable = condition.Feedback != FeedbackMode.VisualDiagnostic && !gptRequestRunning;
        }

        if (teachButton != null)
        {
            teachButton.interactable = condition.Feedback == FeedbackMode.TeachAgent;
        }

        if (conditionText != null)
        {
            conditionText.text =
                condition.Label + "\n" +
                "Feedback: " + condition.Feedback + " | Difficulty: " + condition.Difficulty + "\n" +
                condition.ResearchQuestion;
        }
    }

    private void StartCase(int requestedIndex)
    {
        caseIndex = (requestedIndex + cases.Count) % cases.Count;
        currentCaseSolved = false;
        attempts = 0;
        hintsUsed = 0;
        actionsThisCase = 0;
        viewSwitchesThisCase = 0;
        mentalEffort = 2;
        activeLayer = 0;
        caseStartTime = Time.time;
        lastDiagnostic = null;
        hoverCell = new Vector3Int(0, 0, 0);
        hoverValid = true;
        hoverIsBlock = false;
        isPainting = false;
        isDragging = false;
        draggingObject = null;

        userVoxels.Clear();
        ClearBlockObjects();
        ClearChildren(diagnosticGhostRoot);

        if (teachInput != null)
        {
            teachInput.text = "";
        }

        if (nextButton != null)
        {
            nextButton.interactable = false;
        }

        RefreshBlockColors();
        UpdateCursorVisual();
        UpdateProjectionPanels();
        diagnosticText.text = "";
        tutorText.text = OpeningTutorText();
        cubeText.text = "Cube: I will watch your strategy. In Teach conditions, explain your rule after a diagnostic.";
        ApplyCameraView(DetectiveView.Free);
        SetStatus("Drag blocks with the mouse, paint empty cells, right-click to remove, mouse wheel changes layer.");
        LogEvent("start_case", cases[caseIndex].Id);
    }

    private void UpdateHoverFromMouse()
    {
        if (mainCamera == null)
        {
            return;
        }

        hoverValid = false;
        hoverIsBlock = false;
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        Array.Sort(hits, delegate (RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (IsDraggingCollider(hit.collider))
            {
                continue;
            }

            BlockDetectiveDeluxeBlock block = hit.collider.GetComponent<BlockDetectiveDeluxeBlock>();
            if (block != null)
            {
                hoverCell = block.Cell;
                activeLayer = block.Cell.y;
                hoverValid = true;
                hoverIsBlock = true;
                break;
            }

            BlockDetectiveDeluxeCell cell = hit.collider.GetComponent<BlockDetectiveDeluxeCell>();
            if (cell != null)
            {
                hoverCell = new Vector3Int(cell.X, activeLayer, cell.Z);
                hoverValid = true;
                break;
            }
        }

        if (isDragging && hoverValid)
        {
            hoverCell = new Vector3Int(hoverCell.x, activeLayer, hoverCell.z);
        }

        UpdateCursorVisual();
    }

    private bool IsDraggingCollider(Collider hitCollider)
    {
        if (!isDragging || draggingObject == null || hitCollider == null)
        {
            return false;
        }

        return hitCollider.gameObject == draggingObject || hitCollider.transform.IsChildOf(draggingObject.transform);
    }

    private void HandleMouseInput()
    {
        bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (!pointerOverUi)
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                SetActiveLayer(activeLayer + (wheel > 0f ? 1 : -1));
            }
        }

        if (pointerOverUi && !isDragging && !isPainting)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) && hoverValid)
        {
            if (hoverIsBlock && userVoxels.Contains(hoverCell))
            {
                BeginDrag(hoverCell);
            }
            else
            {
                isPainting = true;
                AddBlockAtCell(hoverCell);
            }
        }

        if (Input.GetMouseButton(0))
        {
            if (isDragging)
            {
                UpdateDragObject();
            }
            else if (isPainting && hoverValid)
            {
                AddBlockAtCell(hoverCell);
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isDragging)
            {
                EndDrag();
            }

            isPainting = false;
        }

        if (!pointerOverUi && Input.GetMouseButton(1) && hoverValid)
        {
            RemoveBlockAtCell(hoverCell);
        }
    }

    private void HandleKeyboardInput()
    {
        if (IsTypingInInput())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            SetActiveLayer(activeLayer - 1);
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            SetActiveLayer(activeLayer + 1);
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleBlockAtCell(GetActiveCell());
        }
        else if (Input.GetKeyDown(KeyCode.Return))
        {
            SubmitAnswer();
        }
        else if (Input.GetKeyDown(KeyCode.Backspace))
        {
            RemoveBlockAtCell(GetActiveCell());
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

    private bool IsTypingInInput()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
        {
            return false;
        }

        return EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;
    }

    private void BeginDrag(Vector3Int cell)
    {
        dragStartCell = cell;
        GameObject block;
        if (!blockObjects.TryGetValue(cell, out block))
        {
            return;
        }

        isDragging = true;
        draggingObject = block;
        SetStatus("Dragging block. Drop on an empty highlighted cell.");
        LogEvent("begin_drag", cell.ToString());
    }

    private void UpdateDragObject()
    {
        if (draggingObject == null || !hoverValid)
        {
            return;
        }

        draggingObject.transform.position = GridToWorld(hoverCell) + new Vector3(0f, 0.12f, 0f);
    }

    private void EndDrag()
    {
        if (draggingObject == null)
        {
            isDragging = false;
            return;
        }

        Vector3Int dropCell = hoverValid ? hoverCell : dragStartCell;
        bool validDrop = IsInsideGrid(dropCell) && (!userVoxels.Contains(dropCell) || dropCell == dragStartCell);

        if (!validDrop)
        {
            draggingObject.transform.position = GridToWorld(dragStartCell);
            SetStatus("That cell is occupied. The block returned to its start position.");
            LogEvent("drag_rejected", dropCell.ToString());
        }
        else
        {
            userVoxels.Remove(dragStartCell);
            blockObjects.Remove(dragStartCell);
            userVoxels.Add(dropCell);
            blockObjects[dropCell] = draggingObject;
            draggingObject.transform.position = GridToWorld(dropCell);

            BlockDetectiveDeluxeBlock marker = draggingObject.GetComponent<BlockDetectiveDeluxeBlock>();
            if (marker != null)
            {
                marker.Cell = dropCell;
            }

            actionsThisCase++;
            lastDiagnostic = null;
            ClearChildren(diagnosticGhostRoot);
            RefreshBlockColors();
            UpdateProjectionPanels();
            SetStatus("Block moved to x" + dropCell.x + " y" + dropCell.y + " z" + dropCell.z + ".");
            LogEvent("end_drag", dragStartCell + " -> " + dropCell);
        }

        draggingObject = null;
        isDragging = false;
    }

    private void SetActiveLayer(int layer)
    {
        int nextLayer = Mathf.Clamp(layer, 0, GridHeight - 1);
        if (nextLayer == activeLayer)
        {
            return;
        }

        activeLayer = nextLayer;
        hoverCell = new Vector3Int(hoverCell.x, activeLayer, hoverCell.z);
        actionsThisCase++;
        UpdateCursorVisual();
        SetStatus("Active layer changed to y" + activeLayer + ".");
        LogEvent("set_layer", activeLayer.ToString());
    }

    private Vector3Int GetActiveCell()
    {
        if (hoverValid)
        {
            return new Vector3Int(hoverCell.x, activeLayer, hoverCell.z);
        }

        return new Vector3Int(GridWidth / 2, activeLayer, GridDepth / 2);
    }

    private void ToggleBlockAtCell(Vector3Int cell)
    {
        if (userVoxels.Contains(cell))
        {
            RemoveBlockAtCell(cell);
        }
        else
        {
            AddBlockAtCell(cell);
        }
    }

    private void AddBlockAtCell(Vector3Int cell)
    {
        if (!IsInsideGrid(cell) || userVoxels.Contains(cell))
        {
            return;
        }

        userVoxels.Add(cell);
        GameObject block = CreateBlockObject(cell);
        blockObjects[cell] = block;
        actionsThisCase++;
        lastDiagnostic = null;
        ClearChildren(diagnosticGhostRoot);
        UpdateProjectionPanels();
        SetStatus("Painted block at x" + cell.x + " y" + cell.y + " z" + cell.z + ".");
        LogEvent("add_block", cell.ToString());
    }

    private void RemoveBlockAtCell(Vector3Int cell)
    {
        if (!userVoxels.Contains(cell))
        {
            return;
        }

        userVoxels.Remove(cell);
        GameObject block;
        if (blockObjects.TryGetValue(cell, out block))
        {
            Destroy(block);
            blockObjects.Remove(cell);
        }

        actionsThisCase++;
        lastDiagnostic = null;
        ClearChildren(diagnosticGhostRoot);
        UpdateProjectionPanels();
        SetStatus("Removed block at x" + cell.x + " y" + cell.y + " z" + cell.z + ".");
        LogEvent("remove_block", cell.ToString());
    }

    private GameObject CreateBlockObject(Vector3Int cell)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "Player Block " + cell;
        block.transform.SetParent(blockRoot, false);
        block.transform.position = GridToWorld(cell);
        block.transform.localScale = Vector3.one * 0.72f;
        block.GetComponent<Renderer>().sharedMaterial = GetBlockMaterial(cell);
        block.AddComponent<BlockDetectiveDeluxeBlock>().Cell = cell;

        GameObject shine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shine.name = "Block Shine";
        shine.transform.SetParent(block.transform, false);
        shine.transform.localPosition = new Vector3(-0.18f, 0.52f, -0.18f);
        shine.transform.localScale = new Vector3(0.48f, 0.03f, 0.48f);
        shine.GetComponent<Renderer>().sharedMaterial = blockShineMaterial;
        Destroy(shine.GetComponent<Collider>());
        return block;
    }

    private Material GetBlockMaterial(Vector3Int cell)
    {
        int index = Mathf.Abs(cell.x + cell.y * 2 + cell.z * 3 + caseIndex) % blockMaterials.Length;
        return blockMaterials[index];
    }

    private void SubmitAnswer()
    {
        attempts++;
        lastDiagnostic = Diagnose(userVoxels, cases[caseIndex]);
        ApplyDiagnosticVisuals(lastDiagnostic);
        UpdateProjectionPanels();

        diagnosticText.text = FormatDiagnostic(lastDiagnostic);
        tutorText.text = GenerateLocalTutorFeedback(lastDiagnostic);
        cubeText.text = GenerateCubePrompt(lastDiagnostic);

        if (lastDiagnostic.Passed)
        {
            currentCaseSolved = true;
            solvedCases++;
            int earned = Mathf.Max(50, 180 - Mathf.RoundToInt(Time.time - caseStartTime) - hintsUsed * 10 - attempts * 8);
            score += earned;
            if (nextButton != null)
            {
                nextButton.interactable = true;
            }

            SetStatus("Case solved. Explain the reusable strategy, then continue.");
            LogEvent("submit_pass", "points=" + earned);
        }
        else
        {
            score = Mathf.Max(0, score - 4);
            SetStatus("Diagnostic complete. Red blocks are extras; yellow ghosts are missing target blocks.");
            LogEvent("submit_fail", lastDiagnostic.Error.ToString());
        }
    }

    private DiagnosticResult Diagnose(HashSet<Vector3Int> answer, CaseData data)
    {
        DiagnosticResult result = new DiagnosticResult();
        HashSet<Vector3Int> target = new HashSet<Vector3Int>(data.Target);
        result.Passed = target.SetEquals(answer);

        foreach (Vector3Int targetCell in target)
        {
            if (!answer.Contains(targetCell))
            {
                result.MissingCells.Add(targetCell);
            }
        }

        foreach (Vector3Int answerCell in answer)
        {
            if (!target.Contains(answerCell))
            {
                result.ExtraCells.Add(answerCell);
            }
        }

        AddProjectionFact(result, "front", CompareBoolGrid(GetFrontProjection(answer), GetFrontProjection(target)));
        AddProjectionFact(result, "right", CompareBoolGrid(GetRightProjection(answer), GetRightProjection(target)));
        AddProjectionFact(result, "top", CompareBoolGrid(GetTopProjection(answer), GetTopProjection(target)));

        if (result.Passed)
        {
            result.Error = ErrorType.None;
            result.EngineFacts.Add("exact voxel set matches target");
            return result;
        }

        if (target.SetEquals(MirrorX(answer)))
        {
            result.Error = ErrorType.LeftRightMirror;
            result.EngineFacts.Add("answer becomes the target after an x-axis mirror");
        }
        else if (target.SetEquals(MirrorZ(answer)))
        {
            result.Error = ErrorType.FrontBackReverse;
            result.EngineFacts.Add("answer becomes the target after a front-back reversal");
        }
        else if (SameFootprint(answer, target))
        {
            result.Error = ErrorType.HeightError;
            result.EngineFacts.Add("top footprint matches, but at least one column height differs");
        }
        else if (answer.Count < target.Count)
        {
            result.Error = ErrorType.MissingBlock;
            result.EngineFacts.Add("answer has fewer blocks than target");
        }
        else if (answer.Count > target.Count)
        {
            result.Error = ErrorType.ExtraBlock;
            result.EngineFacts.Add("answer has more blocks than target");
        }
        else if (result.MatchedViews.Count >= 2)
        {
            result.Error = ErrorType.ProjectionAmbiguity;
            result.EngineFacts.Add("multiple projections match, but exact 3D structure differs");
        }
        else
        {
            result.Error = ErrorType.Unknown;
            result.EngineFacts.Add("no single simple misconception pattern matched");
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

    private void ApplyDiagnosticVisuals(DiagnosticResult result)
    {
        ClearChildren(diagnosticGhostRoot);
        HashSet<Vector3Int> target = new HashSet<Vector3Int>(cases[caseIndex].Target);

        foreach (KeyValuePair<Vector3Int, GameObject> entry in blockObjects)
        {
            Renderer renderer = entry.Value.GetComponent<Renderer>();
            if (renderer == null)
            {
                continue;
            }

            if (result.Passed || target.Contains(entry.Key))
            {
                renderer.sharedMaterial = correctMaterial;
            }
            else
            {
                renderer.sharedMaterial = extraMaterial;
            }
        }

        for (int i = 0; i < result.MissingCells.Count; i++)
        {
            CreateGhostBlock("Missing Ghost " + i, result.MissingCells[i], missingGhostMaterial, diagnosticGhostRoot);
        }
    }

    private void RefreshBlockColors()
    {
        foreach (KeyValuePair<Vector3Int, GameObject> entry in blockObjects)
        {
            Renderer renderer = entry.Value.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetBlockMaterial(entry.Key);
            }
        }
    }

    private void ShowTargetGhost()
    {
        ClearChildren(diagnosticGhostRoot);
        CaseData data = cases[caseIndex];
        for (int i = 0; i < data.Target.Length; i++)
        {
            if (!userVoxels.Contains(data.Target[i]))
            {
                CreateGhostBlock("Target Ghost " + i, data.Target[i], targetGhostMaterial, diagnosticGhostRoot);
            }
        }

        hintsUsed++;
        SetStatus("Target ghost shown as a hint. It will be counted in telemetry.");
        LogEvent("target_ghost", data.Id);
    }

    private void CreateGhostBlock(string name, Vector3Int cell, Material material, Transform parent)
    {
        GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghost.name = name;
        ghost.transform.SetParent(parent, false);
        ghost.transform.position = GridToWorld(cell);
        ghost.transform.localScale = Vector3.one * 0.78f;
        ghost.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(ghost.GetComponent<Collider>());
    }

    private void AskGptTutor()
    {
        ConditionData condition = conditions[conditionIndex];
        if (condition.Feedback == FeedbackMode.VisualDiagnostic)
        {
            tutorText.text = "This condition intentionally uses visual diagnostic feedback only. Switch to an LLM or Teach condition to call GPT.";
            return;
        }

        if (gptRequestRunning)
        {
            return;
        }

        string apiKey = apiKeyInput == null ? "" : apiKeyInput.text.Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            tutorText.text = GenerateLocalTutorFeedback(lastDiagnostic) + "\n\nLocal mode: enter an OpenAI API key to call GPT from Unity.";
            SetStatus("No API key entered. Local tutor feedback was used.");
            return;
        }

        StartCoroutine(CallOpenAiTutor(apiKey, GetModelName(), BuildGptPrompt()));
    }

    private IEnumerator CallOpenAiTutor(string apiKey, string modelName, string prompt)
    {
        gptRequestRunning = true;
        UpdateConditionControls();
        SetStatus("Calling GPT tutor...");
        tutorText.text = "GPT tutor is reading the geometry facts...";

        string body =
            "{" +
            "\"model\":\"" + EscapeJson(modelName) + "\"," +
            "\"input\":\"" + EscapeJson(prompt) + "\"," +
            "\"max_output_tokens\":260," +
            "\"temperature\":0.4" +
            "}";

        byte[] bytes = Encoding.UTF8.GetBytes(body);
        UnityWebRequest request = new UnityWebRequest(OpenAiResponsesUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(bytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return request.SendWebRequest();

        gptRequestRunning = false;
        UpdateConditionControls();

        bool failed = request.result == UnityWebRequest.Result.ConnectionError ||
                      request.result == UnityWebRequest.Result.ProtocolError ||
                      request.result == UnityWebRequest.Result.DataProcessingError;

        if (failed)
        {
            tutorText.text = "GPT call failed: " + request.error + "\n\nFallback feedback:\n" + GenerateLocalTutorFeedback(lastDiagnostic);
            SetStatus("GPT failed, so the deterministic local tutor stayed active.");
            LogEvent("gpt_error", request.error);
            request.Dispose();
            yield break;
        }

        string text = ExtractOpenAiText(request.downloadHandler.text);
        if (string.IsNullOrEmpty(text))
        {
            tutorText.text = "GPT responded, but no output text was found.\n\nFallback feedback:\n" + GenerateLocalTutorFeedback(lastDiagnostic);
            SetStatus("GPT response had no readable text.");
            LogEvent("gpt_empty", "empty");
        }
        else
        {
            tutorText.text = "GPT Tutor\n" + text;
            SetStatus("GPT tutor feedback received.");
            LogEvent("gpt_feedback", "chars=" + text.Length);
        }

        request.Dispose();
    }

    private string GetModelName()
    {
        if (modelInput == null || string.IsNullOrWhiteSpace(modelInput.text))
        {
            return DefaultModel;
        }

        return modelInput.text.Trim();
    }

    private string BuildGptPrompt()
    {
        CaseData data = cases[caseIndex];
        ConditionData condition = conditions[conditionIndex];
        DiagnosticResult result = lastDiagnostic;
        if (result == null)
        {
            result = Diagnose(userVoxels, data);
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("You are the GPT tutor inside a spatial skills game called Block Detective.");
        builder.AppendLine("The deterministic geometry engine decides correctness. You only explain the engine facts.");
        builder.AppendLine("Give concise, supportive feedback for a novice learner. Use at most 5 short bullet points.");
        builder.AppendLine("Avoid claiming a block is correct unless it appears in the geometry facts.");
        builder.AppendLine();
        builder.AppendLine("Condition: " + condition.Label);
        builder.AppendLine("Case: " + data.Id + " - " + data.Title);
        builder.AppendLine("Mission: " + data.Mission);
        builder.AppendLine("Prediction prompt: " + data.PredictionPrompt);
        builder.AppendLine("Vocabulary: " + data.Vocabulary);
        builder.AppendLine("Cognitive load: " + EstimateLoadBand() + " (" + Mathf.RoundToInt(EstimateLoadScore() * 100f) + "/100)");
        builder.AppendLine("Attempts: " + attempts + ", hints: " + hintsUsed + ", actions: " + actionsThisCase);
        builder.AppendLine("Passed: " + result.Passed);
        builder.AppendLine("Error type: " + FormatErrorType(result.Error));
        builder.AppendLine("Matched views: " + JoinOrNone(result.MatchedViews));
        builder.AppendLine("Mismatched views: " + JoinOrNone(result.MismatchedViews));
        builder.AppendLine("Missing cells: " + FormatCells(result.MissingCells));
        builder.AppendLine("Extra cells: " + FormatCells(result.ExtraCells));
        builder.AppendLine("Target front:\n" + ProjectionToString(GetFrontProjection(data.Target)));
        builder.AppendLine("Player front:\n" + ProjectionToString(GetFrontProjection(userVoxels)));
        builder.AppendLine("Target right:\n" + ProjectionToString(GetRightProjection(data.Target)));
        builder.AppendLine("Player right:\n" + ProjectionToString(GetRightProjection(userVoxels)));
        builder.AppendLine("Target top:\n" + ProjectionToString(GetTopProjection(data.Target)));
        builder.AppendLine("Player top:\n" + ProjectionToString(GetTopProjection(userVoxels)));
        builder.AppendLine("If this is a TeachAgent condition, end with one rule the learner can teach Cube.");
        return builder.ToString();
    }

    private string GenerateLocalTutorFeedback(DiagnosticResult result)
    {
        if (result == null)
        {
            return OpeningTutorText();
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Visual diagnostic feedback");
        builder.AppendLine("Load state: " + EstimateLoadBand());

        if (result.Passed)
        {
            builder.AppendLine("Correct. Your voxel set matches the target exactly.");
            builder.AppendLine("Self-explain: which view helped you avoid the most tempting mistake?");
            return builder.ToString();
        }

        if (EstimateLoadBand() == LoadBand.Overloaded)
        {
            builder.AppendLine("Short path: change one view at a time. Do not rebuild the whole model yet.");
        }

        builder.AppendLine(FeedbackForError(result.Error));
        builder.AppendLine("Matched views: " + JoinOrNone(result.MatchedViews));
        builder.AppendLine("Check next: " + NextViewToCheck(result));
        builder.AppendLine("Teach prompt: turn this into an if-then spatial rule.");
        return builder.ToString();
    }

    private string OpeningTutorText()
    {
        CaseData data = cases[caseIndex];
        return
            "Learning loop\n" +
            "1. Inspect front, right, and top evidence.\n" +
            "2. Predict the hidden relation.\n" +
            "3. Drag or paint blocks directly on the board.\n" +
            "4. Submit for deterministic geometry diagnosis.\n" +
            "5. In GPT/Teach conditions, ask for an explanation or teach Cube.\n\n" +
            "Prediction prompt: " + data.PredictionPrompt + "\n" +
            "Spatial vocabulary: " + data.Vocabulary;
    }

    private string GenerateCubePrompt(DiagnosticResult result)
    {
        ConditionData condition = conditions[conditionIndex];
        if (condition.Feedback != FeedbackMode.TeachAgent)
        {
            return "Cube: Teach-agent feedback is disabled in this condition. Switch to C3 or C6 to test recursive feedback.";
        }

        if (result.Passed)
        {
            return "Cube: Nice. Teach me the general rule so I can solve a new case, not just copy this one.";
        }

        switch (result.Error)
        {
            case ErrorType.FrontBackReverse:
                return "Cube: I trusted the front view too much. What side/top rule tells me where depth goes?";
            case ErrorType.LeftRightMirror:
                return "Cube: I mixed up left and right. How should I use viewpoint before placing blocks?";
            case ErrorType.HeightError:
                return "Cube: My footprint looked right. How do I check column height systematically?";
            case ErrorType.MissingBlock:
                return "Cube: I left out a block. Which projection mark should I compare first?";
            case ErrorType.ExtraBlock:
                return "Cube: I added too much. How do I identify the block that creates an extra mark?";
            default:
                return "Cube: Teach me a rule that mentions a view and a spatial relation.";
        }
    }

    private void TeachCube()
    {
        ConditionData condition = conditions[conditionIndex];
        if (condition.Feedback != FeedbackMode.TeachAgent)
        {
            cubeText.text = "Cube: This condition does not include teach-the-agent feedback. Use C3 or C6 for the teaching loop.";
            return;
        }

        string rule = teachInput == null ? "" : teachInput.text.Trim();
        if (rule.Length < 10)
        {
            cubeText.text = "Cube: I need a fuller rule. Mention a view and a relation such as depth, height, mirror, left, or right.";
            SetStatus("Teach Cube with a complete spatial rule.");
            return;
        }

        ErrorType error = lastDiagnostic == null ? ErrorType.Unknown : lastDiagnostic.Error;
        int quality = ScoreRuleQuality(rule, error);
        bool success = quality >= 64;

        cubeText.text =
            "Structured rule extracted\n" +
            "{\n" +
            "  ruleName: \"" + ExtractRuleName(rule, error) + "\",\n" +
            "  condition: \"" + ExtractCondition(rule) + "\",\n" +
            "  strategy: \"" + ExtractStrategy(rule, error) + "\",\n" +
            "  quality: " + quality + "/100\n" +
            "}\n\n" +
            (success
                ? "Cube test: success on a near-transfer case. Recursive feedback: you turned a move into a reusable strategy."
                : "Cube test: partial. Add a clearer view cue and a spatial relation, then teach again.");

        score += success ? 22 : 5;
        SetStatus(success ? "Cube learned your rule." : "Cube needs a clearer rule.");
        LogEvent("teach_cube", "quality=" + quality + ", success=" + success);
    }

    private void ShowHint()
    {
        hintsUsed++;
        actionsThisCase++;

        if (lastDiagnostic == null)
        {
            tutorText.text =
                "Hint ladder\n" +
                "1. Start with the target top footprint.\n" +
                "2. Use front/right views to decide height.\n" +
                "3. If two views match but the model fails, suspect hidden depth or ambiguity.\n\n" +
                cases[caseIndex].PredictionPrompt;
        }
        else
        {
            tutorText.text =
                "Hint ladder\n" +
                "1. " + NextViewToCheck(lastDiagnostic) + "\n" +
                "2. " + FeedbackForError(lastDiagnostic.Error) + "\n" +
                "3. Change one block or one column before submitting again.";
        }

        SetStatus("Hint recorded in telemetry.");
        LogEvent("hint", cases[caseIndex].Id);
    }

    private void ResetBuild()
    {
        userVoxels.Clear();
        ClearBlockObjects();
        ClearChildren(diagnosticGhostRoot);
        currentCaseSolved = false;
        lastDiagnostic = null;

        if (nextButton != null)
        {
            nextButton.interactable = false;
        }

        diagnosticText.text = "";
        tutorText.text = OpeningTutorText();
        cubeText.text = "Cube: Reset noted. I am watching for your next strategy.";
        UpdateProjectionPanels();
        SetStatus("Build reset. No files or project assets were deleted.");
        LogEvent("reset_build", cases[caseIndex].Id);
    }

    private void NextCase()
    {
        int nextIndex = caseIndex + 1;
        if (conditions[conditionIndex].Difficulty == DifficultyPolicy.Adaptive)
        {
            int desiredDifficulty = cases[caseIndex].Difficulty;
            LoadBand load = EstimateLoadBand();
            if (attempts <= 1 && hintsUsed == 0 && load != LoadBand.Overloaded)
            {
                desiredDifficulty = Mathf.Min(4, desiredDifficulty + 1);
            }
            else if (attempts >= 3 || load == LoadBand.Overloaded)
            {
                desiredDifficulty = Mathf.Max(1, desiredDifficulty - 1);
            }

            nextIndex = FindNextCaseAtDifficulty(desiredDifficulty);
            SetStatus("Adaptive difficulty selected a level " + desiredDifficulty + " case.");
            LogEvent("adaptive_next", "difficulty=" + desiredDifficulty);
        }

        StartCase(nextIndex);
    }

    private int FindNextCaseAtDifficulty(int difficulty)
    {
        for (int step = 1; step <= cases.Count; step++)
        {
            int candidate = (caseIndex + step) % cases.Count;
            if (cases[candidate].Difficulty == difficulty)
            {
                return candidate;
            }
        }

        return (caseIndex + 1) % cases.Count;
    }

    private void SetMentalEffort(int value)
    {
        mentalEffort = Mathf.Clamp(value, 1, 3);
        SetStatus("Subjective mental effort saved: " + mentalEffort + "/3.");
        LogEvent("mental_effort", mentalEffort.ToString());
    }

    private void ApplyCameraView(DetectiveView view)
    {
        if (mainCamera == null)
        {
            return;
        }

        Vector3 target = new Vector3(0f, 0.82f, 0f);
        switch (view)
        {
            case DetectiveView.Front:
                mainCamera.transform.position = new Vector3(0f, 1.9f, -7.4f);
                break;
            case DetectiveView.Right:
                mainCamera.transform.position = new Vector3(7.4f, 1.9f, 0f);
                break;
            case DetectiveView.Top:
                mainCamera.transform.position = new Vector3(0.01f, 8.2f, 0.01f);
                target = Vector3.zero;
                break;
            case DetectiveView.Witness:
                mainCamera.transform.position = new Vector3(5.7f, 2.7f, 3.9f);
                break;
            default:
                mainCamera.transform.position = new Vector3(4.8f, 4.6f, -6.7f);
                break;
        }

        mainCamera.transform.LookAt(target);
        viewSwitchesThisCase++;
        LogEvent("switch_view", view.ToString());
    }

    private void UpdateHud()
    {
        if (cases.Count == 0 || titleText == null)
        {
            return;
        }

        CaseData data = cases[caseIndex];
        ConditionData condition = conditions[conditionIndex];
        titleText.text = "Block Detective Deluxe";
        metaText.text = data.Id + " | Difficulty " + data.Difficulty + " | Score " + score + " | Solved " + solvedCases;
        float loadScore = EstimateLoadScore();
        LoadBand load = EstimateLoadBand(loadScore);
        loadText.text = "Load " + load + " | Effort " + mentalEffort + " | Time " + Mathf.FloorToInt(Time.time - caseStartTime) + "s";
        if (loadFill != null)
        {
            loadFill.fillAmount = loadScore;
            loadFill.color = LoadColor(load);
        }

        missionText.text =
            data.Title + "\n" +
            data.Mission + "\n\n" +
            data.PredictionPrompt + "\n" +
            "Witness note: " + data.WitnessNote;

        cursorText.text =
            "Mouse: drag blocks, paint empty cells, right-click remove, wheel layer | " +
            "Keyboard: Space toggle, Q/E layer, Enter submit | Active cell x" + GetActiveCell().x +
            " y" + activeLayer + " z" + GetActiveCell().z;

        telemetryText.text =
            "Telemetry snapshot\n" +
            "Condition: " + condition.Label + "\n" +
            "Blocks: " + userVoxels.Count + " / target " + data.Target.Length + "\n" +
            "Attempts: " + attempts + " | Hints: " + hintsUsed + "\n" +
            "Actions: " + actionsThisCase + " | Views: " + viewSwitchesThisCase + "\n" +
            "Events recorded: " + sessionEvents.Count;

        UpdateConditionControls();
        UpdateProjectionPanels();
    }

    private void UpdateProjectionPanels()
    {
        if (frontProjection == null || cases.Count == 0)
        {
            return;
        }

        CaseData data = cases[caseIndex];
        bool revealTop = data.ShowTop || lastDiagnostic != null || currentCaseSolved;
        UpdateProjectionGrid(frontProjection, GetFrontProjection(data.Target), GetFrontProjection(userVoxels), data.ShowFront, true);
        UpdateProjectionGrid(rightProjection, GetRightProjection(data.Target), GetRightProjection(userVoxels), data.ShowRight, true);
        UpdateProjectionGrid(topProjection, GetTopProjection(data.Target), GetTopProjection(userVoxels), revealTop, revealTop || lastDiagnostic != null);
    }

    private void UpdateProjectionGrid(ProjectionGrid grid, bool[,] target, bool[,] current, bool showTarget, bool compareToTarget)
    {
        for (int row = 0; row < grid.Rows; row++)
        {
            for (int col = 0; col < grid.Columns; col++)
            {
                bool targetFilled = target[row, col];
                bool currentFilled = current[row, col];

                grid.TargetCells[row, col].color = showTarget
                    ? (targetFilled ? new Color(0.13f, 0.5f, 0.96f, 1f) : new Color(0.86f, 0.91f, 0.95f, 0.82f))
                    : new Color(0.58f, 0.64f, 0.7f, 0.45f);

                if (compareToTarget)
                {
                    if (currentFilled && targetFilled)
                    {
                        grid.CurrentCells[row, col].color = new Color(0.24f, 0.86f, 0.48f, 1f);
                    }
                    else if (currentFilled && !targetFilled)
                    {
                        grid.CurrentCells[row, col].color = new Color(1f, 0.28f, 0.25f, 1f);
                    }
                    else if (!currentFilled && targetFilled)
                    {
                        grid.CurrentCells[row, col].color = new Color(1f, 0.82f, 0.16f, 1f);
                    }
                    else
                    {
                        grid.CurrentCells[row, col].color = new Color(0.86f, 0.91f, 0.95f, 0.72f);
                    }
                }
                else
                {
                    grid.CurrentCells[row, col].color = currentFilled
                        ? new Color(1f, 0.5f, 0.2f, 1f)
                        : new Color(0.86f, 0.91f, 0.95f, 0.72f);
                }
            }
        }

        grid.TargetLabel.text = showTarget ? "Target" : "Hidden";
        grid.CurrentLabel.text = compareToTarget ? "Yours diff" : "Yours";
    }

    private void UpdateCursorVisual()
    {
        if (cursorRoot == null)
        {
            return;
        }

        Vector3Int cell = GetActiveCell();
        cursorRoot.position = GridToWorld(cell);
        bool invalid = userVoxels.Contains(cell) && (!hoverIsBlock || isDragging);
        if (isDragging)
        {
            invalid = !IsInsideGrid(cell) || (userVoxels.Contains(cell) && cell != dragStartCell);
        }

        if (cursorRenderer != null)
        {
            cursorRenderer.sharedMaterial = invalid ? invalidCursorMaterial : cursorMaterial;
        }
    }

    private void AnimateAgent()
    {
        if (agentRoot == null)
        {
            return;
        }

        Vector3 basePosition = new Vector3(3.2f, 0.1f, 2.35f);
        agentRoot.position = basePosition + new Vector3(0f, Mathf.Sin(Time.time * 2.4f) * 0.045f, 0f);
        agentRoot.rotation = Quaternion.Euler(0f, -28f + Mathf.Sin(Time.time * 1.5f) * 3.2f, 0f);
    }

    private LoadBand EstimateLoadBand()
    {
        return EstimateLoadBand(EstimateLoadScore());
    }

    private LoadBand EstimateLoadBand(float scoreValue)
    {
        if (scoreValue >= 0.82f)
        {
            return LoadBand.Overloaded;
        }

        if (scoreValue >= 0.62f)
        {
            return LoadBand.Strained;
        }

        return scoreValue >= 0.32f ? LoadBand.Focused : LoadBand.Calm;
    }

    private float EstimateLoadScore()
    {
        float elapsed = Time.time - caseStartTime;
        float value = 0.12f;
        value += mentalEffort == 3 ? 0.34f : mentalEffort == 2 ? 0.17f : 0.05f;
        value += Mathf.Clamp01(elapsed / 180f) * 0.18f;
        value += Mathf.Clamp01(attempts / 4f) * 0.16f;
        value += Mathf.Clamp01(hintsUsed / 3f) * 0.13f;
        value += Mathf.Clamp01(actionsThisCase / 42f) * 0.16f;
        value += Mathf.Clamp01(viewSwitchesThisCase / 18f) * 0.08f;
        return Mathf.Clamp01(value);
    }

    private Color LoadColor(LoadBand load)
    {
        switch (load)
        {
            case LoadBand.Calm:
                return new Color(0.16f, 0.73f, 0.94f, 1f);
            case LoadBand.Focused:
                return new Color(0.22f, 0.84f, 0.5f, 1f);
            case LoadBand.Strained:
                return new Color(1f, 0.68f, 0.22f, 1f);
            default:
                return new Color(1f, 0.25f, 0.25f, 1f);
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

    private string FormatDiagnostic(DiagnosticResult result)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Geometry engine");
        builder.AppendLine("Result: " + (result.Passed ? "solved" : "not solved"));
        builder.AppendLine("Error: " + FormatErrorType(result.Error));
        builder.AppendLine("Matched: " + JoinOrNone(result.MatchedViews));
        builder.AppendLine("Mismatched: " + JoinOrNone(result.MismatchedViews));
        builder.AppendLine("Missing cells: " + FormatCells(result.MissingCells));
        builder.AppendLine("Extra cells: " + FormatCells(result.ExtraCells));
        return builder.ToString();
    }

    private string FeedbackForError(ErrorType error)
    {
        switch (error)
        {
            case ErrorType.LeftRightMirror:
                return "Left-right mirror: keep height/depth stable, then check whether x positions are reversed from the viewer.";
            case ErrorType.FrontBackReverse:
                return "Front-back reversal: the front view hides depth. Use right or top view before changing height.";
            case ErrorType.HeightError:
                return "Height error: your footprint is close. Compare one column from bottom layer upward.";
            case ErrorType.MissingBlock:
                return "Missing block: find a target projection mark that your model does not produce.";
            case ErrorType.ExtraBlock:
                return "Extra block: remove a block that creates a mark absent from the target projections.";
            case ErrorType.ProjectionAmbiguity:
                return "Projection ambiguity: two 2D views can match while the 3D voxel set differs.";
            default:
                return "Mixed mismatch: choose one projection, fix that view, then submit again.";
        }
    }

    private string NextViewToCheck(DiagnosticResult result)
    {
        if (result.MismatchedViews.Count == 0)
        {
            return "Check the exact 3D cells after matching the visible projections.";
        }

        if (result.MismatchedViews.Contains("top"))
        {
            return "Check top view for footprint and front-back position.";
        }

        if (result.MismatchedViews.Contains("right"))
        {
            return "Check right view for depth and column height.";
        }

        return "Check front view for width and height.";
    }

    private int ScoreRuleQuality(string rule, ErrorType error)
    {
        string lower = rule.ToLowerInvariant();
        int value = 18;

        if (ContainsAny(lower, "front", "right", "top", "side", "view", "projection"))
        {
            value += 24;
        }

        if (ContainsAny(lower, "depth", "front-back", "height", "left", "right", "mirror", "column", "above", "below", "footprint"))
        {
            value += 26;
        }

        if (ContainsAny(lower, "check", "compare", "first", "then", "before", "because", "if"))
        {
            value += 18;
        }

        if (error == ErrorType.FrontBackReverse && ContainsAny(lower, "depth", "front-back", "right", "top"))
        {
            value += 16;
        }
        else if (error == ErrorType.LeftRightMirror && ContainsAny(lower, "mirror", "left", "right", "viewpoint"))
        {
            value += 16;
        }
        else if (error == ErrorType.HeightError && ContainsAny(lower, "height", "column", "layer", "above"))
        {
            value += 16;
        }

        return Mathf.Clamp(value, 0, 100);
    }

    private string ExtractRuleName(string rule, ErrorType error)
    {
        switch (error)
        {
            case ErrorType.FrontBackReverse:
                return "front_view_hides_depth";
            case ErrorType.LeftRightMirror:
                return "viewpoint_controls_left_right";
            case ErrorType.HeightError:
                return "footprint_then_column_height";
            case ErrorType.ExtraBlock:
                return "projection_marks_find_extra_blocks";
            case ErrorType.MissingBlock:
                return "missing_marks_find_missing_blocks";
            default:
                return ContainsAny(rule.ToLowerInvariant(), "top") ? "top_view_checks_footprint" : "compare_views_before_rebuild";
        }
    }

    private string ExtractCondition(string rule)
    {
        string lower = rule.ToLowerInvariant();
        if (ContainsAny(lower, "front") && ContainsAny(lower, "right", "side", "top"))
        {
            return "front evidence must be checked against side or top evidence";
        }

        if (ContainsAny(lower, "top", "footprint"))
        {
            return "top evidence verifies footprint and front-back position";
        }

        if (ContainsAny(lower, "height", "column", "layer"))
        {
            return "matching footprint is not enough; column height must be checked";
        }

        return "use deterministic diagnostic facts to choose the next view";
    }

    private string ExtractStrategy(string rule, ErrorType error)
    {
        switch (error)
        {
            case ErrorType.FrontBackReverse:
                return "use right or top view to verify front-back depth";
            case ErrorType.LeftRightMirror:
                return "confirm viewer position before deciding left and right";
            case ErrorType.HeightError:
                return "compare each column from the bottom layer upward";
            case ErrorType.ExtraBlock:
                return "remove blocks that create extra projection marks";
            case ErrorType.MissingBlock:
                return "add blocks that create missing projection marks";
            default:
                return "compare one projection at a time and revise locally";
        }
    }

    private bool ContainsAny(string value, params string[] tokens)
    {
        for (int i = 0; i < tokens.Length; i++)
        {
            if (value.Contains(tokens[i]))
            {
                return true;
            }
        }

        return false;
    }

    private string FormatErrorType(ErrorType error)
    {
        switch (error)
        {
            case ErrorType.None:
                return "none";
            case ErrorType.LeftRightMirror:
                return "left_right_mirror";
            case ErrorType.FrontBackReverse:
                return "front_back_reverse";
            case ErrorType.HeightError:
                return "height_error";
            case ErrorType.MissingBlock:
                return "missing_block";
            case ErrorType.ExtraBlock:
                return "extra_block";
            case ErrorType.ProjectionAmbiguity:
                return "projection_ambiguity";
            default:
                return "unknown";
        }
    }

    private string JoinOrNone(List<string> values)
    {
        return values.Count == 0 ? "none" : string.Join(", ", values.ToArray());
    }

    private string FormatCells(List<Vector3Int> cells)
    {
        if (cells.Count == 0)
        {
            return "none";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < cells.Count; i++)
        {
            if (i > 0)
            {
                builder.Append("; ");
            }

            Vector3Int cell = cells[i];
            builder.Append("x").Append(cell.x).Append(" y").Append(cell.y).Append(" z").Append(cell.z);
        }

        return builder.ToString();
    }

    private string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        StringBuilder builder = new StringBuilder(value.Length + 16);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    private string ExtractOpenAiText(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return "";
        }

        int typeIndex = json.IndexOf("\"output_text\"", StringComparison.Ordinal);
        int searchStart = typeIndex >= 0 ? typeIndex : 0;
        int textKey = json.IndexOf("\"text\"", searchStart, StringComparison.Ordinal);
        if (textKey < 0)
        {
            return "";
        }

        int colon = json.IndexOf(':', textKey);
        if (colon < 0)
        {
            return "";
        }

        int quote = json.IndexOf('"', colon + 1);
        if (quote < 0)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        bool escaped = false;
        for (int i = quote + 1; i < json.Length; i++)
        {
            char c = json[i];
            if (escaped)
            {
                switch (c)
                {
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case '"':
                        builder.Append('"');
                        break;
                    case '\\':
                        builder.Append('\\');
                        break;
                    default:
                        builder.Append(c);
                        break;
                }

                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                break;
            }

            builder.Append(c);
        }

        return builder.ToString().Trim();
    }

    private ProjectionGrid CreateProjectionGrid(Transform parent, string title, int rows, int columns)
    {
        GameObject root = CreatePanelObject(parent, title + " Panel", new Color(0.94f, 0.97f, 1f, 0.92f));
        AddLayoutSize(root, 0f, 134f, 1f, 0f);

        ProjectionGrid grid = new ProjectionGrid
        {
            Header = CreateAbsoluteText(root.transform, title + " Header", title, 14, TextAnchor.UpperLeft, new Color(0.08f, 0.14f, 0.22f, 1f), 12f, 8f, 260f, 20f),
            TargetLabel = CreateAbsoluteText(root.transform, title + " Target Label", "Target", 11, TextAnchor.UpperLeft, new Color(0.22f, 0.3f, 0.38f, 1f), 16f, 31f, 100f, 16f),
            CurrentLabel = CreateAbsoluteText(root.transform, title + " Current Label", "Yours", 11, TextAnchor.UpperLeft, new Color(0.22f, 0.3f, 0.38f, 1f), 172f, 31f, 116f, 16f),
            TargetCells = new Image[rows, columns],
            CurrentCells = new Image[rows, columns],
            Rows = rows,
            Columns = columns
        };

        CreateProjectionCells(root.transform, grid.TargetCells, rows, columns, 16f, 51f);
        CreateProjectionCells(root.transform, grid.CurrentCells, rows, columns, 172f, 51f);
        return grid;
    }

    private void CreateProjectionCells(Transform parent, Image[,] target, int rows, int columns, float startX, float startY)
    {
        float cell = 14f;
        float gap = 4f;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                GameObject cellObject = CreatePanelObject(parent, "Projection Cell", new Color(0.86f, 0.91f, 0.95f, 0.72f));
                RectTransform rect = cellObject.GetComponent<RectTransform>();
                ConfigureRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(startX + col * (cell + gap), -(startY + row * (cell + gap))), new Vector2(cell, cell));
                target[row, col] = cellObject.GetComponent<Image>();
            }
        }
    }

    private GameObject CreateLayoutGroup(Transform parent, string name, bool horizontal, int spacing, int padding, TextAnchor alignment)
    {
        GameObject group = new GameObject(name, typeof(RectTransform));
        group.transform.SetParent(parent, false);
        if (horizontal)
        {
            AddHorizontalLayout(group, padding, spacing, alignment, false);
        }
        else
        {
            AddVerticalLayout(group, padding, spacing, alignment);
        }

        return group;
    }

    private GameObject CreatePanelObject(Transform parent, string name, Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        return panelObject;
    }

    private Text CreateText(Transform parent, string name, string value, int size, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = GetUiFont();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(9, size - 4);
        text.resizeTextMaxSize = size;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Text CreateAbsoluteText(Transform parent, string name, string value, int size, TextAnchor alignment, Color color, float x, float y, float width, float height)
    {
        Text text = CreateText(parent, name, value, size, alignment, color);
        ConfigureRect(text.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));
        return text;
    }

    private Button CreateButton(Transform parent, string label, float width, float height, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.24f, 0.32f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.16f, 0.24f, 0.32f, 1f);
        colors.highlightedColor = new Color(0.26f, 0.42f, 0.56f, 1f);
        colors.pressedColor = new Color(0.1f, 0.58f, 0.86f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.24f, 0.28f, 0.32f, 0.42f);
        button.colors = colors;

        Text labelText = CreateText(buttonObject.transform, label + " Label", label, 13, TextAnchor.MiddleCenter, Color.white);
        ConfigureRect(labelText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        AddLayoutSize(buttonObject, width, height, 0f, 0f);
        return button;
    }

    private InputField CreateInputField(Transform parent, string name, string placeholder, float width, float height, bool password)
    {
        GameObject fieldObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        fieldObject.transform.SetParent(parent, false);
        fieldObject.GetComponent<Image>().color = new Color(0.98f, 1f, 1f, 0.95f);
        AddLayoutSize(fieldObject, width, height, width <= 0f ? 1f : 0f, 0f);

        Text text = CreateText(fieldObject.transform, "Text", "", 13, TextAnchor.MiddleLeft, new Color(0.06f, 0.12f, 0.18f, 1f));
        text.raycastTarget = true;
        ConfigureRect(text.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(8f, 0f), new Vector2(-16f, -8f));

        Text placeholderText = CreateText(fieldObject.transform, "Placeholder", placeholder, 13, TextAnchor.MiddleLeft, new Color(0.42f, 0.5f, 0.58f, 0.86f));
        ConfigureRect(placeholderText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(8f, 0f), new Vector2(-16f, -8f));

        InputField input = fieldObject.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholderText;
        input.lineType = InputField.LineType.SingleLine;
        input.characterLimit = 0;
        if (password)
        {
            input.contentType = InputField.ContentType.Password;
        }

        return input;
    }

    private void AddHorizontalLayout(GameObject target, int padding, int spacing, TextAnchor alignment, bool expandWidth)
    {
        HorizontalLayoutGroup group = target.AddComponent<HorizontalLayoutGroup>();
        group.padding = new RectOffset(padding, padding, padding, padding);
        group.spacing = spacing;
        group.childAlignment = alignment;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = expandWidth;
        group.childForceExpandHeight = false;
    }

    private void AddVerticalLayout(GameObject target, int padding, int spacing, TextAnchor alignment)
    {
        VerticalLayoutGroup group = target.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(padding, padding, padding, padding);
        group.spacing = spacing;
        group.childAlignment = alignment;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
    }

    private void AddLayoutSize(GameObject target, float width, float height, float flexibleWidth, float flexibleHeight)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = target.AddComponent<LayoutElement>();
        }

        if (width > 0f)
        {
            layout.preferredWidth = width;
            layout.minWidth = Mathf.Min(width, 64f);
        }

        if (height > 0f)
        {
            layout.preferredHeight = height;
            layout.minHeight = Mathf.Min(height, 24f);
        }

        layout.flexibleWidth = flexibleWidth;
        layout.flexibleHeight = flexibleHeight;
    }

    private void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private Font GetUiFont()
    {
        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return uiFont;
    }

    private Material CreateMaterial(string name, Color color, bool transparent)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Diffuse");
        }

        Material material = new Material(shader);
        material.name = name;
        material.color = color;

        if (transparent && material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
        }

        return material;
    }

    private Material CreateLineMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material material = new Material(shader);
        material.name = name;
        material.color = color;
        return material;
    }

    private void CreateLight(string name, LightType type, Vector3 position, Quaternion rotation, float intensity, bool shadows)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.position = position;
        lightObject.transform.rotation = rotation;
        Light light = lightObject.AddComponent<Light>();
        light.type = type;
        light.intensity = intensity;
        light.range = 8f;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
    }

    private void ClearBlockObjects()
    {
        foreach (KeyValuePair<Vector3Int, GameObject> entry in blockObjects)
        {
            if (entry.Value != null)
            {
                Destroy(entry.Value);
            }
        }

        blockObjects.Clear();
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

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void LogEvent(string action, string detail)
    {
        if (cases.Count == 0 || conditions.Count == 0)
        {
            return;
        }

        SessionEvent evt = new SessionEvent
        {
            Time = Time.time,
            CaseId = cases[Mathf.Clamp(caseIndex, 0, cases.Count - 1)].Id,
            Condition = conditions[Mathf.Clamp(conditionIndex, 0, conditions.Count - 1)].Label,
            Action = action,
            Detail = detail
        };

        sessionEvents.Add(evt);
        Debug.Log("[BlockDetectiveDeluxe] " + evt.CaseId + " | " + evt.Condition + " | " + action + " | " + detail);
    }
}

public sealed class BlockDetectiveDeluxeBlock : MonoBehaviour
{
    public Vector3Int Cell;
}

public sealed class BlockDetectiveDeluxeCell : MonoBehaviour
{
    public int X;
    public int Z;
}

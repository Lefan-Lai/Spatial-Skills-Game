using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class AstroBrickMissionGame : MonoBehaviour
{
    private const int GridWidth = 8;
    private const int GridDepth = 8;
    private const int MaxLayerUnits = 10;
    private const float StudSize = 0.52f;
    private const float PlateHeight = 0.16f;
    private const string OpenAiResponsesUrl = "https://api.openai.com/v1/responses";
    private const string DefaultModel = "gpt-5.4-mini";

    private enum PartCategory
    {
        Brick,
        Plate,
        Tile,
        Slope,
        Wedge,
        CornerSlope
    }

    private enum ConditionMode
    {
        Control,
        FixedFeedback,
        LlmMcq,
        FullSystem
    }

    private enum ErrorType
    {
        None,
        WrongPart,
        WrongFootprint,
        WrongPosition,
        WrongLayer,
        WrongOrientation,
        MirrorError,
        SupportError,
        MissingElement,
        ExtraElement,
        ReferenceFrameError,
        Unknown
    }

    private enum ViewMode
    {
        Free,
        Front,
        Side,
        Top,
        Robot
    }

    private enum RepresentationMode
    {
        Full3D,
        Rotated3D,
        ThreeViews,
        Language,
        RobotView
    }

    private enum LoadBand
    {
        Calm,
        Focused,
        Strained,
        Overloaded
    }

    private sealed class BrickSpec
    {
        public string Id;
        public string Label;
        public PartCategory Category;
        public int Width;
        public int Depth;
        public int HeightUnits;
        public bool Directional;
        public bool Chiral;
        public string Chirality;
        public Color Color;
    }

    private sealed class BrickPlacement
    {
        public string InstanceId;
        public BrickSpec Spec;
        public Vector3Int Anchor;
        public int Orientation;
    }

    private sealed class MissionData
    {
        public string Id;
        public string Title;
        public string Brief;
        public string SkillPhase;
        public string TargetDescription;
        public string Vocabulary;
        public RepresentationMode Representation;
        public int Difficulty;
        public readonly List<BrickPlacement> Target = new List<BrickPlacement>();
        public readonly List<string> AllowedPartIds = new List<string>();
    }

    private sealed class DiagnosticResult
    {
        public bool Passed;
        public ErrorType Error;
        public string MainFact;
        public BrickPlacement TargetReference;
        public BrickPlacement PlayerReference;
        public readonly List<string> Facts = new List<string>();
        public readonly List<BrickPlacement> Missing = new List<BrickPlacement>();
        public readonly List<BrickPlacement> Extra = new List<BrickPlacement>();
    }

    private sealed class SessionEvent
    {
        public float Time;
        public string MissionId;
        public string Action;
        public string Detail;
    }

    private sealed class McqData
    {
        public string Question;
        public readonly string[] Options = new string[4];
        public int CorrectIndex;
        public string CorrectFeedback;
        public readonly string[] WrongFeedback = new string[4];
    }

    private sealed class ProjectionGrid
    {
        public Text Header;
        public Image[,] TargetCells;
        public Image[,] PlayerCells;
        public int Rows;
        public int Cols;
    }

    private readonly Dictionary<string, BrickSpec> partLibrary = new Dictionary<string, BrickSpec>();
    private readonly List<MissionData> missions = new List<MissionData>();
    private readonly List<BrickPlacement> playerBuild = new List<BrickPlacement>();
    private readonly Dictionary<string, GameObject> placementObjects = new Dictionary<string, GameObject>();
    private readonly List<SessionEvent> sessionEvents = new List<SessionEvent>();

    private Camera mainCamera;
    private Transform worldRoot;
    private Transform boardRoot;
    private Transform targetRoot;
    private Transform buildRoot;
    private Transform diagnosticRoot;
    private Transform cursorRoot;
    private Transform astronautRoot;
    private Transform noviRoot;
    private Transform starRoot;

    private Font uiFont;
    private Material moonMaterial;
    private Material gridMaterial;
    private Material cursorMaterial;
    private Material invalidCursorMaterial;
    private Material ghostMaterial;
    private Material highlightMaterial;
    private Material missingMaterial;
    private Material extraMaterial;
    private Material astronautSuitMaterial;
    private Material astronautVisorMaterial;
    private Material noviBodyMaterial;
    private Material noviFaceMaterial;
    private Material studMaterial;
    private Material starMaterial;

    private Renderer cursorRenderer;
    private Text titleText;
    private Text missionText;
    private Text partListText;
    private Text targetInfoText;
    private Text statusText;
    private Text diagnosisText;
    private Text feedbackText;
    private Text mcqText;
    private Text noviText;
    private Text telemetryText;
    private Text loadText;
    private Image loadFill;
    private InputField apiKeyInput;
    private InputField modelInput;
    private InputField teachInput;
    private Button askGptButton;
    private Button teachButton;
    private Button nextButton;
    private readonly List<Button> partButtons = new List<Button>();
    private readonly List<Button> conditionButtons = new List<Button>();
    private readonly List<Button> mcqButtons = new List<Button>();
    private ProjectionGrid topProjection;
    private ProjectionGrid frontProjection;
    private ProjectionGrid sideProjection;

    private int missionIndex;
    private int selectedPartIndex;
    private int selectedOrientation;
    private int activeLayer;
    private int score;
    private int solvedMissions;
    private int attempts;
    private int hintsUsed;
    private int viewSwitches;
    private int actionsThisMission;
    private int mentalEffort = 2;
    private int mcqCorrect;
    private int mcqTotal;
    private float missionStartTime;
    private bool targetGhostVisible = true;
    private bool isDragging;
    private bool gptRunning;
    private string draggingId;
    private Vector3Int dragStartAnchor;
    private Vector3Int hoverCell;
    private bool hoverValid;
    private DiagnosticResult lastDiagnostic;
    private McqData currentMcq;
    private ConditionMode conditionMode = ConditionMode.FullSystem;

    private void Start()
    {
        Application.targetFrameRate = 60;
        QualitySettings.antiAliasing = Mathf.Max(4, QualitySettings.antiAliasing);

        BuildPartLibrary();
        BuildMissions();
        SetupScene();
        BuildInterface();
        SelectCondition(ConditionMode.FullSystem);
        StartMission(0);
    }

    private void Update()
    {
        UpdateHover();
        HandleMouse();
        HandleKeyboard();
        AnimateCharacters();
        UpdateHud();
    }

    private void BuildPartLibrary()
    {
        AddPart("brick_1x2", "Brick 1x2", PartCategory.Brick, 1, 2, 3, false, false, "", new Color(0.95f, 0.28f, 0.24f, 1f));
        AddPart("brick_1x3", "Brick 1x3", PartCategory.Brick, 1, 3, 3, false, false, "", new Color(1f, 0.62f, 0.18f, 1f));
        AddPart("brick_1x4", "Brick 1x4", PartCategory.Brick, 1, 4, 3, false, false, "", new Color(0.12f, 0.58f, 0.92f, 1f));
        AddPart("brick_2x2", "Brick 2x2", PartCategory.Brick, 2, 2, 3, false, false, "", new Color(0.32f, 0.76f, 0.36f, 1f));
        AddPart("brick_2x4", "Brick 2x4", PartCategory.Brick, 2, 4, 3, false, false, "", new Color(0.48f, 0.36f, 0.86f, 1f));
        AddPart("plate_1x4", "Plate 1x4", PartCategory.Plate, 1, 4, 1, false, false, "", new Color(0.2f, 0.75f, 0.82f, 1f));
        AddPart("plate_2x4", "Plate 2x4", PartCategory.Plate, 2, 4, 1, false, false, "", new Color(0.1f, 0.68f, 0.94f, 1f));
        AddPart("plate_4x4", "Plate 4x4", PartCategory.Plate, 4, 4, 1, false, false, "", new Color(0.18f, 0.86f, 0.58f, 1f));
        AddPart("tile_1x2", "Tile 1x2", PartCategory.Tile, 1, 2, 1, false, false, "", new Color(0.95f, 0.95f, 0.98f, 1f));
        AddPart("tile_2x2", "Tile 2x2", PartCategory.Tile, 2, 2, 1, false, false, "", new Color(0.88f, 0.9f, 0.96f, 1f));
        AddPart("tile_2x4", "Tile 2x4", PartCategory.Tile, 2, 4, 1, false, false, "", new Color(0.75f, 0.82f, 0.9f, 1f));
        AddPart("slope_1x2", "Slope 1x2", PartCategory.Slope, 1, 2, 3, true, false, "", new Color(1f, 0.82f, 0.22f, 1f));
        AddPart("slope_2x2", "Slope 2x2", PartCategory.Slope, 2, 2, 3, true, false, "", new Color(1f, 0.74f, 0.26f, 1f));
        AddPart("wedge_left_2x2", "Left Wedge", PartCategory.Wedge, 2, 2, 3, true, true, "left", new Color(0.96f, 0.38f, 0.78f, 1f));
        AddPart("wedge_right_2x2", "Right Wedge", PartCategory.Wedge, 2, 2, 3, true, true, "right", new Color(0.72f, 0.42f, 1f, 1f));
        AddPart("corner_slope_2x2", "Corner Slope", PartCategory.CornerSlope, 2, 2, 3, true, false, "", new Color(0.38f, 0.88f, 0.78f, 1f));
    }

    private void AddPart(string id, string label, PartCategory category, int width, int depth, int heightUnits, bool directional, bool chiral, string chirality, Color color)
    {
        BrickSpec spec = new BrickSpec
        {
            Id = id,
            Label = label,
            Category = category,
            Width = width,
            Depth = depth,
            HeightUnits = heightUnits,
            Directional = directional,
            Chiral = chiral,
            Chirality = chirality,
            Color = color
        };

        partLibrary[id] = spec;
    }

    private void BuildMissions()
    {
        MissionData platform = Mission("M1", "Lunar Base Platform", "Mission Control needs a flat landing platform made from plates and tiles.", "Phase 1: footprint and same-layer layout", "Match the top outline and cover the base symmetrically.", "front, back, left, right, footprint, cover", RepresentationMode.Full3D, 1);
        AddAllowed(platform, "plate_4x4", "plate_2x4", "tile_2x2", "tile_2x4", "brick_1x2");
        AddTarget(platform, "plate_4x4", 2, 0, 2, 0);
        AddTarget(platform, "tile_2x4", 2, 1, 2, 0);
        AddTarget(platform, "tile_2x2", 2, 1, 0, 0);
        AddTarget(platform, "tile_2x2", 4, 1, 0, 0);
        missions.Add(platform);

        MissionData stairs = Mission("M2", "Moon Stair Steps", "Build a small set of steps for the astronaut to reach the platform.", "Phase 2: layer decomposition and support", "Each higher brick must be supported by the layer below.", "above, below, layer, support, stack", RepresentationMode.Full3D, 2);
        AddAllowed(stairs, "brick_1x2", "brick_1x3", "plate_2x4", "plate_4x4", "slope_1x2");
        AddTarget(stairs, "plate_4x4", 2, 0, 2, 0);
        AddTarget(stairs, "brick_1x2", 2, 1, 3, 90);
        AddTarget(stairs, "brick_1x2", 3, 4, 3, 90);
        AddTarget(stairs, "slope_1x2", 4, 7, 3, 180);
        missions.Add(stairs);

        MissionData tower = Mission("M3", "Comms Support Tower", "Rebuild a centered tower for the moon-base antenna marker.", "Phase 2: center alignment and vertical relation", "Use plates for levels and bricks for centered supports.", "center, align, level, vertical, column", RepresentationMode.Rotated3D, 2);
        AddAllowed(tower, "brick_1x2", "brick_2x2", "plate_2x4", "tile_2x2", "slope_2x2");
        AddTarget(tower, "plate_2x4", 3, 0, 2, 90);
        AddTarget(tower, "brick_2x2", 3, 1, 3, 0);
        AddTarget(tower, "plate_2x4", 3, 4, 2, 90);
        AddTarget(tower, "slope_2x2", 3, 5, 3, 0);
        missions.Add(tower);

        MissionData solar = Mission("M4", "Solar Panel Support", "Repair the solar stand using slopes and mirrored wedges.", "Phase 3: orientation and chirality", "The two slopes face the center; left and right wedges are not interchangeable.", "rotate, high edge, low edge, mirror, center", RepresentationMode.ThreeViews, 3);
        AddAllowed(solar, "plate_2x4", "brick_1x2", "tile_2x2", "slope_1x2", "wedge_left_2x2", "wedge_right_2x2");
        AddTarget(solar, "plate_2x4", 3, 0, 2, 90);
        AddTarget(solar, "brick_1x2", 2, 1, 2, 0);
        AddTarget(solar, "brick_1x2", 5, 1, 2, 0);
        AddTarget(solar, "slope_1x2", 2, 4, 2, 180);
        AddTarget(solar, "slope_1x2", 5, 4, 2, 180);
        AddTarget(solar, "wedge_left_2x2", 1, 4, 4, 0);
        AddTarget(solar, "wedge_right_2x2", 5, 4, 4, 0);
        missions.Add(solar);

        MissionData bridge = Mission("M5", "Station Connection Bridge", "Build a stable bridge between two base pads.", "Phase 4: bridge support and part-whole planning", "A top plate can span the gap only when support exists at both ends.", "bridge, support, edge, span, whole", RepresentationMode.ThreeViews, 3);
        AddAllowed(bridge, "brick_2x2", "brick_1x4", "plate_2x4", "tile_2x4", "plate_4x4");
        AddTarget(bridge, "brick_2x2", 1, 0, 3, 0);
        AddTarget(bridge, "brick_2x2", 5, 0, 3, 0);
        AddTarget(bridge, "plate_2x4", 2, 3, 3, 90);
        AddTarget(bridge, "tile_2x4", 2, 4, 3, 90);
        missions.Add(bridge);

        MissionData airlock = Mission("M6", "Airlock Door Frame", "Construct an open frame without filling the doorway.", "Phase 4: negative space and perspective", "The opening is part of the target, so extra center bricks are errors.", "open, frame, inside, outside, front", RepresentationMode.ThreeViews, 4);
        AddAllowed(airlock, "brick_1x4", "brick_1x2", "plate_2x4", "corner_slope_2x2", "tile_1x2");
        AddTarget(airlock, "brick_1x4", 2, 0, 2, 0);
        AddTarget(airlock, "brick_1x4", 5, 0, 2, 0);
        AddTarget(airlock, "plate_2x4", 2, 3, 2, 90);
        AddTarget(airlock, "corner_slope_2x2", 2, 4, 1, 90);
        AddTarget(airlock, "corner_slope_2x2", 5, 4, 1, 180);
        missions.Add(airlock);

        MissionData energy = Mission("M7", "Energy Module Base", "Place mirrored wedges around a tiled energy base.", "Phase 5: mirror reasoning and top-view checking", "The layout is symmetric, but the wedge chirality changes by side.", "symmetric, mirror, wedge, viewpoint, top view", RepresentationMode.RobotView, 4);
        AddAllowed(energy, "plate_4x4", "tile_2x2", "wedge_left_2x2", "wedge_right_2x2", "brick_1x2");
        AddTarget(energy, "plate_4x4", 2, 0, 2, 0);
        AddTarget(energy, "tile_2x2", 3, 1, 3, 0);
        AddTarget(energy, "wedge_left_2x2", 1, 1, 2, 0);
        AddTarget(energy, "wedge_right_2x2", 5, 1, 2, 0);
        AddTarget(energy, "wedge_right_2x2", 1, 1, 5, 180);
        AddTarget(energy, "wedge_left_2x2", 5, 1, 5, 180);
        missions.Add(energy);

        MissionData beacon = Mission("M8", "Navigation Beacon", "Teach Novi to rebuild an asymmetric marker from robot-view instructions.", "Phase 5: reference frame and teaching", "The model is intentionally asymmetric, so left and right must be named from a reference frame.", "robot view, reference frame, rotate, asymmetric, teach", RepresentationMode.RobotView, 5);
        AddAllowed(beacon, "brick_1x2", "brick_2x2", "plate_2x4", "tile_1x2", "slope_1x2", "wedge_left_2x2", "wedge_right_2x2");
        AddTarget(beacon, "plate_2x4", 3, 0, 2, 90);
        AddTarget(beacon, "brick_2x2", 3, 1, 3, 0);
        AddTarget(beacon, "slope_1x2", 2, 4, 3, 90);
        AddTarget(beacon, "wedge_left_2x2", 4, 4, 3, 0);
        AddTarget(beacon, "tile_1x2", 3, 4, 2, 90);
        missions.Add(beacon);
    }

    private MissionData Mission(string id, string title, string brief, string phase, string targetDescription, string vocabulary, RepresentationMode representation, int difficulty)
    {
        return new MissionData
        {
            Id = id,
            Title = title,
            Brief = brief,
            SkillPhase = phase,
            TargetDescription = targetDescription,
            Vocabulary = vocabulary,
            Representation = representation,
            Difficulty = difficulty
        };
    }

    private void AddAllowed(MissionData mission, params string[] partIds)
    {
        mission.AllowedPartIds.AddRange(partIds);
    }

    private void AddTarget(MissionData mission, string partId, int x, int y, int z, int orientation)
    {
        mission.Target.Add(new BrickPlacement
        {
            InstanceId = "target_" + mission.Target.Count,
            Spec = partLibrary[partId],
            Anchor = new Vector3Int(x, y, z),
            Orientation = NormalizeOrientation(orientation)
        });
    }

    private void SetupScene()
    {
        RenderSettings.ambientLight = new Color(0.55f, 0.62f, 0.75f, 1f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.03f, 0.05f, 0.11f, 1f);
        RenderSettings.fogDensity = 0.012f;

        CreateMaterials();
        SetupCamera();

        worldRoot = new GameObject("AstroBrick Mission Runtime").transform;
        boardRoot = new GameObject("Moon Build Grid").transform;
        targetRoot = new GameObject("Target Preview").transform;
        buildRoot = new GameObject("Player Brick Model").transform;
        diagnosticRoot = new GameObject("Diagnostic Ghosts").transform;
        cursorRoot = new GameObject("Placement Cursor").transform;
        astronautRoot = new GameObject("Little Astronaut").transform;
        noviRoot = new GameObject("Novi Robot").transform;
        starRoot = new GameObject("Star Field").transform;

        boardRoot.SetParent(worldRoot, false);
        targetRoot.SetParent(worldRoot, false);
        buildRoot.SetParent(worldRoot, false);
        diagnosticRoot.SetParent(worldRoot, false);
        cursorRoot.SetParent(worldRoot, false);
        astronautRoot.SetParent(worldRoot, false);
        noviRoot.SetParent(worldRoot, false);
        starRoot.SetParent(worldRoot, false);

        CreateLights();
        CreateStars();
        CreateMoonStage();
        CreateCursor();
        CreateAstronaut();
        CreateNovi();
        ApplyView(ViewMode.Free);
    }

    private void CreateMaterials()
    {
        moonMaterial = CreateMaterial("Moon Surface", new Color(0.78f, 0.8f, 0.84f, 1f), false);
        gridMaterial = CreateMaterial("Grid Tile", new Color(0.9f, 0.92f, 0.96f, 1f), false);
        cursorMaterial = CreateMaterial("Valid Cursor", new Color(0.24f, 0.92f, 1f, 0.28f), true);
        invalidCursorMaterial = CreateMaterial("Invalid Cursor", new Color(1f, 0.22f, 0.28f, 0.38f), true);
        ghostMaterial = CreateMaterial("Target Ghost", new Color(0.22f, 0.72f, 1f, 0.24f), true);
        highlightMaterial = CreateMaterial("Correct Highlight", new Color(0.25f, 0.94f, 0.56f, 1f), false);
        missingMaterial = CreateMaterial("Missing Highlight", new Color(1f, 0.82f, 0.14f, 0.35f), true);
        extraMaterial = CreateMaterial("Extra Highlight", new Color(1f, 0.25f, 0.24f, 1f), false);
        astronautSuitMaterial = CreateMaterial("Astronaut Suit", new Color(0.96f, 0.98f, 1f, 1f), false);
        astronautVisorMaterial = CreateMaterial("Astronaut Visor", new Color(0.1f, 0.18f, 0.32f, 1f), false);
        noviBodyMaterial = CreateMaterial("Novi Body", new Color(0.18f, 0.68f, 1f, 1f), false);
        noviFaceMaterial = CreateMaterial("Novi Face", new Color(0.98f, 1f, 1f, 1f), false);
        studMaterial = CreateMaterial("Stud Top", new Color(1f, 1f, 1f, 0.28f), true);
        starMaterial = CreateMaterial("Star", new Color(1f, 0.96f, 0.7f, 1f), false);
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
        mainCamera.backgroundColor = new Color(0.025f, 0.035f, 0.09f, 1f);
        mainCamera.fieldOfView = 43f;

        if (mainCamera.GetComponent<AudioListener>() == null)
        {
            mainCamera.gameObject.AddComponent<AudioListener>();
        }
    }

    private void CreateLights()
    {
        CreateLight("Earthshine Key", LightType.Directional, new Vector3(-5f, 7f, -4f), Quaternion.Euler(48f, -32f, 0f), 1.05f, true);
        CreateLight("Mission Fill", LightType.Point, new Vector3(2.6f, 3.2f, -2.8f), Quaternion.identity, 1.4f, false);
        CreateLight("Novi Glow", LightType.Point, new Vector3(4.0f, 1.8f, 2.8f), Quaternion.identity, 0.9f, false);
    }

    private void CreateStars()
    {
        for (int i = 0; i < 60; i++)
        {
            GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            star.name = "Background Star";
            star.transform.SetParent(starRoot, false);
            float x = UnityEngine.Random.Range(-9f, 9f);
            float y = UnityEngine.Random.Range(2.2f, 6.6f);
            float z = UnityEngine.Random.Range(4.8f, 8.8f);
            star.transform.position = new Vector3(x, y, z);
            star.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.025f, 0.065f);
            star.GetComponent<Renderer>().sharedMaterial = starMaterial;
            Destroy(star.GetComponent<Collider>());
        }
    }

    private void CreateMoonStage()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Moon Base Floor";
        ground.transform.SetParent(boardRoot, false);
        ground.transform.position = new Vector3(0f, -0.12f, 0f);
        ground.transform.localScale = new Vector3(GridWidth * StudSize + 1.6f, 0.18f, GridDepth * StudSize + 1.6f);
        ground.GetComponent<Renderer>().sharedMaterial = moonMaterial;

        for (int x = 0; x < GridWidth; x++)
        {
            for (int z = 0; z < GridDepth; z++)
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "Build Cell " + x + "," + z;
                tile.transform.SetParent(boardRoot, false);
                tile.transform.position = GridToWorld(new Vector3Int(x, 0, z)) + new Vector3(0f, -0.02f, 0f);
                tile.transform.localScale = new Vector3(StudSize * 0.9f, 0.035f, StudSize * 0.9f);
                Renderer renderer = tile.GetComponent<Renderer>();
                renderer.sharedMaterial = gridMaterial;
                AstroBrickCell marker = tile.AddComponent<AstroBrickCell>();
                marker.X = x;
                marker.Z = z;
            }
        }

        CreateSign("MISSION BUILD ZONE", new Vector3(-2.4f, 0.08f, -2.8f), 0.095f, new Color(0.1f, 0.18f, 0.3f, 1f));
    }

    private void CreateSign(string label, Vector3 position, float scale, Color color)
    {
        GameObject sign = new GameObject(label, typeof(TextMesh));
        sign.transform.SetParent(worldRoot, false);
        sign.transform.position = position;
        sign.transform.rotation = Quaternion.Euler(68f, 0f, 0f);
        sign.transform.localScale = Vector3.one * scale;
        TextMesh text = sign.GetComponent<TextMesh>();
        text.text = label;
        text.anchor = TextAnchor.MiddleLeft;
        text.alignment = TextAlignment.Left;
        text.fontSize = 64;
        text.color = color;
    }

    private void CreateCursor()
    {
        GameObject cursor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cursor.name = "AstroBrick Cursor";
        cursor.transform.SetParent(cursorRoot, false);
        cursor.transform.localScale = new Vector3(StudSize, PlateHeight * 3f, StudSize);
        cursorRenderer = cursor.GetComponent<Renderer>();
        cursorRenderer.sharedMaterial = cursorMaterial;
        Destroy(cursor.GetComponent<Collider>());
    }

    private void CreateAstronaut()
    {
        astronautRoot.position = new Vector3(-2.8f, 0.24f, -1.8f);
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Astronaut Body";
        body.transform.SetParent(astronautRoot, false);
        body.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        body.transform.localScale = new Vector3(0.22f, 0.36f, 0.22f);
        body.GetComponent<Renderer>().sharedMaterial = astronautSuitMaterial;
        Destroy(body.GetComponent<Collider>());

        GameObject helmet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        helmet.name = "Astronaut Helmet";
        helmet.transform.SetParent(astronautRoot, false);
        helmet.transform.localPosition = new Vector3(0f, 0.86f, 0f);
        helmet.transform.localScale = Vector3.one * 0.36f;
        helmet.GetComponent<Renderer>().sharedMaterial = astronautSuitMaterial;
        Destroy(helmet.GetComponent<Collider>());

        GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "Astronaut Visor";
        visor.transform.SetParent(astronautRoot, false);
        visor.transform.localPosition = new Vector3(0f, 0.88f, -0.18f);
        visor.transform.localScale = new Vector3(0.24f, 0.12f, 0.03f);
        visor.GetComponent<Renderer>().sharedMaterial = astronautVisorMaterial;
        Destroy(visor.GetComponent<Collider>());
    }

    private void CreateNovi()
    {
        noviRoot.position = new Vector3(3.15f, 0.06f, 2.6f);
        noviRoot.rotation = Quaternion.Euler(0f, -28f, 0f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Novi Body";
        body.transform.SetParent(noviRoot, false);
        body.transform.localPosition = new Vector3(0f, 0.42f, 0f);
        body.transform.localScale = Vector3.one * 0.58f;
        body.GetComponent<Renderer>().sharedMaterial = noviBodyMaterial;
        Destroy(body.GetComponent<Collider>());

        GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
        face.name = "Novi Face";
        face.transform.SetParent(noviRoot, false);
        face.transform.localPosition = new Vector3(0f, 0.45f, -0.31f);
        face.transform.localScale = new Vector3(0.36f, 0.22f, 0.03f);
        face.GetComponent<Renderer>().sharedMaterial = noviFaceMaterial;
        Destroy(face.GetComponent<Collider>());

        CreateNoviEye(-0.1f);
        CreateNoviEye(0.1f);
    }

    private void CreateNoviEye(float x)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "Novi Eye";
        eye.transform.SetParent(noviRoot, false);
        eye.transform.localPosition = new Vector3(x, 0.48f, -0.34f);
        eye.transform.localScale = Vector3.one * 0.055f;
        eye.GetComponent<Renderer>().sharedMaterial = astronautVisorMaterial;
        Destroy(eye.GetComponent<Collider>());
    }

    private void BuildInterface()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        GameObject canvasObject = new GameObject("AstroBrick HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject top = Panel(canvasObject.transform, "Top Bar", new Color(0.04f, 0.07f, 0.14f, 0.92f));
        Rect(top.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 86f));
        Horizontal(top, 16, 10, TextAnchor.MiddleLeft, true);

        GameObject left = Panel(canvasObject.transform, "Mission Panel", new Color(0.97f, 0.99f, 1f, 0.94f));
        Rect(left.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, -28f), new Vector2(370f, -178f));
        Vertical(left, 14, 8, TextAnchor.UpperCenter);

        GameObject right = Panel(canvasObject.transform, "Mission Control Panel", new Color(0.05f, 0.08f, 0.15f, 0.92f));
        Rect(right.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-18f, -28f), new Vector2(430f, -178f));
        Vertical(right, 14, 8, TextAnchor.UpperCenter);

        GameObject bottom = Panel(canvasObject.transform, "Command Bar", new Color(0.97f, 0.99f, 1f, 0.94f));
        Rect(bottom.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 154f));
        Vertical(bottom, 12, 6, TextAnchor.UpperCenter);

        BuildTop(top.transform);
        BuildLeft(left.transform);
        BuildRight(right.transform);
        BuildBottom(bottom.transform);
    }

    private void BuildTop(Transform parent)
    {
        GameObject titleGroup = LayoutGroup(parent, "Title Group", false, 0, 0, TextAnchor.MiddleLeft);
        Layout(titleGroup, 410f, 64f, 0f, 0f);
        titleText = TextUi(titleGroup.transform, "Title", "AstroBrick Mission", 28, TextAnchor.MiddleLeft, Color.white);
        Layout(titleText.gameObject, 390f, 34f, 0f, 0f);
        loadText = TextUi(titleGroup.transform, "Load", "", 13, TextAnchor.MiddleLeft, new Color(0.76f, 0.88f, 1f, 1f));
        Layout(loadText.gameObject, 390f, 22f, 0f, 0f);

        GameObject conditionGroup = LayoutGroup(parent, "Condition Buttons", true, 8, 0, TextAnchor.MiddleCenter);
        Layout(conditionGroup, 560f, 58f, 1f, 0f);
        AddConditionButton(conditionGroup.transform, "Control", ConditionMode.Control);
        AddConditionButton(conditionGroup.transform, "Fixed Feedback", ConditionMode.FixedFeedback);
        AddConditionButton(conditionGroup.transform, "LLM + MCQ", ConditionMode.LlmMcq);
        AddConditionButton(conditionGroup.transform, "Full System", ConditionMode.FullSystem);

        GameObject stats = LayoutGroup(parent, "Stats", false, 4, 0, TextAnchor.MiddleRight);
        Layout(stats, 430f, 64f, 0f, 0f);
        telemetryText = TextUi(stats.transform, "Telemetry Top", "", 13, TextAnchor.UpperRight, new Color(0.85f, 0.94f, 1f, 1f));
        Layout(telemetryText.gameObject, 410f, 38f, 0f, 0f);
        GameObject loadTrack = Panel(stats.transform, "Load Track", new Color(0.36f, 0.44f, 0.55f, 0.7f));
        Layout(loadTrack, 410f, 10f, 0f, 0f);
        loadFill = Panel(loadTrack.transform, "Load Fill", new Color(0.22f, 0.82f, 0.5f, 1f)).GetComponent<Image>();
        Rect(loadFill.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        loadFill.type = Image.Type.Filled;
        loadFill.fillMethod = Image.FillMethod.Horizontal;
    }

    private void AddConditionButton(Transform parent, string label, ConditionMode mode)
    {
        Button button = ButtonUi(parent, label, 130f, 34f, delegate { SelectCondition(mode); });
        conditionButtons.Add(button);
    }

    private void BuildLeft(Transform parent)
    {
        Text header = TextUi(parent, "Mission Header", "Mission Brief", 24, TextAnchor.MiddleLeft, new Color(0.08f, 0.14f, 0.24f, 1f));
        Layout(header.gameObject, 0f, 30f, 1f, 0f);
        missionText = TextUi(parent, "Mission Text", "", 14, TextAnchor.UpperLeft, new Color(0.12f, 0.18f, 0.27f, 1f));
        Layout(missionText.gameObject, 0f, 130f, 1f, 0f);
        partListText = TextUi(parent, "Part List", "", 13, TextAnchor.UpperLeft, new Color(0.12f, 0.18f, 0.27f, 1f));
        Layout(partListText.gameObject, 0f, 94f, 1f, 0f);
        topProjection = Projection(parent, "Top View", GridDepth, GridWidth);
        frontProjection = Projection(parent, "Front View", MaxLayerUnits, GridWidth);
        sideProjection = Projection(parent, "Side View", MaxLayerUnits, GridDepth);
        targetInfoText = TextUi(parent, "Target Info", "", 12, TextAnchor.UpperLeft, new Color(0.22f, 0.3f, 0.4f, 1f));
        Layout(targetInfoText.gameObject, 0f, 70f, 1f, 0f);
    }

    private void BuildRight(Transform parent)
    {
        Text header = TextUi(parent, "Control Header", "Mission Control + Novi", 23, TextAnchor.MiddleLeft, Color.white);
        Layout(header.gameObject, 0f, 30f, 1f, 0f);
        diagnosisText = TextUi(parent, "Diagnosis", "", 13, TextAnchor.UpperLeft, new Color(1f, 0.9f, 0.58f, 1f));
        Layout(diagnosisText.gameObject, 0f, 118f, 1f, 0f);
        feedbackText = TextUi(parent, "Feedback", "", 14, TextAnchor.UpperLeft, new Color(0.94f, 0.98f, 1f, 1f));
        Layout(feedbackText.gameObject, 0f, 142f, 1f, 0f);
        mcqText = TextUi(parent, "MCQ Text", "", 13, TextAnchor.UpperLeft, new Color(0.86f, 0.96f, 1f, 1f));
        Layout(mcqText.gameObject, 0f, 82f, 1f, 0f);

        GameObject mcqRow1 = LayoutGroup(parent, "MCQ Row 1", true, 6, 0, TextAnchor.MiddleLeft);
        Layout(mcqRow1, 0f, 34f, 1f, 0f);
        mcqButtons.Add(ButtonUi(mcqRow1.transform, "A", 44f, 32f, delegate { AnswerMcq(0); }));
        mcqButtons.Add(ButtonUi(mcqRow1.transform, "B", 44f, 32f, delegate { AnswerMcq(1); }));
        mcqButtons.Add(ButtonUi(mcqRow1.transform, "C", 44f, 32f, delegate { AnswerMcq(2); }));
        mcqButtons.Add(ButtonUi(mcqRow1.transform, "D", 44f, 32f, delegate { AnswerMcq(3); }));

        noviText = TextUi(parent, "Novi Text", "", 13, TextAnchor.UpperLeft, new Color(0.78f, 0.92f, 1f, 1f));
        Layout(noviText.gameObject, 0f, 86f, 1f, 0f);

        GameObject llmRow = LayoutGroup(parent, "LLM Row", true, 6, 0, TextAnchor.MiddleLeft);
        Layout(llmRow, 0f, 36f, 1f, 0f);
        apiKeyInput = InputUi(llmRow.transform, "API Key", "OpenAI API key", 178f, 34f, true);
        modelInput = InputUi(llmRow.transform, "Model", DefaultModel, 126f, 34f, false);
        modelInput.text = DefaultModel;
        askGptButton = ButtonUi(llmRow.transform, "Ask GPT", 82f, 34f, AskGpt);

        teachInput = InputUi(parent, "Teach Novi", "Teach Novi: name parts, layers, orientation, and reference frame...", 0f, 50f, false);
        teachInput.lineType = InputField.LineType.MultiLineNewline;
        teachInput.characterLimit = 280;
        GameObject teachRow = LayoutGroup(parent, "Teach Row", true, 6, 0, TextAnchor.MiddleLeft);
        Layout(teachRow, 0f, 36f, 1f, 0f);
        teachButton = ButtonUi(teachRow.transform, "Teach Novi", 112f, 34f, TeachNovi);
        ButtonUi(teachRow.transform, "Hint", 68f, 34f, ShowHint);
        ButtonUi(teachRow.transform, "Ghost", 72f, 34f, ToggleTargetGhost);
    }

    private void BuildBottom(Transform parent)
    {
        statusText = TextUi(parent, "Status", "", 15, TextAnchor.MiddleCenter, new Color(0.08f, 0.14f, 0.24f, 1f));
        Layout(statusText.gameObject, 0f, 25f, 1f, 0f);

        GameObject palette = LayoutGroup(parent, "Part Palette", true, 6, 0, TextAnchor.MiddleCenter);
        Layout(palette, 0f, 38f, 1f, 0f);
        partButtons.Clear();
        foreach (KeyValuePair<string, BrickSpec> entry in partLibrary)
        {
            Button button = ButtonUi(palette.transform, entry.Value.Label, 92f, 32f, delegate { });
            partButtons.Add(button);
        }

        GameObject actions = LayoutGroup(parent, "Actions", true, 8, 0, TextAnchor.MiddleCenter);
        Layout(actions, 0f, 38f, 1f, 0f);
        ButtonUi(actions.transform, "Rotate", 84f, 34f, RotateSelectedPart);
        ButtonUi(actions.transform, "Layer -", 84f, 34f, delegate { SetActiveLayer(activeLayer - 1); });
        ButtonUi(actions.transform, "Layer +", 84f, 34f, delegate { SetActiveLayer(activeLayer + 1); });
        ButtonUi(actions.transform, "Submit", 92f, 34f, SubmitBuild);
        ButtonUi(actions.transform, "Reset", 82f, 34f, ResetBuild);
        nextButton = ButtonUi(actions.transform, "Next", 82f, 34f, NextMission);
        ButtonUi(actions.transform, "Free", 68f, 34f, delegate { ApplyView(ViewMode.Free); });
        ButtonUi(actions.transform, "Front", 72f, 34f, delegate { ApplyView(ViewMode.Front); });
        ButtonUi(actions.transform, "Side", 68f, 34f, delegate { ApplyView(ViewMode.Side); });
        ButtonUi(actions.transform, "Top", 64f, 34f, delegate { ApplyView(ViewMode.Top); });
        ButtonUi(actions.transform, "Robot", 72f, 34f, delegate { ApplyView(ViewMode.Robot); });

        GameObject effort = LayoutGroup(parent, "Effort", true, 8, 0, TextAnchor.MiddleCenter);
        Layout(effort, 0f, 30f, 1f, 0f);
        ButtonUi(effort.transform, "Effort 1", 84f, 28f, delegate { SetMentalEffort(1); });
        ButtonUi(effort.transform, "Effort 2", 84f, 28f, delegate { SetMentalEffort(2); });
        ButtonUi(effort.transform, "Effort 3", 84f, 28f, delegate { SetMentalEffort(3); });
    }

    private void StartMission(int index)
    {
        missionIndex = (index + missions.Count) % missions.Count;
        attempts = 0;
        hintsUsed = 0;
        viewSwitches = 0;
        actionsThisMission = 0;
        mentalEffort = 2;
        activeLayer = 0;
        selectedOrientation = 0;
        selectedPartIndex = 0;
        targetGhostVisible = true;
        lastDiagnostic = null;
        currentMcq = null;
        missionStartTime = Time.time;
        playerBuild.Clear();
        ClearPlacementObjects();
        RenderTargetPreview();
        RefreshPaletteButtons();
        UpdateAllProjectionPanels();

        if (nextButton != null)
        {
            nextButton.interactable = false;
        }

        diagnosisText.text = "";
        feedbackText.text = OpeningFeedback();
        mcqText.text = "";
        noviText.text = "Novi: I am ready to learn, but I will ask if your left/right or high-edge instructions are unclear.";
        SetMcqButtons(false);
        SetStatus("Choose a basic brick, then click the moon grid. Drag placed bricks to move them; right-click removes.");
        Log("start_mission", missions[missionIndex].Id);
    }

    private void RefreshPaletteButtons()
    {
        MissionData mission = missions[missionIndex];
        for (int i = 0; i < partButtons.Count; i++)
        {
            Button button = partButtons[i];
            BrickSpec spec = PartByIndex(i);
            bool allowed = mission.AllowedPartIds.Contains(spec.Id);
            button.gameObject.SetActive(allowed);
            button.onClick.RemoveAllListeners();
            int captured = i;
            button.onClick.AddListener(delegate { SelectPart(captured); });
        }

        SelectFirstAllowedPart();
    }

    private void SelectFirstAllowedPart()
    {
        MissionData mission = missions[missionIndex];
        for (int i = 0; i < partButtons.Count; i++)
        {
            BrickSpec spec = PartByIndex(i);
            if (mission.AllowedPartIds.Contains(spec.Id))
            {
                SelectPart(i);
                return;
            }
        }
    }

    private void SelectPart(int index)
    {
        selectedPartIndex = Mathf.Clamp(index, 0, partLibrary.Count - 1);
        for (int i = 0; i < partButtons.Count; i++)
        {
            Image image = partButtons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = i == selectedPartIndex ? new Color(0.12f, 0.58f, 0.95f, 1f) : new Color(0.16f, 0.24f, 0.34f, 1f);
            }
        }

        SetStatus("Selected " + GetSelectedSpec().Label + " | orientation " + selectedOrientation + " deg.");
    }

    private void SelectCondition(ConditionMode mode)
    {
        conditionMode = mode;
        for (int i = 0; i < conditionButtons.Count; i++)
        {
            Image image = conditionButtons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = ((ConditionMode)i) == conditionMode ? new Color(0.1f, 0.58f, 0.95f, 1f) : new Color(0.16f, 0.24f, 0.34f, 1f);
            }
        }

        UpdateConditionControls();
        SetStatus("Condition: " + conditionMode);
        Log("condition", conditionMode.ToString());
    }

    private void UpdateConditionControls()
    {
        if (askGptButton != null)
        {
            askGptButton.interactable = (conditionMode == ConditionMode.LlmMcq || conditionMode == ConditionMode.FullSystem) && !gptRunning;
        }

        if (teachButton != null)
        {
            teachButton.interactable = conditionMode == ConditionMode.FullSystem;
        }
    }

    private void UpdateHover()
    {
        hoverValid = false;
        if (mainCamera == null)
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        Array.Sort(hits, delegate (RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            AstroBrickObject brickObject = hit.collider.GetComponentInParent<AstroBrickObject>();
            if (brickObject != null)
            {
                if (!isDragging || brickObject.InstanceId != draggingId)
                {
                    BrickPlacement placement = FindPlayerPlacement(brickObject.InstanceId);
                    if (placement != null)
                    {
                        hoverCell = placement.Anchor;
                        hoverValid = true;
                        break;
                    }
                }
            }

            AstroBrickCell cell = hit.collider.GetComponent<AstroBrickCell>();
            if (cell != null)
            {
                hoverCell = new Vector3Int(cell.X, activeLayer, cell.Z);
                hoverValid = true;
                break;
            }
        }

        UpdateCursor();
    }

    private void HandleMouse()
    {
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (!overUi)
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                SetActiveLayer(activeLayer + (wheel > 0f ? 1 : -1));
            }
        }

        if (overUi && !isDragging)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) && hoverValid)
        {
            string hitId = HitBrickIdUnderMouse();
            if (!string.IsNullOrEmpty(hitId))
            {
                BeginDrag(hitId);
            }
            else
            {
                PlaceSelectedAt(hoverCell);
            }
        }

        if (Input.GetMouseButton(0) && isDragging && hoverValid)
        {
            MoveDraggingPreview(hoverCell);
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            EndDrag();
        }

        if (!overUi && Input.GetMouseButtonDown(1))
        {
            string hitId = HitBrickIdUnderMouse();
            if (!string.IsNullOrEmpty(hitId))
            {
                RemovePlacement(hitId);
            }
        }
    }

    private void HandleKeyboard()
    {
        if (IsTyping())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            RotateSelectedPart();
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            SetActiveLayer(activeLayer - 1);
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            SetActiveLayer(activeLayer + 1);
        }
        else if (Input.GetKeyDown(KeyCode.Return))
        {
            SubmitBuild();
        }
        else if (Input.GetKeyDown(KeyCode.H))
        {
            ShowHint();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ApplyView(ViewMode.Free);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ApplyView(ViewMode.Front);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ApplyView(ViewMode.Side);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            ApplyView(ViewMode.Top);
        }
    }

    private bool IsTyping()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
        {
            return false;
        }

        return EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;
    }

    private string HitBrickIdUnderMouse()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        Array.Sort(hits, delegate (RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });
        for (int i = 0; i < hits.Length; i++)
        {
            AstroBrickObject marker = hits[i].collider.GetComponentInParent<AstroBrickObject>();
            if (marker != null)
            {
                return marker.InstanceId;
            }
        }

        return "";
    }

    private void BeginDrag(string instanceId)
    {
        BrickPlacement placement = FindPlayerPlacement(instanceId);
        if (placement == null)
        {
            return;
        }

        isDragging = true;
        draggingId = instanceId;
        dragStartAnchor = placement.Anchor;
        SetStatus("Dragging " + placement.Spec.Label + ". Release on a valid grid cell.");
        Log("begin_drag", instanceId);
    }

    private void MoveDraggingPreview(Vector3Int cell)
    {
        GameObject obj;
        if (placementObjects.TryGetValue(draggingId, out obj))
        {
            obj.transform.position = GridToWorld(cell);
        }
    }

    private void EndDrag()
    {
        BrickPlacement placement = FindPlayerPlacement(draggingId);
        if (placement != null)
        {
            Vector3Int target = hoverValid ? hoverCell : dragStartAnchor;
            Vector3Int oldAnchor = placement.Anchor;
            placement.Anchor = target;
            if (!CanPlace(placement, placement.InstanceId))
            {
                placement.Anchor = oldAnchor;
                SetStatus("That move would collide or leave the grid. Returned to start.");
            }
            else
            {
                actionsThisMission++;
                lastDiagnostic = null;
                SetStatus("Moved " + placement.Spec.Label + " to x" + target.x + " layer " + target.y + " z" + target.z + ".");
                Log("end_drag", oldAnchor + " -> " + target);
            }

            RenderPlayerBuild();
        }

        draggingId = "";
        isDragging = false;
        UpdateAllProjectionPanels();
    }

    private void PlaceSelectedAt(Vector3Int anchor)
    {
        BrickPlacement placement = new BrickPlacement
        {
            InstanceId = "player_" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Spec = GetSelectedSpec(),
            Anchor = anchor,
            Orientation = selectedOrientation
        };

        if (!CanPlace(placement, ""))
        {
            SetStatus("Cannot place " + placement.Spec.Label + " there. Try another cell, layer, or rotation.");
            return;
        }

        playerBuild.Add(placement);
        CreatePlacementObject(placement, buildRoot, false);
        actionsThisMission++;
        lastDiagnostic = null;
        UpdateAllProjectionPanels();
        SetStatus("Placed " + placement.Spec.Label + " at x" + anchor.x + " layer " + anchor.y + " z" + anchor.z + ".");
        Log("place", placement.Spec.Id + "@" + anchor);
    }

    private void RemovePlacement(string instanceId)
    {
        for (int i = playerBuild.Count - 1; i >= 0; i--)
        {
            if (playerBuild[i].InstanceId == instanceId)
            {
                playerBuild.RemoveAt(i);
            }
        }

        GameObject obj;
        if (placementObjects.TryGetValue(instanceId, out obj))
        {
            Destroy(obj);
            placementObjects.Remove(instanceId);
        }

        actionsThisMission++;
        lastDiagnostic = null;
        UpdateAllProjectionPanels();
        SetStatus("Removed part.");
        Log("remove", instanceId);
    }

    private bool CanPlace(BrickPlacement placement, string ignoreInstanceId)
    {
        List<Vector3Int> cells = OccupiedCells(placement);
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int cell = cells[i];
            if (cell.x < 0 || cell.x >= GridWidth || cell.z < 0 || cell.z >= GridDepth || cell.y < 0 || cell.y >= MaxLayerUnits)
            {
                return false;
            }
        }

        HashSet<Vector3Int> occupied = Occupancy(playerBuild, ignoreInstanceId);
        for (int i = 0; i < cells.Count; i++)
        {
            if (occupied.Contains(cells[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void RotateSelectedPart()
    {
        selectedOrientation = NormalizeOrientation(selectedOrientation + 90);
        SetStatus("Rotation set to " + selectedOrientation + " degrees.");
        UpdateCursor();
    }

    private void SetActiveLayer(int layer)
    {
        activeLayer = Mathf.Clamp(layer, 0, MaxLayerUnits - 1);
        SetStatus("Active layer: " + activeLayer + " plate units.");
        UpdateCursor();
    }

    private void SubmitBuild()
    {
        attempts++;
        lastDiagnostic = Diagnose();
        diagnosisText.text = FormatDiagnostic(lastDiagnostic);
        feedbackText.text = LocalFeedback(lastDiagnostic);
        currentMcq = BuildMcq(lastDiagnostic);
        ShowMcq(currentMcq);
        ApplyDiagnosticMaterials(lastDiagnostic);
        UpdateAllProjectionPanels();

        if (lastDiagnostic.Passed)
        {
            solvedMissions++;
            int earned = Mathf.Max(45, 170 - Mathf.RoundToInt(Time.time - missionStartTime) - hintsUsed * 8 - attempts * 8);
            score += earned;
            nextButton.interactable = true;
            noviText.text = "Novi: Mission complete. Now teach me how to rebuild it using layers, orientation, and a reference frame.";
            SetStatus("Mission solved. Teach Novi, then continue.");
            Log("submit_pass", "points=" + earned);
        }
        else
        {
            score = Mathf.Max(0, score - 3);
            SetStatus("Mission Control diagnosed the build. Answer the reflective prompt before revising.");
            Log("submit_fail", lastDiagnostic.Error.ToString());
        }
    }

    private DiagnosticResult Diagnose()
    {
        MissionData mission = missions[missionIndex];
        DiagnosticResult result = new DiagnosticResult();
        result.Passed = ExactSetMatch(playerBuild, mission.Target);

        if (result.Passed)
        {
            result.Error = ErrorType.None;
            result.MainFact = "Player structure exactly matches every required basic brick.";
            result.Facts.Add("all part categories match");
            result.Facts.Add("all footprints, layers, positions, and orientations match");
            return result;
        }

        CollectMissingAndExtra(result, mission.Target, playerBuild);

        ErrorType supportError = FindSupportError(playerBuild, result);
        if (supportError == ErrorType.SupportError)
        {
            return result;
        }

        if (FindWrongOrientation(result, mission.Target, playerBuild))
        {
            return result;
        }

        if (FindMirrorError(result, mission.Target, playerBuild))
        {
            return result;
        }

        if (FindWrongLayer(result, mission.Target, playerBuild))
        {
            return result;
        }

        if (FindWrongPart(result, mission.Target, playerBuild))
        {
            return result;
        }

        if (FindWrongPosition(result, mission.Target, playerBuild))
        {
            return result;
        }

        if (result.Missing.Count > 0 && result.Extra.Count == 0)
        {
            result.Error = ErrorType.MissingElement;
            result.TargetReference = result.Missing[0];
            result.MainFact = "At least one required part is missing.";
            result.Facts.Add("missing: " + PartSummary(result.TargetReference));
            return result;
        }

        if (result.Extra.Count > 0 && result.Missing.Count == 0)
        {
            result.Error = ErrorType.ExtraElement;
            result.PlayerReference = result.Extra[0];
            result.MainFact = "At least one extra part is present.";
            result.Facts.Add("extra: " + PartSummary(result.PlayerReference));
            return result;
        }

        result.Error = ErrorType.Unknown;
        result.MainFact = "The build differs in a mixed way: part, layer, and/or position need checking.";
        result.Facts.Add("missing count: " + result.Missing.Count);
        result.Facts.Add("extra count: " + result.Extra.Count);
        return result;
    }

    private void CollectMissingAndExtra(DiagnosticResult result, List<BrickPlacement> target, List<BrickPlacement> player)
    {
        bool[] playerMatched = new bool[player.Count];

        for (int i = 0; i < target.Count; i++)
        {
            bool found = false;
            for (int j = 0; j < player.Count; j++)
            {
                if (!playerMatched[j] && EquivalentPlacement(target[i], player[j], true, true, true, true))
                {
                    playerMatched[j] = true;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                result.Missing.Add(target[i]);
            }
        }

        for (int j = 0; j < player.Count; j++)
        {
            if (!playerMatched[j])
            {
                result.Extra.Add(player[j]);
            }
        }
    }

    private ErrorType FindSupportError(List<BrickPlacement> build, DiagnosticResult result)
    {
        HashSet<Vector3Int> occupied = Occupancy(build, "");
        for (int i = 0; i < build.Count; i++)
        {
            BrickPlacement placement = build[i];
            if (placement.Anchor.y <= 0)
            {
                continue;
            }

            List<Vector2Int> footprint = FootprintCells2D(placement);
            for (int j = 0; j < footprint.Count; j++)
            {
                Vector3Int below = new Vector3Int(footprint[j].x, placement.Anchor.y - 1, footprint[j].y);
                if (!occupied.Contains(below))
                {
                    result.Error = ErrorType.SupportError;
                    result.PlayerReference = placement;
                    result.MainFact = "A raised part does not have support underneath every stud footprint.";
                    result.Facts.Add("unsupported: " + PartSummary(placement));
                    return ErrorType.SupportError;
                }
            }
        }

        return ErrorType.None;
    }

    private bool FindWrongOrientation(DiagnosticResult result, List<BrickPlacement> target, List<BrickPlacement> player)
    {
        for (int i = 0; i < target.Count; i++)
        {
            for (int j = 0; j < player.Count; j++)
            {
                if (SamePart(target[i], player[j]) &&
                    target[i].Anchor == player[j].Anchor &&
                    target[i].Orientation != player[j].Orientation &&
                    target[i].Spec.Directional)
                {
                    result.Error = ErrorType.WrongOrientation;
                    result.TargetReference = target[i];
                    result.PlayerReference = player[j];
                    result.MainFact = "The selected directional part is in the right place, but its orientation is wrong.";
                    result.Facts.Add("target orientation: " + target[i].Orientation + " deg");
                    result.Facts.Add("player orientation: " + player[j].Orientation + " deg");
                    return true;
                }
            }
        }

        return false;
    }

    private bool FindMirrorError(DiagnosticResult result, List<BrickPlacement> target, List<BrickPlacement> player)
    {
        for (int i = 0; i < target.Count; i++)
        {
            BrickPlacement t = target[i];
            if (!t.Spec.Chiral)
            {
                continue;
            }

            for (int j = 0; j < player.Count; j++)
            {
                BrickPlacement p = player[j];
                if (t.Anchor == p.Anchor &&
                    t.Orientation == p.Orientation &&
                    t.Spec.Category == p.Spec.Category &&
                    t.Spec.Width == p.Spec.Width &&
                    t.Spec.Depth == p.Spec.Depth &&
                    t.Spec.Chirality != p.Spec.Chirality)
                {
                    result.Error = ErrorType.MirrorError;
                    result.TargetReference = t;
                    result.PlayerReference = p;
                    result.MainFact = "A left/right wedge pair was mirrored.";
                    result.Facts.Add("target chirality: " + t.Spec.Chirality);
                    result.Facts.Add("player chirality: " + p.Spec.Chirality);
                    return true;
                }
            }
        }

        return false;
    }

    private bool FindWrongLayer(DiagnosticResult result, List<BrickPlacement> target, List<BrickPlacement> player)
    {
        for (int i = 0; i < target.Count; i++)
        {
            for (int j = 0; j < player.Count; j++)
            {
                if (SamePart(target[i], player[j]) &&
                    target[i].Anchor.x == player[j].Anchor.x &&
                    target[i].Anchor.z == player[j].Anchor.z &&
                    target[i].Orientation == player[j].Orientation &&
                    target[i].Anchor.y != player[j].Anchor.y)
                {
                    result.Error = ErrorType.WrongLayer;
                    result.TargetReference = target[i];
                    result.PlayerReference = player[j];
                    result.MainFact = "A correct part is aligned in x/z but placed on the wrong layer.";
                    result.Facts.Add("target layer: " + target[i].Anchor.y);
                    result.Facts.Add("player layer: " + player[j].Anchor.y);
                    return true;
                }
            }
        }

        return false;
    }

    private bool FindWrongPart(DiagnosticResult result, List<BrickPlacement> target, List<BrickPlacement> player)
    {
        for (int i = 0; i < target.Count; i++)
        {
            for (int j = 0; j < player.Count; j++)
            {
                if (target[i].Anchor == player[j].Anchor &&
                    target[i].Orientation == player[j].Orientation &&
                    !SamePart(target[i], player[j]))
                {
                    result.Error = SameFootprint(target[i], player[j]) ? ErrorType.WrongPart : ErrorType.WrongFootprint;
                    result.TargetReference = target[i];
                    result.PlayerReference = player[j];
                    result.MainFact = result.Error == ErrorType.WrongPart
                        ? "A part at the correct place has the wrong category."
                        : "A part at the correct place has the wrong footprint.";
                    result.Facts.Add("target: " + target[i].Spec.Label);
                    result.Facts.Add("player: " + player[j].Spec.Label);
                    return true;
                }
            }
        }

        return false;
    }

    private bool FindWrongPosition(DiagnosticResult result, List<BrickPlacement> target, List<BrickPlacement> player)
    {
        for (int i = 0; i < target.Count; i++)
        {
            for (int j = 0; j < player.Count; j++)
            {
                if (SamePart(target[i], player[j]) &&
                    target[i].Orientation == player[j].Orientation &&
                    target[i].Anchor.y == player[j].Anchor.y &&
                    target[i].Anchor != player[j].Anchor)
                {
                    result.Error = ErrorType.WrongPosition;
                    result.TargetReference = target[i];
                    result.PlayerReference = player[j];
                    result.MainFact = "A correct part is on the correct layer but mapped to the wrong x/z position.";
                    result.Facts.Add("target anchor: " + target[i].Anchor);
                    result.Facts.Add("player anchor: " + player[j].Anchor);
                    return true;
                }
            }
        }

        return false;
    }

    private bool ExactSetMatch(List<BrickPlacement> player, List<BrickPlacement> target)
    {
        if (player.Count != target.Count)
        {
            return false;
        }

        bool[] matched = new bool[player.Count];
        for (int i = 0; i < target.Count; i++)
        {
            bool found = false;
            for (int j = 0; j < player.Count; j++)
            {
                if (!matched[j] && EquivalentPlacement(target[i], player[j], true, true, true, true))
                {
                    matched[j] = true;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    private bool EquivalentPlacement(BrickPlacement a, BrickPlacement b, bool requirePart, bool requireAnchor, bool requireOrientation, bool requireChirality)
    {
        if (requirePart && !SamePart(a, b))
        {
            return false;
        }

        if (requireChirality && a.Spec.Chirality != b.Spec.Chirality)
        {
            return false;
        }

        if (requireAnchor && a.Anchor != b.Anchor)
        {
            return false;
        }

        if (requireOrientation && a.Orientation != b.Orientation)
        {
            return false;
        }

        return true;
    }

    private bool SamePart(BrickPlacement a, BrickPlacement b)
    {
        return a.Spec.Id == b.Spec.Id;
    }

    private bool SameFootprint(BrickPlacement a, BrickPlacement b)
    {
        return a.Spec.Width == b.Spec.Width && a.Spec.Depth == b.Spec.Depth && a.Spec.HeightUnits == b.Spec.HeightUnits;
    }

    private void ApplyDiagnosticMaterials(DiagnosticResult result)
    {
        ClearChildren(diagnosticRoot);
        HashSet<string> targetKeys = new HashSet<string>();
        for (int i = 0; i < missions[missionIndex].Target.Count; i++)
        {
            targetKeys.Add(PlacementKey(missions[missionIndex].Target[i]));
        }

        for (int i = 0; i < playerBuild.Count; i++)
        {
            GameObject obj;
            if (!placementObjects.TryGetValue(playerBuild[i].InstanceId, out obj))
            {
                continue;
            }

            Material material = targetKeys.Contains(PlacementKey(playerBuild[i])) || result.Passed ? highlightMaterial : extraMaterial;
            ApplyMaterialRecursive(obj.transform, material);
        }

        for (int i = 0; i < result.Missing.Count; i++)
        {
            CreatePlacementObject(result.Missing[i], diagnosticRoot, true);
        }
    }

    private string PlacementKey(BrickPlacement p)
    {
        return p.Spec.Id + "|" + p.Anchor.x + "," + p.Anchor.y + "," + p.Anchor.z + "|" + p.Orientation;
    }

    private string FormatDiagnostic(DiagnosticResult result)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Deterministic geometry diagnosis");
        builder.AppendLine("Result: " + (result.Passed ? "solved" : "not solved"));
        builder.AppendLine("Error type: " + ErrorLabel(result.Error));
        builder.AppendLine(result.MainFact);
        for (int i = 0; i < result.Facts.Count; i++)
        {
            builder.AppendLine("- " + result.Facts[i]);
        }

        return builder.ToString();
    }

    private string LocalFeedback(DiagnosticResult result)
    {
        if (conditionMode == ConditionMode.Control)
        {
            return result.Passed ? "Correct." : "The structure is not correct yet.";
        }

        if (result.Passed)
        {
            return "Mission Control: excellent work. Now turn your construction strategy into instructions that Novi can reuse.";
        }

        string prefix = "Mission Control: ";
        if (EstimateLoadBand() == LoadBand.Overloaded)
        {
            prefix += "short path first. Do not rebuild everything. ";
        }

        switch (result.Error)
        {
            case ErrorType.WrongOrientation:
                return prefix + "your part choice and location are close. Focus only on the directional face or high edge.";
            case ErrorType.MirrorError:
                return prefix + "this looks like a left/right mirror. Stand at the target front and compare wedge chirality.";
            case ErrorType.WrongLayer:
                return prefix + "the part belongs in this x/z area, but the layer is wrong. Think in plate-height units.";
            case ErrorType.WrongPart:
                return prefix + "the location is useful, but the part category is not the target category.";
            case ErrorType.WrongFootprint:
                return prefix + "the location is useful, but the footprint length or width is different.";
            case ErrorType.SupportError:
                return prefix + "a raised component needs support underneath. Check the footprint directly below it.";
            case ErrorType.WrongPosition:
                return prefix + "the part type is correct, but it is mapped to the wrong grid position.";
            case ErrorType.MissingElement:
                return prefix + "a required component is missing. Compare the target part list and top view.";
            case ErrorType.ExtraElement:
                return prefix + "there is an extra component. Look for marks that appear in yours but not in the target.";
            default:
                return prefix + "several relations differ. Fix one feature at a time: part, layer, then orientation.";
        }
    }

    private McqData BuildMcq(DiagnosticResult result)
    {
        if (result == null || result.Passed || conditionMode == ConditionMode.Control)
        {
            return null;
        }

        McqData data = new McqData();
        switch (result.Error)
        {
            case ErrorType.WrongOrientation:
                data.Question = "Reflective correction: what should change?";
                data.Options[0] = "Keep the part and place, then rotate the directional part.";
                data.Options[1] = "Replace it with a basic brick.";
                data.Options[2] = "Move it to the bottom layer.";
                data.Options[3] = "Do nothing; it is already correct.";
                data.CorrectIndex = 0;
                data.CorrectFeedback = "Correct. This is an orientation correction, not a part or layer correction.";
                data.WrongFeedback[1] = "That treats orientation as a wrong-part error. The part category is already useful.";
                data.WrongFeedback[2] = "That treats orientation as a layer error. The location is the stronger clue.";
                data.WrongFeedback[3] = "The geometry engine found a rotation mismatch.";
                break;
            case ErrorType.MirrorError:
                data.Question = "Reflective correction: how do you resolve the mirror?";
                data.Options[0] = "Use the opposite left/right wedge from the target front view.";
                data.Options[1] = "Add one extra plate in the center.";
                data.Options[2] = "Raise both wedges by one layer.";
                data.Options[3] = "Ignore left/right and only match color.";
                data.CorrectIndex = 0;
                data.CorrectFeedback = "Correct. Chirality is the critical feature here.";
                data.WrongFeedback[1] = "Adding a plate does not solve a left/right wedge swap.";
                data.WrongFeedback[2] = "The issue is mirror relation, not layer height.";
                data.WrongFeedback[3] = "Color is not the scored spatial relation.";
                break;
            case ErrorType.WrongLayer:
                data.Question = "Reflective correction: what is the best first fix?";
                data.Options[0] = "Move the same part to the target layer.";
                data.Options[1] = "Change the part into a different footprint.";
                data.Options[2] = "Rotate it four times.";
                data.Options[3] = "Remove every part on the board.";
                data.CorrectIndex = 0;
                data.CorrectFeedback = "Correct. Keep part identity stable and fix layer.";
                break;
            case ErrorType.SupportError:
                data.Question = "Reflective correction: what makes the structure stable?";
                data.Options[0] = "Add or move support directly under the raised footprint.";
                data.Options[1] = "Change all tiles into slopes.";
                data.Options[2] = "Hide the top view.";
                data.Options[3] = "Swap left and right wedges.";
                data.CorrectIndex = 0;
                data.CorrectFeedback = "Correct. Support is checked below the occupied studs.";
                break;
            default:
                data.Question = "Reflective correction: what should you compare first?";
                data.Options[0] = "Compare the target part list, then one view at a time.";
                data.Options[1] = "Randomly add more parts.";
                data.Options[2] = "Ignore the grid coordinates.";
                data.Options[3] = "Only look from one camera angle.";
                data.CorrectIndex = 0;
                data.CorrectFeedback = "Correct. Controlled comparison reduces cognitive load.";
                break;
        }

        for (int i = 0; i < data.Options.Length; i++)
        {
            if (string.IsNullOrEmpty(data.WrongFeedback[i]))
            {
                data.WrongFeedback[i] = "Not quite. This choice targets a different misconception than the one diagnosed.";
            }
        }

        return data;
    }

    private void ShowMcq(McqData data)
    {
        bool visible = data != null && (conditionMode == ConditionMode.FixedFeedback || conditionMode == ConditionMode.LlmMcq || conditionMode == ConditionMode.FullSystem);
        SetMcqButtons(visible);
        if (!visible)
        {
            mcqText.text = "";
            return;
        }

        mcqText.text =
            data.Question + "\n" +
            "A. " + data.Options[0] + "\n" +
            "B. " + data.Options[1] + "\n" +
            "C. " + data.Options[2] + "\n" +
            "D. " + data.Options[3];
    }

    private void AnswerMcq(int index)
    {
        if (currentMcq == null)
        {
            return;
        }

        mcqTotal++;
        if (index == currentMcq.CorrectIndex)
        {
            mcqCorrect++;
            feedbackText.text = currentMcq.CorrectFeedback + "\n\n" + LocalFeedback(lastDiagnostic);
            score += 8;
            SetStatus("Reflective prompt correct.");
        }
        else
        {
            feedbackText.text = currentMcq.WrongFeedback[index] + "\n\nCorrect answer: " + (char)('A' + currentMcq.CorrectIndex) + ". " + currentMcq.Options[currentMcq.CorrectIndex];
            SetStatus("Reflective prompt answered. Use the explanation before revising.");
        }

        SetMcqButtons(false);
        Log("mcq", "choice=" + index + ", correct=" + currentMcq.CorrectIndex);
    }

    private void AskGpt()
    {
        if (conditionMode != ConditionMode.LlmMcq && conditionMode != ConditionMode.FullSystem)
        {
            feedbackText.text = "This condition does not use GPT. Switch to LLM + MCQ or Full System.";
            return;
        }

        string apiKey = apiKeyInput == null ? "" : apiKeyInput.text.Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            feedbackText.text = LocalFeedback(lastDiagnostic) + "\n\nLocal mode: paste an OpenAI API key to let Mission Control call GPT.";
            return;
        }

        StartCoroutine(CallGpt(apiKey, GetModel(), BuildGptPrompt()));
    }

    private IEnumerator CallGpt(string apiKey, string model, string prompt)
    {
        gptRunning = true;
        UpdateConditionControls();
        SetStatus("Mission Control is calling GPT...");

        string body =
            "{" +
            "\"model\":\"" + JsonEscape(model) + "\"," +
            "\"input\":\"" + JsonEscape(prompt) + "\"," +
            "\"max_output_tokens\":280," +
            "\"temperature\":0.35" +
            "}";

        UnityWebRequest request = new UnityWebRequest(OpenAiResponsesUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return request.SendWebRequest();

        gptRunning = false;
        UpdateConditionControls();

        bool failed = request.result == UnityWebRequest.Result.ConnectionError ||
                      request.result == UnityWebRequest.Result.ProtocolError ||
                      request.result == UnityWebRequest.Result.DataProcessingError;

        if (failed)
        {
            feedbackText.text = "GPT call failed: " + request.error + "\n\nFallback:\n" + LocalFeedback(lastDiagnostic);
            SetStatus("GPT failed; local feedback remains active.");
            Log("gpt_error", request.error);
        }
        else
        {
            string text = ExtractOutputText(request.downloadHandler.text);
            feedbackText.text = string.IsNullOrEmpty(text) ? LocalFeedback(lastDiagnostic) : "Mission Control GPT\n" + text;
            SetStatus("GPT feedback received.");
            Log("gpt_success", "chars=" + text.Length);
        }

        request.Dispose();
    }

    private string BuildGptPrompt()
    {
        DiagnosticResult diagnostic = lastDiagnostic;
        if (diagnostic == null)
        {
            diagnostic = Diagnose();
        }

        MissionData mission = missions[missionIndex];
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("You are Mission Control in AstroBrick Mission, a LEGO-inspired spatial skills training game.");
        builder.AppendLine("The geometry engine decides correctness. You only explain structured facts and ask reflective questions.");
        builder.AppendLine("Keep feedback concise, constructive, and specific. Do not give a full answer unless the learner is overloaded.");
        builder.AppendLine("Mission: " + mission.Id + " " + mission.Title);
        builder.AppendLine("Skill phase: " + mission.SkillPhase);
        builder.AppendLine("Representation: " + mission.Representation);
        builder.AppendLine("Vocabulary: " + mission.Vocabulary);
        builder.AppendLine("Condition: " + conditionMode);
        builder.AppendLine("Cognitive load: " + EstimateLoadBand() + " " + Mathf.RoundToInt(EstimateLoadScore() * 100f) + "/100");
        builder.AppendLine("Attempts: " + attempts + ", hints: " + hintsUsed + ", view switches: " + viewSwitches);
        builder.AppendLine("Passed: " + diagnostic.Passed);
        builder.AppendLine("Error type: " + ErrorLabel(diagnostic.Error));
        builder.AppendLine("Main fact: " + diagnostic.MainFact);
        builder.AppendLine("Target part list: " + TargetPartList(mission.Target));
        builder.AppendLine("Player part list: " + TargetPartList(playerBuild));
        builder.AppendLine("If there is an error, give one next action and one reflective MCQ idea.");
        if (conditionMode == ConditionMode.FullSystem)
        {
            builder.AppendLine("End with one sentence Novi might ask if the learner's spatial language is ambiguous.");
        }

        return builder.ToString();
    }

    private void TeachNovi()
    {
        if (conditionMode != ConditionMode.FullSystem)
        {
            noviText.text = "Novi: Teach-the-agent is only enabled in Full System.";
            return;
        }

        string rule = teachInput == null ? "" : teachInput.text.Trim();
        if (rule.Length < 12)
        {
            noviText.text = "Novi: I need more detail. Name a part, layer, orientation, or reference frame.";
            return;
        }

        int scoreValue = ScoreTeaching(rule);
        bool success = scoreValue >= 65;
        noviText.text =
            "Novi parsed your instruction\n" +
            "part/layer/orientation/reference score: " + scoreValue + "/100\n" +
            (success
                ? "Novi: I can rebuild the structure from your rule. Your spatial language is transferable."
                : "Novi: I still need a clearer reference frame. Is left/right from your view, target front, or robot view?");

        score += success ? 18 : 4;
        Log("teach_novi", "score=" + scoreValue);
    }

    private int ScoreTeaching(string rule)
    {
        string lower = rule.ToLowerInvariant();
        int value = 12;
        if (ContainsAny(lower, "brick", "plate", "tile", "slope", "wedge", "part"))
        {
            value += 22;
        }

        if (ContainsAny(lower, "layer", "above", "below", "top", "bottom", "support"))
        {
            value += 22;
        }

        if (ContainsAny(lower, "rotate", "orientation", "high edge", "low edge", "face", "toward", "away"))
        {
            value += 22;
        }

        if (ContainsAny(lower, "front", "back", "left", "right", "center", "robot", "target", "view"))
        {
            value += 22;
        }

        return Mathf.Clamp(value, 0, 100);
    }

    private void ShowHint()
    {
        hintsUsed++;
        string hint;
        if (lastDiagnostic == null)
        {
            hint = "Hint ladder: check part identity, then footprint, then layer, then directional orientation.";
        }
        else
        {
            hint = "Hint ladder: " + LocalFeedback(lastDiagnostic) + "\nChange only one feature before submitting again.";
        }

        feedbackText.text = hint;
        SetStatus("Hint recorded.");
        Log("hint", missions[missionIndex].Id);
    }

    private void ToggleTargetGhost()
    {
        targetGhostVisible = !targetGhostVisible;
        RenderTargetPreview();
        SetStatus(targetGhostVisible ? "Target ghost visible." : "Target ghost hidden.");
    }

    private void ResetBuild()
    {
        playerBuild.Clear();
        ClearPlacementObjects();
        ClearChildren(diagnosticRoot);
        lastDiagnostic = null;
        currentMcq = null;
        diagnosisText.text = "";
        feedbackText.text = OpeningFeedback();
        mcqText.text = "";
        SetMcqButtons(false);
        nextButton.interactable = false;
        UpdateAllProjectionPanels();
        SetStatus("Build reset. No project files were deleted.");
        Log("reset", missions[missionIndex].Id);
    }

    private void NextMission()
    {
        int next = missionIndex + 1;
        if (conditionMode == ConditionMode.FullSystem)
        {
            next = AdaptiveNextMission();
        }

        StartMission(next);
    }

    private int AdaptiveNextMission()
    {
        int desiredDifficulty = missions[missionIndex].Difficulty;
        if (lastDiagnostic != null && lastDiagnostic.Passed && attempts <= 1 && hintsUsed == 0 && EstimateLoadBand() != LoadBand.Overloaded)
        {
            desiredDifficulty = Mathf.Min(5, desiredDifficulty + 1);
        }
        else if (attempts >= 3 || EstimateLoadBand() == LoadBand.Overloaded)
        {
            desiredDifficulty = Mathf.Max(1, desiredDifficulty - 1);
        }

        for (int offset = 1; offset <= missions.Count; offset++)
        {
            int candidate = (missionIndex + offset) % missions.Count;
            if (missions[candidate].Difficulty == desiredDifficulty)
            {
                return candidate;
            }
        }

        return missionIndex + 1;
    }

    private void SetMentalEffort(int effort)
    {
        mentalEffort = Mathf.Clamp(effort, 1, 3);
        SetStatus("Mental effort saved: " + mentalEffort + "/3.");
        Log("effort", mentalEffort.ToString());
    }

    private void ApplyView(ViewMode view)
    {
        Vector3 target = new Vector3(0f, 0.72f, 0f);
        switch (view)
        {
            case ViewMode.Front:
                mainCamera.transform.position = new Vector3(0f, 2f, -6.8f);
                break;
            case ViewMode.Side:
                mainCamera.transform.position = new Vector3(6.8f, 2f, 0f);
                break;
            case ViewMode.Top:
                mainCamera.transform.position = new Vector3(0.01f, 7.3f, 0.01f);
                target = Vector3.zero;
                break;
            case ViewMode.Robot:
                mainCamera.transform.position = new Vector3(4.8f, 2.1f, 3.5f);
                break;
            default:
                mainCamera.transform.position = new Vector3(4.3f, 4.1f, -5.8f);
                break;
        }

        mainCamera.transform.LookAt(target);
        viewSwitches++;
        Log("view", view.ToString());
    }

    private void UpdateHud()
    {
        if (missions.Count == 0)
        {
            return;
        }

        MissionData mission = missions[missionIndex];
        titleText.text = "AstroBrick Mission";
        loadText.text = "Load " + EstimateLoadBand() + " | effort " + mentalEffort + " | layer " + activeLayer + " | selected " + GetSelectedSpec().Label + " @ " + selectedOrientation + " deg";
        telemetryText.text = mission.Id + " | score " + score + " | solved " + solvedMissions + "/" + missions.Count + "\n" +
                             "attempts " + attempts + " | hints " + hintsUsed + " | MCQ " + mcqCorrect + "/" + Mathf.Max(1, mcqTotal);
        float load = EstimateLoadScore();
        loadFill.fillAmount = load;
        loadFill.color = LoadColor(EstimateLoadBand(load));

        missionText.text =
            mission.Title + "\n" +
            mission.Brief + "\n\n" +
            mission.SkillPhase + "\n" +
            "Target: " + mission.TargetDescription + "\n" +
            "Vocabulary: " + mission.Vocabulary;

        partListText.text = "Required target parts\n" + TargetPartList(mission.Target);
        targetInfoText.text =
            "Representation: " + mission.Representation + "\n" +
            "Rule: basic bricks only. No functional or decorative special parts.\n" +
            "Disclaimer: LEGO-inspired; not affiliated with or endorsed by LEGO Group.";

        UpdateConditionControls();
        UpdateCursor();
    }

    private string OpeningFeedback()
    {
        return "Mission Control: inspect the target, choose a basic brick, place it on the grid, rotate when needed, then submit for geometric diagnosis.";
    }

    private void RenderPlayerBuild()
    {
        ClearPlacementObjects();
        ClearChildren(diagnosticRoot);
        for (int i = 0; i < playerBuild.Count; i++)
        {
            CreatePlacementObject(playerBuild[i], buildRoot, false);
        }
    }

    private void RenderTargetPreview()
    {
        ClearChildren(targetRoot);
        if (!targetGhostVisible)
        {
            return;
        }

        MissionData mission = missions[missionIndex];
        for (int i = 0; i < mission.Target.Count; i++)
        {
            BrickPlacement copy = CopyPlacement(mission.Target[i]);
            copy.Anchor = new Vector3Int(copy.Anchor.x + 9, copy.Anchor.y, copy.Anchor.z);
            CreatePlacementObject(copy, targetRoot, true);
        }
    }

    private GameObject CreatePlacementObject(BrickPlacement placement, Transform parent, bool ghost)
    {
        GameObject root = new GameObject((ghost ? "Ghost " : "Brick ") + placement.Spec.Label);
        root.transform.SetParent(parent, false);
        root.transform.position = GridToWorld(placement.Anchor) + VisualRotationOffset(placement);
        root.transform.rotation = Quaternion.Euler(0f, placement.Orientation, 0f);
        AstroBrickObject marker = root.AddComponent<AstroBrickObject>();
        marker.InstanceId = placement.InstanceId;

        Material material = ghost ? (parent == buildRoot ? missingMaterial : ghostMaterial) : CreateMaterial("Part " + placement.Spec.Id, placement.Spec.Color, false);
        if (placement.Spec.Category == PartCategory.Slope)
        {
            CreateSlopeBody(root.transform, placement.Spec, material);
        }
        else if (placement.Spec.Category == PartCategory.Wedge || placement.Spec.Category == PartCategory.CornerSlope)
        {
            CreateWedgeBody(root.transform, placement.Spec, material);
        }
        else
        {
            CreateBoxBody(root.transform, placement.Spec, material);
        }

        if (placement.Spec.Category != PartCategory.Tile)
        {
            CreateStuds(root.transform, placement.Spec, ghost);
        }

        if (!ghost)
        {
            placementObjects[placement.InstanceId] = root;
        }

        return root;
    }

    private void CreateBoxBody(Transform parent, BrickSpec spec, Material material)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Brick Body";
        body.transform.SetParent(parent, false);
        body.transform.localPosition = new Vector3((spec.Width - 1) * StudSize * 0.5f, spec.HeightUnits * PlateHeight * 0.5f, (spec.Depth - 1) * StudSize * 0.5f);
        body.transform.localScale = new Vector3(spec.Width * StudSize * 0.96f, spec.HeightUnits * PlateHeight, spec.Depth * StudSize * 0.96f);
        body.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void CreateSlopeBody(Transform parent, BrickSpec spec, Material material)
    {
        GameObject meshObject = new GameObject("Slope Body", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
        meshObject.transform.SetParent(parent, false);
        meshObject.transform.localPosition = new Vector3((spec.Width - 1) * StudSize * 0.5f, 0f, (spec.Depth - 1) * StudSize * 0.5f);
        meshObject.GetComponent<MeshFilter>().mesh = SlopeMesh(spec.Width * StudSize * 0.96f, spec.Depth * StudSize * 0.96f, spec.HeightUnits * PlateHeight);
        meshObject.GetComponent<MeshRenderer>().sharedMaterial = material;
        BoxCollider collider = meshObject.GetComponent<BoxCollider>();
        collider.center = new Vector3(0f, spec.HeightUnits * PlateHeight * 0.5f, 0f);
        collider.size = new Vector3(spec.Width * StudSize * 0.96f, spec.HeightUnits * PlateHeight, spec.Depth * StudSize * 0.96f);
    }

    private void CreateWedgeBody(Transform parent, BrickSpec spec, Material material)
    {
        CreateBoxBody(parent, spec, material);
        GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.name = spec.Chirality == "right" ? "Right Wedge Stripe" : "Left Wedge Stripe";
        stripe.transform.SetParent(parent, false);
        float sign = spec.Chirality == "right" ? 1f : -1f;
        stripe.transform.localPosition = new Vector3((spec.Width - 1) * StudSize * 0.5f + sign * 0.16f, spec.HeightUnits * PlateHeight + 0.012f, (spec.Depth - 1) * StudSize * 0.5f);
        stripe.transform.localRotation = Quaternion.Euler(0f, sign > 0f ? 32f : -32f, 0f);
        stripe.transform.localScale = new Vector3(0.08f, 0.03f, spec.Depth * StudSize * 0.9f);
        stripe.GetComponent<Renderer>().sharedMaterial = studMaterial;
        Destroy(stripe.GetComponent<Collider>());
    }

    private Mesh SlopeMesh(float width, float depth, float height)
    {
        float w = width * 0.5f;
        float d = depth * 0.5f;
        float low = Mathf.Max(0.04f, height * 0.24f);
        Vector3[] vertices =
        {
            new Vector3(-w, 0f, -d),
            new Vector3(w, 0f, -d),
            new Vector3(-w, 0f, d),
            new Vector3(w, 0f, d),
            new Vector3(-w, low, -d),
            new Vector3(w, low, -d),
            new Vector3(-w, height, d),
            new Vector3(w, height, d)
        };

        int[] triangles =
        {
            0, 2, 1, 1, 2, 3,
            0, 1, 4, 1, 5, 4,
            2, 6, 3, 3, 6, 7,
            0, 4, 2, 2, 4, 6,
            1, 3, 5, 3, 7, 5,
            4, 5, 6, 5, 7, 6
        };

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private void CreateStuds(Transform parent, BrickSpec spec, bool ghost)
    {
        if (ghost)
        {
            return;
        }

        for (int x = 0; x < spec.Width; x++)
        {
            for (int z = 0; z < spec.Depth; z++)
            {
                GameObject stud = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stud.name = "Stud";
                stud.transform.SetParent(parent, false);
                stud.transform.localPosition = new Vector3(x * StudSize, spec.HeightUnits * PlateHeight + 0.025f, z * StudSize);
                stud.transform.localScale = new Vector3(0.13f, 0.018f, 0.13f);
                stud.GetComponent<Renderer>().sharedMaterial = studMaterial;
                Destroy(stud.GetComponent<Collider>());
            }
        }
    }

    private void UpdateAllProjectionPanels()
    {
        if (topProjection == null || missions.Count == 0)
        {
            return;
        }

        MissionData mission = missions[missionIndex];
        UpdateProjection(topProjection, TopProjection(mission.Target), TopProjection(playerBuild));
        UpdateProjection(frontProjection, FrontProjection(mission.Target), FrontProjection(playerBuild));
        UpdateProjection(sideProjection, SideProjection(mission.Target), SideProjection(playerBuild));
    }

    private void UpdateProjection(ProjectionGrid grid, bool[,] target, bool[,] player)
    {
        for (int row = 0; row < grid.Rows; row++)
        {
            for (int col = 0; col < grid.Cols; col++)
            {
                bool t = target[row, col];
                bool p = player[row, col];
                grid.TargetCells[row, col].color = t ? new Color(0.1f, 0.48f, 0.95f, 1f) : new Color(0.84f, 0.89f, 0.94f, 0.75f);
                if (p && t)
                {
                    grid.PlayerCells[row, col].color = new Color(0.22f, 0.86f, 0.48f, 1f);
                }
                else if (p)
                {
                    grid.PlayerCells[row, col].color = new Color(1f, 0.28f, 0.25f, 1f);
                }
                else if (t)
                {
                    grid.PlayerCells[row, col].color = new Color(1f, 0.82f, 0.16f, 1f);
                }
                else
                {
                    grid.PlayerCells[row, col].color = new Color(0.84f, 0.89f, 0.94f, 0.75f);
                }
            }
        }
    }

    private bool[,] TopProjection(List<BrickPlacement> build)
    {
        bool[,] projection = new bool[GridDepth, GridWidth];
        for (int i = 0; i < build.Count; i++)
        {
            List<Vector2Int> cells = FootprintCells2D(build[i]);
            for (int j = 0; j < cells.Count; j++)
            {
                Vector2Int c = cells[j];
                if (c.x >= 0 && c.x < GridWidth && c.y >= 0 && c.y < GridDepth)
                {
                    projection[c.y, c.x] = true;
                }
            }
        }

        return projection;
    }

    private bool[,] FrontProjection(List<BrickPlacement> build)
    {
        bool[,] projection = new bool[MaxLayerUnits, GridWidth];
        HashSet<Vector3Int> occupied = Occupancy(build, "");
        foreach (Vector3Int c in occupied)
        {
            if (c.x >= 0 && c.x < GridWidth && c.y >= 0 && c.y < MaxLayerUnits)
            {
                projection[MaxLayerUnits - 1 - c.y, c.x] = true;
            }
        }

        return projection;
    }

    private bool[,] SideProjection(List<BrickPlacement> build)
    {
        bool[,] projection = new bool[MaxLayerUnits, GridDepth];
        HashSet<Vector3Int> occupied = Occupancy(build, "");
        foreach (Vector3Int c in occupied)
        {
            if (c.z >= 0 && c.z < GridDepth && c.y >= 0 && c.y < MaxLayerUnits)
            {
                projection[MaxLayerUnits - 1 - c.y, c.z] = true;
            }
        }

        return projection;
    }

    private HashSet<Vector3Int> Occupancy(List<BrickPlacement> build, string ignoreInstanceId)
    {
        HashSet<Vector3Int> cells = new HashSet<Vector3Int>();
        for (int i = 0; i < build.Count; i++)
        {
            if (!string.IsNullOrEmpty(ignoreInstanceId) && build[i].InstanceId == ignoreInstanceId)
            {
                continue;
            }

            List<Vector3Int> occupied = OccupiedCells(build[i]);
            for (int j = 0; j < occupied.Count; j++)
            {
                cells.Add(occupied[j]);
            }
        }

        return cells;
    }

    private List<Vector3Int> OccupiedCells(BrickPlacement placement)
    {
        List<Vector3Int> cells = new List<Vector3Int>();
        List<Vector2Int> footprint = FootprintCells2D(placement);
        for (int i = 0; i < footprint.Count; i++)
        {
            for (int y = 0; y < placement.Spec.HeightUnits; y++)
            {
                cells.Add(new Vector3Int(footprint[i].x, placement.Anchor.y + y, footprint[i].y));
            }
        }

        return cells;
    }

    private List<Vector2Int> FootprintCells2D(BrickPlacement placement)
    {
        int width = placement.Spec.Width;
        int depth = placement.Spec.Depth;
        if (placement.Orientation == 90 || placement.Orientation == 270)
        {
            int temp = width;
            width = depth;
            depth = temp;
        }

        List<Vector2Int> cells = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                cells.Add(new Vector2Int(placement.Anchor.x + x, placement.Anchor.z + z));
            }
        }

        return cells;
    }

    private void UpdateCursor()
    {
        if (cursorRoot == null)
        {
            return;
        }

        BrickSpec spec = GetSelectedSpec();
        Vector3Int anchor = hoverValid ? new Vector3Int(hoverCell.x, activeLayer, hoverCell.z) : new Vector3Int(0, activeLayer, 0);
        BrickPlacement test = new BrickPlacement { InstanceId = "cursor", Spec = spec, Anchor = anchor, Orientation = selectedOrientation };
        cursorRoot.position = GridToWorld(anchor) + VisualRotationOffset(test);
        cursorRoot.rotation = Quaternion.Euler(0f, selectedOrientation, 0f);
        cursorRoot.localScale = new Vector3(spec.Width, Mathf.Max(1, spec.HeightUnits), spec.Depth);
        if (cursorRenderer != null)
        {
            cursorRenderer.sharedMaterial = CanPlace(test, "") ? cursorMaterial : invalidCursorMaterial;
        }
    }

    private Vector3 VisualRotationOffset(BrickPlacement placement)
    {
        float width = (placement.Spec.Width - 1) * StudSize;
        float depth = (placement.Spec.Depth - 1) * StudSize;
        switch (placement.Orientation)
        {
            case 90:
                return new Vector3(0f, 0f, width);
            case 180:
                return new Vector3(width, 0f, depth);
            case 270:
                return new Vector3(depth, 0f, 0f);
            default:
                return Vector3.zero;
        }
    }

    private void AnimateCharacters()
    {
        if (hoverValid)
        {
            Vector3 target = GridToWorld(hoverCell) + new Vector3(-0.72f, 0.05f, -0.56f);
            astronautRoot.position = Vector3.Lerp(astronautRoot.position, target, Time.deltaTime * 4f);
            astronautRoot.rotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 2f) * 8f, 0f);
        }

        if (noviRoot != null)
        {
            noviRoot.position = new Vector3(3.15f, 0.06f + Mathf.Sin(Time.time * 2.3f) * 0.04f, 2.6f);
            noviRoot.rotation = Quaternion.Euler(0f, -28f + Mathf.Sin(Time.time * 1.6f) * 4f, 0f);
        }
    }

    private BrickPlacement FindPlayerPlacement(string instanceId)
    {
        for (int i = 0; i < playerBuild.Count; i++)
        {
            if (playerBuild[i].InstanceId == instanceId)
            {
                return playerBuild[i];
            }
        }

        return null;
    }

    private BrickSpec GetSelectedSpec()
    {
        return PartByIndex(selectedPartIndex);
    }

    private BrickSpec PartByIndex(int index)
    {
        int i = 0;
        foreach (KeyValuePair<string, BrickSpec> entry in partLibrary)
        {
            if (i == index)
            {
                return entry.Value;
            }

            i++;
        }

        foreach (KeyValuePair<string, BrickSpec> entry in partLibrary)
        {
            return entry.Value;
        }

        return null;
    }

    private int NormalizeOrientation(int orientation)
    {
        int value = orientation % 360;
        if (value < 0)
        {
            value += 360;
        }

        return (value / 90) * 90;
    }

    private Vector3 GridToWorld(Vector3Int cell)
    {
        float x = (cell.x - (GridWidth - 1) * 0.5f) * StudSize;
        float y = cell.y * PlateHeight;
        float z = (cell.z - (GridDepth - 1) * 0.5f) * StudSize;
        return new Vector3(x, y, z);
    }

    private BrickPlacement CopyPlacement(BrickPlacement source)
    {
        return new BrickPlacement
        {
            InstanceId = source.InstanceId,
            Spec = source.Spec,
            Anchor = source.Anchor,
            Orientation = source.Orientation
        };
    }

    private string TargetPartList(List<BrickPlacement> placements)
    {
        if (placements.Count == 0)
        {
            return "none";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < placements.Count; i++)
        {
            BrickPlacement p = placements[i];
            builder.Append("- ").Append(p.Spec.Label)
                .Append(" @ x").Append(p.Anchor.x)
                .Append(" y").Append(p.Anchor.y)
                .Append(" z").Append(p.Anchor.z)
                .Append(" rot ").Append(p.Orientation);
            if (p.Spec.Chiral)
            {
                builder.Append(" ").Append(p.Spec.Chirality);
            }

            if (i < placements.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private string PartSummary(BrickPlacement p)
    {
        if (p == null)
        {
            return "none";
        }

        return p.Spec.Label + " at x" + p.Anchor.x + " layer " + p.Anchor.y + " z" + p.Anchor.z + " rot " + p.Orientation;
    }

    private string ErrorLabel(ErrorType error)
    {
        switch (error)
        {
            case ErrorType.None:
                return "none";
            case ErrorType.WrongPart:
                return "wrong_part";
            case ErrorType.WrongFootprint:
                return "wrong_footprint";
            case ErrorType.WrongPosition:
                return "wrong_position";
            case ErrorType.WrongLayer:
                return "wrong_layer";
            case ErrorType.WrongOrientation:
                return "wrong_orientation";
            case ErrorType.MirrorError:
                return "mirror_error";
            case ErrorType.SupportError:
                return "support_error";
            case ErrorType.MissingElement:
                return "missing_element";
            case ErrorType.ExtraElement:
                return "extra_element";
            case ErrorType.ReferenceFrameError:
                return "reference_frame_error";
            default:
                return "unknown";
        }
    }

    private bool ContainsAny(string text, params string[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (text.Contains(values[i]))
            {
                return true;
            }
        }

        return false;
    }

    private float EstimateLoadScore()
    {
        float elapsed = Time.time - missionStartTime;
        float value = 0.1f;
        value += mentalEffort == 3 ? 0.34f : mentalEffort == 2 ? 0.17f : 0.05f;
        value += Mathf.Clamp01(elapsed / 210f) * 0.14f;
        value += Mathf.Clamp01(attempts / 4f) * 0.16f;
        value += Mathf.Clamp01(hintsUsed / 3f) * 0.13f;
        value += Mathf.Clamp01(viewSwitches / 16f) * 0.11f;
        value += Mathf.Clamp01(actionsThisMission / 38f) * 0.16f;
        return Mathf.Clamp01(value);
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

    private Color LoadColor(LoadBand band)
    {
        switch (band)
        {
            case LoadBand.Calm:
                return new Color(0.12f, 0.72f, 1f, 1f);
            case LoadBand.Focused:
                return new Color(0.24f, 0.86f, 0.5f, 1f);
            case LoadBand.Strained:
                return new Color(1f, 0.65f, 0.22f, 1f);
            default:
                return new Color(1f, 0.24f, 0.26f, 1f);
        }
    }

    private void SetMcqButtons(bool active)
    {
        for (int i = 0; i < mcqButtons.Count; i++)
        {
            mcqButtons[i].interactable = active;
        }
    }

    private string GetModel()
    {
        if (modelInput == null || string.IsNullOrWhiteSpace(modelInput.text))
        {
            return DefaultModel;
        }

        return modelInput.text.Trim();
    }

    private string JsonEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
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

    private string ExtractOutputText(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return "";
        }

        int marker = json.IndexOf("\"output_text\"", StringComparison.Ordinal);
        int textKey = json.IndexOf("\"text\"", marker >= 0 ? marker : 0, StringComparison.Ordinal);
        if (textKey < 0)
        {
            return "";
        }

        int colon = json.IndexOf(':', textKey);
        int quote = json.IndexOf('"', colon + 1);
        if (colon < 0 || quote < 0)
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
                if (c == 'n')
                {
                    builder.Append('\n');
                }
                else if (c == 'r')
                {
                    builder.Append('\r');
                }
                else if (c == 't')
                {
                    builder.Append('\t');
                }
                else
                {
                    builder.Append(c);
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

    private ProjectionGrid Projection(Transform parent, string title, int rows, int cols)
    {
        GameObject root = Panel(parent, title + " Panel", new Color(0.94f, 0.97f, 1f, 0.94f));
        Layout(root, 0f, rows > 8 ? 118f : 104f, 1f, 0f);
        ProjectionGrid grid = new ProjectionGrid
        {
            Header = AbsoluteText(root.transform, title, title + "  target | yours", 13, TextAnchor.UpperLeft, new Color(0.08f, 0.14f, 0.24f, 1f), 12f, 8f, 320f, 18f),
            TargetCells = new Image[rows, cols],
            PlayerCells = new Image[rows, cols],
            Rows = rows,
            Cols = cols
        };

        MakeProjectionCells(root.transform, grid.TargetCells, rows, cols, 14f, 32f, 10f);
        MakeProjectionCells(root.transform, grid.PlayerCells, rows, cols, 184f, 32f, 10f);
        return grid;
    }

    private void MakeProjectionCells(Transform parent, Image[,] cells, int rows, int cols, float x0, float y0, float size)
    {
        float gap = 3f;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                GameObject cell = Panel(parent, "Projection Cell", new Color(0.84f, 0.89f, 0.94f, 0.75f));
                Rect(cell.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x0 + col * (size + gap), -(y0 + row * (size + gap))), new Vector2(size, size));
                cells[row, col] = cell.GetComponent<Image>();
            }
        }
    }

    private GameObject Panel(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = color;
        return obj;
    }

    private Text TextUi(Transform parent, string name, string value, int size, TextAnchor anchor, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        obj.transform.SetParent(parent, false);
        Text text = obj.GetComponent<Text>();
        text.text = value;
        text.font = GetFont();
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(9, size - 4);
        text.resizeTextMaxSize = size;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Text AbsoluteText(Transform parent, string name, string value, int size, TextAnchor anchor, Color color, float x, float y, float width, float height)
    {
        Text text = TextUi(parent, name, value, size, anchor, color);
        Rect(text.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));
        return text;
    }

    private Button ButtonUi(Transform parent, string label, float width, float height, UnityEngine.Events.UnityAction action)
    {
        GameObject obj = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = new Color(0.16f, 0.24f, 0.34f, 1f);
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.16f, 0.24f, 0.34f, 1f);
        colors.highlightedColor = new Color(0.28f, 0.42f, 0.56f, 1f);
        colors.pressedColor = new Color(0.1f, 0.58f, 0.95f, 1f);
        colors.disabledColor = new Color(0.22f, 0.24f, 0.28f, 0.44f);
        button.colors = colors;

        Text labelText = TextUi(obj.transform, label + " Label", label, 12, TextAnchor.MiddleCenter, Color.white);
        Rect(labelText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Layout(obj, width, height, 0f, 0f);
        return button;
    }

    private InputField InputUi(Transform parent, string name, string placeholder, float width, float height, bool password)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(0.98f, 1f, 1f, 0.96f);
        Layout(obj, width, height, width <= 0f ? 1f : 0f, 0f);
        Text text = TextUi(obj.transform, "Text", "", 12, TextAnchor.MiddleLeft, new Color(0.05f, 0.09f, 0.14f, 1f));
        text.raycastTarget = true;
        Rect(text.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(8f, 0f), new Vector2(-16f, -8f));
        Text hint = TextUi(obj.transform, "Placeholder", placeholder, 12, TextAnchor.MiddleLeft, new Color(0.42f, 0.5f, 0.6f, 0.84f));
        Rect(hint.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(8f, 0f), new Vector2(-16f, -8f));
        InputField input = obj.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = hint;
        input.lineType = InputField.LineType.SingleLine;
        if (password)
        {
            input.contentType = InputField.ContentType.Password;
        }

        return input;
    }

    private GameObject LayoutGroup(Transform parent, string name, bool horizontal, int spacing, int padding, TextAnchor anchor)
    {
        GameObject group = new GameObject(name, typeof(RectTransform));
        group.transform.SetParent(parent, false);
        if (horizontal)
        {
            Horizontal(group, padding, spacing, anchor, false);
        }
        else
        {
            Vertical(group, padding, spacing, anchor);
        }

        return group;
    }

    private void Horizontal(GameObject obj, int padding, int spacing, TextAnchor anchor, bool expandWidth)
    {
        HorizontalLayoutGroup group = obj.AddComponent<HorizontalLayoutGroup>();
        group.padding = new RectOffset(padding, padding, padding, padding);
        group.spacing = spacing;
        group.childAlignment = anchor;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = expandWidth;
        group.childForceExpandHeight = false;
    }

    private void Vertical(GameObject obj, int padding, int spacing, TextAnchor anchor)
    {
        VerticalLayoutGroup group = obj.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(padding, padding, padding, padding);
        group.spacing = spacing;
        group.childAlignment = anchor;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
    }

    private void Layout(GameObject obj, float width, float height, float flexWidth, float flexHeight)
    {
        LayoutElement element = obj.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = obj.AddComponent<LayoutElement>();
        }

        if (width > 0f)
        {
            element.preferredWidth = width;
        }

        if (height > 0f)
        {
            element.preferredHeight = height;
        }

        element.flexibleWidth = flexWidth;
        element.flexibleHeight = flexHeight;
    }

    private void Rect(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private Font GetFont()
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

    private void CreateLight(string name, LightType type, Vector3 position, Quaternion rotation, float intensity, bool shadows)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.position = position;
        lightObject.transform.rotation = rotation;
        Light light = lightObject.AddComponent<Light>();
        light.type = type;
        light.intensity = intensity;
        light.range = 9f;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
    }

    private void ApplyMaterialRecursive(Transform root, Material material)
    {
        Renderer renderer = root.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            ApplyMaterialRecursive(root.GetChild(i), material);
        }
    }

    private void ClearPlacementObjects()
    {
        foreach (KeyValuePair<string, GameObject> entry in placementObjects)
        {
            if (entry.Value != null)
            {
                Destroy(entry.Value);
            }
        }

        placementObjects.Clear();
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

    private void Log(string action, string detail)
    {
        if (missions.Count == 0)
        {
            return;
        }

        SessionEvent evt = new SessionEvent
        {
            Time = Time.time,
            MissionId = missions[Mathf.Clamp(missionIndex, 0, missions.Count - 1)].Id,
            Action = action,
            Detail = detail
        };
        sessionEvents.Add(evt);
        Debug.Log("[AstroBrick] " + evt.MissionId + " | " + action + " | " + detail);
    }
}

public sealed class AstroBrickObject : MonoBehaviour
{
    public string InstanceId;
}

public sealed class AstroBrickCell : MonoBehaviour
{
    public int X;
    public int Z;
}

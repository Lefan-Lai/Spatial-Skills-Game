using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class LittleShapeEngineerGame : MonoBehaviour
{
    private static readonly bool UseCanvasUi = false;
    private const int GridMinX = -5;
    private const int GridMaxX = 5;
    private const int GridMinZ = -3;
    private const int GridMaxZ = 3;
    private const float CellSize = 0.72f;
    private const float LayerHeight = 0.54f;
    private const float MinCameraDistance = 7.2f;
    private const float MaxCameraDistance = 13.5f;

    private enum ShapeKind
    {
        Cube,
        RectangularPrism,
        Plate,
        Ramp,
        TriangularPrism,
        Cylinder
    }

    private enum TaskKind
    {
        Blueprint,
        Functional,
        Repair,
        Memory,
        Viewpoint,
        Challenge
    }

    private sealed class ShapeDefinition
    {
        public ShapeKind Kind;
        public string Name;
        public string ShortName;
        public Color Color;
        public Vector3 Size;
        public bool Directional;
    }

    private sealed class TargetShape
    {
        public ShapeKind Kind;
        public Vector3Int Cell;
        public int Rotation;
    }

    private sealed class PlacedShape
    {
        public string Id;
        public ShapeKind Kind;
        public Vector3Int Cell;
        public int Rotation;
        public GameObject View;
    }

    private sealed class LevelData
    {
        public int Number;
        public string World;
        public string Title;
        public string Goal;
        public string Hint;
        public TaskKind Task;
        public int Difficulty;
        public readonly List<TargetShape> Target = new List<TargetShape>();
        public readonly List<TargetShape> StartingShapes = new List<TargetShape>();
        public readonly Dictionary<ShapeKind, int> Inventory = new Dictionary<ShapeKind, int>();
    }

    private sealed class ShapeSnapshot
    {
        public string Id;
        public ShapeKind Kind;
        public Vector3Int Cell;
        public int Rotation;
    }

    private sealed class SessionEvent
    {
        public float Time;
        public int Level;
        public string Action;
        public string Detail;
    }

    private sealed class ShapeView : MonoBehaviour
    {
        public string Id;
    }

    private readonly Dictionary<ShapeKind, ShapeDefinition> shapes = new Dictionary<ShapeKind, ShapeDefinition>();
    private readonly List<LevelData> levels = new List<LevelData>();
    private readonly List<PlacedShape> placedShapes = new List<PlacedShape>();
    private readonly List<GameObject> ghostObjects = new List<GameObject>();
    private readonly List<SessionEvent> sessionEvents = new List<SessionEvent>();
    private readonly Stack<List<ShapeSnapshot>> undoStack = new Stack<List<ShapeSnapshot>>();
    private readonly Stack<List<ShapeSnapshot>> redoStack = new Stack<List<ShapeSnapshot>>();
    private readonly Dictionary<ShapeKind, int> remainingInventory = new Dictionary<ShapeKind, int>();
    private readonly Dictionary<ShapeKind, Image> paletteCards = new Dictionary<ShapeKind, Image>();
    private readonly Dictionary<ShapeKind, Text> paletteCounts = new Dictionary<ShapeKind, Text>();

    private Camera mainCamera;
    private Font uiFont;
    private Sprite roundedSprite;
    private Sprite softRoundedSprite;

    private Transform worldRoot;
    private Transform islandRoot;
    private Transform buildRoot;
    private Transform ghostRoot;
    private Transform cursorRoot;
    private Transform robotRoot;
    private Transform lighthouseLight;

    private Material grassMaterial;
    private Material dirtMaterial;
    private Material waterMaterial;
    private Material gridMaterial;
    private Material whiteMaterial;
    private Material ghostMaterial;
    private Material validCursorMaterial;
    private Material invalidCursorMaterial;

    private Canvas canvas;
    private Text titleText;
    private Text worldText;
    private Text taskText;
    private Text goalText;
    private Text robotHintText;
    private Text progressText;
    private Text feedbackText;
    private Text blueprintTitleText;
    private Text blueprintTypeText;
    private Text starText;
    private Button undoButton;
    private Button redoButton;
    private Button nextButton;
    private RectTransform topProjectionGrid;
    private RectTransform frontProjectionGrid;
    private RectTransform sideProjectionGrid;

    private int levelIndex;
    private ShapeKind selectedKind = ShapeKind.Cube;
    private int selectedRotation;
    private bool draggingFromPalette;
    private bool draggingExisting;
    private PlacedShape draggedShape;
    private Vector3Int dragStartCell;
    private int dragStartRotation;
    private bool dragCandidateValid;
    private Vector3Int currentCursorCell;
    private bool currentCursorValid;
    private GameObject cursorView;
    private ShapeKind cursorKind;
    private int cursorRotation = -1;

    private float cameraYaw = 43f;
    private float cameraPitch = 43f;
    private float cameraDistance = 10.6f;
    private Vector3 cameraTarget = new Vector3(0f, 0.75f, 0f);
    private bool orbitingCamera;
    private Vector3 orbitStartMouse;
    private bool blueprintVisible = true;
    private int testAttempts;
    private int hintCount;
    private Coroutine memoryHideRoutine;
    private Coroutine robotWalkRoutine;
    private string hudFeedbackText = "拖动形状到发光网格里。";
    private string hudRobotHintText = "";
    private string hudStarText = "★ ☆ ☆";
    private readonly List<Rect> immediateHudRects = new List<Rect>();
    private readonly Dictionary<string, Texture2D> immediateTextures = new Dictionary<string, Texture2D>();
    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle smallStyle;
    private GUIStyle buttonStyle;
    private GUIStyle testButtonStyle;
    private GUIStyle badgeStyle;
    private GUIStyle paletteStyle;
    private GUIStyle selectedPaletteStyle;
    private float hudScale = 1f;
    private Vector2 hudOffset;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        SetupCameraAndLight();
        SetupMaterials();
        BuildShapeLibrary();
        BuildLevels();
        BuildWorld();
        if (UseCanvasUi)
        {
            BuildUi();
        }

        LoadLevel(0);
    }

    private void Update()
    {
        HandleCameraOrbit();
        HandleKeyboardShortcuts();
        HandleBuildInput();
        AnimateWorld();
    }

    private void OnGUI()
    {
        if (UseCanvasUi)
        {
            return;
        }

        PrepareImmediateHud();
        UpdateImmediateHudRects();

        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(hudOffset, Quaternion.identity, new Vector3(hudScale, hudScale, 1f));
        GUI.depth = 0;

        HandleImmediatePaletteEvents();
        DrawImmediateTopBar();
        DrawImmediateMissionCard();
        DrawImmediateBlueprintCard();
        DrawImmediatePalette();

        GUI.matrix = oldMatrix;
    }

    private void PrepareImmediateHud()
    {
        hudScale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        hudScale = Mathf.Max(0.1f, hudScale);
        hudOffset = new Vector2((Screen.width - 1920f * hudScale) * 0.5f, (Screen.height - 1080f * hudScale) * 0.5f);

        if (panelStyle != null)
        {
            return;
        }

        panelStyle = MakeGuiStyle(new Color(1f, 0.97f, 0.9f, 0.96f), TextAnchor.UpperLeft, 24, FontStyle.Normal, new Color(0.12f, 0.15f, 0.2f));
        titleStyle = MakeGuiStyle(Color.clear, TextAnchor.MiddleLeft, 36, FontStyle.Bold, new Color(0.1f, 0.14f, 0.2f));
        bodyStyle = MakeGuiStyle(Color.clear, TextAnchor.UpperLeft, 25, FontStyle.Normal, new Color(0.08f, 0.1f, 0.14f));
        smallStyle = MakeGuiStyle(Color.clear, TextAnchor.MiddleLeft, 21, FontStyle.Bold, new Color(0.2f, 0.43f, 0.82f));
        buttonStyle = MakeGuiStyle(new Color(1f, 1f, 1f, 0.94f), TextAnchor.MiddleCenter, 24, FontStyle.Bold, new Color(0.16f, 0.28f, 0.43f));
        testButtonStyle = MakeGuiStyle(new Color(0.82f, 1f, 0.48f, 0.98f), TextAnchor.MiddleCenter, 28, FontStyle.Bold, new Color(0.18f, 0.5f, 0.06f));
        badgeStyle = MakeGuiStyle(new Color(0.13f, 0.48f, 0.9f, 0.96f), TextAnchor.MiddleCenter, 23, FontStyle.Bold, Color.white);
        paletteStyle = MakeGuiStyle(new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter, 20, FontStyle.Bold, new Color(0.1f, 0.13f, 0.18f));
        selectedPaletteStyle = MakeGuiStyle(new Color(0.84f, 0.94f, 1f, 0.98f), TextAnchor.MiddleCenter, 20, FontStyle.Bold, new Color(0.05f, 0.22f, 0.44f));
    }

    private GUIStyle MakeGuiStyle(Color background, TextAnchor alignment, int fontSize, FontStyle fontStyle, Color textColor)
    {
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.normal.background = background.a <= 0.01f ? null : GetImmediateTexture(background);
        style.hover.background = style.normal.background;
        style.active.background = style.normal.background;
        style.normal.textColor = textColor;
        style.hover.textColor = textColor;
        style.active.textColor = textColor;
        style.alignment = alignment;
        style.fontSize = fontSize;
        style.fontStyle = fontStyle;
        style.wordWrap = true;
        style.padding = new RectOffset(12, 12, 8, 8);
        return style;
    }

    private Texture2D GetImmediateTexture(Color color)
    {
        string key = ColorUtility.ToHtmlStringRGBA(color);
        Texture2D texture;
        if (immediateTextures.TryGetValue(key, out texture))
        {
            return texture;
        }

        texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        immediateTextures[key] = texture;
        return texture;
    }

    private void UpdateImmediateHudRects()
    {
        immediateHudRects.Clear();
        immediateHudRects.Add(new Rect(18f, 20f, 230f, 108f));
        immediateHudRects.Add(new Rect(590f, 18f, 740f, 106f));
        immediateHudRects.Add(new Rect(1690f, 20f, 205f, 108f));
        immediateHudRects.Add(new Rect(24f, 150f, 326f, 744f));
        immediateHudRects.Add(new Rect(1530f, 150f, 366f, 744f));
        immediateHudRects.Add(new Rect(360f, 844f, 1200f, 206f));
    }

    private void DrawImmediateTopBar()
    {
        if (GUI.Button(new Rect(20f, 24f, 100f, 82f), "返回", buttonStyle))
        {
            PreviousLevel();
        }

        if (GUI.Button(new Rect(136f, 24f, 100f, 82f), "主页", buttonStyle))
        {
            LoadLevel(0);
        }

        DrawPanel(new Rect(590f, 18f, 740f, 98f), new Color(1f, 1f, 1f, 0.92f));
        GUI.enabled = undoStack.Count > 0;
        if (GUI.Button(new Rect(615f, 32f, 132f, 70f), "撤销", buttonStyle))
        {
            Undo();
        }

        GUI.enabled = redoStack.Count > 0;
        if (GUI.Button(new Rect(765f, 32f, 132f, 70f), "重做", buttonStyle))
        {
            Redo();
        }

        GUI.enabled = true;
        if (GUI.Button(new Rect(915f, 32f, 150f, 70f), "旋转", buttonStyle))
        {
            RotateSelected();
        }

        if (GUI.Button(new Rect(1085f, 28f, 215f, 78f), "测试", testButtonStyle))
        {
            RunTest();
        }

        if (GUI.Button(new Rect(1710f, 24f, 170f, 82f), "下一关", buttonStyle))
        {
            NextLevel();
        }
    }

    private void DrawImmediateMissionCard()
    {
        LevelData level = CurrentLevel;
        Rect panel = new Rect(24f, 150f, 326f, 744f);
        DrawPanel(panel, new Color(1f, 0.97f, 0.9f, 0.96f));

        GUI.Label(new Rect(50f, 174f, 260f, 34f), level.World, smallStyle);
        GUI.Label(new Rect(50f, 210f, 260f, 54f), level.Title, titleStyle);
        GUI.Label(new Rect(50f, 272f, 230f, 36f), LevelTaskLabel(level), smallStyle);
        GUI.Label(new Rect(50f, 330f, 254f, 150f), level.Goal, bodyStyle);

        Rect robot = new Rect(44f, 506f, 286f, 176f);
        DrawPanel(robot, new Color(0.85f, 0.95f, 1f, 0.9f));
        DrawRobotFace(new Rect(62f, 548f, 88f, 88f));
        string hint = string.IsNullOrEmpty(hudRobotHintText) ? level.Hint : hudRobotHintText;
        GUI.Label(new Rect(162f, 532f, 142f, 112f), hint, bodyStyle);

        Rect progress = new Rect(44f, 704f, 286f, 150f);
        DrawPanel(progress, new Color(1f, 1f, 1f, 0.86f));
        GUI.Label(new Rect(62f, 724f, 220f, 34f), "进度  第 " + level.Number + " / " + levels.Count + " 关", smallStyle);
        GUI.Label(new Rect(62f, 776f, 236f, 42f), hudStarText, MakeGuiStyle(Color.clear, TextAnchor.MiddleLeft, 40, FontStyle.Bold, new Color(1f, 0.68f, 0.1f)));
    }

    private void DrawRobotFace(Rect rect)
    {
        DrawPanel(rect, new Color(0.96f, 0.99f, 1f, 1f));
        DrawSolid(new Rect(rect.x + 18f, rect.y + 28f, rect.width - 36f, 30f), new Color(0.03f, 0.12f, 0.17f, 1f));
        DrawSolid(new Rect(rect.x + 32f, rect.y + 38f, 10f, 10f), new Color(0.25f, 1f, 1f, 1f));
        DrawSolid(new Rect(rect.x + rect.width - 42f, rect.y + 38f, 10f, 10f), new Color(0.25f, 1f, 1f, 1f));
        DrawSolid(new Rect(rect.x + 40f, rect.y - 10f, 8f, 22f), new Color(0.18f, 0.58f, 0.95f, 1f));
        DrawSolid(new Rect(rect.x + 34f, rect.y - 18f, 20f, 20f), new Color(0.25f, 1f, 1f, 1f));
    }

    private void DrawImmediateBlueprintCard()
    {
        LevelData level = CurrentLevel;
        Rect panel = new Rect(1530f, 150f, 366f, 744f);
        DrawPanel(panel, new Color(0.94f, 0.99f, 1f, 0.96f));
        DrawSolid(new Rect(1530f, 150f, 366f, 72f), new Color(0.1f, 0.47f, 0.9f, 1f));
        GUI.Label(new Rect(1530f, 158f, 366f, 52f), "图纸", MakeGuiStyle(Color.clear, TextAnchor.MiddleCenter, 34, FontStyle.Bold, Color.white));
        GUI.Label(new Rect(1562f, 246f, 280f, 34f), BlueprintLabel(level), smallStyle);

        DrawProjectionImmediate(new Rect(1560f, 302f, 306f, 150f), "俯视图", 0);
        DrawProjectionImmediate(new Rect(1560f, 486f, 306f, 150f), "正面图", 1);
        DrawProjectionImmediate(new Rect(1560f, 670f, 306f, 150f), "侧面图", 2);
    }

    private void DrawProjectionImmediate(Rect rect, string label, int mode)
    {
        DrawPanel(rect, new Color(1f, 1f, 1f, 0.86f));
        GUI.Label(new Rect(rect.x + 16f, rect.y + 8f, 200f, 30f), label, smallStyle);

        Rect grid = new Rect(rect.x + 18f, rect.y + 48f, rect.width - 36f, rect.height - 64f);
        DrawSolid(grid, new Color(0.9f, 0.96f, 1f, 0.55f));
        int cols = 9;
        int rows = 5;
        float cw = grid.width / cols;
        float ch = grid.height / rows;
        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                DrawSolid(new Rect(grid.x + x * cw + 1f, grid.y + y * ch + 1f, cw - 2f, ch - 2f), new Color(1f, 1f, 1f, 0.3f));
            }
        }

        if (!blueprintVisible)
        {
            GUI.Label(grid, "?", MakeGuiStyle(Color.clear, TextAnchor.MiddleCenter, 44, FontStyle.Bold, new Color(0.2f, 0.45f, 0.8f)));
            return;
        }

        for (int i = 0; i < CurrentLevel.Target.Count; i++)
        {
            TargetShape target = CurrentLevel.Target[i];
            int col;
            int row;
            if (mode == 0)
            {
                col = target.Cell.x + 4;
                row = target.Cell.z + 2;
            }
            else if (mode == 1)
            {
                col = target.Cell.x + 4;
                row = target.Cell.y;
            }
            else
            {
                col = target.Cell.z + 4;
                row = target.Cell.y;
            }

            if (col < 0 || col >= cols || row < 0 || row >= rows)
            {
                continue;
            }

            Color mark = shapes[target.Kind].Color;
            mark.a = 0.95f;
            DrawSolid(new Rect(grid.x + col * cw + 4f, grid.y + (rows - 1 - row) * ch + 4f, cw - 8f, ch - 8f), mark);
        }
    }

    private void DrawImmediatePalette()
    {
        DrawPanel(new Rect(360f, 844f, 1200f, 206f), new Color(1f, 0.98f, 0.92f, 0.96f));
        DrawSolid(new Rect(450f, 792f, 1020f, 46f), new Color(0.1f, 0.47f, 0.9f, 0.88f));
        GUI.Label(new Rect(450f, 792f, 1020f, 46f), hudFeedbackText, MakeGuiStyle(Color.clear, TextAnchor.MiddleCenter, 26, FontStyle.Bold, Color.white));

        ShapeKind[] order = new ShapeKind[]
        {
            ShapeKind.Cube,
            ShapeKind.RectangularPrism,
            ShapeKind.Plate,
            ShapeKind.Ramp,
            ShapeKind.TriangularPrism,
            ShapeKind.Cylinder
        };

        for (int i = 0; i < order.Length; i++)
        {
            ShapeKind kind = order[i];
            Rect card = PaletteCardRect(i);
            GUIStyle style = kind == selectedKind ? selectedPaletteStyle : paletteStyle;
            GUI.Box(card, GUIContent.none, style);
            DrawShapeBadge(new Rect(card.x + 40f, card.y + 18f, 100f, 54f), kind);
            GUI.Label(new Rect(card.x + 10f, card.y + 78f, card.width - 20f, 28f), shapes[kind].Name, style);
            GUI.Label(new Rect(card.x + card.width * 0.5f - 28f, card.y + 112f, 56f, 30f), GetRemaining(kind).ToString(), badgeStyle);
        }
    }

    private Rect PaletteCardRect(int index)
    {
        return new Rect(384f + index * 190f, 870f, 168f, 156f);
    }

    private void DrawShapeBadge(Rect rect, ShapeKind kind)
    {
        Color color = shapes[kind].Color;
        if (kind == ShapeKind.RectangularPrism)
        {
            DrawSolid(new Rect(rect.x + 2f, rect.y + 16f, rect.width - 4f, 30f), color);
        }
        else if (kind == ShapeKind.Plate)
        {
            DrawSolid(new Rect(rect.x + 8f, rect.y + 26f, rect.width - 16f, 18f), color);
        }
        else if (kind == ShapeKind.Ramp)
        {
            DrawSolid(new Rect(rect.x + 14f, rect.y + 30f, rect.width - 28f, 18f), color);
            GUI.Label(new Rect(rect.x, rect.y - 2f, rect.width, rect.height), "▲", MakeGuiStyle(Color.clear, TextAnchor.MiddleCenter, 52, FontStyle.Bold, color));
        }
        else if (kind == ShapeKind.TriangularPrism)
        {
            GUI.Label(rect, "▲", MakeGuiStyle(Color.clear, TextAnchor.MiddleCenter, 56, FontStyle.Bold, color));
        }
        else if (kind == ShapeKind.Cylinder)
        {
            DrawSolid(new Rect(rect.x + 30f, rect.y + 10f, rect.width - 60f, rect.height - 20f), color);
            DrawSolid(new Rect(rect.x + 22f, rect.y + 8f, rect.width - 44f, 14f), Color.Lerp(color, Color.white, 0.25f));
        }
        else
        {
            DrawSolid(new Rect(rect.x + 24f, rect.y + 10f, rect.width - 48f, rect.height - 20f), color);
        }
    }

    private void HandleImmediatePaletteEvents()
    {
        Event current = Event.current;
        Vector2 mouse = MouseDesignPosition();

        if (current.type == EventType.MouseDown && current.button == 0)
        {
            for (int i = 0; i < 6; i++)
            {
                Rect card = PaletteCardRect(i);
                if (!card.Contains(mouse))
                {
                    continue;
                }

                ShapeKind kind = (ShapeKind)i;
                BeginPaletteDrag(kind);
                current.Use();
                return;
            }
        }

        if (current.type == EventType.MouseUp && current.button == 0 && draggingFromPalette)
        {
            EndPaletteDrag();
            current.Use();
        }
    }

    private void DrawPanel(Rect rect, Color color)
    {
        DrawSolid(new Rect(rect.x + 5f, rect.y + 6f, rect.width, rect.height), new Color(0.06f, 0.12f, 0.18f, 0.18f));
        DrawSolid(rect, color);
        DrawSolid(new Rect(rect.x, rect.y, rect.width, 3f), new Color(1f, 1f, 1f, 0.6f));
    }

    private void DrawSolid(Rect rect, Color color)
    {
        GUI.DrawTexture(rect, GetImmediateTexture(color));
    }

    private Vector2 MouseDesignPosition()
    {
        return new Vector2(
            (Input.mousePosition.x - hudOffset.x) / hudScale,
            (Screen.height - Input.mousePosition.y - hudOffset.y) / hudScale
        );
    }

    private void SetupCameraAndLight()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.58f, 0.84f, 1f);
        mainCamera.fieldOfView = 39f;
        mainCamera.nearClipPlane = 0.03f;
        mainCamera.farClipPlane = 120f;

        Light existingLight = FindObjectOfType<Light>();
        if (existingLight == null)
        {
            GameObject lightObject = new GameObject("Sun");
            existingLight = lightObject.AddComponent<Light>();
        }

        existingLight.type = LightType.Directional;
        existingLight.intensity = 1.18f;
        existingLight.color = new Color(1f, 0.97f, 0.86f);
        existingLight.transform.rotation = Quaternion.Euler(48f, -30f, 0f);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.72f, 0.82f, 0.92f);
        ApplyOrbitCamera();
    }

    private void SetupMaterials()
    {
        roundedSprite = CreateRoundedSprite(48);
        softRoundedSprite = CreateRoundedSprite(24);

        grassMaterial = MakeMaterial(new Color(0.47f, 0.82f, 0.24f), 0.18f, 0.52f);
        dirtMaterial = MakeMaterial(new Color(0.63f, 0.39f, 0.2f), 0.16f, 0.35f);
        waterMaterial = MakeTransparentMaterial(new Color(0.15f, 0.66f, 1f, 0.72f), 0.3f, 0.7f);
        gridMaterial = MakeTransparentMaterial(new Color(1f, 1f, 1f, 0.42f), 0.05f, 0.1f);
        whiteMaterial = MakeMaterial(new Color(1f, 0.96f, 0.86f), 0.1f, 0.5f);
        ghostMaterial = MakeTransparentMaterial(new Color(1f, 1f, 1f, 0.38f), 0.1f, 0.25f);
        validCursorMaterial = MakeTransparentMaterial(new Color(0.36f, 0.91f, 1f, 0.55f), 0.08f, 0.3f);
        invalidCursorMaterial = MakeTransparentMaterial(new Color(1f, 0.35f, 0.35f, 0.5f), 0.08f, 0.2f);
    }

    private void BuildShapeLibrary()
    {
        AddShape(ShapeKind.Cube, "方块", "方块", new Color(0.14f, 0.46f, 0.91f), new Vector3(0.92f, 0.92f, 0.92f), false);
        AddShape(ShapeKind.RectangularPrism, "长方体", "长条", new Color(0.41f, 0.82f, 0.15f), new Vector3(0.9f, 0.5f, 2.05f), true);
        AddShape(ShapeKind.Plate, "薄板", "薄板", new Color(1f, 0.78f, 0.17f), new Vector3(1.7f, 0.24f, 0.9f), true);
        AddShape(ShapeKind.Ramp, "斜坡", "斜坡", new Color(0.56f, 0.38f, 0.96f), new Vector3(0.95f, 0.88f, 1.75f), true);
        AddShape(ShapeKind.TriangularPrism, "三角柱", "三角", new Color(0.98f, 0.48f, 0.11f), new Vector3(1.75f, 0.95f, 0.9f), true);
        AddShape(ShapeKind.Cylinder, "圆柱", "圆柱", new Color(0.18f, 0.74f, 0.86f), new Vector3(0.88f, 0.95f, 0.88f), true);
    }

    private void AddShape(ShapeKind kind, string name, string shortName, Color color, Vector3 size, bool directional)
    {
        ShapeDefinition definition = new ShapeDefinition();
        definition.Kind = kind;
        definition.Name = name;
        definition.ShortName = shortName;
        definition.Color = color;
        definition.Size = size;
        definition.Directional = directional;
        shapes.Add(kind, definition);
    }

    private void BuildLevels()
    {
        levels.Clear();

        LevelData bridge = Level(1, "工程海湾", TaskKind.Functional, "修好小桥", "搭一座小桥，让小机器人走到灯塔。", "先把桥面连起来，再看看两边有没有对齐。", 1);
        AddInventory(bridge, ShapeKind.RectangularPrism, 5);
        AddInventory(bridge, ShapeKind.Cube, 8);
        AddInventory(bridge, ShapeKind.Plate, 6);
        bridge.Target.Add(T(ShapeKind.Cube, -3, 0, -1, 0));
        bridge.Target.Add(T(ShapeKind.Cube, 3, 0, -1, 0));
        bridge.Target.Add(T(ShapeKind.RectangularPrism, -2, 1, 0, 1));
        bridge.Target.Add(T(ShapeKind.RectangularPrism, 0, 1, 0, 1));
        bridge.Target.Add(T(ShapeKind.RectangularPrism, 2, 1, 0, 1));
        bridge.Target.Add(T(ShapeKind.Plate, -4, 1, 0, 1));
        bridge.Target.Add(T(ShapeKind.Plate, 4, 1, 0, 1));
        levels.Add(bridge);

        LevelData stairs = Level(2, "形状新手岛", TaskKind.Blueprint, "三层小台阶", "用方块搭出一格一格升高的小台阶。", "每一层都比前一层高一格。", 1);
        AddInventory(stairs, ShapeKind.Cube, 8);
        stairs.Target.Add(T(ShapeKind.Cube, -1, 0, 0, 0));
        stairs.Target.Add(T(ShapeKind.Cube, 0, 0, 0, 0));
        stairs.Target.Add(T(ShapeKind.Cube, 0, 1, 0, 0));
        stairs.Target.Add(T(ShapeKind.Cube, 1, 0, 0, 0));
        stairs.Target.Add(T(ShapeKind.Cube, 1, 1, 0, 0));
        stairs.Target.Add(T(ShapeKind.Cube, 1, 2, 0, 0));
        levels.Add(stairs);

        LevelData table = Level(3, "形状新手岛", TaskKind.Blueprint, "小桌子", "用四个方块和一块薄板搭一个小桌子。", "先放四条腿，再把薄板放在上面。", 1);
        AddInventory(table, ShapeKind.Cube, 6);
        AddInventory(table, ShapeKind.Plate, 3);
        table.Target.Add(T(ShapeKind.Cube, -1, 0, -1, 0));
        table.Target.Add(T(ShapeKind.Cube, 1, 0, -1, 0));
        table.Target.Add(T(ShapeKind.Cube, -1, 0, 1, 0));
        table.Target.Add(T(ShapeKind.Cube, 1, 0, 1, 0));
        table.Target.Add(T(ShapeKind.Plate, 0, 1, 0, 1));
        levels.Add(table);

        LevelData platform = Level(4, "形状新手岛", TaskKind.Blueprint, "连接平台", "用长方体把两个小平台连起来。", "长边要朝向两个平台之间。", 2);
        AddInventory(platform, ShapeKind.Cube, 8);
        AddInventory(platform, ShapeKind.RectangularPrism, 4);
        platform.Target.Add(T(ShapeKind.Cube, -3, 0, 0, 0));
        platform.Target.Add(T(ShapeKind.Cube, -3, 1, 0, 0));
        platform.Target.Add(T(ShapeKind.Cube, 3, 0, 0, 0));
        platform.Target.Add(T(ShapeKind.Cube, 3, 1, 0, 0));
        platform.Target.Add(T(ShapeKind.RectangularPrism, -1, 2, 0, 1));
        platform.Target.Add(T(ShapeKind.RectangularPrism, 1, 2, 0, 1));
        levels.Add(platform);

        LevelData houseBase = Level(5, "图纸塔", TaskKind.Blueprint, "小屋底座", "用薄板和方块搭出小屋的底座。", "先看底座的长和宽，再搭四个角。", 2);
        AddInventory(houseBase, ShapeKind.Cube, 12);
        AddInventory(houseBase, ShapeKind.Plate, 6);
        houseBase.Target.Add(T(ShapeKind.Plate, -1, 0, -1, 1));
        houseBase.Target.Add(T(ShapeKind.Plate, 1, 0, -1, 1));
        houseBase.Target.Add(T(ShapeKind.Plate, -1, 0, 1, 1));
        houseBase.Target.Add(T(ShapeKind.Plate, 1, 0, 1, 1));
        houseBase.Target.Add(T(ShapeKind.Cube, -2, 1, -1, 0));
        houseBase.Target.Add(T(ShapeKind.Cube, 2, 1, -1, 0));
        houseBase.Target.Add(T(ShapeKind.Cube, -2, 1, 1, 0));
        houseBase.Target.Add(T(ShapeKind.Cube, 2, 1, 1, 0));
        levels.Add(houseBase);

        LevelData wallDoor = Level(6, "图纸塔", TaskKind.Blueprint, "门洞墙", "搭一面有门洞的小墙。", "中间留出门，左右两边一样高。", 2);
        AddInventory(wallDoor, ShapeKind.Cube, 12);
        AddInventory(wallDoor, ShapeKind.RectangularPrism, 3);
        wallDoor.Target.Add(T(ShapeKind.Cube, -2, 0, 0, 0));
        wallDoor.Target.Add(T(ShapeKind.Cube, -2, 1, 0, 0));
        wallDoor.Target.Add(T(ShapeKind.Cube, 2, 0, 0, 0));
        wallDoor.Target.Add(T(ShapeKind.Cube, 2, 1, 0, 0));
        wallDoor.Target.Add(T(ShapeKind.RectangularPrism, 0, 2, 0, 1));
        levels.Add(wallDoor);

        LevelData roof = Level(7, "旋转工坊", TaskKind.Blueprint, "小屋屋顶", "用三角柱和斜坡搭一个屋顶。", "屋顶的斜面要朝外。", 3);
        AddInventory(roof, ShapeKind.TriangularPrism, 4);
        AddInventory(roof, ShapeKind.Ramp, 4);
        AddInventory(roof, ShapeKind.Cube, 6);
        roof.Target.Add(T(ShapeKind.Cube, -1, 0, 0, 0));
        roof.Target.Add(T(ShapeKind.Cube, 1, 0, 0, 0));
        roof.Target.Add(T(ShapeKind.Ramp, -1, 1, 0, 0));
        roof.Target.Add(T(ShapeKind.Ramp, 1, 1, 0, 2));
        roof.Target.Add(T(ShapeKind.TriangularPrism, 0, 2, 0, 1));
        levels.Add(roof);

        LevelData tower = Level(8, "图纸塔", TaskKind.Blueprint, "小灯塔", "用方块、长方体和圆柱搭一座小塔。", "圆柱是灯塔的灯，放在最上面。", 3);
        AddInventory(tower, ShapeKind.Cube, 8);
        AddInventory(tower, ShapeKind.RectangularPrism, 4);
        AddInventory(tower, ShapeKind.Cylinder, 3);
        tower.Target.Add(T(ShapeKind.Cube, 0, 0, 0, 0));
        tower.Target.Add(T(ShapeKind.Cube, 0, 1, 0, 0));
        tower.Target.Add(T(ShapeKind.RectangularPrism, 0, 2, 0, 0));
        tower.Target.Add(T(ShapeKind.Cylinder, 0, 3, 0, 0));
        levels.Add(tower);

        LevelData ramp = Level(9, "旋转工坊", TaskKind.Functional, "上坡小路", "用斜坡连接低平台和高平台。", "斜坡低的一端要朝向小机器人。", 3);
        AddInventory(ramp, ShapeKind.Cube, 8);
        AddInventory(ramp, ShapeKind.Ramp, 4);
        ramp.Target.Add(T(ShapeKind.Cube, -2, 0, 0, 0));
        ramp.Target.Add(T(ShapeKind.Cube, 2, 0, 0, 0));
        ramp.Target.Add(T(ShapeKind.Cube, 2, 1, 0, 0));
        ramp.Target.Add(T(ShapeKind.Ramp, 0, 0, 0, 1));
        levels.Add(ramp);

        LevelData slide = Level(10, "工程任务岛", TaskKind.Functional, "小球滑道", "用斜坡和薄板搭一条滑道。", "先让斜坡朝向终点，再接一块平平的薄板。", 3);
        AddInventory(slide, ShapeKind.Ramp, 4);
        AddInventory(slide, ShapeKind.Plate, 4);
        AddInventory(slide, ShapeKind.Cube, 5);
        slide.Target.Add(T(ShapeKind.Cube, -2, 0, 0, 0));
        slide.Target.Add(T(ShapeKind.Cube, -2, 1, 0, 0));
        slide.Target.Add(T(ShapeKind.Ramp, -1, 1, 0, 1));
        slide.Target.Add(T(ShapeKind.Plate, 1, 0, 0, 1));
        levels.Add(slide);

        LevelData windmill = Level(11, "旋转工坊", TaskKind.Blueprint, "小风车", "用圆柱和长方体搭风车中心与叶片。", "四片叶子围着圆心转一圈。", 4);
        AddInventory(windmill, ShapeKind.Cylinder, 3);
        AddInventory(windmill, ShapeKind.RectangularPrism, 6);
        AddInventory(windmill, ShapeKind.Cube, 4);
        windmill.Target.Add(T(ShapeKind.Cube, 0, 0, 0, 0));
        windmill.Target.Add(T(ShapeKind.Cylinder, 0, 1, 0, 0));
        windmill.Target.Add(T(ShapeKind.RectangularPrism, 0, 2, -1, 0));
        windmill.Target.Add(T(ShapeKind.RectangularPrism, 1, 2, 0, 1));
        windmill.Target.Add(T(ShapeKind.RectangularPrism, 0, 2, 1, 0));
        windmill.Target.Add(T(ShapeKind.RectangularPrism, -1, 2, 0, 1));
        levels.Add(windmill);

        LevelData repairWind = Level(12, "维修工坊", TaskKind.Repair, "修风车叶片", "有一片叶子方向错了，把它转回来。", "只看圆心周围，找那片朝向不一样的叶子。", 4);
        AddInventory(repairWind, ShapeKind.RectangularPrism, 2);
        repairWind.Target.AddRange(windmill.Target);
        repairWind.StartingShapes.Add(T(ShapeKind.Cube, 0, 0, 0, 0));
        repairWind.StartingShapes.Add(T(ShapeKind.Cylinder, 0, 1, 0, 0));
        repairWind.StartingShapes.Add(T(ShapeKind.RectangularPrism, 0, 2, -1, 0));
        repairWind.StartingShapes.Add(T(ShapeKind.RectangularPrism, 1, 2, 0, 1));
        repairWind.StartingShapes.Add(T(ShapeKind.RectangularPrism, 0, 2, 1, 1));
        repairWind.StartingShapes.Add(T(ShapeKind.RectangularPrism, -1, 2, 0, 1));
        levels.Add(repairWind);

        LevelData threeViews = Level(13, "图纸塔", TaskKind.Blueprint, "三视图小塔", "根据图纸搭一个左右对称的小塔。", "从正面看两边一样高，从侧面看中间最高。", 4);
        AddInventory(threeViews, ShapeKind.Cube, 10);
        AddInventory(threeViews, ShapeKind.Plate, 3);
        AddInventory(threeViews, ShapeKind.Cylinder, 2);
        threeViews.Target.Add(T(ShapeKind.Cube, -1, 0, 0, 0));
        threeViews.Target.Add(T(ShapeKind.Cube, 0, 0, 0, 0));
        threeViews.Target.Add(T(ShapeKind.Cube, 1, 0, 0, 0));
        threeViews.Target.Add(T(ShapeKind.Plate, 0, 1, 0, 1));
        threeViews.Target.Add(T(ShapeKind.Cylinder, 0, 2, 0, 0));
        levels.Add(threeViews);

        LevelData memoryBridge = Level(14, "记忆海湾", TaskKind.Memory, "记忆小桥", "先观察图纸，再凭记忆搭出小桥。", "先记住桥面有几段，再记住两边支撑。", 4);
        AddInventory(memoryBridge, ShapeKind.Cube, 6);
        AddInventory(memoryBridge, ShapeKind.RectangularPrism, 4);
        memoryBridge.Target.Add(T(ShapeKind.Cube, -2, 0, 0, 0));
        memoryBridge.Target.Add(T(ShapeKind.Cube, 2, 0, 0, 0));
        memoryBridge.Target.Add(T(ShapeKind.RectangularPrism, -1, 1, 0, 1));
        memoryBridge.Target.Add(T(ShapeKind.RectangularPrism, 1, 1, 0, 1));
        levels.Add(memoryBridge);

        LevelData rotatedHouse = Level(15, "旋转工坊", TaskKind.Blueprint, "旋转小屋", "搭出旋转后的房子底座。", "长边现在朝前后方向。", 4);
        AddInventory(rotatedHouse, ShapeKind.Cube, 8);
        AddInventory(rotatedHouse, ShapeKind.Plate, 5);
        AddInventory(rotatedHouse, ShapeKind.TriangularPrism, 3);
        rotatedHouse.Target.Add(T(ShapeKind.Plate, 0, 0, -1, 0));
        rotatedHouse.Target.Add(T(ShapeKind.Plate, 0, 0, 1, 0));
        rotatedHouse.Target.Add(T(ShapeKind.Cube, -1, 1, -1, 0));
        rotatedHouse.Target.Add(T(ShapeKind.Cube, 1, 1, -1, 0));
        rotatedHouse.Target.Add(T(ShapeKind.TriangularPrism, 0, 2, 0, 0));
        levels.Add(rotatedHouse);

        LevelData heightRepair = Level(16, "维修工坊", TaskKind.Repair, "修高度错误", "有一块放低了，把它放到正确高度。", "从侧面看，三块应该在同一层。", 4);
        AddInventory(heightRepair, ShapeKind.Cube, 2);
        heightRepair.Target.Add(T(ShapeKind.Cube, -1, 1, 0, 0));
        heightRepair.Target.Add(T(ShapeKind.Cube, 0, 1, 0, 0));
        heightRepair.Target.Add(T(ShapeKind.Cube, 1, 1, 0, 0));
        heightRepair.StartingShapes.Add(T(ShapeKind.Cube, -1, 1, 0, 0));
        heightRepair.StartingShapes.Add(T(ShapeKind.Cube, 0, 0, 0, 0));
        heightRepair.StartingShapes.Add(T(ShapeKind.Cube, 1, 1, 0, 0));
        levels.Add(heightRepair);

        LevelData road = Level(17, "工程任务岛", TaskKind.Functional, "连续道路", "用有限的形状搭出可通行道路。", "每一块都要接住下一块，不能断开。", 5);
        AddInventory(road, ShapeKind.Plate, 6);
        AddInventory(road, ShapeKind.Ramp, 3);
        AddInventory(road, ShapeKind.Cube, 4);
        road.Target.Add(T(ShapeKind.Plate, -3, 0, 0, 1));
        road.Target.Add(T(ShapeKind.Plate, -1, 0, 0, 1));
        road.Target.Add(T(ShapeKind.Ramp, 1, 0, 0, 1));
        road.Target.Add(T(ShapeKind.Cube, 3, 0, 0, 0));
        road.Target.Add(T(ShapeKind.Plate, 3, 1, 0, 1));
        levels.Add(road);

        LevelData robotView = Level(18, "视角花园", TaskKind.Viewpoint, "机器人左边", "从机器人的视角补上一块绿色方块。", "小机器人面对灯塔时，它的左边在画面的前方。", 5);
        AddInventory(robotView, ShapeKind.Cube, 5);
        robotView.Target.Add(T(ShapeKind.Cube, 0, 0, 0, 0));
        robotView.Target.Add(T(ShapeKind.Cube, 0, 0, -1, 0));
        robotView.Target.Add(T(ShapeKind.Cube, 0, 0, 1, 0));
        robotView.StartingShapes.Add(T(ShapeKind.Cube, 0, 0, 0, 0));
        robotView.StartingShapes.Add(T(ShapeKind.Cube, 0, 0, -1, 0));
        levels.Add(robotView);

        LevelData memoryTower = Level(19, "记忆海湾", TaskKind.Memory, "记忆小塔", "观察小塔图纸，再凭记忆复原。", "记住底座、中心和最上面的形状。", 5);
        AddInventory(memoryTower, ShapeKind.Cube, 8);
        AddInventory(memoryTower, ShapeKind.Plate, 4);
        AddInventory(memoryTower, ShapeKind.Cylinder, 3);
        AddInventory(memoryTower, ShapeKind.Ramp, 2);
        memoryTower.Target.Add(T(ShapeKind.Cube, -1, 0, 0, 0));
        memoryTower.Target.Add(T(ShapeKind.Cube, 1, 0, 0, 0));
        memoryTower.Target.Add(T(ShapeKind.Plate, 0, 1, 0, 1));
        memoryTower.Target.Add(T(ShapeKind.Cylinder, 0, 2, 0, 0));
        memoryTower.Target.Add(T(ShapeKind.Ramp, 0, 3, 0, 1));
        levels.Add(memoryTower);

        LevelData final = Level(20, "形状大师城", TaskKind.Challenge, "工程小岛", "连接桥、塔、坡道和道路，完成综合工程。", "先搭连续路线，再补上高处的灯塔。", 6);
        AddInventory(final, ShapeKind.Cube, 16);
        AddInventory(final, ShapeKind.RectangularPrism, 8);
        AddInventory(final, ShapeKind.Plate, 8);
        AddInventory(final, ShapeKind.Ramp, 4);
        AddInventory(final, ShapeKind.Cylinder, 4);
        final.Target.Add(T(ShapeKind.RectangularPrism, -3, 0, 0, 1));
        final.Target.Add(T(ShapeKind.RectangularPrism, -1, 0, 0, 1));
        final.Target.Add(T(ShapeKind.Ramp, 1, 0, 0, 1));
        final.Target.Add(T(ShapeKind.Cube, 3, 0, 0, 0));
        final.Target.Add(T(ShapeKind.Plate, 3, 1, 0, 1));
        final.Target.Add(T(ShapeKind.Cube, 4, 2, 0, 0));
        final.Target.Add(T(ShapeKind.Cylinder, 4, 3, 0, 0));
        levels.Add(final);
    }

    private LevelData Level(int number, string world, TaskKind task, string title, string goal, string hint, int difficulty)
    {
        LevelData level = new LevelData();
        level.Number = number;
        level.World = world;
        level.Task = task;
        level.Title = title;
        level.Goal = goal;
        level.Hint = hint;
        level.Difficulty = difficulty;
        return level;
    }

    private static TargetShape T(ShapeKind kind, int x, int y, int z, int rotation)
    {
        TargetShape target = new TargetShape();
        target.Kind = kind;
        target.Cell = new Vector3Int(x, y, z);
        target.Rotation = NormalizeRotation(rotation);
        return target;
    }

    private static void AddInventory(LevelData level, ShapeKind kind, int count)
    {
        level.Inventory[kind] = count;
    }

    private void BuildWorld()
    {
        worldRoot = new GameObject("Little Shape Engineer World").transform;
        islandRoot = new GameObject("Floating Shape Islands").transform;
        islandRoot.SetParent(worldRoot, false);
        buildRoot = new GameObject("Player Shapes").transform;
        buildRoot.SetParent(worldRoot, false);
        ghostRoot = new GameObject("Blueprint Ghosts").transform;
        ghostRoot.SetParent(worldRoot, false);
        cursorRoot = new GameObject("Build Cursor").transform;
        cursorRoot.SetParent(worldRoot, false);

        CreateOcean();
        CreateGrid();
        CreateIsland(new Vector3(-3.7f, -0.44f, 0f), 3, 3);
        CreateIsland(new Vector3(3.7f, -0.44f, 0f), 3, 3);
        CreateTinyIsland(new Vector3(0f, -0.5f, 3.2f), 1);
        CreateLighthouse(new Vector3(4.6f, 0.1f, 1.55f));
        CreateTrees();
        CreateClouds();
        CreateRobot(new Vector3(-4.4f, 0.25f, 1.15f));
    }

    private void CreateOcean()
    {
        GameObject ocean = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ocean.name = "Soft Ocean";
        ocean.transform.SetParent(worldRoot, false);
        ocean.transform.position = new Vector3(0f, -0.72f, 0f);
        ocean.transform.localScale = new Vector3(14.6f, 0.08f, 9.4f);
        ocean.GetComponent<Renderer>().sharedMaterial = waterMaterial;
        Destroy(ocean.GetComponent<Collider>());

        for (int i = 0; i < 15; i++)
        {
            float x = -6.5f + i * 0.95f;
            CreateWaveLine(new Vector3(x, -0.64f, -3.7f + Mathf.Sin(i) * 0.38f), 0.5f + (i % 3) * 0.18f);
        }
    }

    private void CreateWaveLine(Vector3 center, float width)
    {
        GameObject line = new GameObject("Wave Sparkle");
        line.transform.SetParent(worldRoot, false);
        LineRenderer renderer = line.AddComponent<LineRenderer>();
        renderer.useWorldSpace = false;
        renderer.positionCount = 2;
        renderer.SetPosition(0, new Vector3(-width, 0f, 0f));
        renderer.SetPosition(1, new Vector3(width, 0f, 0f));
        renderer.widthMultiplier = 0.025f;
        renderer.material = gridMaterial;
        line.transform.position = center;
        line.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(-8f, 8f), 0f);
    }

    private void CreateGrid()
    {
        for (int x = GridMinX; x <= GridMaxX; x++)
        {
            Vector3 from = CellToWorld(new Vector3Int(x, 0, GridMinZ)) + new Vector3(0f, 0.025f, -CellSize * 0.5f);
            Vector3 to = CellToWorld(new Vector3Int(x, 0, GridMaxZ)) + new Vector3(0f, 0.025f, CellSize * 0.5f);
            CreateGridLine(from, to);
        }

        for (int z = GridMinZ; z <= GridMaxZ; z++)
        {
            Vector3 from = CellToWorld(new Vector3Int(GridMinX, 0, z)) + new Vector3(-CellSize * 0.5f, 0.026f, 0f);
            Vector3 to = CellToWorld(new Vector3Int(GridMaxX, 0, z)) + new Vector3(CellSize * 0.5f, 0.026f, 0f);
            CreateGridLine(from, to);
        }
    }

    private void CreateGridLine(Vector3 from, Vector3 to)
    {
        GameObject line = new GameObject("Build Grid Line");
        line.transform.SetParent(worldRoot, false);
        LineRenderer renderer = line.AddComponent<LineRenderer>();
        renderer.positionCount = 2;
        renderer.SetPosition(0, from);
        renderer.SetPosition(1, to);
        renderer.widthMultiplier = 0.012f;
        renderer.material = gridMaterial;
    }

    private void CreateIsland(Vector3 center, int radiusX, int radiusZ)
    {
        for (int x = -radiusX; x <= radiusX; x++)
        {
            for (int z = -radiusZ; z <= radiusZ; z++)
            {
                float edge = Mathf.Abs(x) / (float)radiusX + Mathf.Abs(z) / (float)radiusZ;
                if (edge > 1.42f)
                {
                    continue;
                }

                float yOffset = UnityEngine.Random.value > 0.68f ? -0.08f : 0f;
                CreateStaticBlock(center + new Vector3(x * 0.62f, yOffset, z * 0.62f), grassMaterial, new Vector3(0.62f, 0.34f, 0.62f), "Grass Block");

                if (UnityEngine.Random.value > 0.2f)
                {
                    CreateStaticBlock(center + new Vector3(x * 0.62f, -0.32f + yOffset, z * 0.62f), dirtMaterial, new Vector3(0.62f, 0.36f, 0.62f), "Dirt Block");
                }
            }
        }
    }

    private void CreateTinyIsland(Vector3 center, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                CreateStaticBlock(center + new Vector3(x * 0.58f, 0f, z * 0.58f), grassMaterial, new Vector3(0.58f, 0.32f, 0.58f), "Tiny Island Block");
                CreateStaticBlock(center + new Vector3(x * 0.58f, -0.3f, z * 0.58f), dirtMaterial, new Vector3(0.58f, 0.34f, 0.58f), "Tiny Island Dirt");
            }
        }
    }

    private void CreateStaticBlock(Vector3 position, Material material, Vector3 scale, string name)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(islandRoot, false);
        block.transform.position = position;
        block.transform.localScale = scale;
        block.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(block.GetComponent<Collider>());
    }

    private void CreateLighthouse(Vector3 position)
    {
        Transform root = new GameObject("Lighthouse").transform;
        root.SetParent(worldRoot, false);
        root.position = position;

        GameObject baseCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseCube.transform.SetParent(root, false);
        baseCube.transform.localPosition = new Vector3(0f, 0f, 0f);
        baseCube.transform.localScale = new Vector3(1.15f, 0.22f, 1.15f);
        baseCube.GetComponent<Renderer>().sharedMaterial = whiteMaterial;
        Destroy(baseCube.GetComponent<Collider>());

        Material red = MakeMaterial(new Color(0.94f, 0.22f, 0.18f), 0.18f, 0.46f);
        for (int i = 0; i < 4; i++)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.transform.SetParent(root, false);
            cylinder.transform.localPosition = new Vector3(0f, 0.18f + i * 0.39f, 0f);
            cylinder.transform.localScale = new Vector3(0.45f, 0.19f, 0.45f);
            cylinder.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? red : whiteMaterial;
            Destroy(cylinder.GetComponent<Collider>());
        }

        GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lamp.transform.SetParent(root, false);
        lamp.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        lamp.transform.localScale = new Vector3(0.37f, 0.16f, 0.37f);
        lamp.GetComponent<Renderer>().sharedMaterial = MakeMaterial(new Color(1f, 0.85f, 0.32f), 0f, 0.7f);
        Destroy(lamp.GetComponent<Collider>());
        lighthouseLight = lamp.transform;

        GameObject roof = CreateCone("Lighthouse Roof", 0.55f, 0.55f, red);
        roof.transform.SetParent(root, false);
        roof.transform.localPosition = new Vector3(0f, 2.02f, 0f);
    }

    private void CreateTrees()
    {
        CreateTree(new Vector3(-5.1f, 0.06f, -1.35f));
        CreateTree(new Vector3(-4.6f, 0.06f, 1.75f));
        CreateTree(new Vector3(3.2f, 0.06f, -1.9f));
        CreateTree(new Vector3(5.05f, 0.06f, 0.35f));
    }

    private void CreateTree(Vector3 position)
    {
        Transform root = new GameObject("Blocky Tree").transform;
        root.SetParent(worldRoot, false);
        root.position = position;

        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.SetParent(root, false);
        trunk.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        trunk.transform.localScale = new Vector3(0.16f, 0.32f, 0.16f);
        trunk.GetComponent<Renderer>().sharedMaterial = MakeMaterial(new Color(0.52f, 0.31f, 0.16f), 0.15f, 0.2f);
        Destroy(trunk.GetComponent<Collider>());

        for (int i = 0; i < 3; i++)
        {
            GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.transform.SetParent(root, false);
            leaf.transform.localPosition = new Vector3((i - 1) * 0.18f, 0.75f + (i == 1 ? 0.18f : 0f), i == 1 ? 0f : 0.08f);
            leaf.transform.localScale = new Vector3(0.48f, 0.4f, 0.48f);
            leaf.GetComponent<Renderer>().sharedMaterial = grassMaterial;
            Destroy(leaf.GetComponent<Collider>());
        }
    }

    private void CreateClouds()
    {
        CreateCloud(new Vector3(-5.8f, 4.1f, 3.4f), 0.75f);
        CreateCloud(new Vector3(1.2f, 4.55f, 3.9f), 0.95f);
        CreateCloud(new Vector3(5.7f, 4.25f, 2.8f), 0.72f);
    }

    private void CreateCloud(Vector3 position, float scale)
    {
        Transform root = new GameObject("Soft Cloud").transform;
        root.SetParent(worldRoot, false);
        root.position = position;
        for (int i = 0; i < 5; i++)
        {
            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.transform.SetParent(root, false);
            puff.transform.localPosition = new Vector3((i - 2) * 0.34f, Mathf.Sin(i) * 0.08f, 0f);
            puff.transform.localScale = Vector3.one * scale * (0.42f + (i % 2) * 0.18f);
            puff.GetComponent<Renderer>().sharedMaterial = MakeMaterial(new Color(1f, 1f, 1f, 0.95f), 0.05f, 0.35f);
            Destroy(puff.GetComponent<Collider>());
        }
    }

    private void CreateRobot(Vector3 position)
    {
        robotRoot = new GameObject("Buddy Robot").transform;
        robotRoot.SetParent(worldRoot, false);
        robotRoot.position = position;
        robotRoot.rotation = Quaternion.Euler(0f, 95f, 0f);

        Material body = MakeMaterial(new Color(0.9f, 0.96f, 1f), 0.2f, 0.7f);
        Material blue = MakeMaterial(new Color(0.16f, 0.52f, 0.95f), 0.12f, 0.75f);
        Material face = MakeMaterial(new Color(0.04f, 0.13f, 0.18f), 0.02f, 0.5f);
        Material glow = MakeMaterial(new Color(0.35f, 1f, 1f), 0.02f, 0.8f);

        GameObject bodyCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bodyCube.transform.SetParent(robotRoot, false);
        bodyCube.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        bodyCube.transform.localScale = new Vector3(0.52f, 0.58f, 0.36f);
        bodyCube.GetComponent<Renderer>().sharedMaterial = body;
        Destroy(bodyCube.GetComponent<Collider>());

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.transform.SetParent(robotRoot, false);
        head.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        head.transform.localScale = new Vector3(0.72f, 0.5f, 0.42f);
        head.GetComponent<Renderer>().sharedMaterial = body;
        Destroy(head.GetComponent<Collider>());

        GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
        screen.transform.SetParent(robotRoot, false);
        screen.transform.localPosition = new Vector3(0f, 0.96f, 0.23f);
        screen.transform.localScale = new Vector3(0.46f, 0.26f, 0.035f);
        screen.GetComponent<Renderer>().sharedMaterial = face;
        Destroy(screen.GetComponent<Collider>());

        CreateRobotEye(new Vector3(-0.13f, 0.99f, 0.255f), glow);
        CreateRobotEye(new Vector3(0.13f, 0.99f, 0.255f), glow);

        for (int side = -1; side <= 1; side += 2)
        {
            GameObject ear = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ear.transform.SetParent(robotRoot, false);
            ear.transform.localPosition = new Vector3(side * 0.44f, 0.96f, 0f);
            ear.transform.localScale = new Vector3(0.18f, 0.24f, 0.24f);
            ear.GetComponent<Renderer>().sharedMaterial = blue;
            Destroy(ear.GetComponent<Collider>());

            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.transform.SetParent(robotRoot, false);
            arm.transform.localPosition = new Vector3(side * 0.43f, 0.48f, 0.02f);
            arm.transform.localScale = new Vector3(0.12f, 0.45f, 0.12f);
            arm.transform.localRotation = Quaternion.Euler(0f, 0f, side * -18f);
            arm.GetComponent<Renderer>().sharedMaterial = body;
            Destroy(arm.GetComponent<Collider>());
        }

        GameObject antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        antenna.transform.SetParent(robotRoot, false);
        antenna.transform.localPosition = new Vector3(0f, 1.32f, 0f);
        antenna.transform.localScale = new Vector3(0.025f, 0.13f, 0.025f);
        antenna.GetComponent<Renderer>().sharedMaterial = blue;
        Destroy(antenna.GetComponent<Collider>());

        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.transform.SetParent(robotRoot, false);
        tip.transform.localPosition = new Vector3(0f, 1.48f, 0f);
        tip.transform.localScale = Vector3.one * 0.11f;
        tip.GetComponent<Renderer>().sharedMaterial = glow;
        Destroy(tip.GetComponent<Collider>());
    }

    private void CreateRobotEye(Vector3 localPosition, Material material)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.transform.SetParent(robotRoot, false);
        eye.transform.localPosition = localPosition;
        eye.transform.localScale = new Vector3(0.055f, 0.055f, 0.025f);
        eye.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(eye.GetComponent<Collider>());
    }

    private void BuildUi()
    {
        uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        GameObject eventSystemObject = GameObject.Find("EventSystem");
        if (eventSystemObject == null)
        {
            eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        GameObject canvasObject = new GameObject("Little Shape Engineer UI");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        CreateTopUi(canvas.transform);
        CreateLeftMissionPanel(canvas.transform);
        CreateRightBlueprintPanel(canvas.transform);
        CreateBottomPalette(canvas.transform);
    }

    private void CreateTopUi(Transform parent)
    {
        RectTransform leftHome = CreatePanel("Top Left Buttons", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -120f), new Vector2(260f, -20f), Color.clear, 0f);
        CreateButton(leftHome, "返回", new Vector2(0f, 0.5f), new Vector2(92f, 92f), delegate { PreviousLevel(); }, new Color(1f, 1f, 1f, 0.92f), new Color(0.22f, 0.48f, 0.82f));
        CreateButton(leftHome, "主页", new Vector2(116f, 0.5f), new Vector2(92f, 92f), delegate { LoadLevel(0); }, new Color(1f, 1f, 1f, 0.92f), new Color(0.22f, 0.48f, 0.82f));

        RectTransform centerPanel = CreatePanel("Action Bar", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-350f, -118f), new Vector2(350f, -20f), new Color(1f, 1f, 1f, 0.92f), 0.92f);
        undoButton = CreateButton(centerPanel, "撤销", new Vector2(-245f, 0f), new Vector2(130f, 78f), delegate { Undo(); }, new Color(1f, 0.98f, 0.92f, 1f), new Color(0.93f, 0.48f, 0.06f));
        redoButton = CreateButton(centerPanel, "重做", new Vector2(-85f, 0f), new Vector2(130f, 78f), delegate { Redo(); }, new Color(1f, 0.98f, 0.92f, 1f), new Color(0.45f, 0.55f, 0.65f));
        CreateButton(centerPanel, "旋转", new Vector2(75f, 0f), new Vector2(130f, 78f), delegate { RotateSelected(); }, new Color(0.94f, 0.98f, 1f, 1f), new Color(0.08f, 0.55f, 0.92f));
        CreateButton(centerPanel, "测试", new Vector2(245f, 0f), new Vector2(150f, 82f), delegate { RunTest(); }, new Color(0.84f, 1f, 0.55f, 1f), new Color(0.22f, 0.63f, 0.06f));

        RectTransform rightTop = CreatePanel("Top Right Buttons", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-206f, -120f), new Vector2(-24f, -20f), Color.clear, 0f);
        nextButton = CreateButton(rightTop, "下一关", new Vector2(0f, 0.5f), new Vector2(170f, 92f), delegate { NextLevel(); }, new Color(1f, 1f, 1f, 0.94f), new Color(0.2f, 0.46f, 0.82f));
    }

    private void CreateLeftMissionPanel(Transform parent)
    {
        RectTransform panel = CreatePanel("Mission Card", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(24f, 186f), new Vector2(350f, -150f), new Color(1f, 0.97f, 0.9f, 0.94f), 0.94f);

        worldText = CreateText(panel, "形状岛", 22, FontStyle.Bold, new Color(0.23f, 0.46f, 0.85f), new Vector2(26f, -34f), new Vector2(270f, 32f), TextAnchor.MiddleLeft);
        titleText = CreateText(panel, "任务", 34, FontStyle.Bold, new Color(0.13f, 0.16f, 0.22f), new Vector2(26f, -78f), new Vector2(270f, 46f), TextAnchor.MiddleLeft);
        taskText = CreateText(panel, "小小形状工程师", 22, FontStyle.Bold, new Color(0.33f, 0.42f, 0.55f), new Vector2(26f, -128f), new Vector2(270f, 42f), TextAnchor.MiddleLeft);
        goalText = CreateText(panel, "搭建目标", 24, FontStyle.Normal, new Color(0.12f, 0.13f, 0.16f), new Vector2(26f, -218f), new Vector2(276f, 116f), TextAnchor.UpperLeft);

        RectTransform robotPanel = CreatePanel("Robot Tip", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 104f), new Vector2(-20f, 276f), new Color(0.85f, 0.95f, 1f, 0.88f), 0.88f);
        CreateRobotPortrait(robotPanel);
        robotHintText = CreateText(robotPanel, "先观察图纸。", 22, FontStyle.Bold, new Color(0.12f, 0.18f, 0.26f), new Vector2(124f, -32f), new Vector2(168f, 110f), TextAnchor.MiddleLeft);

        RectTransform progressPanel = CreatePanel("Progress", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 20f), new Vector2(-20f, 88f), new Color(1f, 1f, 1f, 0.82f), 0.82f);
        progressText = CreateText(progressPanel, "进度", 22, FontStyle.Bold, new Color(0.2f, 0.48f, 0.86f), new Vector2(18f, -10f), new Vector2(90f, 30f), TextAnchor.MiddleLeft);
        starText = CreateText(progressPanel, "☆ ☆ ☆", 34, FontStyle.Bold, new Color(1f, 0.68f, 0.12f), new Vector2(116f, -14f), new Vector2(160f, 40f), TextAnchor.MiddleLeft);
    }

    private void CreateRobotPortrait(RectTransform parent)
    {
        RectTransform face = CreatePanel("Robot Face", parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, -58f), new Vector2(110f, 58f), new Color(0.94f, 0.98f, 1f, 1f), 1f);
        RectTransform visor = CreatePanel("Robot Visor", face, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-34f, -20f), new Vector2(34f, 22f), new Color(0.03f, 0.12f, 0.17f, 1f), 1f);
        CreatePanel("Left Eye", visor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-20f, -4f), new Vector2(-10f, 8f), new Color(0.34f, 1f, 1f, 1f), 1f);
        CreatePanel("Right Eye", visor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(10f, -4f), new Vector2(20f, 8f), new Color(0.34f, 1f, 1f, 1f), 1f);
        CreatePanel("Ear L", face, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-10f, -20f), new Vector2(12f, 20f), new Color(0.18f, 0.58f, 0.95f, 1f), 1f);
        CreatePanel("Ear R", face, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, -20f), new Vector2(10f, 20f), new Color(0.18f, 0.58f, 0.95f, 1f), 1f);
    }

    private void CreateRightBlueprintPanel(Transform parent)
    {
        RectTransform panel = CreatePanel("Blueprint Panel", parent, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-390f, 170f), new Vector2(-26f, -150f), new Color(0.95f, 0.99f, 1f, 0.94f), 0.94f);
        RectTransform header = CreatePanel("Blueprint Header", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(0f, 0f), new Color(0.1f, 0.47f, 0.9f, 1f), 1f);
        blueprintTitleText = CreateText(header, "图纸", 32, FontStyle.Bold, Color.white, new Vector2(0f, -8f), new Vector2(0f, 52f), TextAnchor.MiddleCenter);
        blueprintTypeText = CreateText(panel, "3D 目标 + 三视图", 22, FontStyle.Bold, new Color(0.15f, 0.22f, 0.3f), new Vector2(28f, -100f), new Vector2(280f, 36f), TextAnchor.MiddleLeft);

        topProjectionGrid = CreateProjection(panel, "俯视图", new Vector2(28f, -270f));
        frontProjectionGrid = CreateProjection(panel, "正面图", new Vector2(28f, -458f));
        sideProjectionGrid = CreateProjection(panel, "侧面图", new Vector2(28f, -646f));
    }

    private RectTransform CreateProjection(RectTransform parent, string label, Vector2 anchoredPosition)
    {
        RectTransform holder = CreatePanel(label + " Panel", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(anchoredPosition.x, anchoredPosition.y), new Vector2(anchoredPosition.x + 308f, anchoredPosition.y + 158f), new Color(1f, 1f, 1f, 0.86f), 0.86f);
        CreateText(holder, label, 20, FontStyle.Bold, new Color(0.12f, 0.18f, 0.25f), new Vector2(14f, -10f), new Vector2(250f, 28f), TextAnchor.MiddleLeft);
        RectTransform grid = CreatePanel(label + " Grid", holder, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-136f, 16f), new Vector2(136f, 106f), new Color(0.9f, 0.96f, 1f, 0.58f), 0.58f);
        return grid;
    }

    private void CreateBottomPalette(Transform parent)
    {
        RectTransform palette = CreatePanel("Shape Palette", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-600f, 26f), new Vector2(600f, 164f), new Color(1f, 0.98f, 0.92f, 0.94f), 0.94f);

        ShapeKind[] order = new ShapeKind[]
        {
            ShapeKind.Cube,
            ShapeKind.RectangularPrism,
            ShapeKind.Plate,
            ShapeKind.Ramp,
            ShapeKind.TriangularPrism,
            ShapeKind.Cylinder
        };

        for (int i = 0; i < order.Length; i++)
        {
            ShapeKind kind = order[i];
            ShapeDefinition shape = shapes[kind];
            RectTransform card = CreatePanel(shape.Name + " Card", palette, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f + i * 194f, -58f), new Vector2(186f + i * 194f, 58f), new Color(1f, 1f, 1f, 0.88f), 0.88f);
            Image bg = card.GetComponent<Image>();
            paletteCards[kind] = bg;

            Image icon = CreateImage(card, shape.Name + " Icon", new Vector2(0.5f, 0.5f), new Vector2(96f, 62f), new Vector2(0f, 18f), Color.white);
            icon.sprite = CreateShapeIconSprite(kind, shape.Color);
            icon.preserveAspect = true;

            CreateText(card, shape.Name, 20, FontStyle.Bold, new Color(0.12f, 0.15f, 0.19f), new Vector2(0f, -38f), new Vector2(0f, 26f), TextAnchor.MiddleCenter);
            Text count = CreateText(card, "0", 24, FontStyle.Bold, Color.white, new Vector2(55f, -54f), new Vector2(54f, 28f), TextAnchor.MiddleCenter);
            RectTransform bubble = CreatePanel("Count Bubble", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(34f, 8f), new Vector2(88f, 36f), new Color(0.18f, 0.5f, 0.9f, 1f), 1f);
            bubble.SetAsFirstSibling();
            count.transform.SetAsLastSibling();
            paletteCounts[kind] = count;

            ShapeKind capturedKind = kind;
            AddPointerEvent(card.gameObject, EventTriggerType.PointerDown, delegate { BeginPaletteDrag(capturedKind); });
            AddPointerEvent(card.gameObject, EventTriggerType.BeginDrag, delegate { BeginPaletteDrag(capturedKind); });
            AddPointerEvent(card.gameObject, EventTriggerType.PointerUp, delegate { EndPaletteDrag(); });
            AddPointerEvent(card.gameObject, EventTriggerType.EndDrag, delegate { EndPaletteDrag(); });
        }

        RectTransform banner = CreatePanel("Feedback Banner", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-520f, 174f), new Vector2(520f, 226f), new Color(0.1f, 0.47f, 0.9f, 0.86f), 0.86f);
        feedbackText = CreateStretchText(banner, "把形状拖到发光网格里，完成你的工程小岛。", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
    }

    private void LoadLevel(int index)
    {
        if (index < 0 || index >= levels.Count)
        {
            return;
        }

        levelIndex = index;
        LevelData level = CurrentLevel;
        testAttempts = 0;
        hintCount = 0;
        blueprintVisible = true;
        selectedRotation = 0;

        if (memoryHideRoutine != null)
        {
            StopCoroutine(memoryHideRoutine);
            memoryHideRoutine = null;
        }

        if (robotWalkRoutine != null)
        {
            StopCoroutine(robotWalkRoutine);
            robotWalkRoutine = null;
        }

        robotRoot.position = new Vector3(-4.4f, 0.25f, 1.15f);
        robotRoot.rotation = Quaternion.Euler(0f, 95f, 0f);

        ClearRuntimeObjects(buildRoot);
        ClearRuntimeObjects(ghostRoot);
        placedShapes.Clear();
        ghostObjects.Clear();
        undoStack.Clear();
        redoStack.Clear();
        remainingInventory.Clear();

        foreach (KeyValuePair<ShapeKind, int> pair in level.Inventory)
        {
            remainingInventory[pair.Key] = pair.Value;
        }

        foreach (TargetShape start in level.StartingShapes)
        {
            AddPlacedShape(start.Kind, start.Cell, start.Rotation, false, false);
        }

        selectedKind = FirstAvailableKind();
        SetFeedbackText("选择底部形状，拖到 3D 网格里。");
        SetRobotHintText(level.Hint);
        SetStarText("★ ☆ ☆");
        RefreshGhosts();
        RefreshProjectionViews();
        RefreshUi();
        LogEvent("load_level", level.Title);

        if (level.Task == TaskKind.Memory)
        {
            memoryHideRoutine = StartCoroutine(HideBlueprintAfterSeconds(8f));
        }
    }

    private LevelData CurrentLevel
    {
        get { return levels[levelIndex]; }
    }

    private ShapeKind FirstAvailableKind()
    {
        foreach (ShapeKind kind in Enum.GetValues(typeof(ShapeKind)))
        {
            if (GetRemaining(kind) > 0)
            {
                return kind;
            }
        }

        return ShapeKind.Cube;
    }

    private IEnumerator HideBlueprintAfterSeconds(float seconds)
    {
        SetRobotHintText("先认真看图纸。");
        yield return new WaitForSeconds(seconds);
        blueprintVisible = false;
        SetRobotHintText("现在凭记忆搭出来。");
        RefreshGhosts();
        RefreshProjectionViews();
    }

    private void RefreshUi()
    {
        if (titleText == null)
        {
            return;
        }

        LevelData level = CurrentLevel;
        titleText.text = level.Title;
        worldText.text = level.World;
        taskText.text = LevelTaskLabel(level);
        goalText.text = level.Goal;
        SetRobotHintText(level.Hint);
        progressText.text = "第 " + level.Number + " / " + levels.Count + " 关";
        blueprintTitleText.text = "图纸";
        blueprintTypeText.text = BlueprintLabel(level);
        SetStarText(GetSolvedCount() == level.Target.Count ? "★ ★ ★" : "★ ☆ ☆");

        foreach (KeyValuePair<ShapeKind, Text> pair in paletteCounts)
        {
            pair.Value.text = GetRemaining(pair.Key).ToString();
        }

        foreach (KeyValuePair<ShapeKind, Image> pair in paletteCards)
        {
            Color color = pair.Key == selectedKind ? new Color(0.84f, 0.94f, 1f, 0.98f) : new Color(1f, 1f, 1f, 0.88f);
            if (GetRemaining(pair.Key) <= 0 && !HasShapeInBuild(pair.Key))
            {
                color = new Color(0.88f, 0.9f, 0.92f, 0.72f);
            }

            pair.Value.color = color;
        }

        undoButton.interactable = undoStack.Count > 0;
        redoButton.interactable = redoStack.Count > 0;
        nextButton.interactable = true;
    }

    private void SetFeedbackText(string value)
    {
        hudFeedbackText = value;
        if (feedbackText != null)
        {
            feedbackText.text = value;
        }
    }

    private void SetRobotHintText(string value)
    {
        hudRobotHintText = value;
        if (robotHintText != null)
        {
            robotHintText.text = value;
        }
    }

    private void SetStarText(string value)
    {
        hudStarText = value;
        if (starText != null)
        {
            starText.text = value;
        }
    }

    private string LevelTaskLabel(LevelData level)
    {
        switch (level.Task)
        {
            case TaskKind.Functional:
                return "工程任务";
            case TaskKind.Repair:
                return "修复任务";
            case TaskKind.Memory:
                return "记忆任务";
            case TaskKind.Viewpoint:
                return "视角任务";
            case TaskKind.Challenge:
                return "综合挑战";
            default:
                return "图纸复刻";
        }
    }

    private string BlueprintLabel(LevelData level)
    {
        if (level.Task == TaskKind.Memory && !blueprintVisible)
        {
            return "图纸已隐藏";
        }

        if (level.Task == TaskKind.Viewpoint)
        {
            return "机器人视角 + 三视图";
        }

        if (level.Task == TaskKind.Functional)
        {
            return "功能目标 + 路径";
        }

        return "3D 目标 + 三视图";
    }

    private void BeginPaletteDrag(ShapeKind kind)
    {
        if (GetRemaining(kind) <= 0)
        {
            SetRobotHintText("这种形状已经用完了。");
            return;
        }

        selectedKind = kind;
        draggingFromPalette = true;
        selectedRotation = 0;
        RefreshUi();
        EnsureCursor();
    }

    private void EndPaletteDrag()
    {
        if (!draggingFromPalette)
        {
            return;
        }

        if (currentCursorValid && !IsPointerOverUi())
        {
            PlaceSelectedAtCursor();
        }

        draggingFromPalette = false;
        HideCursor();
    }

    private void HandleBuildInput()
    {
        bool pointerOverUi = IsPointerOverUi();

        if (draggingFromPalette)
        {
            UpdateCursorFromMouse(selectedKind, selectedRotation, null);
            if (Input.GetMouseButtonUp(0))
            {
                EndPaletteDrag();
            }

            return;
        }

        if (draggingExisting)
        {
            UpdateCursorFromMouse(draggedShape.Kind, draggedShape.Rotation, draggedShape);
            if (dragCandidateValid)
            {
                draggedShape.Cell = currentCursorCell;
                UpdatePlacedShapeView(draggedShape);
            }

            if (Input.GetMouseButtonUp(0))
            {
                FinishExistingDrag();
            }

            return;
        }

        if (!pointerOverUi)
        {
            UpdateCursorFromMouse(selectedKind, selectedRotation, null);
        }
        else
        {
            HideCursor();
        }

        if (pointerOverUi)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            PlacedShape hitShape = RaycastPlacedShape();
            if (hitShape != null)
            {
                StartExistingDrag(hitShape);
                return;
            }

            if (currentCursorValid)
            {
                PlaceSelectedAtCursor();
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            PlacedShape hitShape = RaycastPlacedShape();
            if (hitShape != null)
            {
                RemoveShape(hitShape);
            }
        }
    }

    private void StartExistingDrag(PlacedShape shape)
    {
        draggedShape = shape;
        draggingExisting = true;
        dragCandidateValid = true;
        dragStartCell = shape.Cell;
        dragStartRotation = shape.Rotation;
        PushUndo();
        EnsureCursor(shape.Kind, shape.Rotation);
        SetViewAlpha(shape.View, 0.35f);
        LogEvent("start_drag", shapes[shape.Kind].Name);
    }

    private void FinishExistingDrag()
    {
        if (!dragCandidateValid)
        {
            draggedShape.Cell = dragStartCell;
            draggedShape.Rotation = dragStartRotation;
            UpdatePlacedShapeView(draggedShape);
        }

        SetViewAlpha(draggedShape.View, 1f);
        draggingExisting = false;
        draggedShape = null;
        HideCursor();
        redoStack.Clear();
        RefreshGhosts();
        RefreshProjectionViews();
        RefreshUi();
        LogEvent("move_shape", "drag");
    }

    private void PlaceSelectedAtCursor()
    {
        if (!currentCursorValid)
        {
            return;
        }

        AddPlacedShape(selectedKind, currentCursorCell, selectedRotation, true, true);
        RefreshGhosts();
        RefreshProjectionViews();
        RefreshUi();
    }

    private void AddPlacedShape(ShapeKind kind, Vector3Int cell, int rotation, bool consumeInventory, bool recordUndo)
    {
        if (consumeInventory && GetRemaining(kind) <= 0)
        {
            return;
        }

        if (recordUndo)
        {
            PushUndo();
            redoStack.Clear();
        }

        PlacedShape placed = new PlacedShape();
        placed.Id = Guid.NewGuid().ToString("N");
        placed.Kind = kind;
        placed.Cell = cell;
        placed.Rotation = NormalizeRotation(rotation);
        placedShapes.Add(placed);
        placed.View = CreateShapeObject(shapes[kind], placed.Cell, placed.Rotation, buildRoot, null, true);
        ShapeView[] views = placed.View.GetComponentsInChildren<ShapeView>(true);
        for (int i = 0; i < views.Length; i++)
        {
            views[i].Id = placed.Id;
        }

        if (consumeInventory)
        {
            remainingInventory[kind] = GetRemaining(kind) - 1;
        }

        LogEvent("place_shape", shapes[kind].Name + " " + cell);
    }

    private void RemoveShape(PlacedShape shape)
    {
        PushUndo();
        redoStack.Clear();
        placedShapes.Remove(shape);
        remainingInventory[shape.Kind] = GetRemaining(shape.Kind) + 1;
        if (shape.View != null)
        {
            Destroy(shape.View);
        }

        RefreshGhosts();
        RefreshProjectionViews();
        RefreshUi();
        LogEvent("remove_shape", shapes[shape.Kind].Name);
    }

    private void RotateSelected()
    {
        if (draggingExisting && draggedShape != null)
        {
            draggedShape.Rotation = NormalizeRotation(draggedShape.Rotation + 1);
            UpdatePlacedShapeView(draggedShape);
            EnsureCursor(draggedShape.Kind, draggedShape.Rotation);
            LogEvent("rotate_shape", shapes[draggedShape.Kind].Name);
            return;
        }

        selectedRotation = NormalizeRotation(selectedRotation + 1);
        EnsureCursor(selectedKind, selectedRotation);
        LogEvent("rotate_selected", shapes[selectedKind].Name);
    }

    private void HandleKeyboardShortcuts()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RotateSelected();
        }

        if (Input.GetKeyDown(KeyCode.Z) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            Undo();
        }

        if (Input.GetKeyDown(KeyCode.Y) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            Redo();
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            RunTest();
        }
    }

    private void UpdateCursorFromMouse(ShapeKind kind, int rotation, PlacedShape ignore)
    {
        EnsureCursor(kind, rotation);

        Vector3 hit;
        if (!MouseToWorldOnBuildPlane(out hit))
        {
            currentCursorValid = false;
            HideCursor();
            return;
        }

        Vector3Int cell = WorldToCell(hit);
        cell.y = FindTopLayer(cell.x, cell.z, ignore);
        currentCursorCell = cell;
        currentCursorValid = IsPlacementValid(kind, cell, ignore);
        dragCandidateValid = currentCursorValid;

        cursorView.SetActive(true);
        cursorView.transform.position = CellToWorld(cell);
        cursorView.transform.rotation = Quaternion.Euler(0f, NormalizeRotation(rotation) * 90f, 0f);
        SetPreviewMaterial(currentCursorValid ? validCursorMaterial : invalidCursorMaterial);
    }

    private bool MouseToWorldOnBuildPlane(out Vector3 hit)
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, 0f, 0f));
        float enter;
        if (plane.Raycast(ray, out enter))
        {
            hit = ray.GetPoint(enter);
            return true;
        }

        hit = Vector3.zero;
        return false;
    }

    private Vector3Int WorldToCell(Vector3 world)
    {
        return new Vector3Int(
            Mathf.RoundToInt(world.x / CellSize),
            0,
            Mathf.RoundToInt(world.z / CellSize)
        );
    }

    private Vector3 CellToWorld(Vector3Int cell)
    {
        return new Vector3(cell.x * CellSize, cell.y * LayerHeight, cell.z * CellSize);
    }

    private bool IsPlacementValid(ShapeKind kind, Vector3Int cell, PlacedShape ignore)
    {
        if (cell.x < GridMinX || cell.x > GridMaxX || cell.z < GridMinZ || cell.z > GridMaxZ || cell.y < 0 || cell.y > 5)
        {
            return false;
        }

        if (GetRemaining(kind) <= 0 && ignore == null)
        {
            return false;
        }

        for (int i = 0; i < placedShapes.Count; i++)
        {
            PlacedShape other = placedShapes[i];
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

    private int FindTopLayer(int x, int z, PlacedShape ignore)
    {
        int top = 0;
        for (int i = 0; i < placedShapes.Count; i++)
        {
            PlacedShape other = placedShapes[i];
            if (other == ignore)
            {
                continue;
            }

            if (other.Cell.x == x && other.Cell.z == z)
            {
                top = Mathf.Max(top, other.Cell.y + 1);
            }
        }

        return top;
    }

    private PlacedShape RaycastPlacedShape()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f))
        {
            ShapeView view = hit.collider.GetComponentInParent<ShapeView>();
            if (view != null)
            {
                for (int i = 0; i < placedShapes.Count; i++)
                {
                    if (placedShapes[i].Id == view.Id)
                    {
                        return placedShapes[i];
                    }
                }
            }
        }

        return null;
    }

    private void RunTest()
    {
        testAttempts++;
        Diagnostic diagnostic = EvaluateBuild();
        LogEvent("run_test", diagnostic.Passed ? "pass" : diagnostic.Message);

        if (diagnostic.Passed)
        {
            SetFeedbackText("成功！小岛又被修好了一点。");
            SetRobotHintText("太棒了！这些基础形状组合成了真正有用的结构。");
            SetStarText("★ ★ ★");
            FlashLighthouse();

            if (CurrentLevel.Task == TaskKind.Functional || CurrentLevel.Number == 1)
            {
                if (robotWalkRoutine != null)
                {
                    StopCoroutine(robotWalkRoutine);
                }

                robotWalkRoutine = StartCoroutine(WalkRobotAcross());
            }
        }
        else
        {
            hintCount++;
            SetFeedbackText(diagnostic.Message);
            SetRobotHintText(diagnostic.Hint);
            SetStarText("★ ☆ ☆");
            RefreshGhosts();
        }

        RefreshUi();
    }

    private sealed class Diagnostic
    {
        public bool Passed;
        public string Message;
        public string Hint;
    }

    private Diagnostic EvaluateBuild()
    {
        Diagnostic diagnostic = new Diagnostic();
        LevelData level = CurrentLevel;
        bool[] matchedPlayer = new bool[placedShapes.Count];
        int matched = 0;

        for (int i = 0; i < level.Target.Count; i++)
        {
            TargetShape target = level.Target[i];
            bool found = false;
            for (int j = 0; j < placedShapes.Count; j++)
            {
                if (matchedPlayer[j])
                {
                    continue;
                }

                if (Matches(target, placedShapes[j]))
                {
                    matchedPlayer[j] = true;
                    matched++;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                diagnostic.Passed = false;
                diagnostic.Message = "还差一块" + shapes[target.Kind].Name + "。";
                diagnostic.Hint = HintForMissing(target);
                return diagnostic;
            }
        }

        if (placedShapes.Count > matched)
        {
            diagnostic.Passed = false;
            diagnostic.Message = "有一块多出来了。";
            diagnostic.Hint = "把多余的形状拿走，再测试一次。";
            return diagnostic;
        }

        diagnostic.Passed = true;
        diagnostic.Message = "完成";
        diagnostic.Hint = "完成";
        return diagnostic;
    }

    private bool Matches(TargetShape target, PlacedShape placed)
    {
        if (target.Kind != placed.Kind)
        {
            return false;
        }

        if (target.Cell != placed.Cell)
        {
            return false;
        }

        ShapeDefinition definition = shapes[target.Kind];
        if (definition.Directional && NormalizeRotation(target.Rotation) != NormalizeRotation(placed.Rotation))
        {
            return false;
        }

        return true;
    }

    private string HintForMissing(TargetShape target)
    {
        if (target.Cell.y > 0)
        {
            return "这块要放在第 " + (target.Cell.y + 1) + " 层。";
        }

        if (target.Kind == ShapeKind.RectangularPrism || target.Kind == ShapeKind.Plate)
        {
            return "长边方向要和图纸一样。";
        }

        if (target.Kind == ShapeKind.Ramp)
        {
            return "斜坡的方向很重要。";
        }

        return "看发光轮廓，把形状放到同一个格子。";
    }

    private int GetSolvedCount()
    {
        int count = 0;
        LevelData level = CurrentLevel;
        bool[] matchedPlayer = new bool[placedShapes.Count];
        for (int i = 0; i < level.Target.Count; i++)
        {
            for (int j = 0; j < placedShapes.Count; j++)
            {
                if (matchedPlayer[j])
                {
                    continue;
                }

                if (Matches(level.Target[i], placedShapes[j]))
                {
                    matchedPlayer[j] = true;
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    private void RefreshGhosts()
    {
        ClearRuntimeObjects(ghostRoot);
        ghostObjects.Clear();

        if (!blueprintVisible)
        {
            return;
        }

        for (int i = 0; i < CurrentLevel.Target.Count; i++)
        {
            TargetShape target = CurrentLevel.Target[i];
            if (HasMatchingPlacedShape(target))
            {
                continue;
            }

            GameObject ghost = CreateShapeObject(shapes[target.Kind], target.Cell, target.Rotation, ghostRoot, ghostMaterial, false);
            ghost.name = "Ghost " + shapes[target.Kind].Name;
            ghostObjects.Add(ghost);
        }
    }

    private bool HasMatchingPlacedShape(TargetShape target)
    {
        for (int i = 0; i < placedShapes.Count; i++)
        {
            if (Matches(target, placedShapes[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshProjectionViews()
    {
        if (topProjectionGrid == null || frontProjectionGrid == null || sideProjectionGrid == null)
        {
            return;
        }

        RefreshProjection(topProjectionGrid, 0);
        RefreshProjection(frontProjectionGrid, 1);
        RefreshProjection(sideProjectionGrid, 2);
    }

    private void RefreshProjection(RectTransform grid, int mode)
    {
        ClearUiChildren(grid);
        int cols = 9;
        int rows = 5;
        float cell = 28f;
        Vector2 start = new Vector2(-cols * cell * 0.5f + cell * 0.5f, -rows * cell * 0.5f + cell * 0.5f);

        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                Image bg = CreateImage(grid, "Projection Cell", new Vector2(0.5f, 0.5f), new Vector2(cell - 2f, cell - 2f), new Vector2(start.x + x * cell, start.y + y * cell), new Color(1f, 1f, 1f, 0.28f));
                bg.sprite = null;
            }
        }

        if (!blueprintVisible)
        {
            CreateText(grid, "?", 44, FontStyle.Bold, new Color(0.2f, 0.45f, 0.8f), new Vector2(0f, 0f), new Vector2(160f, 70f), TextAnchor.MiddleCenter);
            return;
        }

        for (int i = 0; i < CurrentLevel.Target.Count; i++)
        {
            TargetShape target = CurrentLevel.Target[i];
            int col;
            int row;
            if (mode == 0)
            {
                col = target.Cell.x + 4;
                row = target.Cell.z + 2;
            }
            else if (mode == 1)
            {
                col = target.Cell.x + 4;
                row = target.Cell.y;
            }
            else
            {
                col = target.Cell.z + 4;
                row = target.Cell.y;
            }

            if (col < 0 || col >= cols || row < 0 || row >= rows)
            {
                continue;
            }

            Color color = shapes[target.Kind].Color;
            color.a = 0.92f;
            Image mark = CreateImage(grid, "Projection Shape", new Vector2(0.5f, 0.5f), new Vector2(cell - 5f, cell - 5f), new Vector2(start.x + col * cell, start.y + row * cell), color);
            mark.sprite = softRoundedSprite;
        }
    }

    private void Undo()
    {
        if (undoStack.Count == 0)
        {
            return;
        }

        redoStack.Push(CaptureSnapshot());
        RestoreSnapshot(undoStack.Pop());
        RefreshGhosts();
        RefreshProjectionViews();
        RefreshUi();
        LogEvent("undo", "");
    }

    private void Redo()
    {
        if (redoStack.Count == 0)
        {
            return;
        }

        undoStack.Push(CaptureSnapshot());
        RestoreSnapshot(redoStack.Pop());
        RefreshGhosts();
        RefreshProjectionViews();
        RefreshUi();
        LogEvent("redo", "");
    }

    private void PushUndo()
    {
        undoStack.Push(CaptureSnapshot());
        while (undoStack.Count > 40)
        {
            List<ShapeSnapshot>[] snapshots = undoStack.ToArray();
            undoStack.Clear();
            for (int i = snapshots.Length - 2; i >= 0; i--)
            {
                undoStack.Push(snapshots[i]);
            }
        }
    }

    private List<ShapeSnapshot> CaptureSnapshot()
    {
        List<ShapeSnapshot> snapshot = new List<ShapeSnapshot>();
        for (int i = 0; i < placedShapes.Count; i++)
        {
            PlacedShape shape = placedShapes[i];
            ShapeSnapshot item = new ShapeSnapshot();
            item.Id = shape.Id;
            item.Kind = shape.Kind;
            item.Cell = shape.Cell;
            item.Rotation = shape.Rotation;
            snapshot.Add(item);
        }

        return snapshot;
    }

    private void RestoreSnapshot(List<ShapeSnapshot> snapshot)
    {
        ClearRuntimeObjects(buildRoot);
        placedShapes.Clear();
        remainingInventory.Clear();
        foreach (KeyValuePair<ShapeKind, int> pair in CurrentLevel.Inventory)
        {
            remainingInventory[pair.Key] = pair.Value;
        }

        for (int i = 0; i < snapshot.Count; i++)
        {
            ShapeSnapshot item = snapshot[i];
            PlacedShape placed = new PlacedShape();
            placed.Id = item.Id;
            placed.Kind = item.Kind;
            placed.Cell = item.Cell;
            placed.Rotation = item.Rotation;
            placed.View = CreateShapeObject(shapes[placed.Kind], placed.Cell, placed.Rotation, buildRoot, null, true);
            ShapeView[] views = placed.View.GetComponentsInChildren<ShapeView>(true);
            for (int v = 0; v < views.Length; v++)
            {
                views[v].Id = placed.Id;
            }

            placedShapes.Add(placed);
            remainingInventory[placed.Kind] = GetRemaining(placed.Kind) - 1;
        }
    }

    private void NextLevel()
    {
        int next = levelIndex + 1;
        if (next >= levels.Count)
        {
            next = 0;
        }

        LoadLevel(next);
    }

    private void PreviousLevel()
    {
        int previous = levelIndex - 1;
        if (previous < 0)
        {
            previous = levels.Count - 1;
        }

        LoadLevel(previous);
    }

    private GameObject CreateShapeObject(ShapeDefinition definition, Vector3Int cell, int rotation, Transform parent, Material overrideMaterial, bool interactive)
    {
        Transform root = new GameObject(definition.Name).transform;
        root.SetParent(parent, false);
        root.position = CellToWorld(cell);
        root.rotation = Quaternion.Euler(0f, NormalizeRotation(rotation) * 90f, 0f);

        Material material = overrideMaterial != null ? overrideMaterial : MakeMaterial(definition.Color, 0.16f, 0.56f);
        if (definition.Kind == ShapeKind.Cube || definition.Kind == ShapeKind.RectangularPrism || definition.Kind == ShapeKind.Plate)
        {
            CreateCuboid(root, definition.Size, material, interactive, definition.Kind != ShapeKind.Plate);
        }
        else if (definition.Kind == ShapeKind.Ramp)
        {
            CreateRamp(root, definition.Size, material, interactive);
        }
        else if (definition.Kind == ShapeKind.TriangularPrism)
        {
            CreateTriangularPrism(root, definition.Size, material, interactive);
        }
        else if (definition.Kind == ShapeKind.Cylinder)
        {
            CreateCylinderShape(root, definition.Size, material, interactive, rotation);
        }

        return root.gameObject;
    }

    private void CreateCuboid(Transform root, Vector3 size, Material material, bool interactive, bool studs)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(root, false);
        body.transform.localPosition = new Vector3(0f, size.y * CellSize * 0.5f, 0f);
        body.transform.localScale = new Vector3(size.x * CellSize, size.y * LayerHeight, size.z * CellSize);
        body.GetComponent<Renderer>().sharedMaterial = material;
        if (interactive)
        {
            body.AddComponent<ShapeView>();
        }
        else
        {
            Destroy(body.GetComponent<Collider>());
        }

        if (!studs)
        {
            return;
        }

        int studX = Mathf.Max(1, Mathf.RoundToInt(size.x));
        int studZ = Mathf.Max(1, Mathf.RoundToInt(size.z));
        float stepX = size.x * CellSize / studX;
        float stepZ = size.z * CellSize / studZ;
        for (int x = 0; x < studX; x++)
        {
            for (int z = 0; z < studZ; z++)
            {
                Vector3 local = new Vector3(-size.x * CellSize * 0.5f + stepX * (x + 0.5f), size.y * LayerHeight + 0.035f, -size.z * CellSize * 0.5f + stepZ * (z + 0.5f));
                CreateStud(root, local, material);
            }
        }
    }

    private void CreateStud(Transform parent, Vector3 localPosition, Material material)
    {
        GameObject stud = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stud.transform.SetParent(parent, false);
        stud.transform.localPosition = localPosition;
        stud.transform.localScale = new Vector3(0.18f, 0.035f, 0.18f);
        stud.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(stud.GetComponent<Collider>());
    }

    private void CreateRamp(Transform root, Vector3 size, Material material, bool interactive)
    {
        float w = size.x * CellSize;
        float h = size.y * LayerHeight;
        float d = size.z * CellSize;
        Mesh mesh = new Mesh();
        mesh.name = "Ramp Mesh";
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-w * 0.5f, 0f, -d * 0.5f),
            new Vector3(w * 0.5f, 0f, -d * 0.5f),
            new Vector3(-w * 0.5f, 0f, d * 0.5f),
            new Vector3(w * 0.5f, 0f, d * 0.5f),
            new Vector3(-w * 0.5f, h, d * 0.5f),
            new Vector3(w * 0.5f, h, d * 0.5f)
        };
        int[] triangles = new int[]
        {
            0, 2, 1, 1, 2, 3,
            2, 4, 3, 3, 4, 5,
            0, 1, 4, 1, 5, 4,
            0, 4, 2,
            1, 3, 5
        };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        GameObject ramp = new GameObject("Ramp Body");
        ramp.transform.SetParent(root, false);
        MeshFilter filter = ramp.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = ramp.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        BoxCollider collider = ramp.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, h * 0.5f, 0f);
        collider.size = new Vector3(w, h, d);
        if (interactive)
        {
            ramp.AddComponent<ShapeView>();
        }
        else
        {
            Destroy(collider);
        }
    }

    private void CreateTriangularPrism(Transform root, Vector3 size, Material material, bool interactive)
    {
        float w = size.x * CellSize;
        float h = size.y * LayerHeight;
        float d = size.z * CellSize;
        Mesh mesh = new Mesh();
        mesh.name = "Triangular Prism Mesh";
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-w * 0.5f, 0f, -d * 0.5f),
            new Vector3(-w * 0.5f, 0f, d * 0.5f),
            new Vector3(-w * 0.5f, h, 0f),
            new Vector3(w * 0.5f, 0f, -d * 0.5f),
            new Vector3(w * 0.5f, 0f, d * 0.5f),
            new Vector3(w * 0.5f, h, 0f)
        };
        int[] triangles = new int[]
        {
            0, 2, 1,
            3, 4, 5,
            0, 3, 2, 3, 5, 2,
            1, 2, 4, 2, 5, 4,
            0, 1, 3, 1, 4, 3
        };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        GameObject prism = new GameObject("Triangular Prism Body");
        prism.transform.SetParent(root, false);
        MeshFilter filter = prism.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = prism.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        BoxCollider collider = prism.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, h * 0.5f, 0f);
        collider.size = new Vector3(w, h, d);
        if (interactive)
        {
            prism.AddComponent<ShapeView>();
        }
        else
        {
            Destroy(collider);
        }
    }

    private void CreateCylinderShape(Transform root, Vector3 size, Material material, bool interactive, int rotation)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.transform.SetParent(root, false);
        cylinder.transform.localPosition = new Vector3(0f, size.y * LayerHeight * 0.5f, 0f);
        cylinder.transform.localScale = new Vector3(size.x * CellSize, size.y * LayerHeight * 0.5f, size.z * CellSize);
        if (NormalizeRotation(rotation) == 1)
        {
            cylinder.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        else if (NormalizeRotation(rotation) == 3)
        {
            cylinder.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        cylinder.GetComponent<Renderer>().sharedMaterial = material;
        if (interactive)
        {
            cylinder.AddComponent<ShapeView>();
        }
        else
        {
            Destroy(cylinder.GetComponent<Collider>());
        }
    }

    private void UpdatePlacedShapeView(PlacedShape placed)
    {
        if (placed.View == null)
        {
            return;
        }

        placed.View.transform.position = CellToWorld(placed.Cell);
        placed.View.transform.rotation = Quaternion.Euler(0f, NormalizeRotation(placed.Rotation) * 90f, 0f);
    }

    private void EnsureCursor()
    {
        EnsureCursor(selectedKind, selectedRotation);
    }

    private void EnsureCursor(ShapeKind kind, int rotation)
    {
        if (cursorView != null && cursorKind == kind && cursorRotation == NormalizeRotation(rotation))
        {
            return;
        }

        if (cursorView != null)
        {
            Destroy(cursorView);
        }

        cursorKind = kind;
        cursorRotation = NormalizeRotation(rotation);
        cursorView = CreateShapeObject(shapes[kind], Vector3Int.zero, cursorRotation, cursorRoot, validCursorMaterial, false);
        cursorView.name = "Placement Cursor";
        cursorView.SetActive(false);
    }

    private void HideCursor()
    {
        if (cursorView != null)
        {
            cursorView.SetActive(false);
        }
    }

    private void SetPreviewMaterial(Material material)
    {
        if (cursorView == null)
        {
            return;
        }

        Renderer[] renderers = cursorView.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sharedMaterial = material;
        }
    }

    private void SetViewAlpha(GameObject view, float alpha)
    {
        if (view == null)
        {
            return;
        }

        Renderer[] renderers = view.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].material;
            Color color = material.color;
            color.a = alpha;
            if (alpha < 0.99f)
            {
                SetupTransparentMaterial(material);
            }
            else
            {
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.DisableKeyword("_ALPHABLEND_ON");
                material.renderQueue = -1;
            }

            material.color = color;
        }
    }

    private void HandleCameraOrbit()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUi())
        {
            cameraDistance = Mathf.Clamp(cameraDistance - scroll * 0.55f, MinCameraDistance, MaxCameraDistance);
            ApplyOrbitCamera();
        }

        if (Input.GetMouseButtonDown(1) && !IsPointerOverUi())
        {
            orbitingCamera = true;
            orbitStartMouse = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(1))
        {
            orbitingCamera = false;
        }

        if (orbitingCamera)
        {
            Vector3 delta = Input.mousePosition - orbitStartMouse;
            orbitStartMouse = Input.mousePosition;
            cameraYaw += delta.x * 0.18f;
            cameraPitch = Mathf.Clamp(cameraPitch - delta.y * 0.14f, 24f, 68f);
            ApplyOrbitCamera();
        }
    }

    private void ApplyOrbitCamera()
    {
        if (mainCamera == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        Vector3 direction = rotation * Vector3.back;
        mainCamera.transform.position = cameraTarget + direction * cameraDistance;
        mainCamera.transform.LookAt(cameraTarget);
    }

    private void AnimateWorld()
    {
        float time = Time.time;
        if (lighthouseLight != null)
        {
            float pulse = 1f + Mathf.Sin(time * 3f) * 0.08f;
            lighthouseLight.localScale = new Vector3(0.37f * pulse, 0.16f, 0.37f * pulse);
        }

        if (robotRoot != null && !draggingExisting)
        {
            Vector3 position = robotRoot.position;
            position.y = 0.25f + Mathf.Sin(time * 2.4f) * 0.025f;
            robotRoot.position = position;
        }
    }

    private IEnumerator WalkRobotAcross()
    {
        Vector3 start = new Vector3(-4.4f, 0.25f, 1.15f);
        Vector3 bridgeStart = new Vector3(-3.2f, 1.24f, 0f);
        Vector3 bridgeEnd = new Vector3(3.2f, 1.24f, 0f);
        Vector3 end = new Vector3(4.4f, 0.25f, 1.15f);
        robotRoot.rotation = Quaternion.Euler(0f, 90f, 0f);
        yield return MoveRobot(start, bridgeStart, 1.2f);
        yield return MoveRobot(bridgeStart, bridgeEnd, 2.4f);
        yield return MoveRobot(bridgeEnd, end, 1.2f);
        robotRoot.rotation = Quaternion.Euler(0f, -40f, 0f);
    }

    private IEnumerator MoveRobot(Vector3 from, Vector3 to, float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / seconds);
            robotRoot.position = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI * 8f) * 0.025f;
            yield return null;
        }
    }

    private void FlashLighthouse()
    {
        if (lighthouseLight != null)
        {
            StartCoroutine(LighthousePulse());
        }
    }

    private IEnumerator LighthousePulse()
    {
        Material material = lighthouseLight.GetComponent<Renderer>().material;
        Color original = material.color;
        for (int i = 0; i < 8; i++)
        {
            material.color = Color.Lerp(original, Color.white, 0.55f);
            yield return new WaitForSeconds(0.08f);
            material.color = original;
            yield return new WaitForSeconds(0.08f);
        }
    }

    private int GetRemaining(ShapeKind kind)
    {
        int count;
        if (remainingInventory.TryGetValue(kind, out count))
        {
            return count;
        }

        return 0;
    }

    private bool HasShapeInBuild(ShapeKind kind)
    {
        for (int i = 0; i < placedShapes.Count; i++)
        {
            if (placedShapes[i].Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPointerOverUi()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        if (!UseCanvasUi && IsPointerOverImmediateHud())
        {
            return true;
        }

        return false;
    }

    private bool IsPointerOverImmediateHud()
    {
        PrepareImmediateHud();
        UpdateImmediateHudRects();
        Vector2 mouse = MouseDesignPosition();
        for (int i = 0; i < immediateHudRects.Count; i++)
        {
            if (immediateHudRects[i].Contains(mouse))
            {
                return true;
            }
        }

        return false;
    }

    private void LogEvent(string action, string detail)
    {
        SessionEvent sessionEvent = new SessionEvent();
        sessionEvent.Time = Time.timeSinceLevelLoad;
        sessionEvent.Level = CurrentLevel.Number;
        sessionEvent.Action = action;
        sessionEvent.Detail = detail;
        sessionEvents.Add(sessionEvent);
    }

    private static int NormalizeRotation(int rotation)
    {
        int value = rotation % 4;
        if (value < 0)
        {
            value += 4;
        }

        return value;
    }

    private Material MakeMaterial(Color color, float metallic, float smoothness)
    {
        Material material = new Material(Shader.Find("Standard"));
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        return material;
    }

    private Material MakeTransparentMaterial(Color color, float metallic, float smoothness)
    {
        Material material = MakeMaterial(color, metallic, smoothness);
        SetupTransparentMaterial(material);
        return material;
    }

    private void SetupTransparentMaterial(Material material)
    {
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

    private GameObject CreateCone(string name, float radius, float height, Material material)
    {
        GameObject cone = new GameObject(name);
        MeshFilter filter = cone.AddComponent<MeshFilter>();
        MeshRenderer renderer = cone.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        int segments = 28;
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 6];
        vertices[0] = new Vector3(0f, height, 0f);
        vertices[vertices.Length - 1] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = i == segments - 1 ? 1 : i + 2;
            triangles[t++] = 0;
            triangles[t++] = i + 1;
            triangles[t++] = next;
            triangles[t++] = vertices.Length - 1;
            triangles[t++] = next;
            triangles[t++] = i + 1;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        filter.sharedMesh = mesh;
        return cone;
    }

    private RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color, float alpha)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        Image image = gameObject.AddComponent<Image>();
        image.sprite = roundedSprite;
        Color actual = color;
        actual.a = alpha;
        image.color = actual;
        if (alpha > 0.01f)
        {
            Shadow shadow = gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.08f, 0.18f, 0.27f, 0.18f);
            shadow.effectDistance = new Vector2(0f, -4f);
        }

        return rect;
    }

    private Button CreateButton(RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick, Color background, Color textColor)
    {
        GameObject buttonObject = new GameObject(label + " Button");
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = softRoundedSprite;
        image.color = background;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(background, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(background, Color.gray, 0.12f);
        button.colors = colors;

        Text text = CreateStretchText(rect, label, 23, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
        text.raycastTarget = false;
        return button;
    }

    private Text CreateStretchText(RectTransform parent, string textValue, int size, FontStyle style, Color color, TextAnchor anchor)
    {
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = textObject.AddComponent<Text>();
        text.font = uiFont;
        text.text = textValue;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Text CreateText(RectTransform parent, string textValue, int size, FontStyle style, Color color, Vector2 anchoredPosition, Vector2 sizeDelta, TextAnchor anchor)
    {
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;

        Text text = textObject.AddComponent<Text>();
        text.font = uiFont;
        text.text = textValue;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Image CreateImage(RectTransform parent, string name, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = imageObject.AddComponent<Image>();
        image.sprite = softRoundedSprite;
        image.color = color;
        return image;
    }

    private void AddPointerEvent(GameObject target, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = target.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = type;
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    private Sprite CreateRoundedSprite(int radius)
    {
        int width = 96;
        int height = 96;
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.filterMode = FilterMode.Bilinear;
        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = Color.white;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float alpha = RoundedRectAlpha(x, y, width, height, radius);
                texture.SetPixel(x, y, Color.Lerp(clear, white, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 96f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private float RoundedRectAlpha(int x, int y, int width, int height, int radius)
    {
        int left = radius;
        int right = width - radius - 1;
        int bottom = radius;
        int top = height - radius - 1;
        int cx = Mathf.Clamp(x, left, right);
        int cy = Mathf.Clamp(y, bottom, top);
        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
        return Mathf.Clamp01(radius + 0.5f - distance);
    }

    private Sprite CreateShapeIconSprite(ShapeKind kind, Color color)
    {
        int width = 160;
        int height = 110;
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, clear);
            }
        }

        Color top = Color.Lerp(color, Color.white, 0.28f);
        Color front = color;
        Color side = Color.Lerp(color, Color.black, 0.16f);

        if (kind == ShapeKind.Cylinder)
        {
            FillRect(texture, 52, 30, 108, 72, front);
            FillEllipse(texture, 80, 72, 28, 12, top);
            FillEllipse(texture, 80, 30, 28, 12, side);
        }
        else if (kind == ShapeKind.Ramp)
        {
            FillPolygon(texture, new Vector2[] { new Vector2(40, 28), new Vector2(120, 28), new Vector2(120, 78) }, front);
            FillPolygon(texture, new Vector2[] { new Vector2(40, 28), new Vector2(120, 78), new Vector2(96, 92), new Vector2(18, 44) }, top);
            FillPolygon(texture, new Vector2[] { new Vector2(120, 28), new Vector2(142, 44), new Vector2(142, 90), new Vector2(120, 78) }, side);
        }
        else if (kind == ShapeKind.TriangularPrism)
        {
            FillPolygon(texture, new Vector2[] { new Vector2(32, 30), new Vector2(98, 30), new Vector2(65, 84) }, front);
            FillPolygon(texture, new Vector2[] { new Vector2(98, 30), new Vector2(132, 46), new Vector2(100, 96), new Vector2(65, 84) }, side);
            FillPolygon(texture, new Vector2[] { new Vector2(65, 84), new Vector2(100, 96), new Vector2(132, 46), new Vector2(98, 30) }, top);
        }
        else
        {
            float longScale = kind == ShapeKind.RectangularPrism ? 1.55f : (kind == ShapeKind.Plate ? 1.35f : 1f);
            float flatScale = kind == ShapeKind.Plate ? 0.42f : 1f;
            Vector2 p1 = new Vector2(48, 34);
            Vector2 p2 = new Vector2(48 + 48 * longScale, 34);
            Vector2 p3 = new Vector2(48 + 48 * longScale, 34 + 34 * flatScale);
            Vector2 p4 = new Vector2(48, 34 + 34 * flatScale);
            Vector2 offset = new Vector2(24, 18);
            FillPolygon(texture, new Vector2[] { p4, p3, p3 + offset, p4 + offset }, top);
            FillPolygon(texture, new Vector2[] { p1, p2, p3, p4 }, front);
            FillPolygon(texture, new Vector2[] { p2, p2 + offset, p3 + offset, p3 }, side);
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void FillRect(Texture2D texture, int left, int bottom, int right, int top, Color color)
    {
        for (int y = bottom; y <= top; y++)
        {
            for (int x = left; x <= right; x++)
            {
                SetPixelSafe(texture, x, y, color);
            }
        }
    }

    private void FillEllipse(Texture2D texture, int cx, int cy, int rx, int ry, Color color)
    {
        for (int y = cy - ry; y <= cy + ry; y++)
        {
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = (x - cx) / (float)rx;
                float dy = (y - cy) / (float)ry;
                if (dx * dx + dy * dy <= 1f)
                {
                    SetPixelSafe(texture, x, y, color);
                }
            }
        }
    }

    private void FillPolygon(Texture2D texture, Vector2[] points, Color color)
    {
        float minX = points[0].x;
        float maxX = points[0].x;
        float minY = points[0].y;
        float maxY = points[0].y;
        for (int i = 1; i < points.Length; i++)
        {
            minX = Mathf.Min(minX, points[i].x);
            maxX = Mathf.Max(maxX, points[i].x);
            minY = Mathf.Min(minY, points[i].y);
            maxY = Mathf.Max(maxY, points[i].y);
        }

        for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
        {
            for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
            {
                if (PointInPolygon(new Vector2(x + 0.5f, y + 0.5f), points))
                {
                    SetPixelSafe(texture, x, y, color);
                }
            }
        }
    }

    private bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y + 0.0001f) + polygon[i].x))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private void SetPixelSafe(Texture2D texture, int x, int y, Color color)
    {
        if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
        {
            texture.SetPixel(x, y, color);
        }
    }

    private void ClearRuntimeObjects(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private void ClearUiChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class SpatialSkillsGame : MonoBehaviour
{
    private const float BlockSpacing = 0.78f;
    private const float DragSensitivity = 0.35f;
    private const float MatchTolerance = 12f;

    private readonly List<Vector3Int[]> shapeLibrary = new List<Vector3Int[]>
    {
        new Vector3Int[]
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(2, 0, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 1, 0)
        },
        new Vector3Int[]
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(1, 1, 0),
            new Vector3Int(0, 1, 1)
        },
        new Vector3Int[]
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(2, 0, 0),
            new Vector3Int(1, 1, 0),
            new Vector3Int(1, 1, 1),
            new Vector3Int(2, 2, 1)
        },
        new Vector3Int[]
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(2, 0, 0),
            new Vector3Int(2, 1, 0),
            new Vector3Int(2, 1, 1),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 1, 1)
        },
        new Vector3Int[]
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(1, 1, 0),
            new Vector3Int(2, 1, 0),
            new Vector3Int(2, 1, 1),
            new Vector3Int(2, 2, 1),
            new Vector3Int(3, 2, 1)
        },
        new Vector3Int[]
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(1, 1, 0),
            new Vector3Int(1, 1, 1),
            new Vector3Int(2, 1, 1),
            new Vector3Int(2, 2, 1),
            new Vector3Int(2, 2, 2),
            new Vector3Int(3, 2, 2)
        }
    };

    private readonly Vector3[] targetEulerAngles =
    {
        new Vector3(0f, 90f, 0f),
        new Vector3(0f, 180f, 0f),
        new Vector3(90f, 0f, 0f),
        new Vector3(90f, 90f, 0f),
        new Vector3(0f, 90f, 90f),
        new Vector3(270f, 0f, 90f),
        new Vector3(180f, 270f, 0f),
        new Vector3(90f, 180f, 270f)
    };

    private Camera mainCamera;
    private Transform stageRoot;
    private Transform playerRoot;
    private Transform targetRoot;
    private Material playerMaterial;
    private Material targetMaterial;
    private Material solvedTargetMaterial;
    private Material baseMaterial;
    private Material floorMaterial;
    private Material gridMaterial;
    private Font uiFont;

    private Text titleText;
    private Text levelText;
    private Text scoreText;
    private Text timerText;
    private Text objectiveText;
    private Text statusText;
    private Button nextButton;

    private int levelIndex;
    private int score;
    private int attempts;
    private int matches;
    private float levelTimer;
    private bool solved;
    private bool dragging;
    private Vector3 lastMousePosition;
    private Quaternion targetRotation;

    private void Start()
    {
        Application.targetFrameRate = 60;
        SetupScene();
        BuildInterface();
        StartLevel(0);
    }

    private void Update()
    {
        if (playerRoot == null)
        {
            return;
        }

        levelTimer += Time.deltaTime;
        HandleDesktopInput();
        UpdateHud();
    }

    private void SetupScene()
    {
        playerMaterial = CreateMaterial("Player Blocks", new Color(1f, 0.36f, 0.18f, 1f), false);
        targetMaterial = CreateMaterial("Target Blocks", new Color(0.08f, 0.85f, 1f, 0.44f), true);
        solvedTargetMaterial = CreateMaterial("Solved Target Blocks", new Color(0.35f, 1f, 0.48f, 0.5f), true);
        baseMaterial = CreateMaterial("Base Plates", new Color(0.12f, 0.14f, 0.17f, 1f), false);
        floorMaterial = CreateMaterial("Floor", new Color(0.07f, 0.08f, 0.1f, 1f), false);
        gridMaterial = CreateLineMaterial("Grid Lines", new Color(0.36f, 0.42f, 0.46f, 0.32f));

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        mainCamera.transform.position = new Vector3(0f, 4.4f, -8.6f);
        mainCamera.transform.LookAt(new Vector3(0f, 0.25f, 0f));
        mainCamera.fieldOfView = 46f;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.035f, 0.045f, 0.06f, 1f);

        if (mainCamera.GetComponent<AudioListener>() == null)
        {
            mainCamera.gameObject.AddComponent<AudioListener>();
        }

        CreateLight("Key Light", LightType.Directional, new Vector3(-1.5f, 5f, -2f), Quaternion.Euler(48f, -34f, 0f), 1.1f);
        CreateLight("Soft Fill", LightType.Point, new Vector3(0f, 3.2f, -3f), Quaternion.identity, 2.2f);

        stageRoot = new GameObject("Runtime Stage").transform;
        CreateFloor();
        CreateBasePlate("Player Base", new Vector3(-2.45f, -0.58f, 0f));
        CreateBasePlate("Target Base", new Vector3(2.45f, -0.58f, 0f));
        CreateGrid();

        playerRoot = new GameObject("Player Model").transform;
        playerRoot.position = new Vector3(-2.45f, 0.15f, 0f);

        targetRoot = new GameObject("Target Model").transform;
        targetRoot.position = new Vector3(2.45f, 0.15f, 0f);
    }

    private void BuildInterface()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        GameObject canvasObject = new GameObject("Game HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        titleText = CreateText(canvasObject.transform, "Title", "Spatial Skills Game", 26, TextAnchor.UpperLeft, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(330f, 40f));
        levelText = CreateText(canvasObject.transform, "Level", "", 18, TextAnchor.UpperLeft, new Color(0.73f, 0.82f, 0.88f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(25f, -58f), new Vector2(260f, 30f));
        objectiveText = CreateText(canvasObject.transform, "Objective", "Match the cyan target.", 22, TextAnchor.UpperCenter, Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(480f, 36f));
        scoreText = CreateText(canvasObject.transform, "Score", "", 20, TextAnchor.UpperRight, Color.white, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(270f, 34f));
        timerText = CreateText(canvasObject.transform, "Timer", "", 17, TextAnchor.UpperRight, new Color(0.73f, 0.82f, 0.88f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -56f), new Vector2(310f, 30f));
        statusText = CreateText(canvasObject.transform, "Status", "", 22, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(720f, 44f));

        CreateButton(canvasObject.transform, "Reset", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-204f, 28f), new Vector2(116f, 44f), ResetPlayerRotation);
        CreateButton(canvasObject.transform, "Snap", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-68f, 28f), new Vector2(116f, 44f), SnapPlayerRotation);
        CreateButton(canvasObject.transform, "Check", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(68f, 28f), new Vector2(116f, 44f), CheckMatch);
        nextButton = CreateButton(canvasObject.transform, "Next", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(204f, 28f), new Vector2(116f, 44f), AdvanceLevel);
    }

    private void StartLevel(int requestedIndex)
    {
        levelIndex = requestedIndex % shapeLibrary.Count;
        solved = false;
        levelTimer = 0f;
        playerRoot.rotation = Quaternion.identity;
        targetRotation = Quaternion.Euler(targetEulerAngles[Random.Range(0, targetEulerAngles.Length)]);
        targetRoot.rotation = targetRotation;

        ClearChildren(playerRoot);
        ClearChildren(targetRoot);
        BuildShape(playerRoot, shapeLibrary[levelIndex], playerMaterial);
        BuildShape(targetRoot, shapeLibrary[levelIndex], targetMaterial);

        SetStatus("Match the cyan target.");
        nextButton.interactable = false;
        UpdateHud();
    }

    private void HandleDesktopInput()
    {
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUi())
        {
            dragging = true;
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            dragging = false;
        }

        if (dragging && Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            lastMousePosition = Input.mousePosition;
            playerRoot.Rotate(mainCamera.transform.up, -delta.x * DragSensitivity, Space.World);
            playerRoot.Rotate(mainCamera.transform.right, delta.y * DragSensitivity, Space.World);
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            mainCamera.fieldOfView = Mathf.Clamp(mainCamera.fieldOfView - scroll * 2f, 34f, 58f);
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            RotatePlayer(Vector3.up, 90f);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            RotatePlayer(Vector3.up, -90f);
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            RotatePlayer(Vector3.right, 90f);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            RotatePlayer(Vector3.right, -90f);
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            RotatePlayer(Vector3.forward, 90f);
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            RotatePlayer(Vector3.forward, -90f);
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            ResetPlayerRotation();
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            SnapPlayerRotation();
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            CheckMatch();
        }
        else if (Input.GetKeyDown(KeyCode.N) && solved)
        {
            AdvanceLevel();
        }
    }

    private void CheckMatch()
    {
        if (solved)
        {
            SetStatus("Matched.");
            return;
        }

        attempts++;
        float angle = Quaternion.Angle(playerRoot.rotation, targetRotation);

        if (angle <= MatchTolerance)
        {
            solved = true;
            matches++;
            int timeBonus = Mathf.Max(0, 60 - Mathf.RoundToInt(levelTimer));
            int attemptBonus = attempts == matches ? 30 : 0;
            int points = 100 + timeBonus + attemptBonus;
            score += points;
            ApplyMaterial(targetRoot, solvedTargetMaterial);
            nextButton.interactable = true;
            SetStatus("Matched +" + points);
        }
        else
        {
            score = Mathf.Max(0, score - 5);
            string feedback = angle < 30f ? "Close: " : "Offset: ";
            SetStatus(feedback + Mathf.RoundToInt(angle) + " degrees");
        }

        UpdateHud();
    }

    private void AdvanceLevel()
    {
        StartLevel(levelIndex + 1);
    }

    private void ResetPlayerRotation()
    {
        playerRoot.rotation = Quaternion.identity;
        SetStatus("Reset.");
    }

    private void SnapPlayerRotation()
    {
        Vector3 euler = playerRoot.eulerAngles;
        playerRoot.rotation = Quaternion.Euler(RoundToRightAngle(euler.x), RoundToRightAngle(euler.y), RoundToRightAngle(euler.z));
        SetStatus("Snapped.");
    }

    private void RotatePlayer(Vector3 axis, float degrees)
    {
        playerRoot.Rotate(axis, degrees, Space.World);
    }

    private void BuildShape(Transform root, Vector3Int[] cells, Material material)
    {
        Vector3 center = GetShapeCenter(cells);

        for (int i = 0; i < cells.Length; i++)
        {
            Vector3Int cell = cells[i];
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Block " + i;
            cube.transform.SetParent(root, false);
            cube.transform.localPosition = (new Vector3(cell.x, cell.y, cell.z) - center) * BlockSpacing;
            cube.transform.localScale = Vector3.one * 0.72f;

            Renderer renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private Vector3 GetShapeCenter(Vector3Int[] cells)
    {
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < cells.Length; i++)
        {
            Vector3 point = new Vector3(cells[i].x, cells[i].y, cells[i].z);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        return (min + max) * 0.5f;
    }

    private void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private void ApplyMaterial(Transform root, Material material)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sharedMaterial = material;
        }
    }

    private void CreateFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(stageRoot, false);
        floor.transform.position = new Vector3(0f, -0.68f, 0f);
        floor.transform.localScale = new Vector3(8.8f, 0.08f, 4.8f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
    }

    private void CreateBasePlate(string name, Vector3 position)
    {
        GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plate.name = name;
        plate.transform.SetParent(stageRoot, false);
        plate.transform.position = position;
        plate.transform.localScale = new Vector3(1.5f, 0.04f, 1.5f);
        plate.GetComponent<Renderer>().sharedMaterial = baseMaterial;
    }

    private void CreateGrid()
    {
        for (int i = -5; i <= 5; i++)
        {
            float coordinate = i * 0.8f;
            CreateGridLine("Grid X " + i, new Vector3(-4.2f, -0.61f, coordinate), new Vector3(4.2f, -0.61f, coordinate));
            CreateGridLine("Grid Z " + i, new Vector3(coordinate, -0.61f, -2.1f), new Vector3(coordinate, -0.61f, 2.1f));
        }
    }

    private void CreateGridLine(string name, Vector3 start, Vector3 end)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(stageRoot, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = 0.012f;
        line.endWidth = 0.012f;
        line.material = gridMaterial;
        line.startColor = gridMaterial.color;
        line.endColor = gridMaterial.color;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
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

    private Button CreateButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, UnityAction action)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.16f, 0.2f, 0.92f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.16f, 0.2f, 0.92f);
        colors.highlightedColor = new Color(0.18f, 0.28f, 0.34f, 0.96f);
        colors.pressedColor = new Color(0.08f, 0.56f, 0.67f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.08f, 0.09f, 0.1f, 0.42f);
        button.colors = colors;

        CreateText(buttonObject.transform, label + " Label", label, 17, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return button;
    }

    private Text CreateText(Transform parent, string name, string value, int size, TextAnchor alignment, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = GetUiFont();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(10, size - 6);
        text.resizeTextMaxSize = size;
        return text;
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

    private void UpdateHud()
    {
        int accuracy = attempts == 0 ? 100 : Mathf.RoundToInt((matches * 100f) / attempts);
        titleText.text = "Spatial Skills Game";
        levelText.text = "Level " + (levelIndex + 1) + " / " + shapeLibrary.Count;
        scoreText.text = "Score " + score;
        timerText.text = "Accuracy " + accuracy + "%   Time " + Mathf.FloorToInt(levelTimer) + "s";
        objectiveText.text = solved ? "Great match." : "Match the cyan target.";
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private float RoundToRightAngle(float value)
    {
        return Mathf.Round(value / 90f) * 90f;
    }
}

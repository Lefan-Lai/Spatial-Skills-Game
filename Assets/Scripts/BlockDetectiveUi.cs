using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed partial class BlockDetectiveGame
{
    private void BuildInterface()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        GameObject canvasObject = new GameObject("Block Detective HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440f, 900f);
        scaler.matchWidthOrHeight = 0.5f;

        CreatePanel(canvasObject.transform, "Top Bar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 92f), new Color(0.035f, 0.045f, 0.055f, 0.96f));
        CreatePanel(canvasObject.transform, "Evidence Panel", new Vector2(0f, 0f), new Vector2(0.29f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, -8f), new Vector2(-24f, -188f), new Color(0.07f, 0.09f, 0.105f, 0.92f));
        CreatePanel(canvasObject.transform, "Tutor Panel", new Vector2(0.71f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-18f, -8f), new Vector2(-24f, -188f), new Color(0.065f, 0.08f, 0.095f, 0.92f));
        CreatePanel(canvasObject.transform, "Bottom Bar", new Vector2(0.29f, 0f), new Vector2(0.71f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(-18f, 162f), new Color(0.045f, 0.055f, 0.065f, 0.94f));

        titleText = CreateText(canvasObject.transform, "Title", "Block Detective 2.0", 30, TextAnchor.UpperLeft, Color.white, new Vector2(0f, 1f), new Vector2(0.44f, 1f), new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(-48f, 38f));
        metaText = CreateText(canvasObject.transform, "Meta", "", 17, TextAnchor.UpperLeft, new Color(0.68f, 0.78f, 0.84f, 1f), new Vector2(0f, 1f), new Vector2(0.55f, 1f), new Vector2(0f, 1f), new Vector2(25f, -54f), new Vector2(-44f, 28f));
        scoreText = CreateText(canvasObject.transform, "Score", "", 18, TextAnchor.UpperRight, Color.white, new Vector2(0.56f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(-48f, 32f));

        CreateText(canvasObject.transform, "Evidence Header", "Evidence Board", 22, TextAnchor.UpperLeft, Color.white, new Vector2(0f, 1f), new Vector2(0.29f, 1f), new Vector2(0f, 1f), new Vector2(34f, -118f), new Vector2(-64f, 34f));
        evidenceText = CreateText(canvasObject.transform, "Evidence Text", "", 15, TextAnchor.UpperLeft, new Color(0.84f, 0.9f, 0.92f, 1f), new Vector2(0f, 0f), new Vector2(0.29f, 1f), new Vector2(0f, 1f), new Vector2(34f, -158f), new Vector2(-64f, -256f));

        builderText = CreateText(canvasObject.transform, "Builder Header", "", 20, TextAnchor.UpperCenter, Color.white, new Vector2(0.29f, 1f), new Vector2(0.71f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(-24f, 58f));

        CreateText(canvasObject.transform, "Tutor Header", "Tutor + Teach Cube", 22, TextAnchor.UpperLeft, Color.white, new Vector2(0.71f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(24f, -118f), new Vector2(-64f, 32f));
        tutorText = CreateText(canvasObject.transform, "Tutor Text", "", 15, TextAnchor.UpperLeft, new Color(0.84f, 0.9f, 0.92f, 1f), new Vector2(0.71f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(24f, -156f), new Vector2(-64f, -548f));
        diagnosticText = CreateText(canvasObject.transform, "Diagnostic Text", "", 14, TextAnchor.UpperLeft, new Color(0.94f, 0.86f, 0.62f, 1f), new Vector2(0.71f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(24f, 222f), new Vector2(-64f, 112f));
        cubeText = CreateText(canvasObject.transform, "Cube Text", "", 14, TextAnchor.UpperLeft, new Color(0.75f, 0.92f, 1f, 1f), new Vector2(0.71f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(24f, 100f), new Vector2(-64f, 96f));
        loadText = CreateText(canvasObject.transform, "Load Text", "", 13, TextAnchor.UpperRight, new Color(0.76f, 0.84f, 0.88f, 1f), new Vector2(0.56f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -56f), new Vector2(-48f, 26f));
        statusText = CreateText(canvasObject.transform, "Status Text", "", 15, TextAnchor.LowerCenter, Color.white, new Vector2(0.29f, 0f), new Vector2(0.71f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 148f), new Vector2(-36f, 26f));
        cursorText = CreateText(canvasObject.transform, "Cursor Text", "", 14, TextAnchor.UpperCenter, new Color(0.9f, 0.94f, 0.95f, 1f), new Vector2(0.29f, 0f), new Vector2(0.71f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(-36f, 22f));

        teachInput = CreateInputField(canvasObject.transform, "Teach Input", "Teach Cube a spatial rule after feedback...", new Vector2(0.71f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(-172f, 48f));
        CreateButton(canvasObject.transform, "Teach", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 24f), new Vector2(126f, 48f), TeachCube);

        AddBuilderButtons(canvasObject.transform);
    }

    private void AddBuilderButtons(Transform parent)
    {
        float y1 = 54f;
        float y2 = 94f;
        float y3 = 16f;
        CreateButton(parent, "X-", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-306f, y2), new Vector2(58f, 30f), delegate { MoveCursor(-1, 0, 0); });
        CreateButton(parent, "X+", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-242f, y2), new Vector2(58f, 30f), delegate { MoveCursor(1, 0, 0); });
        CreateButton(parent, "Y-", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-178f, y2), new Vector2(58f, 30f), delegate { MoveCursor(0, -1, 0); });
        CreateButton(parent, "Y+", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-114f, y2), new Vector2(58f, 30f), delegate { MoveCursor(0, 1, 0); });
        CreateButton(parent, "Z-", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-50f, y2), new Vector2(58f, 30f), delegate { MoveCursor(0, 0, -1); });
        CreateButton(parent, "Z+", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(14f, y2), new Vector2(58f, 30f), delegate { MoveCursor(0, 0, 1); });
        CreateButton(parent, "Add", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(92f, y2), new Vector2(72f, 30f), AddBlockAtCursor);
        CreateButton(parent, "Remove", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(180f, y2), new Vector2(86f, 30f), RemoveBlockAtCursor);
        CreateButton(parent, "Submit", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(282f, y2), new Vector2(94f, 30f), SubmitAnswer);

        CreateButton(parent, "Front", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-286f, y1), new Vector2(76f, 30f), delegate { ApplyCameraView(DetectiveView.Front); });
        CreateButton(parent, "Right", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-202f, y1), new Vector2(76f, 30f), delegate { ApplyCameraView(DetectiveView.Right); });
        CreateButton(parent, "Top", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-118f, y1), new Vector2(76f, 30f), delegate { ApplyCameraView(DetectiveView.Top); });
        CreateButton(parent, "Free", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-34f, y1), new Vector2(76f, 30f), delegate { ApplyCameraView(DetectiveView.Free); });
        CreateButton(parent, "Witness", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(62f, y1), new Vector2(98f, 30f), delegate { ApplyCameraView(DetectiveView.Witness); });
        CreateButton(parent, "Hint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(168f, y1), new Vector2(76f, 30f), ShowHint);
        CreateButton(parent, "Reset", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(252f, y1), new Vector2(76f, 30f), ResetBuild);
        nextButton = CreateButton(parent, "Next", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(336f, y1), new Vector2(76f, 30f), NextCase);

        CreateButton(parent, "Effort 1", new Vector2(0.29f, 0f), new Vector2(0.29f, 0f), new Vector2(0f, 0f), new Vector2(20f, y3), new Vector2(88f, 26f), delegate { SetMentalEffort(1); });
        CreateButton(parent, "Effort 2", new Vector2(0.29f, 0f), new Vector2(0.29f, 0f), new Vector2(0f, 0f), new Vector2(114f, y3), new Vector2(88f, 26f), delegate { SetMentalEffort(2); });
        CreateButton(parent, "Effort 3", new Vector2(0.29f, 0f), new Vector2(0.29f, 0f), new Vector2(0f, 0f), new Vector2(208f, y3), new Vector2(88f, 26f), delegate { SetMentalEffort(3); });
    }

    private void UpdateHud()
    {
        if (cases.Count == 0 || titleText == null)
        {
            return;
        }

        CaseData data = cases[caseIndex];
        titleText.text = "Block Detective 2.0";
        metaText.text = data.Chapter + "  |  " + data.Mode + "  |  " + data.Id;
        scoreText.text = "Score " + score + "   Solved " + solvedCases + "/" + cases.Count + "   Time " + Mathf.FloorToInt(Time.time - caseStartTime) + "s";
        builderText.text = data.Title + "\n" + data.Brief;
        cursorText.text = "Cursor x" + cursorCell.x + " y" + cursorCell.y + " z" + cursorCell.z + " | WASD move | Q/E height | Space toggle | Enter submit";
        loadText.text = "Cognitive load: " + EstimateLoadState() + "   Effort " + mentalEffort + "   Actions " + actionsThisCase + "   View switches " + viewSwitchesThisCase;
    }

    private void UpdateEvidenceBoard()
    {
        if (evidenceText == null || cases.Count == 0)
        {
            return;
        }

        CaseData data = cases[caseIndex];
        HashSet<Vector3Int> target = new HashSet<Vector3Int>(data.Target);
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(data.Brief);
        builder.AppendLine();
        builder.AppendLine("Prediction before action");
        builder.AppendLine(data.PredictionPrompt);
        builder.AppendLine();

        if (data.ShowFront)
        {
            builder.AppendLine("Target front view");
            builder.AppendLine(ProjectionToString(GetFrontProjection(target)));
        }

        if (data.ShowRight)
        {
            builder.AppendLine("Target right view");
            builder.AppendLine(ProjectionToString(GetRightProjection(target)));
        }

        builder.AppendLine("Target top view");
        builder.AppendLine(data.ShowTop ? ProjectionToString(GetTopProjection(target)) : "[withheld evidence]\n");
        builder.AppendLine("Current model");
        builder.AppendLine("Blocks: " + userVoxels.Count + " / target " + data.Target.Length);
        builder.AppendLine("Your top footprint");
        builder.AppendLine(ProjectionToString(GetTopProjection(userVoxels)));
        builder.AppendLine("Witness note");
        builder.AppendLine(data.WitnessNote);
        evidenceText.text = builder.ToString();
    }

    private string GenerateOpeningBrief()
    {
        CaseData data = cases[caseIndex];
        return "Case loop\n" +
               "1. Inspect evidence\n" +
               "2. Predict the hidden relation\n" +
               "3. Build a voxel model\n" +
               "4. Submit for geometry diagnosis\n" +
               "5. Teach Cube a rule\n\n" +
               "Spatial vocabulary: " + data.Vocabulary;
    }

    private string FormatDiagnostic(DiagnosticResult result)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Geometry diagnostic");
        builder.AppendLine("Result: " + (result.Passed ? "solved" : "not solved"));
        builder.AppendLine("Error: " + FormatErrorType(result.ErrorType));
        builder.AppendLine("Matched: " + JoinOrNone(result.MatchedViews));
        builder.AppendLine("Mismatched: " + JoinOrNone(result.MismatchedViews));
        builder.AppendLine("Engine facts:");

        for (int i = 0; i < result.EngineFacts.Count; i++)
        {
            builder.AppendLine("- " + result.EngineFacts[i]);
        }

        return builder.ToString();
    }

    private string GenerateTutorFeedback(DiagnosticResult result)
    {
        LoadState state = EstimateLoadState();
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Feedback policy: " + state);

        if (result.Passed)
        {
            builder.AppendLine("You matched the voxel structure. Now turn the successful move into a rule Cube can reuse.");
            builder.AppendLine("Self-explain: which view prevented the most likely mistake?");
            return builder.ToString();
        }

        if (state == LoadState.Overload)
        {
            builder.AppendLine("Short version: fix one view at a time. Do not rebuild everything.");
        }

        builder.AppendLine(FeedbackForError(result.ErrorType));
        builder.AppendLine("Prompt: explain the mistake as a rule, not as a guess.");
        return builder.ToString();
    }

    private string GenerateCubePrompt(DiagnosticResult result)
    {
        if (result.Passed)
        {
            return "Cube: I watched your solution. Teach me the rule I should use on the next hidden case.";
        }

        switch (result.ErrorType)
        {
            case ErrorType.FrontBackReverse:
                return "Cube: I also thought a correct front view was enough. Why should I check right or top view for depth?";
            case ErrorType.LeftRightMirror:
                return "Cube: I mirrored the answer. How do I avoid swapping left and right from another viewpoint?";
            case ErrorType.HeightError:
                return "Cube: My footprint looked right. How should I check column height without getting lost?";
            case ErrorType.MissingBlock:
                return "Cube: I left a gap. Which projection row should I compare first?";
            case ErrorType.ExtraBlock:
                return "Cube: I added too much. How do I decide which block does not belong?";
            default:
                return "Cube: Please teach me a spatial rule from this diagnostic.";
        }
    }

    private string FeedbackForError(ErrorType errorType)
    {
        switch (errorType)
        {
            case ErrorType.LeftRightMirror:
                return "The model is a left-right mirror. Keep height and depth, then swap the x direction.\nSpatial language: left and right depend on the viewer's position.";
            case ErrorType.FrontBackReverse:
                return "The front view can hide depth. Width and height are close, but front-back order is reversed.\nStrategy: check right view or top view before changing height.";
            case ErrorType.HeightError:
                return "The footprint is right, so positions are mostly correct. Check column heights layer by layer.";
            case ErrorType.MissingBlock:
                return "The answer is missing at least one block. Find the first target # that your projection does not produce.";
            case ErrorType.ExtraBlock:
                return "There is at least one extra block. Remove blocks that create marks not present in target projections.";
            case ErrorType.ProjectionAmbiguity:
                return "This is a reasonable shadow match, but not the exact structure. Two 2D projections can be insufficient.";
            default:
                return "The mismatch is mixed. Choose one projection, fix it, then submit again.";
        }
    }

    private string HintForError(ErrorType errorType)
    {
        switch (errorType)
        {
            case ErrorType.FrontBackReverse:
                return "Do not change height yet. Compare top/right to decide which blocks are in front or behind.";
            case ErrorType.LeftRightMirror:
                return "Keep the same columns, but test whether x positions are reversed.";
            case ErrorType.HeightError:
                return "The footprint is useful. Raise or lower one column at a time.";
            case ErrorType.MissingBlock:
                return "Look for a target # that your projection does not produce.";
            case ErrorType.ExtraBlock:
                return "Find a user # that is absent from the target view, then remove the block that causes it.";
            default:
                return "Choose one projection and make it match before working on the next.";
        }
    }

    private LoadState EstimateLoadState()
    {
        float elapsed = Time.time - caseStartTime;
        float load = 0.15f;
        load += mentalEffort == 3 ? 0.36f : mentalEffort == 2 ? 0.18f : 0.05f;
        load += Mathf.Clamp01(elapsed / 180f) * 0.2f;
        load += Mathf.Clamp01(attempts / 4f) * 0.18f;
        load += Mathf.Clamp01(hintsUsed / 3f) * 0.12f;
        load += Mathf.Clamp01(actionsThisCase / 36f) * 0.14f;
        load += Mathf.Clamp01(viewSwitchesThisCase / 18f) * 0.08f;

        if (load >= 0.82f)
        {
            return LoadState.Overload;
        }

        if (load >= 0.62f)
        {
            return LoadState.High;
        }

        return load >= 0.32f ? LoadState.Optimal : LoadState.Low;
    }

    private int ScoreRuleQuality(string rule, ErrorType errorType)
    {
        string lower = rule.ToLowerInvariant();
        int value = 20;

        if (ContainsAny(lower, "front", "right", "top", "side", "view", "projection"))
        {
            value += 24;
        }

        if (ContainsAny(lower, "depth", "front-back", "height", "left", "right", "mirror", "column", "above", "below"))
        {
            value += 24;
        }

        if (ContainsAny(lower, "check", "compare", "first", "then", "before", "because"))
        {
            value += 18;
        }

        if (errorType == ErrorType.FrontBackReverse && ContainsAny(lower, "depth", "front-back", "right", "top"))
        {
            value += 14;
        }
        else if (errorType == ErrorType.LeftRightMirror && ContainsAny(lower, "mirror", "left", "right"))
        {
            value += 14;
        }
        else if (errorType == ErrorType.HeightError && ContainsAny(lower, "height", "column", "layer", "above"))
        {
            value += 14;
        }

        return Mathf.Clamp(value, 0, 100);
    }

    private string ExtractCondition(string rule)
    {
        string lower = rule.ToLowerInvariant();
        if (ContainsAny(lower, "front") && ContainsAny(lower, "right", "side"))
        {
            return "front evidence must be checked against side evidence";
        }

        if (ContainsAny(lower, "top"))
        {
            return "top evidence is needed to verify footprint/depth";
        }

        if (ContainsAny(lower, "height", "column", "layer"))
        {
            return "matching footprint is not enough; column height must be checked";
        }

        return "diagnostic facts choose the next view to inspect";
    }

    private string ExtractStrategy(string rule, ErrorType errorType)
    {
        string lower = rule.ToLowerInvariant();
        if (errorType == ErrorType.FrontBackReverse)
        {
            return "check right/top view for front-back depth before rebuilding";
        }

        if (errorType == ErrorType.LeftRightMirror)
        {
            return "confirm viewer position before deciding left and right";
        }

        if (errorType == ErrorType.HeightError)
        {
            return "compare columns from bottom layer upward";
        }

        return ContainsAny(lower, "remove", "extra")
            ? "remove blocks that create extra projection marks"
            : "compare one projection at a time, then revise locally";
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

    private string FormatErrorType(ErrorType errorType)
    {
        switch (errorType)
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

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private Image CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Button CreateButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, UnityAction action)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        ConfigureRect(buttonObject.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.13f, 0.17f, 0.2f, 0.96f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.13f, 0.17f, 0.2f, 0.96f);
        colors.highlightedColor = new Color(0.22f, 0.32f, 0.38f, 1f);
        colors.pressedColor = new Color(0.08f, 0.58f, 0.66f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.08f, 0.09f, 0.1f, 0.42f);
        button.colors = colors;

        CreateText(buttonObject.transform, label + " Label", label, 14, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return button;
    }

    private Text CreateText(Transform parent, string name, string value, int size, TextAnchor alignment, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        ConfigureRect(textObject.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = GetUiFont();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(10, size - 5);
        text.resizeTextMaxSize = size;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject fieldObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        fieldObject.transform.SetParent(parent, false);
        ConfigureRect(fieldObject.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        fieldObject.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.05f, 0.95f);

        Text text = CreateText(fieldObject.transform, "Text", "", 14, TextAnchor.UpperLeft, Color.white, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(8f, -6f), new Vector2(-16f, -12f));
        text.raycastTarget = true;
        Text placeholderText = CreateText(fieldObject.transform, "Placeholder", placeholder, 13, TextAnchor.MiddleLeft, new Color(0.58f, 0.65f, 0.7f, 0.85f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(8f, 0f), new Vector2(-16f, -10f));

        InputField input = fieldObject.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholderText;
        input.lineType = InputField.LineType.MultiLineNewline;
        input.characterLimit = 220;
        return input;
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
}

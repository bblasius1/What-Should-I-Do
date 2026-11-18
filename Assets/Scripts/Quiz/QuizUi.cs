using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class QuizUi : MonoBehaviour
{
    const int EnglishPromptSize = 52;
    const int ChinesePromptSize = 43;
    internal const int MaxAnswers = 3;

    static readonly Color Ink = new Color32(24, 49, 53, 255);
    static readonly Color Muted = new Color32(70, 91, 87, 255);
    static readonly Color Teal = new Color32(27, 97, 78, 255);
    static readonly Color Mint = new Color32(211, 239, 224, 255);
    static readonly Color Paper = new Color32(255, 250, 238, 255);
    static readonly Color AnswerColor = new Color32(234, 242, 234, 255);
    static readonly Color Dim = new Color32(13, 33, 40, 167);
    static readonly Color Warning = new Color32(158, 55, 34, 255);

    readonly Button[] answerButtons = new Button[MaxAnswers];
    readonly Text[] answerLabels = new Text[MaxAnswers];
    Font regularFont;
    Font boldFont;
    Sprite rounded;
    Canvas canvas;
    Image transitionImage;
    readonly List<(Text Label, string Source, FontStyle Style)> labels = new();
    GameObject hud;
    GameObject questionLayer;
    GameObject winLayer;
    RectTransform waypoint;
    Text waypointText;
    Text hudHeading;
    Text objectiveText;
    Text objectiveHint;
    Text questionHeading;
    Text progressText;
    Text promptText;
    Text feedbackText;
    Text resultTitle;
    Text resultSummary;
    Text resultStars;
    Text resultRating;
    Button restartButton;
    Button mainMenuButton;
    Button nextGameButton;
    EventSystem eventSystem;
    Action<int> answerCallback;
    Action restartCallback;
    Action mainMenuCallback;
    Action nextGameCallback;

    public static QuizUi Create(Transform parent)
    {
        var host = new GameObject("Crossing UI");
        host.transform.SetParent(parent, false);
        var ui = host.AddComponent<QuizUi>();
        ui.Build();
        return ui;
    }

    void Build()
    {
        regularFont = Resources.Load<Font>("Fonts/LiberationSans-Regular");
        boldFont = Resources.Load<Font>("Fonts/LiberationSans-Bold");
        rounded = BuildRoundedSprite();

        var canvasObject = NewUiObject("Crossing Canvas", transform);
        canvas = canvasObject.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasObject.gameObject.AddComponent<GraphicRaycaster>();

        eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            var events = new GameObject("Crossing Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
            eventSystem = events.GetComponent<EventSystem>();
        }

        BuildHud(canvasObject);
        BuildQuestionLayer(canvasObject);
        BuildWinLayer(canvasObject);
        BuildTransition();
        GameLanguage.Changed += RefreshLanguage;

        questionLayer.SetActive(false);
        winLayer.SetActive(false);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (restartCallback != null && keyboard.rKey.wasPressedThisFrame)
        {
            restartCallback.Invoke();
            return;
        }

        if (answerCallback == null)
            return;

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            Answer(0);
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            Answer(1);
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            Answer(2);
    }

    public void ShowObjective(string objective, string hint)
    {
        objectiveText.text = GameLanguage.Text(objective);
        objectiveHint.text = GameLanguage.Text(hint);
    }

    internal void SetLevelHeading(GameLevel level)
    {
        string source = level switch
        {
            GameLevel.School => "GET HELP AT THE SCHOOL GATE",
            GameLevel.Playground => "MAKE ROOM FOR EVERYONE",
            _ => "WHAT SHOULD I DO?",
        };
        hudHeading.text = GameLanguage.Text(source);
    }

    internal void SetTransitionOpacity(float opacity)
    {
        transitionImage.transform.parent.gameObject.SetActive(opacity > 0f);
        transitionImage.color = new Color(0.035f, 0.095f, 0.10f, opacity);
    }

    void BuildTransition()
    {
        var layer = NewUiObject("Level transition", transform);
        var fadeCanvas = layer.gameObject.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 200;
        layer.gameObject.AddComponent<GraphicRaycaster>();
        var overlay = NewUiObject("Screen fade", layer);
        Stretch(overlay);
        transitionImage = overlay.gameObject.AddComponent<Image>();
        SetTransitionOpacity(0f);
    }

    public void ShowWaypoint(Camera camera, Vector3 position, string label)
    {
        if (camera == null)
        {
            waypoint.gameObject.SetActive(false);
            return;
        }

        Vector3 screen = camera.WorldToScreenPoint(position);
        Vector2 mark = screen;
        if (screen.z <= 0f)
            mark = new Vector2(Screen.width - mark.x, 0f);

        var canvas = (RectTransform)waypoint.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, mark, null, out Vector2 local);
        Rect bounds = canvas.rect;
        waypoint.anchoredPosition = new Vector2(
            Mathf.Clamp(local.x, bounds.xMin + 165f, bounds.xMax - 165f),
            Mathf.Clamp(local.y, bounds.yMin + 90f, bounds.yMax - 210f));
        waypointText.text = GameLanguage.Text(label);
        waypoint.gameObject.SetActive(true);
    }

    public void ShowQuestion(int number, int total, string prompt, string[] answers, Action<int> onAnswer)
    {
        questionHeading.text = GameLanguage.Text("CROSSING QUIZ");
        progressText.text = $"{number} / {total}";
        ShowChoices(prompt, answers, onAnswer);
    }

    public void ShowDialogue(string heading, string prompt, string[] answers, Action<int> onAnswer)
    {
        questionHeading.text = GameLanguage.Text(heading);
        progressText.text = string.Empty;
        ShowChoices(prompt, answers, onAnswer);
    }

    void ShowChoices(string prompt, string[] answers, Action<int> onAnswer)
    {
        answerCallback = onAnswer;
        promptText.text = GameLanguage.Text(prompt);
        feedbackText.text = string.Empty;

        for (int i = 0; i < answerButtons.Length; i++)
        {
            bool hasAnswer = i < answers.Length;
            answerButtons[i].gameObject.SetActive(hasAnswer);
            answerButtons[i].interactable = hasAnswer;
            if (hasAnswer)
                answerLabels[i].text = GameLanguage.Text(answers[i]);
        }

        hud.SetActive(false);
        questionLayer.SetActive(true);
        eventSystem.SetSelectedGameObject(answerButtons[0].gameObject);
    }

    public void ShowFeedback(string message, bool correct)
    {
        feedbackText.text = GameLanguage.Text(message);
        feedbackText.color = correct ? Teal : Warning;

        if (!correct)
            return;

        answerCallback = null;
        foreach (Button button in answerButtons)
            button.interactable = false;
    }

    public void SetVisible(bool visible)
    {
        canvas.gameObject.SetActive(visible);
    }

    internal void ShowLevelResult(GameLevel level, int stars, int best, Action onRestart,
        Action onMainMenu, Action onNextGame = null)
    {
        string title;
        string summary;
        switch (level)
        {
            case GameLevel.Crossing:
                title = "You crossed safely!";
                summary = "You reached the other sidewalk. Head into the schoolyard for the next game.";
                break;
            case GameLevel.School:
                title = "You got help!";
                summary = "You found the teacher, explained what happened and where, then led them to your classmate.";
                break;
            default:
                title = "Everyone got a turn!";
                summary = "You made room for everyone and shared the ball.";
                break;
        }

        answerCallback = null;
        restartCallback = onRestart;
        mainMenuCallback = onMainMenu;
        nextGameCallback = onNextGame;
        resultTitle.text = GameLanguage.Text(title);
        resultSummary.text = GameLanguage.Text(summary);
        resultStars.text = LevelProgress.Icons(stars);
        resultRating.text = GameLanguage.Format("Stars earned {0}/3", stars) + "   ·   " +
            GameLanguage.Format("Best {0}/3", best);

        bool hasNextGame = onNextGame != null;
        nextGameButton.gameObject.SetActive(hasNextGame);
        InsetTop((RectTransform)restartButton.transform, hasNextGame ? 93f : 143f, 420f, hasNextGame ? 266f : 350f, 90f);
        InsetTop((RectTransform)mainMenuButton.transform, hasNextGame ? 377f : 527f, 420f, hasNextGame ? 266f : 350f, 90f);
        mainMenuButton.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = restartButton,
            selectOnRight = hasNextGame ? nextGameButton : null,
        };

        hud.SetActive(false);
        questionLayer.SetActive(false);
        winLayer.SetActive(true);
        eventSystem.SetSelectedGameObject((hasNextGame ? nextGameButton : restartButton).gameObject);
    }

    public void HideModal()
    {
        answerCallback = null;
        restartCallback = null;
        mainMenuCallback = null;
        nextGameCallback = null;
        eventSystem.SetSelectedGameObject(null);
        questionLayer.SetActive(false);
        winLayer.SetActive(false);
        hud.SetActive(true);
    }

    void Answer(int choice)
    {
        if (answerCallback == null || !answerButtons[choice].gameObject.activeSelf)
            return;

        eventSystem.SetSelectedGameObject(answerButtons[choice].gameObject);
        answerCallback.Invoke(choice);
    }

    void BuildHud(Transform parent)
    {
        var layer = NewUiObject("Route HUD", parent);
        Stretch(layer);
        hud = layer.gameObject;

        var card = Plate("Objective card", layer, Ink);
        Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(50f, -44f), new Vector2(710f, 166f));

        hudHeading = Label("Game title", card.transform, "WHAT SHOULD I DO?", 25, Mint, TextAnchor.MiddleLeft, FontStyle.Bold);
        InsetTop(hudHeading.rectTransform, 29f, 20f, 652f, 32f);
        objectiveText = Label("Objective", card.transform, string.Empty, 37, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        InsetTop(objectiveText.rectTransform, 29f, 59f, 652f, 49f);
        objectiveHint = Label("Movement and route hints", card.transform, string.Empty, 25, Mint, TextAnchor.MiddleLeft);
        InsetTop(objectiveHint.rectTransform, 29f, 112f, 652f, 34f);

        var pointer = Plate("Waypoint pointer", layer, Paper);
        waypoint = pointer.rectTransform;
        Place(waypoint, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(275f, 60f));
        waypointText = Label("Waypoint name", waypoint, string.Empty, 29, Teal, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(waypointText.rectTransform);

        var tail = NewUiObject("Pointer tip", waypoint);
        Place(tail, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(19f, 19f));
        tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var tailImage = tail.gameObject.AddComponent<Image>();
        tailImage.color = Paper;
        tailImage.raycastTarget = false;
        tail.SetAsFirstSibling();
        waypoint.gameObject.SetActive(false);
    }

    void BuildQuestionLayer(Transform parent)
    {
        RectTransform card = BuildModal("Question", parent, new Vector2(1140f, 780f), out questionLayer);
        questionHeading = Label("Question heading", card, string.Empty, 30, Teal, TextAnchor.MiddleLeft, FontStyle.Bold);
        InsetTop(questionHeading.rectTransform, 62f, 44f, 840f, 44f);
        progressText = Label("Question progress", card, string.Empty, 30, Teal, TextAnchor.MiddleRight, FontStyle.Bold);
        InsetTop(progressText.rectTransform, 901f, 44f, 176f, 44f);

        promptText = Label("Question prompt", card, string.Empty, PromptFontSize(), Ink,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        InsetTop(promptText.rectTransform, 62f, 106f, 1016f, 143f);

        for (int i = 0; i < answerButtons.Length; i++)
            BuildAnswer(card, i);

        var navigation = new Navigation { mode = Navigation.Mode.Explicit };
        for (int i = 0; i < answerButtons.Length; i++)
        {
            navigation.selectOnUp = i > 0 ? answerButtons[i - 1] : null;
            navigation.selectOnDown = i + 1 < answerButtons.Length ? answerButtons[i + 1] : null;
            answerButtons[i].navigation = navigation;
        }

        feedbackText = Label("Answer feedback", card, string.Empty, 33, Teal, TextAnchor.MiddleLeft, FontStyle.Bold);
        InsetTop(feedbackText.rectTransform, 62f, 593f, 1016f, 90f);
        Text input = Label("Answer controls", card, "Click an answer, press 1–3, or choose with a gamepad.", 27, Muted, TextAnchor.MiddleLeft);
        InsetTop(input.rectTransform, 62f, 708f, 1016f, 41f);
    }

    void BuildAnswer(Transform card, int index)
    {
        Image row = Plate($"Answer {index + 1}", card, AnswerColor);
        InsetTop(row.rectTransform, 62f, 271f + index * 104f, 1016f, 90f);
        answerButtons[index] = MakeButton(row, () => Answer(index));

        Image badge = Plate("Answer number", row.transform, Teal);
        Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(16f, 0f), new Vector2(52f, 52f));
        Text number = Label("Number", badge.transform, (index + 1).ToString(), 31, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(number.rectTransform);

        answerLabels[index] = Label("Answer text", row.transform, string.Empty, 37, Ink, TextAnchor.MiddleLeft);
        InsetTop(answerLabels[index].rectTransform, 91f, 3f, 899f, 84f);
    }

    void BuildWinLayer(Transform parent)
    {
        RectTransform card = BuildModal("Win", parent, new Vector2(1020f, 556f), out winLayer);
        resultTitle = Label("Round result", card, string.Empty, 67, Teal, TextAnchor.MiddleCenter, FontStyle.Bold);
        InsetTop(resultTitle.rectTransform, 60f, 44f, 900f, 100f);
        resultStars = Label("Stars", card, string.Empty, 61, new Color32(151, 94, 23, 255),
            TextAnchor.MiddleCenter, FontStyle.Bold);
        resultStars.font = GameLanguage.FontFor(boldFont, true);
        InsetTop(resultStars.rectTransform, 120f, 150f, 780f, 73f);
        resultRating = Label("Current and best stars", card, string.Empty, 28, Muted, TextAnchor.MiddleCenter);
        InsetTop(resultRating.rectTransform, 120f, 226f, 780f, 44f);
        resultSummary = Label("Round summary", card, string.Empty, 39, Ink, TextAnchor.MiddleCenter);
        InsetTop(resultSummary.rectTransform, 100f, 272f, 820f, 116f);

        restartButton = BuildResultButton(card, "Play again", false, () => restartCallback?.Invoke());
        mainMenuButton = BuildResultButton(card, "Main menu", false, () => mainMenuCallback?.Invoke());
        nextGameButton = BuildResultButton(card, "Next game", true, () => nextGameCallback?.Invoke());
        InsetTop((RectTransform)nextGameButton.transform, 661f, 420f, 266f, 90f);
        restartButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = mainMenuButton };
        nextGameButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = mainMenuButton };
    }

    Button BuildResultButton(Transform parent, string name, bool primary, Action onClick)
    {
        var plate = Plate(name, parent, primary ? Teal : AnswerColor);
        var label = Label("Label", plate.transform, name, 36, primary ? Color.white : Teal,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(label.rectTransform);
        var button = MakeButton(plate, onClick);
        if (primary)
        {
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color32(234, 255, 244, 255);
            colors.selectedColor = new Color32(194, 242, 215, 255);
            button.colors = colors;
        }
        return button;
    }

    RectTransform BuildModal(string name, Transform parent, Vector2 size, out GameObject modal)
    {
        var layer = NewUiObject(name, parent);
        Stretch(layer);
        layer.gameObject.AddComponent<Image>().color = Dim;
        modal = layer.gameObject;

        var card = Plate("Card", layer, Paper);
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        return card.rectTransform;
    }

    static Button MakeButton(Image image, Action onClick)
    {
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick());
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color32(229, 246, 235, 255);
        colors.selectedColor = new Color32(194, 229, 207, 255);
        colors.pressedColor = new Color32(164, 205, 181, 255);
        colors.disabledColor = new Color32(217, 228, 221, 255);
        button.colors = colors;
        return button;
    }

    Image Plate(string name, Transform parent, Color color)
    {
        var image = NewUiObject(name, parent).gameObject.AddComponent<Image>();
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Text Label(string name, Transform parent, string content, int size, Color color, TextAnchor alignment,
        FontStyle style = FontStyle.Normal)
    {
        var text = NewUiObject(name, parent).gameObject.AddComponent<Text>();
        text.font = GameLanguage.FontFor(style == FontStyle.Bold ? boldFont : regularFont);
        labels.Add((text, content, style));
        text.text = GameLanguage.Text(content);
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static RectTransform NewUiObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)gameObject.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void InsetTop(RectTransform rect, float x, float y, float width, float height)
    {
        var topLeft = new Vector2(0f, 1f);
        Place(rect, topLeft, topLeft, new Vector2(x, -y), new Vector2(width, height));
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    internal static Sprite BuildRoundedSprite()
    {
        const int radius = 21;
        const int size = radius * 2 + 4;
        const float half = size * 0.5f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Crossing UI Rounded Corners",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Max(0f, Mathf.Abs(x + 0.5f - half) - (half - radius));
                float py = Mathf.Max(0f, Mathf.Abs(y + 0.5f - half) - (half - radius));
                float distance = Mathf.Sqrt(px * px + py * py);
                byte alpha = (byte)(Mathf.Clamp01(radius + 0.5f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        var border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    void RefreshLanguage()
    {
        foreach (var (label, source, style) in labels)
        {
            label.font = GameLanguage.FontFor(style == FontStyle.Bold ? boldFont : regularFont, label == resultStars);
            label.text = GameLanguage.Text(source);
        }
        promptText.fontSize = PromptFontSize();
    }

    static int PromptFontSize() => GameLanguage.Current == GameLanguageOption.SimplifiedChinese
        ? ChinesePromptSize
        : EnglishPromptSize;

    void OnDestroy()
    {
        GameLanguage.Changed -= RefreshLanguage;
        if (rounded != null)
        {
            Destroy(rounded.texture);
            Destroy(rounded);
        }
    }
}

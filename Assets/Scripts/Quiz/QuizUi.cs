using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds the question card at runtime so the game needs no prefab or font asset setup.
/// Answers can be clicked or picked with the 1/2/3 keys.
/// </summary>
public class QuizUi : MonoBehaviour
{
    const int ReferenceWidth = 1920;
    const int ReferenceHeight = 1080;
    const int CornerRadius = 28;

    static readonly Color DimColor = new Color(0.04f, 0.07f, 0.12f, 0.55f);
    static readonly Color CardColor = new Color(1f, 0.97f, 0.89f, 1f);
    static readonly Color TitleColor = new Color(0.36f, 0.56f, 0.86f, 1f);
    static readonly Color PromptColor = new Color(0.15f, 0.19f, 0.29f, 1f);
    static readonly Color ButtonColor = new Color(0.25f, 0.63f, 0.95f, 1f);
    static readonly Color GoodColor = new Color(0.13f, 0.62f, 0.31f, 1f);
    static readonly Color BadColor = new Color(0.84f, 0.26f, 0.23f, 1f);

    Font font;
    Sprite rounded;
    GameObject questionLayer;
    GameObject winLayer;
    GameObject[] answerButtons;
    Text promptText;
    Text feedbackText;
    Text winText;
    Action<int> answerCallback;
    Action restartCallback;

    public static QuizUi Create(Transform parent)
    {
        var host = new GameObject("Quiz UI");
        host.transform.SetParent(parent, false);

        var ui = host.AddComponent<QuizUi>();
        ui.Build();
        return ui;
    }

    void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        rounded = BuildRoundedSprite(CornerRadius);

        var canvasObject = new GameObject("Quiz Canvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        questionLayer = BuildQuestionLayer(canvasObject.transform);
        winLayer = BuildWinLayer(canvasObject.transform);

        questionLayer.SetActive(false);
        winLayer.SetActive(false);
    }

    void Update()
    {
        if (answerCallback == null || questionLayer == null || !questionLayer.activeSelf)
            return;

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            Answer(0);
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            Answer(1);
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            Answer(2);
    }

    public void ShowQuestion(string prompt, string[] answers, Action<int> onAnswer)
    {
        answerCallback = onAnswer;
        promptText.text = prompt;
        feedbackText.text = string.Empty;

        for (int i = 0; i < answerButtons.Length; i++)
        {
            bool hasAnswer = answers != null && i < answers.Length;
            answerButtons[i].SetActive(hasAnswer);
            if (hasAnswer)
                answerButtons[i].transform.GetChild(0).GetComponent<Text>().text = $"{i + 1}.  {answers[i]}";
        }

        questionLayer.SetActive(true);
    }

    public void ShowFeedback(string message, bool good)
    {
        feedbackText.text = message;
        feedbackText.color = good ? GoodColor : BadColor;
    }

    /// Shown once every question has been answered, right before the player may cross.
    public void ShowGo(string message)
    {
        answerCallback = null;
        promptText.text = message;
        feedbackText.text = string.Empty;

        foreach (var button in answerButtons)
            button.SetActive(false);
    }

    public void Hide()
    {
        answerCallback = null;
        questionLayer.SetActive(false);
    }

    public void ShowWin(string message, Action onRestart)
    {
        restartCallback = onRestart;
        winText.text = message;
        questionLayer.SetActive(false);
        winLayer.SetActive(true);
    }

    public void HideWin()
    {
        restartCallback = null;
        winLayer.SetActive(false);
    }

    void Answer(int choice)
    {
        var callback = answerCallback;
        if (callback == null)
            return;

        callback(choice);
    }

    GameObject BuildQuestionLayer(Transform parent)
    {
        var layer = NewUiObject("Question", parent);
        Stretch((RectTransform)layer.transform);

        var dim = NewUiObject("Dim", layer.transform);
        Stretch((RectTransform)dim.transform);
        dim.AddComponent<Image>().color = DimColor;

        var card = NewUiObject("Card", layer.transform);
        var cardRect = (RectTransform)card.transform;
        Center(cardRect, new Vector2(1240f, 780f));
        var cardImage = card.AddComponent<Image>();
        cardImage.sprite = rounded;
        cardImage.type = Image.Type.Sliced;
        cardImage.color = CardColor;

        var title = CreateText(card.transform, "Title", "What should I do?", 44, TitleColor, TextAnchor.MiddleCenter);
        PlaceTop((RectTransform)title.transform, -26f, 60f, 1080f);

        promptText = CreateText(card.transform, "Prompt", string.Empty, 52, PromptColor, TextAnchor.MiddleCenter);
        promptText.horizontalOverflow = HorizontalWrapMode.Wrap;
        promptText.verticalOverflow = VerticalWrapMode.Overflow;
        PlaceTop((RectTransform)promptText.transform, -100f, 190f, 1080f);

        answerButtons = new GameObject[3];
        for (int i = 0; i < answerButtons.Length; i++)
            answerButtons[i] = CreateAnswerButton(card.transform, i, -300f - i * 124f);

        feedbackText = CreateText(card.transform, "Feedback", string.Empty, 38, PromptColor, TextAnchor.MiddleCenter);
        var feedbackRect = (RectTransform)feedbackText.transform;
        feedbackRect.anchorMin = new Vector2(0.5f, 0f);
        feedbackRect.anchorMax = new Vector2(0.5f, 0f);
        feedbackRect.pivot = new Vector2(0.5f, 0f);
        feedbackRect.sizeDelta = new Vector2(1080f, 90f);
        feedbackRect.anchoredPosition = new Vector2(0f, 26f);

        return layer;
    }

    GameObject CreateAnswerButton(Transform parent, int index, float topOffset)
    {
        var buttonObject = NewUiObject($"Answer {index + 1}", parent);
        PlaceTop((RectTransform)buttonObject.transform, topOffset, 104f, 980f);

        var image = buttonObject.AddComponent<Image>();
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
        image.color = ButtonColor;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => Answer(index));

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.96f, 1f);
        colors.pressedColor = new Color(0.78f, 0.86f, 0.95f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        var label = CreateText(buttonObject.transform, "Label", string.Empty, 40, Color.white, TextAnchor.MiddleCenter);
        Stretch((RectTransform)label.transform);
        ((RectTransform)label.transform).offsetMin = new Vector2(40f, 0f);
        ((RectTransform)label.transform).offsetMax = new Vector2(-40f, 0f);

        return buttonObject;
    }

    GameObject BuildWinLayer(Transform parent)
    {
        var layer = NewUiObject("Win", parent);
        Stretch((RectTransform)layer.transform);

        var dim = NewUiObject("Dim", layer.transform);
        Stretch((RectTransform)dim.transform);
        dim.AddComponent<Image>().color = DimColor;

        var card = NewUiObject("Card", layer.transform);
        Center((RectTransform)card.transform, new Vector2(1120f, 620f));
        var cardImage = card.AddComponent<Image>();
        cardImage.sprite = rounded;
        cardImage.type = Image.Type.Sliced;
        cardImage.color = CardColor;

        winText = CreateText(card.transform, "Message", string.Empty, 54, GoodColor, TextAnchor.MiddleCenter);
        winText.horizontalOverflow = HorizontalWrapMode.Wrap;
        PlaceTop((RectTransform)winText.transform, -90f, 320f, 960f);

        var buttonObject = NewUiObject("Play Again", card.transform);
        var buttonRect = (RectTransform)buttonObject.transform;
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.sizeDelta = new Vector2(460f, 110f);
        buttonRect.anchoredPosition = new Vector2(0f, 56f);

        var image = buttonObject.AddComponent<Image>();
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
        image.color = ButtonColor;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => restartCallback?.Invoke());

        var label = CreateText(buttonObject.transform, "Label", "Play again", 40, Color.white, TextAnchor.MiddleCenter);
        Stretch((RectTransform)label.transform);

        return layer;
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null)
            return;

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    Text CreateText(Transform parent, string name, string content, int size, Color color, TextAnchor anchor)
    {
        var textObject = NewUiObject(name, parent);
        var text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = anchor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    static GameObject NewUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void Center(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
    }

    static void PlaceTop(RectTransform rect, float topOffset, float height, float width)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(0f, topOffset);
    }

    static Sprite BuildRoundedSprite(int radius)
    {
        int size = radius * 2 + 4;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "RoundedCard",
        };

        var pixels = new Color32[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Signed distance to a rounded box that fills the texture, then use it for 1px antialiasing.
                float px = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float py = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outsideX = Mathf.Max(px, 0f);
                float outsideY = Mathf.Max(py, 0f);
                float distance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                float alpha = Mathf.Clamp01(0.5f - distance);

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        var border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class WelcomeMenu : MonoBehaviour
{
    const string AvatarKey = "wsid.avatar.girl";
    const string CameraMotionKey = "wsid.camera.reducedMotion";
    const string FullscreenKey = "wsid.display.fullscreen";

    static readonly Color Ink = new Color32(24, 49, 53, 255);
    static readonly Color Paper = new Color32(255, 250, 238, 255);
    static readonly Color Mint = new Color32(211, 239, 224, 255);
    static readonly Color Leaf = new Color32(50, 86, 81, 255);
    static readonly Color MutedCard = new Color32(36, 64, 60, 255);
    static readonly Color Yellow = new Color32(255, 218, 133, 255);
    static readonly Color SoftShadow = new Color32(11, 26, 29, 112);

    PlayerWalker player;
    ThirdPersonFollowCamera followCamera;
    Action<GameLevel> onPlay;
    EventSystem eventSystem;
    Font regularFont;
    Font boldFont;
    Sprite rounded;
    GameObject canvasObject;
    RectTransform welcomePanel;
    RectTransform settingsPanel;
    RectTransform audioPanel;
    RectTransform levelsPanel;
    readonly List<(Text Label, string Source, bool Bold, bool Native)> labels = new();
    readonly Button[] levelButtons = new Button[3];
    readonly Image[] levelFills = new Image[3];
    readonly Text[] levelStates = new Text[3];
    readonly Text[] levelStars = new Text[3];
    Button levelsButton;
    Button levelsBack;
    Button[] levelsOrder;
    Text startLabel;
    Button startButton;
    Button settingsButton;
    Button audioButton;
    Button audioBack;
    Button[] welcomeOrder;
    Button[] settingsOrder;
    Slider[] audioSliders;
    Option boy;
    Option girl;
    Option smooth;
    Option reduced;
    Option windowed;
    Option fullscreen;
    Option english;
    Option chinese;
    bool useGirl;
    bool reduceMotion;
    bool fullScreen;

    internal static WelcomeMenu Create(Transform parent, PlayerWalker player, ThirdPersonFollowCamera camera, Action<GameLevel> onPlay)
    {
        var host = new GameObject("Welcome Menu");
        host.transform.SetParent(parent, false);
        var menu = host.AddComponent<WelcomeMenu>();
        menu.player = player;
        menu.followCamera = camera;
        menu.onPlay = onPlay;
        menu.eventSystem = EventSystem.current != null ? EventSystem.current : FindAnyObjectByType<EventSystem>();
        menu.useGirl = PlayerPrefs.GetInt(AvatarKey) != 0;
        menu.reduceMotion = PlayerPrefs.GetInt(CameraMotionKey) != 0;
        menu.fullScreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
        if (PlayerPrefs.HasKey(FullscreenKey) && menu.fullScreen != Screen.fullScreen)
            menu.ApplyDisplayMode();

        menu.Build();
        return menu;
    }

    public void Show()
    {
        player.SelectAvatar(useGirl);
        followCamera?.SetReducedMotion(reduceMotion);
        followCamera?.SetWelcomeView(true);
        canvasObject.SetActive(true);
        RefreshProgress();
        ShowWelcome(startButton);
    }

    void Play()
    {
        GameAudioManager.EnsureInstance().UnlockAudio();
        onPlay?.Invoke(LevelProgress.FirstUnfinished() ?? GameLevel.Crossing);
    }

    internal void HideForPlay()
    {
        eventSystem?.SetSelectedGameObject(null);
        canvasObject.SetActive(false);
        followCamera?.SetWelcomeView(false);
    }

    void OpenLevel(GameLevel level)
    {
        if (LevelProgress.Best(level) > 0)
        {
            GameAudioManager.EnsureInstance().UnlockAudio();
            onPlay?.Invoke(level);
        }
    }

    void Update()
    {
        if (!canvasObject.activeInHierarchy)
            return;

        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        bool inAudio = audioPanel.gameObject.activeSelf;
        bool inSettings = settingsPanel.gameObject.activeSelf;
        bool inLevels = levelsPanel.gameObject.activeSelf;
        if (eventSystem != null && eventSystem.currentSelectedGameObject == null)
        {
            Button defaultButton = inAudio ? audioBack : inSettings ? (reduceMotion ? reduced : smooth).Button : inLevels ? FirstLevelFocus() : startButton;
            eventSystem.SetSelectedGameObject(defaultButton.gameObject);
        }

        if ((inSettings || inLevels || inAudio) && ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                           (gamepad != null && gamepad.buttonEast.wasPressedThisFrame)))
        {
            if (inAudio)
                ShowSettings();
            else
                ShowWelcome(inLevels ? levelsButton : settingsButton);
            return;
        }

        if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
        {
            if (inAudio)
                CycleFocusAudio(keyboard.shiftKey.isPressed);
            else
                CycleFocus(inSettings ? settingsOrder : inLevels ? levelsOrder : welcomeOrder, keyboard.shiftKey.isPressed);
        }
    }

    void CycleFocus(Button[] order, bool backward)
    {
        if (eventSystem == null)
            return;

        int index = Array.FindIndex(order, button => button.gameObject == eventSystem.currentSelectedGameObject);
        if (index < 0 && backward)
            index = 0;
        for (int scanned = 0; scanned < order.Length; scanned++)
        {
            index = (index + (backward ? -1 : 1) + order.Length) % order.Length;
            if (!order[index].interactable)
                continue;
            eventSystem.SetSelectedGameObject(order[index].gameObject);
            return;
        }
    }

    void CycleFocusAudio(bool backward)
    {
        if (eventSystem == null)
            return;

        var items = new Selectable[] { audioSliders[0], audioSliders[1], audioSliders[2], audioSliders[3], audioBack };
        int index = Array.FindIndex(items, item => item.gameObject == eventSystem.currentSelectedGameObject);
        index = (index + (backward ? items.Length - 1 : 1)) % items.Length;
        eventSystem.SetSelectedGameObject(items[index].gameObject);
    }

    void ShowWelcome(Button focus)
    {
        audioPanel.gameObject.SetActive(false);
        settingsPanel.gameObject.SetActive(false);
        levelsPanel.gameObject.SetActive(false);
        welcomePanel.gameObject.SetActive(true);
        eventSystem?.SetSelectedGameObject(focus.gameObject);
    }

    void ShowSettings()
    {
        welcomePanel.gameObject.SetActive(false);
        levelsPanel.gameObject.SetActive(false);
        audioPanel.gameObject.SetActive(false);
        settingsPanel.gameObject.SetActive(true);
        eventSystem?.SetSelectedGameObject((reduceMotion ? reduced : smooth).Button.gameObject);
    }

    void ShowAudioSettings()
    {
        welcomePanel.gameObject.SetActive(false);
        levelsPanel.gameObject.SetActive(false);
        settingsPanel.gameObject.SetActive(false);
        audioPanel.gameObject.SetActive(true);
        eventSystem?.SetSelectedGameObject(audioSliders[0].gameObject);
    }

    void ShowLevels()
    {
        RefreshProgress();
        welcomePanel.gameObject.SetActive(false);
        settingsPanel.gameObject.SetActive(false);
        audioPanel.gameObject.SetActive(false);
        levelsPanel.gameObject.SetActive(true);
        eventSystem?.SetSelectedGameObject(FirstLevelFocus().gameObject);
    }

    Button FirstLevelFocus()
    {
        foreach (Button button in levelButtons)
            if (button.interactable)
                return button;
        return levelsBack;
    }

    void RefreshProgress()
    {
        GameLevel? next = LevelProgress.FirstUnfinished();
        string playText = next == GameLevel.Crossing ? "Start" : next == null ? "Play again" : "Continue";
        startLabel.text = GameLanguage.Text(playText);
        Button previous = null;
        for (int index = 0; index < levelButtons.Length; index++)
        {
            var level = (GameLevel)index;
            int best = LevelProgress.Best(level);
            Button button = levelButtons[index];
            button.interactable = best > 0;
            levelFills[index].color = best > 0 ? Leaf : MutedCard;
            levelStars[index].text = LevelProgress.Icons(best);
            levelStars[index].color = best > 0 ? Yellow : Mint;
            levelStates[index].text = best > 0
                ? GameLanguage.Text("Completed") + " · " + GameLanguage.Format("Best {0}/3", best)
                : GameLanguage.Text(next == level ? "Finish this level to replay it." : "Finish the previous level first.");
            Link(button, null, null, previous, levelsBack);
            if (best <= 0)
                continue;

            if (previous != null)
            {
                Navigation navigation = previous.navigation;
                navigation.selectOnDown = button;
                previous.navigation = navigation;
            }
            previous = button;
        }
        Link(levelsBack, null, null, previous, null);
    }

    void SelectAvatar(bool girlSelected)
    {
        useGirl = girlSelected;
        player.SelectAvatar(useGirl);
        MarkAvatar();
        SaveChoice(AvatarKey, useGirl);
    }

    void SelectMotion(bool isReduced)
    {
        reduceMotion = isReduced;
        followCamera?.SetReducedMotion(reduceMotion);
        MarkSettings();
        SaveChoice(CameraMotionKey, reduceMotion);
    }

    void SelectDisplay(bool isFullScreen)
    {
        if (fullScreen != isFullScreen)
        {
            fullScreen = isFullScreen;
            ApplyDisplayMode();
        }

        MarkSettings();
        SaveChoice(FullscreenKey, fullScreen);
    }

    void ApplyDisplayMode()
    {
        Screen.fullScreenMode = fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    static void SaveChoice(string key, bool selected)
    {
        PlayerPrefs.SetInt(key, selected ? 1 : 0);
        PlayerPrefs.Save();
    }

    void MarkAvatar()
    {
        boy.ShowSelected(!useGirl);
        girl.ShowSelected(useGirl);
    }

    void MarkSettings()
    {
        smooth.ShowSelected(!reduceMotion);
        reduced.ShowSelected(reduceMotion);
        windowed.ShowSelected(!fullScreen);
        fullscreen.ShowSelected(fullScreen);
        english.ShowSelected(GameLanguage.Current == GameLanguageOption.English);
        chinese.ShowSelected(GameLanguage.Current == GameLanguageOption.SimplifiedChinese);
    }

    void Build()
    {
        regularFont = Resources.Load<Font>("Fonts/LiberationSans-Regular");
        boldFont = Resources.Load<Font>("Fonts/LiberationSans-Bold");
        rounded = QuizUi.BuildRoundedSprite();

        RectTransform canvasRect = Rect("Welcome Canvas", transform);
        canvasObject = canvasRect.gameObject;
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform backdrop = Rect("Street shade", canvasRect);
        Stretch(backdrop, 0f);
        backdrop.gameObject.AddComponent<Image>().color = new Color32(13, 34, 38, 38);

        RectTransform shadow = Plate("Card shadow", canvasRect, SoftShadow).rectTransform;
        SetPanelPosition(shadow, 8f);
        welcomePanel = Plate("Welcome", canvasRect, Ink).rectTransform;
        settingsPanel = Plate("Settings panel", canvasRect, Ink).rectTransform;
        audioPanel = Plate("Audio panel", canvasRect, Ink).rectTransform;
        levelsPanel = Plate("Levels panel", canvasRect, Ink).rectTransform;
        SetPanelPosition(welcomePanel, 0f);
        SetPanelPosition(settingsPanel, 0f);
        SetPanelPosition(audioPanel, 0f);
        SetPanelPosition(levelsPanel, 0f);

        BuildWelcome();
        BuildSettings();
        BuildAudioSettings();
        BuildLevels();
        GameLanguage.Changed += RefreshLanguage;
        RefreshLanguage();
        MarkAvatar();
        MarkSettings();
        canvasObject.SetActive(false);
    }

    void BuildWelcome()
    {
        AddCrosswalkMark(welcomePanel);
        Text title = Label("Title", welcomePanel, "What should\nI do?", 91, Paper, true, TextAnchor.MiddleLeft);
        title.lineSpacing = 0.86f;
        Put(title.rectTransform, 44f, 129f, 572f, 204f);

        Put(Label("Game description", welcomePanel,
            "Three games about crossing safely, getting help, and including everyone.",
            30, Mint, false, TextAnchor.MiddleLeft).rectTransform, 48f, 348f, 538f, 77f);

        Put(Label("Character prompt", welcomePanel, "Choose your character", 29, Paper, true,
            TextAnchor.MiddleLeft).rectTransform, 48f, 439f, 568f, 32f);
        boy = MakeOption("Boy", welcomePanel, 44f, 476f, () => SelectAvatar(false));
        girl = MakeOption("Girl", welcomePanel, 343f, 476f, () => SelectAvatar(true));

        startButton = MakeTextButton("Start", welcomePanel, 44f, 602f, 226f, Yellow, Ink, 35, Play);
        startLabel = startButton.GetComponentInChildren<Text>();
        levelsButton = MakeTextButton("Levels", welcomePanel, 282f, 602f, 166f, Leaf, Paper, 30, ShowLevels);
        settingsButton = MakeTextButton("Settings", welcomePanel, 460f, 602f, 156f, Leaf, Paper, 28, ShowSettings);
        welcomeOrder = new[] { boy.Button, girl.Button, startButton, levelsButton, settingsButton };

        Link(boy.Button, null, girl.Button, null, startButton);
        Link(girl.Button, boy.Button, null, null, settingsButton);
        Link(startButton, null, levelsButton, boy.Button, null);
        Link(levelsButton, startButton, settingsButton, girl.Button, null);
        Link(settingsButton, levelsButton, null, girl.Button, null);
    }

    void BuildSettings()
    {
        AddCrosswalkMark(settingsPanel);
        Put(Label("Settings title", settingsPanel, "Settings", 65, Paper, true,
            TextAnchor.MiddleLeft).rectTransform, 44f, 115f, 572f, 75f);

        Put(Label("Camera heading", settingsPanel, "Camera motion", 29, Paper, true,
            TextAnchor.MiddleLeft).rectTransform, 48f, 194f, 568f, 36f);
        smooth = MakeOption("Smooth", settingsPanel, 44f, 230f, () => SelectMotion(false));
        reduced = MakeOption("Reduced", settingsPanel, 343f, 230f, () => SelectMotion(true));
        Put(Label("Camera description", settingsPanel, "Reduced motion steadies the follow camera.", 25, Mint, false,
            TextAnchor.MiddleLeft).rectTransform, 48f, 313f, 568f, 32f);

        Put(Label("Display heading", settingsPanel, "Display", 29, Paper, true,
            TextAnchor.MiddleLeft).rectTransform, 48f, 349f, 568f, 36f);
        windowed = MakeOption("Windowed", settingsPanel, 44f, 385f, () => SelectDisplay(false));
        fullscreen = MakeOption("Fullscreen", settingsPanel, 343f, 385f, () => SelectDisplay(true));

        Put(Label("Language heading", settingsPanel, "Language", 29, Paper, true,
            TextAnchor.MiddleLeft).rectTransform, 48f, 474f, 568f, 32f);
        english = MakeOption("English", settingsPanel, 44f, 507f, () => GameLanguage.Set(GameLanguageOption.English), true);
        chinese = MakeOption("简体中文", settingsPanel, 343f, 507f,
            () => GameLanguage.Set(GameLanguageOption.SimplifiedChinese), true);

        Button back = MakeTextButton("Back", settingsPanel, 44f, 606f, 273f, Yellow, Ink, 34,
            () => ShowWelcome(settingsButton));
        audioButton = MakeTextButton("Audio", settingsPanel, 343f, 606f, 273f, Leaf, Paper, 34,
            ShowAudioSettings);
        settingsOrder = new[] { smooth.Button, reduced.Button, windowed.Button, fullscreen.Button,
            english.Button, chinese.Button, back, audioButton };

        Link(smooth.Button, null, reduced.Button, null, windowed.Button);
        Link(reduced.Button, smooth.Button, null, null, fullscreen.Button);
        Link(windowed.Button, null, fullscreen.Button, smooth.Button, english.Button);
        Link(fullscreen.Button, windowed.Button, null, reduced.Button, chinese.Button);
        Link(english.Button, null, chinese.Button, windowed.Button, back);
        Link(chinese.Button, english.Button, null, fullscreen.Button, audioButton);
        Link(back, null, audioButton, english.Button, null);
        Link(audioButton, back, null, chinese.Button, null);
    }

    void BuildAudioSettings()
    {
        AddCrosswalkMark(audioPanel);
        Put(Label("Audio title", audioPanel, "Audio", 65, Paper, true,
            TextAnchor.MiddleLeft).rectTransform, 44f, 115f, 572f, 75f);

        audioSliders = new Slider[4];
        audioSliders[0] = MakeVolumeSlider("Music volume", 210f,
            GameAudioManager.GetMusicVolume(), GameAudioManager.SetMusicVolume);
        audioSliders[1] = MakeVolumeSlider("Sound effects volume", 305f,
            GameAudioManager.GetSfxVolume(), GameAudioManager.SetSfxVolume);
        audioSliders[2] = MakeVolumeSlider("Voice volume", 400f,
            PlayerPrefs.GetFloat("wsid.audio.voice", 0.95f), GameAudioManager.SetVoiceVolume);
        audioSliders[3] = MakeVolumeSlider("Ambience volume", 495f,
            PlayerPrefs.GetFloat("wsid.audio.ambience", 0.24f), GameAudioManager.SetAmbienceVolume);

        audioBack = MakeTextButton("Back", audioPanel, 44f, 606f, 273f, Yellow, Ink, 34, ShowSettings);
        for (int i = 0; i < audioSliders.Length; i++)
        {
            audioSliders[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i == 0 ? audioBack : audioSliders[i - 1],
                selectOnDown = i == audioSliders.Length - 1 ? audioBack : audioSliders[i + 1],
            };
        }
        Link(audioBack, null, null, audioSliders[3], audioSliders[0]);
    }

    Slider MakeVolumeSlider(string label, float y, float initial, Action<float> onChanged)
    {
        Put(Label(label, audioPanel, label, 27, Paper, true,
            TextAnchor.MiddleLeft).rectTransform, 48f, y, 568f, 34f);

        var backdrop = Plate(label + " bar", audioPanel, Leaf);
        Put(backdrop.rectTransform, 48f, y + 39f, 548f, 27f);
        var fill = Plate("Current volume", backdrop.transform, Mint);
        fill.type = Image.Type.Simple;
        var fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        var handle = Plate("Volume handle", backdrop.transform, Yellow);
        handle.type = Image.Type.Simple;
        var handleRect = handle.rectTransform;
        handleRect.anchorMin = handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(25f, 40f);

        var slider = backdrop.gameObject.AddComponent<Slider>();
        slider.targetGraphic = handle;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.value = Mathf.Clamp01(initial);
        slider.onValueChanged.AddListener(value => onChanged(value));
        return slider;
    }

    void BuildLevels()
    {
        AddCrosswalkMark(levelsPanel);
        Put(Label("Level selection title", levelsPanel, "Choose a level", 59, Paper, true,
            TextAnchor.MiddleLeft).rectTransform, 44f, 121f, 572f, 75f);

        for (int index = 0; index < levelButtons.Length; index++)
        {
            var level = (GameLevel)index;
            string title = LevelProgress.Title(level);
            Button button = MakeButton(title, levelsPanel, 44f, 216f + index * 121f, 572f, 105f,
                Leaf, () => OpenLevel(level), out Image fill);
            levelButtons[index] = button;
            levelFills[index] = fill;
            Put(Label("Level name", fill.transform, title, 31, Paper, true, TextAnchor.MiddleLeft).rectTransform,
                20f, 8f, 528f, 43f);
            levelStates[index] = Label("Level status", fill.transform, string.Empty, 24, Mint, false, TextAnchor.MiddleLeft);
            Put(levelStates[index].rectTransform, 20f, 51f, 422f, 38f);
            levelStars[index] = Label("Best stars", fill.transform, string.Empty, 28, Yellow, true, TextAnchor.MiddleRight, true);
            Put(levelStars[index].rectTransform, 445f, 49f, 99f, 43f);
        }

        levelsBack = MakeTextButton("Back", levelsPanel, 44f, 602f, 273f, Yellow, Ink, 34,
            () => ShowWelcome(levelsButton));
        levelsOrder = new[] { levelButtons[0], levelButtons[1], levelButtons[2], levelsBack };
    }

    void RefreshLanguage()
    {
        foreach (var (label, source, bold, native) in labels)
        {
            label.font = GameLanguage.FontFor(bold ? boldFont : regularFont, native);
            label.text = native ? source : GameLanguage.Text(source);
        }
        RefreshProgress();
        MarkSettings();
    }

    void AddCrosswalkMark(Transform parent)
    {
        Image sign = Plate("Crosswalk mark", parent, Yellow);
        Put(sign.rectTransform, 44f, 42f, 90f, 68f);
        for (int stripe = 0; stripe < 4; stripe++)
        {
            var lane = Rect("Stripe", sign.transform);
            Put(lane, 18f + stripe * 15f, 17f, 8f, 34f);
            lane.localRotation = Quaternion.Euler(0f, 0f, -13f);
            var fill = lane.gameObject.AddComponent<Image>();
            fill.color = Ink;
            fill.raycastTarget = false;
        }
    }

    Option MakeOption(string name, Transform parent, float x, float y, Action onClick, bool native = false)
    {
        Button button = MakeButton(name, parent, x, y, 273f, 80f, Leaf, onClick, out Image fill);
        Image indicator = Plate("Selection mark", fill.transform, Mint);
        indicator.type = Image.Type.Simple;
        Put(indicator.rectTransform, 19f, 25f, 22f, 22f);
        Image center = Plate("Selection mark center", indicator.transform, Leaf);
        center.type = Image.Type.Simple;
        Stretch(center.rectTransform, 4f);
        Text label = Label("Label", fill.transform, name, 32, Paper, true, TextAnchor.MiddleLeft, native);
        Put(label.rectTransform, 54f, 0f, 194f, 72f);
        return new Option(button, fill, indicator, center, label);
    }

    Button MakeTextButton(string name, Transform parent, float x, float y, float width,
        Color background, Color textColor, int size, Action onClick)
    {
        Button button = MakeButton(name, parent, x, y, width, 84f, background, onClick, out Image fill);
        Stretch(Label("Label", fill.transform, name, size, textColor, true, TextAnchor.MiddleCenter).rectTransform, 0f);
        return button;
    }

    Button MakeButton(string name, Transform parent, float x, float y, float width, float height,
        Color background, Action onClick, out Image fill)
    {
        Image focus = Plate(name, parent, Color.white);
        Put(focus.rectTransform, x, y, width, height);
        focus.raycastTarget = true;
        var button = focus.gameObject.AddComponent<Button>();
        button.targetGraphic = focus;
        button.onClick.AddListener(() => onClick());
        button.colors = new ColorBlock
        {
            normalColor = Color.clear,
            highlightedColor = Mint,
            selectedColor = Paper,
            pressedColor = Color.white,
            disabledColor = Color.clear,
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };

        fill = Plate("Fill", focus.transform, background);
        Stretch(fill.rectTransform, 4f);
        fill.raycastTarget = true;
        return button;
    }

    static void Link(Selectable button, Selectable left, Selectable right, Selectable up, Selectable down)
    {
        button.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = left,
            selectOnRight = right,
            selectOnUp = up,
            selectOnDown = down,
        };
    }

    Image Plate(string name, Transform parent, Color color)
    {
        var image = Rect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Text Label(string name, Transform parent, string content, int size, Color color, bool bold, TextAnchor alignment, bool native = false)
    {
        var label = Rect(name, parent).gameObject.AddComponent<Text>();
        label.font = GameLanguage.FontFor(bold ? boldFont : regularFont, native);
        labels.Add((label, content, bold, native));
        label.fontSize = size;
        label.text = content;
        label.color = color;
        label.alignment = alignment;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        return rect;
    }

    static void SetPanelPosition(RectTransform rect, float verticalOffset)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(76f, -verticalOffset);
        rect.sizeDelta = new Vector2(660f, 736f);
    }

    static void Put(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    void OnDestroy()
    {
        GameLanguage.Changed -= RefreshLanguage;
        if (rounded == null)
            return;

        Destroy(rounded.texture);
        Destroy(rounded);
    }

    sealed class Option
    {
        public Button Button { get; }

        readonly Image fill;
        readonly Image indicator;
        readonly Image center;
        readonly Text label;

        public Option(Button button, Image fill, Image indicator, Image center, Text label)
        {
            Button = button;
            this.fill = fill;
            this.indicator = indicator;
            this.center = center;
            this.label = label;
        }

        public void ShowSelected(bool isSelected)
        {
            fill.color = isSelected ? Mint : Leaf;
            indicator.color = isSelected ? Ink : Mint;
            center.color = isSelected ? Yellow : Leaf;
            label.color = isSelected ? Ink : Paper;
        }
    }
}

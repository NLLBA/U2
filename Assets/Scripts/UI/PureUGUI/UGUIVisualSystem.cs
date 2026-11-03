using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-850)]
public sealed class UGUIVisualSystem : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.91f, 0.95f, 0.97f, 1f);
    private static readonly Color MutedInk = new Color(0.52f, 0.62f, 0.67f, 1f);
    private static readonly Color Accent = new Color(0.16f, 0.78f, 0.86f, 1f);
    private static readonly Color Panel = new Color(0.025f, 0.055f, 0.075f, 0.97f);
    private static UGUIVisualSystem instance;

    private readonly HashSet<int> styledControls = new HashSet<int>();
    private readonly HashSet<int> adjustedViews = new HashSet<int>();
    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Ensure();
    }

    public static UGUIVisualSystem Ensure()
    {
        if (instance != null)
            return instance;
        GameObject host = new GameObject("UGUIVisualSystem");
        return host.AddComponent<UGUIVisualSystem>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        styledControls.Clear();
        adjustedViews.Clear();
        StyleScene();
        StartCoroutine(RefreshAfterInitialization());
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + 0.4f;
        StyleScene();
    }

    public static void Style(Button button)
    {
        if (button == null)
            return;
        Ensure().StyleButton(button);
    }

    public static void RefreshNow()
    {
        Ensure().StyleScene();
    }

    public static void PresentPanel(Component controller, GameObject view)
    {
        if (view == null)
            return;

        view.transform.SetAsLastSibling();
        Ensure().PreparePanel(controller, view);
    }

    private IEnumerator RefreshAfterInitialization()
    {
        yield return null;
        yield return null;
        StyleScene();
        ImproveLayouts();
    }

    private void StyleScene()
    {
        UGUIRuntime.EnsureEventSystem();
        EnsureTacticalBackdrop();

        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (IsLoadedSceneObject(button))
                StyleButton(button);
        }

        Toggle[] toggles = Resources.FindObjectsOfTypeAll<Toggle>();
        for (int i = 0; i < toggles.Length; i++)
        {
            Toggle toggle = toggles[i];
            if (!IsLoadedSceneObject(toggle) || !styledControls.Add(toggle.GetInstanceID()))
                continue;
            RepairToggle(toggle);
            StyleSelectable(toggle, ThemeRole.Secondary);
        }

        Slider[] sliders = Resources.FindObjectsOfTypeAll<Slider>();
        for (int i = 0; i < sliders.Length; i++)
        {
            Slider slider = sliders[i];
            if (!IsLoadedSceneObject(slider) || !styledControls.Add(slider.GetInstanceID()))
                continue;
            RepairSlider(slider);
            StyleSelectable(slider, ThemeRole.Secondary);
            if (slider.fillRect != null)
            {
                Image fill = slider.fillRect.GetComponent<Image>();
                if (fill != null) fill.color = new Color(0.12f, 0.82f, 0.9f, 1f);
            }
            if (slider.handleRect != null)
            {
                Image handle = slider.handleRect.GetComponent<Image>();
                if (handle != null) handle.color = new Color(0.92f, 0.98f, 1f, 1f);
            }
        }

        InputField[] inputs = Resources.FindObjectsOfTypeAll<InputField>();
        for (int i = 0; i < inputs.Length; i++)
        {
            InputField input = inputs[i];
            if (!IsLoadedSceneObject(input) || !styledControls.Add(input.GetInstanceID()))
                continue;
            UpgradeInputGraphic(input);
            StyleSelectable(input, ThemeRole.Secondary);
            input.caretColor = new Color(0.2f, 0.92f, 1f, 1f);
            input.selectionColor = new Color(0.15f, 0.65f, 0.78f, 0.55f);
            if (input.textComponent != null)
            {
                input.textComponent.raycastTarget = false;
                input.textComponent.font = ChineseFontFallback.Font;
                input.textComponent.fontStyle = FontStyle.Normal;
                input.textComponent.color = new Color(0.92f, 0.97f, 1f, 1f);
                input.textComponent.resizeTextForBestFit = true;
                input.textComponent.resizeTextMinSize = 14;
                input.textComponent.resizeTextMaxSize = Mathf.Min(input.textComponent.fontSize, 24);
                input.textComponent.fontSize = input.textComponent.resizeTextMaxSize;
                RectTransform textRoot = VisualRoot(input.textComponent);
                if (textRoot != null)
                    textRoot.SetAsLastSibling();
            }
            Graphic placeholder = input.placeholder;
            if (placeholder != null)
                placeholder.color = new Color(0.48f, 0.62f, 0.69f, 0.75f);
        }

        Text[] labels = Resources.FindObjectsOfTypeAll<Text>();
        for (int i = 0; i < labels.Length; i++)
        {
            if (IsLoadedSceneObject(labels[i]))
                StyleLabel(labels[i]);
        }

        UpgradeRecognizedPanels();

        ScrollRect[] scrollRects = Resources.FindObjectsOfTypeAll<ScrollRect>();
        for (int i = 0; i < scrollRects.Length; i++)
        {
            ScrollRect scroll = scrollRects[i];
            if (!IsLoadedSceneObject(scroll))
                continue;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 32f;
        }
    }

    private void StyleButton(Button button)
    {
        // Authored hangar and combat controls own their mesh and selection state.
        if (button.GetComponent<HangarGraphic>() != null || button.GetComponent<CombatGraphic>() != null)
            return;
        if (!styledControls.Add(button.GetInstanceID()))
            return;

        ThemeRole role = RoleFor(button);
        UpgradeButtonGraphic(button, role);
        StyleSelectable(button, role);
        Text label = UGUIRuntime.Label(button);
        if (label != null)
        {
            label.font = ChineseFontFallback.Font;
            label.fontStyle = FontStyle.Normal;
            label.color = Ink;
            label.raycastTarget = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = Mathf.Min(label.fontSize, 26);
            label.fontSize = label.resizeTextMaxSize;
            Shadow shadow = label.GetComponent<Shadow>();
            if (shadow == null)
                shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.04f, 0.08f, 0.36f);
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;
        }

        if (button.GetComponent<UGUIInteractionFeedback>() == null)
            button.gameObject.AddComponent<UGUIInteractionFeedback>();
    }

    private static void StyleLabel(Text label)
    {
        if (label == null)
            return;
        if (label.GetComponentInParent<HangarGraphic>() != null ||
            label.GetComponentInParent<CombatGraphic>() != null ||
            label.GetComponentInParent<RankPanel>() != null)
            return;
        label.raycastTarget = false;
        label.font = ChineseFontFallback.Font;
        label.fontStyle = FontStyle.Normal;
        label.supportRichText = true;
        label.text = UGUIRuntime.ConvertLegacyRichText(label.text);
        label.lineSpacing = Mathf.Max(1f, label.lineSpacing);

        string key = label.name.ToLowerInvariant();
        bool title = key.Contains("title") || key.Contains("header") || key.Contains("caption");
        bool muted = key.Contains("subtitle") || key.Contains("hint") || key.Contains("detail");
        if (title)
            label.color = label.color.a > 0.01f ? new Color(Ink.r, Ink.g, Ink.b, label.color.a) : Ink;
        else if (muted)
            label.color = label.color.a > 0.01f ? new Color(MutedInk.r, MutedInk.g, MutedInk.b, label.color.a) : MutedInk;

        if (title)
        {
            label.fontSize = Mathf.Clamp(label.fontSize, 20, 34);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 15;
            label.resizeTextMaxSize = label.fontSize;
        }
    }

    private static void StyleSelectable(Selectable selectable, ThemeRole role)
    {
        selectable.transition = selectable is Toggle
            ? Selectable.Transition.None
            : Selectable.Transition.ColorTint;
        selectable.navigation = new Navigation { mode = Navigation.Mode.Automatic };

        // Switches draw and update their own track/knob. Applying the generic
        // selectable tint here overwrites the on/off colors and leaves the
        // control looking like two stacked button backgrounds.
        if (selectable is Toggle)
        {
            if (selectable.targetGraphic != null)
                selectable.targetGraphic.raycastTarget = true;
            return;
        }

        if (selectable.targetGraphic is UGUIButtonChrome)
        {
            selectable.targetGraphic.raycastTarget = true;
            ColorBlock chromeColors = selectable.colors;
            chromeColors.normalColor = Color.white;
            chromeColors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            chromeColors.pressedColor = new Color(0.72f, 0.78f, 0.82f, 1f);
            chromeColors.selectedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
            chromeColors.disabledColor = new Color(0.40f, 0.44f, 0.47f, 0.55f);
            chromeColors.colorMultiplier = 1f;
            chromeColors.fadeDuration = 0.08f;
            selectable.colors = chromeColors;
            selectable.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
            return;
        }

        if (selectable.targetGraphic is UGUISliderRailChrome)
        {
            selectable.targetGraphic.raycastTarget = true;
            ColorBlock sliderColors = selectable.colors;
            sliderColors.normalColor = Color.white;
            sliderColors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            sliderColors.pressedColor = new Color(0.78f, 0.88f, 0.92f, 1f);
            sliderColors.selectedColor = sliderColors.highlightedColor;
            sliderColors.disabledColor = new Color(0.45f, 0.50f, 0.52f, 0.55f);
            sliderColors.colorMultiplier = 1f;
            sliderColors.fadeDuration = 0.06f;
            selectable.colors = sliderColors;
            selectable.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
            return;
        }

        if (selectable.targetGraphic != null)
        {
            selectable.targetGraphic.raycastTarget = true;
            Image image = selectable.targetGraphic as Image;
            if (image != null && image.sprite == null && IsNearlyWhite(image.color))
                image.color = SurfaceColor(role);
        }

        ColorBlock colors = selectable.colors;
        colors.normalColor = SurfaceColor(role);
        colors.highlightedColor = role == ThemeRole.Danger
            ? new Color(0.78f, 0.29f, 0.23f, 1f)
            : new Color(0.10f, 0.62f, 0.72f, 1f);
        colors.pressedColor = role == ThemeRole.Danger
            ? new Color(0.52f, 0.20f, 0.17f, 1f)
            : new Color(0.06f, 0.38f, 0.46f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.07f, 0.09f, 0.11f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        selectable.colors = colors;
        if (selectable.targetGraphic != null)
            selectable.targetGraphic.CrossFadeColor(colors.normalColor, 0f, true, true);
    }

    private void PreparePanel(Component controller, GameObject view)
    {
        if (view == null)
            return;

        CanvasGroup group = view.GetComponent<CanvasGroup>();
        if (group == null)
            group = view.AddComponent<CanvasGroup>();
        group.interactable = true;
        group.blocksRaycasts = true;
        group.alpha = 1f;

        if (controller is SettingPanel || controller is RankPanel ||
            controller is DifficultyPanel || controller is QuitPanel || controller is GameOverPanel)
        {
            EnsureModalScrim(view.transform);
        }

        UGUIScreenTransition transition = view.GetComponent<UGUIScreenTransition>();
        if (transition != null)
            Destroy(transition);

        StyleScene();
        ImproveLayout(controller, view);

        if (EventSystem.current != null)
        {
            Button first = view.GetComponentInChildren<Button>(true);
            if (first != null && first.interactable)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
    }

    private static void EnsureModalScrim(Transform view)
    {
        Transform existing = view.Find("UGUI_ModalScrim");
        GameObject blocker = existing != null ? existing.gameObject :
            new GameObject("UGUI_ModalScrim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = blocker.GetComponent<RectTransform>();
        if (existing == null)
            rect.SetParent(view, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        RectTransform canvasRect = UGUIRuntime.Root as RectTransform;
        Vector2 canvasSize = canvasRect != null ? canvasRect.rect.size : new Vector2(1920f, 1080f);
        rect.sizeDelta = new Vector2(Mathf.Max(1920f, canvasSize.x), Mathf.Max(1080f, canvasSize.y));
        rect.SetAsFirstSibling();
        Image image = blocker.GetComponent<Image>();
        image.color = new Color(0f, 0.015f, 0.03f, 0.5f);
        image.raycastTarget = true;
        blocker.SetActive(true);
    }

    private void ImproveLayouts()
    {
        ImproveController(Object.FindObjectOfType<BeginPanel>(true));
        ImproveController(Object.FindObjectOfType<ChoosePanel>(true));
        ImproveController(Object.FindObjectOfType<DifficultyPanel>(true));
        ImproveController(Object.FindObjectOfType<SettingPanel>(true));
        ImproveController(Object.FindObjectOfType<RankPanel>(true));
        ImproveController(Object.FindObjectOfType<GamePanel>(true));
        ImproveController(Object.FindObjectOfType<QuitPanel>(true));
        ImproveController(Object.FindObjectOfType<GameOverPanel>(true));
    }

    private void ImproveController(Component controller)
    {
        if (controller == null)
            return;
        Transform view = UGUIRuntime.View(controller);
        if (view != null)
            ImproveLayout(controller, view.gameObject);
    }

    private void ImproveLayout(Component controller, GameObject view)
    {
        if (controller == null || view == null || !adjustedViews.Add(view.GetInstanceID()))
            return;

        if (controller is GamePanel)
        {
            // The combat HUD is authored as a complete native layout.
        }
        else if (controller is BeginPanel)
        {
            RestylePanelSurface(view.transform, "Polish_MenuPanel", Panel, 0.94f);
            BeginPanel menu = (BeginPanel)controller;
            ResizeButton(menu.btnBegin, 340f, 62f);
            ResizeButton(menu.btnRank, 340f, 58f);
            ResizeButton(menu.btnSetting, 340f, 58f);
            ResizeButton(menu.btnQuit, 340f, 58f);
        }
        else if (controller is ChoosePanel)
        {
            // Authored as a complete native UGUI layout; no legacy positioning.
        }
        else if (controller is DifficultyPanel)
        {
            RestylePanelSurface(view.transform, "Polish_Dialog", Panel, 0.96f);
            DifficultyPanel difficulty = (DifficultyPanel)controller;
            ResizeButton(difficulty.btnEasy, 300f, 74f);
            ResizeButton(difficulty.btnNormal, 300f, 74f);
            ResizeButton(difficulty.btnHard, 300f, 74f);
            ResizeButton(difficulty.btnStart, 310f, 60f);
            ResizeButton(difficulty.btnClose, 240f, 54f);
        }
        else if (controller is SettingPanel)
        {
            RestylePanelSurface(view.transform, "Polish_Dialog", Panel, 0.97f);
            SettingPanel settings = (SettingPanel)controller;
            ResizeButton(settings.btnClose, 220f, 52f);
            RepairSlider(settings.sliderMusic);
            RepairSlider(settings.sliderSound);
            LayoutSettings(view.transform, settings);
        }
        else if (controller is RankPanel)
        {
            RestylePanelSurface(view.transform, "Polish_Dialog", Panel, 0.97f);
            RankPanel rank = (RankPanel)controller;
            ResizeButton(rank.btnClose, 220f, 52f);
        }
        else if (controller is QuitPanel)
        {
            // Authored as a complete native modal layout.
        }
        else if (controller is GameOverPanel)
        {
            // Authored as a complete native modal layout.
        }
    }

    private static void RestylePanelSurface(Transform root, string objectName, Color color, float alpha)
    {
        GameObject target = UGUIRuntime.FindObject(root, objectName);
        Image image = target != null ? target.GetComponent<Image>() : null;
        if (image == null && target != null)
            image = target.GetComponentInChildren<Image>(true);
        if (image == null)
            return;
        image.color = new Color(color.r, color.g, color.b, alpha);
        UpgradePanelGraphic(image, objectName, alpha);
    }

    private void UpgradeRecognizedPanels()
    {
        Image[] images = Resources.FindObjectsOfTypeAll<Image>();
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (!IsLoadedSceneObject(image) || image.GetComponent<Button>() != null ||
                image.GetComponent<InputField>() != null || image.GetComponent<Toggle>() != null)
                continue;
            string key = image.name.ToLowerInvariant();
            bool panel = key.Contains("polish_dialog") || key.Contains("polish_menupanel") ||
                key.Contains("polish_missionpanel") || key.Contains("polish_timerpanel") ||
                key.Contains("polish_healthpanel") || key.Contains("polish_skillpanel") ||
                key.Contains("polish_statrow") || key.Contains("polish_settingsrow") ||
                key.Contains("helpsection") ||
                key == "levelselectpanel" || key == "lobbypanel" ||
                key.Contains("missionresultpanel") || key.Contains("networkresultpanel");
            if (!panel && image.transform.parent != null && image.transform.parent.name == "SupportModal")
                panel = key == "supportpanel" || key.Contains("helpsection");
            if (panel)
                UpgradePanelGraphic(image, image.name, image.color.a);
        }
    }

    private static void UpgradePanelGraphic(Image source, string key, float alpha)
    {
        if (source == null)
            return;
        Transform existing = source.transform.Find("UGUI_PanelChrome");
        UGUIPanelChrome chrome = existing != null ? existing.GetComponent<UGUIPanelChrome>() : null;
        if (chrome == null)
        {
            GameObject go = new GameObject("UGUI_PanelChrome", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(UGUIPanelChrome));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(source.transform, false);
            Stretch(rect);
            rect.SetAsFirstSibling();
            chrome = go.GetComponent<UGUIPanelChrome>();
        }
        bool compact = key.ToLowerInvariant().Contains("missionpanel") ||
            key.ToLowerInvariant().Contains("timerpanel") ||
            key.ToLowerInvariant().Contains("healthpanel") ||
            key.ToLowerInvariant().Contains("skillpanel") || source.rectTransform.rect.height < 260f;
        chrome.Configure(compact);
        chrome.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        chrome.raycastTarget = source.raycastTarget;
        source.enabled = false;
    }

    private static void UpgradeButtonGraphic(Button button, ThemeRole role)
    {
        if (button == null)
            return;
        Image source = button.targetGraphic as Image;
        Transform existing = button.transform.Find("UGUI_ButtonChrome");
        UGUIButtonChrome chrome = existing != null ? existing.GetComponent<UGUIButtonChrome>() : null;
        if (chrome == null)
        {
            GameObject go = new GameObject("UGUI_ButtonChrome", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(UGUIButtonChrome));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(button.transform, false);
            Stretch(rect);
            rect.SetAsFirstSibling();
            chrome = go.GetComponent<UGUIButtonChrome>();
        }
        chrome.SetRole(ToChromeRole(role));
        chrome.SetIcon(ResolveIcon(button));
        chrome.color = Color.white;
        chrome.raycastTarget = true;
        if (source != null)
            source.enabled = false;
        button.targetGraphic = chrome;
    }

    private static void UpgradeInputGraphic(InputField input)
    {
        if (input == null)
            return;
        if (input.targetGraphic is CombatGraphic)
            return;
        Image source = input.targetGraphic as Image;
        Transform existing = input.transform.Find("UGUI_InputChrome");
        UGUIButtonChrome chrome = existing != null ? existing.GetComponent<UGUIButtonChrome>() : null;
        if (chrome == null)
        {
            GameObject go = new GameObject("UGUI_InputChrome", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(UGUIButtonChrome));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(input.transform, false);
            Stretch(rect);
            rect.SetAsFirstSibling();
            chrome = go.GetComponent<UGUIButtonChrome>();
        }
        chrome.SetRole(UGUIChromeRole.Input);
        chrome.color = Color.white;
        chrome.raycastTarget = true;
        if (source != null)
            source.enabled = false;
        input.targetGraphic = chrome;
    }

    private static UGUIChromeRole ToChromeRole(ThemeRole role)
    {
        if (role == ThemeRole.Primary) return UGUIChromeRole.Primary;
        if (role == ThemeRole.Danger) return UGUIChromeRole.Danger;
        return UGUIChromeRole.Secondary;
    }

    public static UGUIChromeIcon ResolveIcon(Button button)
    {
        if (BeginPanel.Instance != null && button == BeginPanel.Instance.btnSetting)
            return UGUIChromeIcon.Audio;
        if (QuitPanel.Instance != null)
        {
            if (button == QuitPanel.Instance.btnClose) return UGUIChromeIcon.Play;
            if (button == QuitPanel.Instance.btnSure) return UGUIChromeIcon.Power;
            if (button == QuitPanel.Instance.btnRestart) return UGUIChromeIcon.Play;
        }
        Text label = UGUIRuntime.Label(button);
        string parentName = button.transform.parent != null ? button.transform.parent.name : string.Empty;
        string key = (button.name + " " + parentName + " " +
            (label != null ? label.text : string.Empty)).ToLowerInvariant();
        if (ContainsAny(key, "levelselect")) return UGUIChromeIcon.Back;
        if (ContainsAny(button.name.ToLowerInvariant(), "close", "back", "menu")) return UGUIChromeIcon.Back;
        if (ContainsAny(key, "network", "host", "join", "lobby")) return UGUIChromeIcon.Network;
        if (ContainsAny(key, "rank", "leader")) return UGUIChromeIcon.Trophy;
        if (ContainsAny(key, "help", "tutorial", "guide", "instruction")) return UGUIChromeIcon.Info;
        if (ContainsAny(key, "setting", "sound", "music", "audio", "btnset", "音频", "音乐", "设置"))
            return UGUIChromeIcon.Audio;
        if (ContainsAny(key, "quit", "exit", "leave")) return UGUIChromeIcon.Power;
        if (ContainsAny(key, "close", "back", "menu")) return UGUIChromeIcon.Back;
        if (ContainsAny(key, "pause")) return UGUIChromeIcon.Pause;
        if (ContainsAny(key, "begin", "start", "sure", "confirm", "ready", "retry", "restart", "execute"))
            return UGUIChromeIcon.Play;
        return UGUIChromeIcon.None;
    }

    private void EnsureTacticalBackdrop()
    {
        if (SceneManager.GetActiveScene().name != "BeginScene" || BeginPanel.Instance == null)
            return;
        Transform view = UGUIRuntime.View(BeginPanel.Instance);
        if (view == null || view.parent == null || view.parent.Find("UGUI_TacticalBackdrop") != null)
            return;
        GameObject go = new GameObject("UGUI_TacticalBackdrop", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(UGUITacticalBackdrop));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(view.parent, false);
        Stretch(rect);
        rect.SetSiblingIndex(Mathf.Max(0, view.GetSiblingIndex()));
        UGUITacticalBackdrop backdrop = go.GetComponent<UGUITacticalBackdrop>();
        backdrop.raycastTarget = false;
    }

    private static void MoveUp(Transform root, string objectName, float amount)
    {
        GameObject target = UGUIRuntime.FindObject(root, objectName);
        RectTransform rect = target != null ? target.transform as RectTransform : null;
        if (rect != null)
            rect.anchoredPosition += Vector2.up * amount;
    }

    private static void SetActive(Transform root, string objectName, bool active)
    {
        GameObject target = UGUIRuntime.FindObject(root, objectName);
        if (target != null)
            target.SetActive(active);
    }

    private static void MoveVisual(Component component, float amount)
    {
        RectTransform rect = VisualRoot(component);
        if (rect != null)
            rect.anchoredPosition += Vector2.up * amount;
    }

    private static void PositionVisual(Component component, Vector2 position)
    {
        RectTransform rect = VisualRoot(component);
        if (rect != null)
            rect.anchoredPosition = position;
    }

    private static void SetVisualY(Component component, float y)
    {
        RectTransform rect = VisualRoot(component);
        if (rect != null)
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
    }

    private static void LayoutObject(Transform root, string objectName, Vector2 position, Vector2 size)
    {
        GameObject target = UGUIRuntime.FindObject(root, objectName);
        LayoutRect(target != null ? target.transform as RectTransform : null, position, size);
    }

    private static void LayoutRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null)
            return;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void LayoutSettings(Transform view, SettingPanel settings)
    {
        LayoutObject(view, "Polish_SettingsRowMusic", new Vector2(0f, 75f), new Vector2(1120f, 142f));
        LayoutObject(view, "Polish_SettingsRowSound", new Vector2(0f, -100f), new Vector2(1120f, 142f));

        LayoutRect(settings.sliderMusic != null ? settings.sliderMusic.transform as RectTransform : null,
            new Vector2(-40f, 75f), new Vector2(430f, 52f));
        LayoutRect(settings.sliderSound != null ? settings.sliderSound.transform as RectTransform : null,
            new Vector2(-40f, -100f), new Vector2(430f, 52f));
        LayoutRect(settings.toggleMusic != null ? settings.toggleMusic.transform as RectTransform : null,
            new Vector2(330f, 75f), new Vector2(60f, 30f));
        LayoutRect(settings.toggleSound != null ? settings.toggleSound.transform as RectTransform : null,
            new Vector2(330f, -100f), new Vector2(60f, 30f));

        GameObject closeRoot = UGUIRuntime.ControlRoot(settings.btnClose);
        LayoutRect(closeRoot != null ? closeRoot.transform as RectTransform : null,
            new Vector2(0f, -290f), new Vector2(220f, 52f));
    }

    private static void LayoutHealthBlock(GameObject source, int index)
    {
        if (source == null)
            return;
        RectTransform visual = source.transform as RectTransform;
        if (visual == null)
            return;

        RectTransform root = VisualRoot(visual);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = new Vector2(-735f + index * 51f, -438f);
        visual.sizeDelta = new Vector2(42f, 42f);
    }

    private static RectTransform VisualRoot(Component component)
    {
        if (component == null)
            return null;
        RectTransform rect = component.transform as RectTransform;
        RectTransform parent = rect != null ? rect.parent as RectTransform : null;
        if (parent != null && parent.GetComponent<Canvas>() == null && parent.GetComponent<Graphic>() == null)
            return parent;
        return rect;
    }

    private static void RepairSlider(Slider slider)
    {
        if (slider == null)
            return;

        EnsureCanvasRenderer(slider.transform.Find("UGUI_Track"));
        EnsureCanvasRenderer(slider.transform.Find("UGUI_HandleArea/UGUI_Handle"));
        if (slider.transform.Find("UGUI_FillArea") != null)
            return;

        RectTransform root = slider.transform as RectTransform;
        if (root == null)
            return;
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 420f);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 52f);

        Graphic[] oldGraphics = slider.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < oldGraphics.Length; i++)
            oldGraphics[i].enabled = false;

        RectTransform railRect = CreateSliderGraphicRect(root, "UGUI_Track");
        Stretch(railRect);
        UGUISliderRailChrome track = railRect.gameObject.AddComponent<UGUISliderRailChrome>();
        track.color = Color.white;
        track.raycastTarget = true;

        RectTransform fillArea = CreateSliderRect(root, "UGUI_FillArea");
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.offsetMin = new Vector2(10f, -5f);
        fillArea.offsetMax = new Vector2(-10f, 5f);
        RectTransform fillRect = CreateSliderGraphicRect(fillArea, "UGUI_Fill");
        Stretch(fillRect);
        UGUISliderFillChrome fill = fillRect.gameObject.AddComponent<UGUISliderFillChrome>();
        fill.color = Color.white;
        fill.raycastTarget = false;

        RectTransform handleArea = CreateSliderRect(root, "UGUI_HandleArea");
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(12f, 15f);
        handleArea.offsetMax = new Vector2(-12f, -15f);
        RectTransform handleRect = CreateSliderGraphicRect(handleArea, "UGUI_Handle");
        UGUISliderHandleChrome handle = handleRect.gameObject.AddComponent<UGUISliderHandleChrome>();
        handle.color = Color.white;
        handle.raycastTarget = false;

        slider.direction = Slider.Direction.LeftToRight;
        slider.targetGraphic = track;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        handleRect.sizeDelta = new Vector2(22f, 0f);
    }

    private static void RepairToggle(Toggle toggle)
    {
        if (toggle == null)
            return;

        UGUIToggleSwitchVisual visual = toggle.GetComponent<UGUIToggleSwitchVisual>();
        if (visual == null)
            toggle.gameObject.AddComponent<UGUIToggleSwitchVisual>();
    }

    private static RectTransform CreateSliderRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private static RectTransform CreateSliderGraphicRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private static void EnsureCanvasRenderer(Transform target)
    {
        if (target != null && target.GetComponent<CanvasRenderer>() == null)
            target.gameObject.AddComponent<CanvasRenderer>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void ResizeButton(Button button, float width, float height)
    {
        GameObject root = UGUIRuntime.ControlRoot(button);
        RectTransform rect = root != null ? root.transform as RectTransform : null;
        if (rect != null)
            rect.sizeDelta = new Vector2(width, height);
    }

    private static ThemeRole RoleFor(Button button)
    {
        if (QuitPanel.Instance != null && button.transform.IsChildOf(UGUIRuntime.View(QuitPanel.Instance)))
        {
            if (button == QuitPanel.Instance.btnClose) return ThemeRole.Primary;
            if (button == QuitPanel.Instance.btnSure) return ThemeRole.Danger;
            return ThemeRole.Secondary;
        }
        if (GameOverPanel.Instance != null && button.transform.IsChildOf(UGUIRuntime.View(GameOverPanel.Instance)))
            return button == GameOverPanel.Instance.btnSure ? ThemeRole.Primary : ThemeRole.Secondary;

        Text label = UGUIRuntime.Label(button);
        string key = (button.name + " " + (label != null ? label.text : string.Empty)).ToLowerInvariant();
        if (ContainsAny(key, "quit", "exit", "leave", "delete", "退出", "离开"))
            return ThemeRole.Danger;
        if (ContainsAny(key, "begin", "start", "sure", "confirm", "ready", "host", "join", "retry", "restart",
            "开始", "确认", "准备", "创建", "加入", "重试", "重开", "保存"))
            return ThemeRole.Primary;
        return ThemeRole.Secondary;
    }

    private static Color SurfaceColor(ThemeRole role)
    {
        if (role == ThemeRole.Primary)
            return new Color(0.22f, 0.68f, 0.76f, 1f);
        if (role == ThemeRole.Danger)
            return new Color(0.72f, 0.30f, 0.24f, 1f);
        return new Color(0.20f, 0.36f, 0.44f, 1f);
    }

    private static bool IsNearlyWhite(Color color)
    {
        return color.r > 0.88f && color.g > 0.88f && color.b > 0.88f;
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        for (int i = 0; i < values.Length; i++)
            if (source.Contains(values[i])) return true;
        return false;
    }

    private static bool IsLoadedSceneObject(Component component)
    {
        return component != null && component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded;
    }

    private enum ThemeRole
    {
        Primary,
        Secondary,
        Danger,
    }
}

[DisallowMultipleComponent]
public sealed class UGUIScreenTransition : MonoBehaviour
{
    private CanvasGroup group;
    private float progress;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (group == null)
            group = GetComponent<CanvasGroup>();
        progress = 1f;
        if (group != null)
            group.alpha = 1f;
    }

    private void OnDisable()
    {
        if (group != null)
            group.alpha = 1f;
    }

    private void Update()
    {
        if (group == null || progress >= 1f)
            return;
        progress = Mathf.Clamp01(progress + Time.unscaledDeltaTime / 0.14f);
        group.alpha = 1f - Mathf.Pow(1f - progress, 3f);
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Toggle))]
public sealed class UGUIToggleSwitchVisual : MonoBehaviour
{
    private static readonly Color OffTrack = new Color(0.035f, 0.14f, 0.18f, 1f);
    private static readonly Color OnTrack = new Color(0.06f, 0.46f, 0.52f, 1f);
    private static readonly Color OffKnob = new Color(0.38f, 0.55f, 0.62f, 1f);
    private static readonly Color OnKnob = new Color(0.86f, 0.98f, 1f, 1f);

    private Toggle toggle;
    private Image track;
    private Image knob;
    private RectTransform knobRect;
    private static Sprite roundedTrackSprite;
    private static Sprite roundKnobSprite;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
        Build();
    }

    private void OnEnable()
    {
        if (toggle == null)
            toggle = GetComponent<Toggle>();
        Build();
        if (toggle != null)
            toggle.onValueChanged.AddListener(OnValueChanged);
        UpdateVisual(toggle != null && toggle.isOn, true);
    }

    private void OnDisable()
    {
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(OnValueChanged);
    }

    private void Build()
    {
        if (toggle == null)
            return;

        RectTransform root = transform as RectTransform;
        if (root != null)
            root.sizeDelta = new Vector2(60f, 30f);

        track = GetOrCreateImage("UGUI_ToggleTrack");
        track.sprite = RoundedSprite(ref roundedTrackSprite, 60, 30, 15f, "UGUI_RoundedTrack");
        track.type = Image.Type.Simple;
        track.rectTransform.anchorMin = Vector2.zero;
        track.rectTransform.anchorMax = Vector2.one;
        track.rectTransform.offsetMin = Vector2.zero;
        track.rectTransform.offsetMax = Vector2.zero;
        track.raycastTarget = true;
        Outline trackOutline = track.GetComponent<Outline>();
        if (trackOutline != null)
            trackOutline.enabled = false;

        knob = GetOrCreateImage("UGUI_ToggleKnob");
        knob.sprite = RoundedSprite(ref roundKnobSprite, 24, 24, 12f, "UGUI_RoundKnob");
        knob.type = Image.Type.Simple;
        knobRect = knob.rectTransform;
        knobRect.anchorMin = knobRect.anchorMax = new Vector2(0.5f, 0.5f);
        knobRect.pivot = new Vector2(0.5f, 0.5f);
        knobRect.sizeDelta = new Vector2(22f, 22f);
        knob.raycastTarget = false;

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic != track && graphic != knob)
                graphic.enabled = false;
        }

        toggle.targetGraphic = track;
        toggle.graphic = null;
    }

    private Image GetOrCreateImage(string name)
    {
        Transform child = transform.Find(name);
        Image image = child != null ? child.GetComponent<Image>() : null;
        if (image != null)
            return image;

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        return go.GetComponent<Image>();
    }

    private void OnValueChanged(bool value)
    {
        UpdateVisual(value, false);
    }

    private void UpdateVisual(bool value, bool immediate)
    {
        if (track == null || knobRect == null)
            return;

        track.color = value ? OnTrack : OffTrack;
        knob.color = value ? OnKnob : OffKnob;
        float x = value ? 14f : -14f;
        if (immediate)
            knobRect.anchoredPosition = new Vector2(x, 0f);
        else
            knobRect.anchoredPosition = new Vector2(x, 0f);
    }

    public void Refresh()
    {
        if (toggle == null)
            toggle = GetComponent<Toggle>();
        Build();
        UpdateVisual(toggle != null && toggle.isOn, true);
    }

    private static Sprite RoundedSprite(ref Sprite cached, int width, int height, float radius, string name)
    {
        if (cached != null)
            return cached;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = name + "Texture";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 solid = new Color32(255, 255, 255, 255);
        float minX = radius - 0.5f;
        float maxX = width - radius - 0.5f;
        float minY = radius - 0.5f;
        float maxY = height - radius - 0.5f;
        float radiusSquared = radius * radius;
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float closestX = Mathf.Clamp(x, minX, maxX);
                float closestY = Mathf.Clamp(y, minY, maxY);
                float dx = x - closestX;
                float dy = y - closestY;
                pixels[y * width + x] = dx * dx + dy * dy <= radiusSquared ? solid : clear;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        cached = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        cached.name = name;
        cached.hideFlags = HideFlags.HideAndDontSave;
        return cached;
    }

    private static Sprite FindLoadedSprite(params string[] names)
    {
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < names.Length; i++)
            for (int j = 0; j < sprites.Length; j++)
                if (sprites[j].name == names[i])
                    return sprites[j];
        return null;
    }
}

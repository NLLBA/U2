using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CampaignLevelSelectUI : MonoBehaviour
{
    private static CampaignLevelSelectUI instance;

    private GameObject viewRoot;
    private Image panelTemplate;
    private Image scrimTemplate;
    private Button buttonTemplate;
    private Image accentTemplate;
    private Text labelTemplate;
    private int selectedLevelId = 1;
    private bool selectedEndless;
    private bool selectedTraining;

    private static readonly Color PanelColor = new Color(0.015f, 0.055f, 0.09f, 0.98f);
    private static readonly Color TextColor = new Color(0.91f, 0.96f, 1f, 1f);
    private static readonly Color MutedColor = new Color(0.52f, 0.69f, 0.76f, 1f);
    private static readonly Color AccentColor = new Color(0.18f, 0.88f, 1f, 1f);
    private static readonly Color EndlessColor = new Color(0.8f, 0.55f, 1f, 1f);

    public static CampaignLevelSelectUI Ensure()
    {
        if (instance != null)
            return instance;
        GameObject host = new GameObject("CampaignLevelSelectUI");
        return host.AddComponent<CampaignLevelSelectUI>();
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
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
            instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DestroyView();
    }

    public void Open()
    {
        selectedLevelId = Mathf.Clamp(GameDataMgr.Instance.selectedLevelId, 1, CampaignProgress.MaxUnlocked);
        selectedEndless = GameDataMgr.Instance.IsEndless;
        selectedTraining = GameDataMgr.Instance.IsTraining;
        if (DifficultyPanel.Instance != null)
            DifficultyPanel.Instance.Hideme();
        Build();
    }

    public void SelectLevel(int levelId)
    {
        if (!CampaignProgress.IsUnlocked(levelId))
            return;
        selectedLevelId = Mathf.Clamp(levelId, 1, GameDataMgr.MaxLevelId);
        selectedEndless = false;
        selectedTraining = false;
        Build();
    }

    public void SelectEndless()
    {
        selectedEndless = true;
        selectedTraining = false;
        Build();
    }

    public void SelectTraining()
    {
        selectedTraining = true;
        selectedEndless = false;
        Build();
    }

    private void Build()
    {
        DestroyView();
        Transform parent = GetUiParent();
        if (parent == null)
            return;

        UGUIRuntime.EnsureEventSystem();
        viewRoot = UGUIRuntime.CreateRect(parent, "CampaignLevelSelect", Vector3.zero, 1920, 1080).gameObject;
        viewRoot.SetActive(false);
        CacheTemplates(parent);

        CreateSprite("LevelSelectScrim", Vector3.zero, 4000, 2400, scrimTemplate,
            new Color(0f, 0.015f, 0.03f, 0.50f), true);
        CreateSprite("LevelSelectPanel", Vector3.zero, 1420, 820, panelTemplate,
            new Color(0.03f, 0.075f, 0.115f, 0.98f), true);
        CreateSprite("LevelSelectAccent", new Vector3(0f, 350f, 0f), 1140, 4, accentTemplate,
            AccentColor, false);

        CreateLabel("LevelSelectTitle", new Vector3(0f, 305f, 0f), 1100, 52,
            "选择任务", 34, TextColor, TextAnchor.MiddleCenter);

        for (int levelId = 1; levelId <= GameDataMgr.MaxLevelId; levelId++)
        {
            LevelInfo level = GameDataMgr.Instance.GetLevelById(levelId);
            bool selected = !selectedEndless && !selectedTraining && selectedLevelId == levelId;
            string text = levelId.ToString("00") + "  " + ShortName(level.name);
            Button button = CreateButton("LevelButton" + levelId,
                new Vector3(-360f, 190f - (levelId - 1) * 60f, 0f), 400, 50,
                text, selected ? AccentColor : TextColor);
            ApplySelectionStyle(button, selected, AccentColor);
            int capturedId = levelId;
            button.onClick.AddListener(() => SelectLevel(capturedId));
        }

        Button endless = CreateButton("EndlessModeButton", new Vector3(-360f, -136f, 0f), 400, 50,
            "无尽模式", selectedEndless ? EndlessColor : TextColor);
        ApplySelectionStyle(endless, selectedEndless, EndlessColor);
        endless.onClick.AddListener(SelectEndless);

        Button training = CreateButton("TrainingModeButton", new Vector3(-360f, -198f, 0f), 400, 50,
            "训练模式", selectedTraining ? AccentColor : TextColor);
        ApplySelectionStyle(training, selectedTraining, AccentColor);
        training.onClick.AddListener(SelectTraining);

        LevelInfo selectedLevel = GameDataMgr.Instance.GetLevelById(selectedLevelId);
        string modeLabel;
        string missionName;
        string objective;
        string pattern;
        string status;
        string record;
        string startText;
        Color modeColor;
        if (selectedTraining)
        {
            modeLabel = "基础训练";
            missionName = "训练模式";
            objective = "练习移动、护盾、技能和点击核心";
            pattern = "无伤害环境 · Tab可跳过当前步骤";
            status = "不计分 · 不写入排行榜";
            record = "完成训练后按 Enter 返回主菜单";
            startText = "开始训练";
            modeColor = AccentColor;
        }
        else if (selectedEndless)
        {
            modeLabel = "无尽模式";
            missionName = "无尽生存";
            objective = "持续生存，挑战最高分";
            pattern = "每12秒进入下一波\n难度与Boss轮次持续增加";
            status = "记录最高波数与最终得分";
            record = "结束后填写名字并提交排行榜";
            startText = "开始无尽挑战";
            modeColor = EndlessColor;
        }
        else
        {
            modeLabel = "任务 " + selectedLevel.id.ToString("00") + " · " +
                GameDataMgr.Instance.DifficultyName() + "难度";
            missionName = ShortName(selectedLevel.name);
            objective = GameDataMgr.Instance.MissionObjectiveText(selectedLevel);
            pattern = PatternDescription(selectedLevel);
            status = "任务评价  " + CampaignProgress.StarsText(selectedLevelId) +
                "  ·  挑战 " + CampaignProgress.ChallengeCount(selectedLevelId) + "/3";
            record = "最高分 " + CampaignProgress.BestScore(selectedLevelId) +
                "  ·  最长生存 " + FormatTime(CampaignProgress.BestTime(selectedLevelId)) +
                "\n挑战：零受击 · 零失误 · 全程连击";
            startText = "执行任务";
            modeColor = AccentColor;
        }

        CreateLabel("SelectedMissionLabel", new Vector3(275f, 175f, 0f), 560, 42,
            modeLabel, 18, modeColor, TextAnchor.MiddleLeft);
        CreateLabel("SelectedMissionName", new Vector3(275f, 125f, 0f), 560, 52,
            missionName, 29, TextColor, TextAnchor.MiddleLeft);
        CreateLabel("SelectedMissionObjective", new Vector3(275f, 45f, 0f), 560, 90,
            objective, 19, TextColor, TextAnchor.MiddleLeft);
        CreateLabel("SelectedMissionPattern", new Vector3(275f, -38f, 0f), 560, 86,
            pattern, 16, MutedColor, TextAnchor.MiddleLeft);
        CreateLabel("SelectedMissionStars", new Vector3(275f, -112f, 0f), 560, 38,
            status, 19, modeColor, TextAnchor.MiddleLeft);
        CreateLabel("SelectedMissionRecord", new Vector3(275f, -168f, 0f), 560, 56,
            record, 15, MutedColor, TextAnchor.MiddleLeft);

        Button back = CreateButton("LevelSelectBack", new Vector3(-275f, -315f, 0f), 260, 54,
            "返回难度选择", MutedColor);
        back.onClick.AddListener(Back);
        Button start = CreateButton("LevelSelectStart", new Vector3(275f, -315f, 0f), 310, 58,
            startText, modeColor);
        start.onClick.AddListener(StartMission);

        // Do not reveal the authored black panel before custom chrome has replaced it.
        UGUIVisualSystem.RefreshNow();
        Canvas.ForceUpdateCanvases();
        viewRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
    }

    private void StartMission()
    {
        if (selectedTraining)
            GameDataMgr.Instance.SelectTraining();
        else if (selectedEndless)
            GameDataMgr.Instance.SelectEndless();
        else
            GameDataMgr.Instance.SetLevel(selectedLevelId);
        Time.timeScale = 1f;
        SceneManager.LoadScene("GameScene");
    }

    private void Back()
    {
        DestroyView();
        if (DifficultyPanel.Instance != null)
            DifficultyPanel.Instance.Showme();
    }

    private void DestroyView()
    {
        if (viewRoot == null)
            return;
        viewRoot.SetActive(false);
        Destroy(viewRoot);
        viewRoot = null;
    }

    private static Transform GetUiParent()
    {
        return UGUIRuntime.Root;
    }

    private void CacheTemplates(Transform parent)
    {
        panelTemplate = scrimTemplate = accentTemplate = null;
        buttonTemplate = null;
        foreach (Image image in parent.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null || image.transform.IsChildOf(viewRoot.transform))
                continue;
            switch (image.sprite.name)
            {
                case "ui_panel": panelTemplate = image; break;
                case "ui_scrim": scrimTemplate = image; break;
                case "ui_button_primary": buttonTemplate = image.GetComponent<Button>(); break;
                case "ui_accent": accentTemplate = image; break;
            }
        }
        if (DifficultyPanel.Instance != null)
            labelTemplate = DifficultyPanel.Instance.labTitle;
    }

    private Image CreateSprite(string name, Vector3 position, int width, int height,
        Image template, Color color, bool raycastTarget)
    {
        return UGUIRuntime.CreateImage(viewRoot.transform, name, position, width, height,
            template, null, color, raycastTarget);
    }

    private Text CreateLabel(string name, Vector3 position, int width, int height, string text,
        int size, Color color, TextAnchor alignment)
    {
        return UGUIRuntime.CreateText(viewRoot.transform, name, position, width, height,
            text, size, color, alignment, labelTemplate);
    }

    private Button CreateButton(string name, Vector3 position, int width, int height, string text, Color color)
    {
        return UGUIRuntime.CreateButton(viewRoot.transform, name, position, width, height,
            text, color, buttonTemplate, labelTemplate);
    }

    private static void ApplySelectionStyle(Button button, bool selected, Color accent)
    {
        if (button == null || button.targetGraphic == null)
            return;

        UGUIButtonChrome chrome = button.targetGraphic as UGUIButtonChrome;
        if (chrome != null)
        {
            chrome.SetRole(selected ? UGUIChromeRole.Selected : UGUIChromeRole.Secondary);
            button.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
            return;
        }

        Color normal = selected
            ? new Color(accent.r * 0.62f, accent.g * 0.62f, accent.b * 0.62f, 1f)
            : new Color(0.12f, 0.25f, 0.31f, 1f);
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.selectedColor = selected ? accent : new Color(0.12f, 0.62f, 0.72f, 1f);
        colors.highlightedColor = selected ? accent : new Color(0.12f, 0.62f, 0.72f, 1f);
        colors.pressedColor = selected
            ? new Color(accent.r * 0.45f, accent.g * 0.45f, accent.b * 0.45f, 1f)
            : new Color(0.06f, 0.38f, 0.46f, 1f);
        button.colors = colors;
        button.targetGraphic.CrossFadeColor(normal, 0f, true, true);
    }

    private static string ShortName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName))
            return "未知任务";
        int separator = fullName.IndexOf('-');
        return separator >= 0 && separator + 1 < fullName.Length ? fullName.Substring(separator + 1) : fullName;
    }

    private static string PatternDescription(LevelInfo level)
    {
        string text = level.featureTitle + "\n" + level.featureHint;
        if (level != null && PlayerObject.AffinityLevelForHero(GameDataMgr.Instance.heroIndex) == level.id)
            text += "\n当前角色：" + PlayerObject.StageAffinityText(GameDataMgr.Instance.heroIndex);
        return text;
    }

    private static string FormatTime(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        return (seconds / 60) + "m" + (seconds % 60).ToString("00") + "s";
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

public class CampaignLevelSelectUI : MonoBehaviour
{
    private static CampaignLevelSelectUI instance;

    private GameObject viewRoot;
    private UI2DSprite panelTemplate;
    private UI2DSprite scrimTemplate;
    private UI2DSprite buttonTemplate;
    private UI2DSprite accentTemplate;
    private UILabel labelTemplate;
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

        viewRoot = new GameObject("CampaignLevelSelect");
        viewRoot.transform.SetParent(parent, false);
        viewRoot.layer = parent.gameObject.layer;
        viewRoot.AddComponent<UIPanel>().depth = 85;
        CacheTemplates(parent);

        UI2DSprite scrim = CreateSprite("LevelSelectScrim", Vector3.zero, 4000, 2400, scrimTemplate,
            new Color(1f, 1f, 1f, 0.78f), true, -100);
        CreateSprite("LevelSelectPanel", Vector3.zero, 1320, 820, panelTemplate, Color.white, true, -50);
        CreateSprite("LevelSelectAccent", new Vector3(0f, 350f, 0f), 1140, 4, accentTemplate,
            AccentColor, false, -10);

        CreateLabel("LevelSelectTitle", new Vector3(0f, 305f, 0f), 1100, 52,
            "选择任务", 34, TextColor, NGUIText.Alignment.Center);

        for (int levelId = 1; levelId <= GameDataMgr.MaxLevelId; levelId++)
        {
            LevelInfo level = GameDataMgr.Instance.GetLevelById(levelId);
            bool selected = !selectedEndless && !selectedTraining && selectedLevelId == levelId;
            string text = levelId.ToString("00") + "  " + ShortName(level.name);
            UIButton button = CreateButton("LevelButton" + levelId,
                new Vector3(-330f, 190f - (levelId - 1) * 66f, 0f), 470, 56,
                text, selected ? AccentColor : TextColor);
            int capturedId = levelId;
            button.onClick.Add(new EventDelegate(() => SelectLevel(capturedId)));
        }

        UIButton endless = CreateButton("EndlessModeButton", new Vector3(-330f, -148f, 0f), 470, 56,
            "无尽模式", selectedEndless ? EndlessColor : TextColor);
        endless.onClick.Add(new EventDelegate(SelectEndless));

        UIButton training = CreateButton("TrainingModeButton", new Vector3(-330f, -220f, 0f), 470, 56,
            "训练模式", selectedTraining ? AccentColor : TextColor);
        training.onClick.Add(new EventDelegate(SelectTraining));

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

        CreateLabel("SelectedMissionLabel", new Vector3(285f, 175f, 0f), 500, 42,
            modeLabel, 18, modeColor, NGUIText.Alignment.Left);
        CreateLabel("SelectedMissionName", new Vector3(285f, 125f, 0f), 500, 52,
            missionName, 29, TextColor, NGUIText.Alignment.Left);
        CreateLabel("SelectedMissionObjective", new Vector3(285f, 45f, 0f), 500, 90,
            objective, 19, TextColor, NGUIText.Alignment.Left);
        CreateLabel("SelectedMissionPattern", new Vector3(285f, -38f, 0f), 500, 86,
            pattern, 16, MutedColor, NGUIText.Alignment.Left);
        CreateLabel("SelectedMissionStars", new Vector3(285f, -112f, 0f), 500, 38,
            status, 19, modeColor, NGUIText.Alignment.Left);
        CreateLabel("SelectedMissionRecord", new Vector3(285f, -168f, 0f), 500, 56,
            record, 15, MutedColor, NGUIText.Alignment.Left);

        UIButton back = CreateButton("LevelSelectBack", new Vector3(-270f, -315f, 0f), 280, 58,
            "返回难度选择", MutedColor);
        back.onClick.Add(new EventDelegate(Back));
        UIButton start = CreateButton("LevelSelectStart", new Vector3(270f, -315f, 0f), 340, 64,
            startText, modeColor);
        start.onClick.Add(new EventDelegate(StartMission));
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
        UIRoot root = FindObjectOfType<UIRoot>();
        return root != null ? root.transform : null;
    }

    private void CacheTemplates(Transform parent)
    {
        panelTemplate = scrimTemplate = buttonTemplate = accentTemplate = null;
        foreach (UI2DSprite sprite in parent.GetComponentsInChildren<UI2DSprite>(true))
        {
            UIPanel owner = NGUITools.FindInParents<UIPanel>(sprite.gameObject);
            if (sprite.sprite2D == null || (owner != null && owner.name == "CampaignLevelSelect"))
                continue;
            switch (sprite.sprite2D.name)
            {
                case "ui_panel": panelTemplate = sprite; break;
                case "ui_scrim": scrimTemplate = sprite; break;
                case "ui_button_primary": buttonTemplate = sprite; break;
                case "ui_accent": accentTemplate = sprite; break;
            }
        }
        if (DifficultyPanel.Instance != null)
            labelTemplate = DifficultyPanel.Instance.labTitle;
    }

    private UI2DSprite CreateSprite(string name, Vector3 position, int width, int height,
        UI2DSprite template, Color color, bool collider, int depth)
    {
        GameObject go = NewObject(name, position);
        UI2DSprite sprite = go.AddComponent<UI2DSprite>();
        CopyStyle(sprite, template);
        sprite.type = UIBasicSprite.Type.Sliced;
        sprite.width = width;
        sprite.height = height;
        sprite.color = color;
        sprite.depth = depth;
        if (collider)
            go.AddComponent<BoxCollider>().size = new Vector3(width, height, 0.1f);
        return sprite;
    }

    private UILabel CreateLabel(string name, Vector3 position, int width, int height, string text,
        int size, Color color, NGUIText.Alignment alignment)
    {
        GameObject go = NewObject(name, position);
        UILabel label = go.AddComponent<UILabel>();
        CopyLabelStyle(label);
        label.text = text;
        label.width = width;
        label.height = height;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.depth = 40;
        label.overflowMethod = UILabel.Overflow.ShrinkContent;
        return label;
    }

    private UIButton CreateButton(string name, Vector3 position, int width, int height, string text, Color color)
    {
        GameObject go = NewObject(name, position);
        UI2DSprite sprite = go.AddComponent<UI2DSprite>();
        CopyStyle(sprite, buttonTemplate);
        sprite.type = UIBasicSprite.Type.Sliced;
        sprite.width = width;
        sprite.height = height;
        sprite.depth = 20;
        go.AddComponent<BoxCollider>().size = new Vector3(width, height, 0.1f);
        UIButton button = go.AddComponent<UIButton>();
        button.tweenTarget = go;
        button.hover = new Color(0.78f, 1f, 1f, 1f);
        button.pressed = new Color(0.68f, 0.84f, 0.9f, 1f);
        CreateLabel(name + "Label", position, width - 24, height - 8, text, 20, color,
            NGUIText.Alignment.Center).transform.SetParent(go.transform, true);
        return button;
    }

    private GameObject NewObject(string name, Vector3 position)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(viewRoot.transform, false);
        go.transform.localPosition = position;
        go.layer = viewRoot.layer;
        return go;
    }

    private static void CopyStyle(UI2DSprite target, UI2DSprite source)
    {
        if (source == null)
            return;
        target.sprite2D = source.sprite2D;
        target.shader = source.shader;
        target.border = source.border;
    }

    private void CopyLabelStyle(UILabel target)
    {
        if (labelTemplate == null)
            return;
        target.font = labelTemplate.font;
        target.trueTypeFont = labelTemplate.trueTypeFont;
        target.fontStyle = labelTemplate.fontStyle;
        target.effectStyle = labelTemplate.effectStyle;
        target.effectColor = labelTemplate.effectColor;
        target.effectDistance = labelTemplate.effectDistance;
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

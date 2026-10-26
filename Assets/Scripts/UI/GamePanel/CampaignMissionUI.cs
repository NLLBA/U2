using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CampaignMissionUI : MonoBehaviour
{
    private static CampaignMissionUI instance;

    private GameObject resultRoot;
    private UI2DSprite panelTemplate;
    private UI2DSprite scrimTemplate;
    private UI2DSprite buttonTemplate;
    private UI2DSprite accentTemplate;
    private UILabel labelTemplate;
    private UILabel challengeDetailsLabel;
    private CampaignMissionController displayedMission;
    private bool expandedChallengeDetails;
    private CampaignMissionState displayedState = CampaignMissionState.Inactive;

    private static readonly Color PanelColor = new Color(0.015f, 0.055f, 0.09f, 0.98f);
    private static readonly Color TextColor = new Color(0.91f, 0.96f, 1f, 1f);
    private static readonly Color MutedColor = new Color(0.52f, 0.69f, 0.76f, 1f);
    private static readonly Color AccentColor = new Color(0.18f, 0.88f, 1f, 1f);
    private static readonly Color FailureColor = new Color(1f, 0.42f, 0.35f, 1f);

    public static CampaignMissionUI Ensure()
    {
        if (instance != null)
            return instance;
        GameObject host = new GameObject("CampaignMissionUI");
        return host.AddComponent<CampaignMissionUI>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
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
        DestroyResult();
        displayedState = CampaignMissionState.Inactive;
    }

    private void Update()
    {
        CampaignMissionController mission = CampaignMissionController.Instance;
        if (mission == null || !mission.IsActiveCampaign)
            return;
        if (mission.State == CampaignMissionState.Running || mission.State == displayedState)
            return;
        if (mission.State == CampaignMissionState.Victory || mission.State == CampaignMissionState.Failed)
        {
            displayedState = mission.State;
            BuildResult(mission);
        }
    }

    private void BuildResult(CampaignMissionController mission)
    {
        DestroyResult();
        displayedMission = mission;
        expandedChallengeDetails = false;
        if (GameOverPanel.Instance != null && GameOverPanel.Instance.gameObject.activeSelf)
            GameOverPanel.Instance.Hideme();

        UIRoot uiRoot = FindObjectOfType<UIRoot>();
        if (uiRoot == null)
            return;
        resultRoot = new GameObject("CampaignMissionResult");
        resultRoot.transform.SetParent(uiRoot.transform, false);
        resultRoot.layer = uiRoot.gameObject.layer;
        resultRoot.AddComponent<UIPanel>().depth = 88;
        CacheTemplates(uiRoot.transform);

        LevelInfo level = mission.Level;
        bool victory = mission.State == CampaignMissionState.Victory;
        Color resultColor = victory ? AccentColor : FailureColor;
        string title = victory ? "任务完成" : "任务失败";
        string subtitle = victory
            ? (level != null && level.id == GameDataMgr.MaxLevelId ? "风暴挑战完成，可前往无尽模式冲榜" : "行动完成，可继续挑战下一关")
            : "调整策略后再次出击";

        UI2DSprite scrim = CreateSprite(resultRoot.transform, "MissionResultScrim", Vector3.zero,
            4000, 2400, scrimTemplate, new Color(1f, 1f, 1f, 0.84f), true, -100);
        CreateSprite(resultRoot.transform, "MissionResultPanel", Vector3.zero,
            1120, 740, panelTemplate, Color.white, true, -50);
        CreateSprite(resultRoot.transform, "MissionResultAccent", new Vector3(0f, 330f, 0f),
            940, 5, accentTemplate, resultColor, false, -10);

        CreateLabel(resultRoot.transform, "MissionResultEyebrow", new Vector3(0f, 285f, 0f), 920, 30,
            "单人任务 · 行动结算", 16, MutedColor, NGUIText.Alignment.Center);
        CreateLabel(resultRoot.transform, "MissionResultTitle", new Vector3(0f, 220f, 0f), 920, 70,
            title, 46, resultColor, NGUIText.Alignment.Center);
        CreateLabel(resultRoot.transform, "MissionResultSubtitle", new Vector3(0f, 165f, 0f), 920, 32,
            subtitle, 17, MutedColor, NGUIText.Alignment.Center);

        string levelName = level != null ? level.name : "任务";
        CreateLabel(resultRoot.transform, "MissionResultLevel", new Vector3(0f, 112f, 0f), 920, 38,
            levelName, 23, TextColor, NGUIText.Alignment.Center);
        CreateSprite(resultRoot.transform, "MissionResultDivider", new Vector3(0f, 80f, 0f),
            880, 2, accentTemplate, new Color(MutedColor.r, MutedColor.g, MutedColor.b, 0.4f), false, -10);

        ScoreManager score = ScoreManager.Instance;
        int totalScore = score != null ? score.TotalScore : 0;
        int runTime = score != null ? Mathf.FloorToInt(score.RunTime) : 0;
        int clears = score != null ? score.ClearCount : 0;
        int bosses = score != null ? score.BossRounds : 0;
        int difficultyId = GameDataMgr.Instance.GetDifficulty() != null ?
            GameDataMgr.Instance.GetDifficulty().id : 2;
        string multiplierText = (score != null ? score.ScoreRate : ScoreManager.DefaultRateFor(difficultyId))
            .ToString("0.00", CultureInfo.InvariantCulture) + "x";
        CreateLabel(resultRoot.transform, "MissionResultScoreCaption", new Vector3(-300f, 30f, 0f), 280, 28,
            "最终分数 · " + multiplierText, 15, MutedColor, NGUIText.Alignment.Center);
        CreateLabel(resultRoot.transform, "MissionResultScore", new Vector3(-300f, -15f, 0f), 280, 52,
            totalScore + " 分", 32, resultColor, NGUIText.Alignment.Center);
        CreateLabel(resultRoot.transform, "MissionResultTimeCaption", new Vector3(0f, 30f, 0f), 280, 28,
            "生存时间", 15, MutedColor, NGUIText.Alignment.Center);
        CreateLabel(resultRoot.transform, "MissionResultTime", new Vector3(0f, -15f, 0f), 280, 52,
            FormatTime(runTime), 30, TextColor, NGUIText.Alignment.Center);
        CreateLabel(resultRoot.transform, "MissionResultActionCaption", new Vector3(300f, 30f, 0f), 280, 28,
            "行动记录", 15, MutedColor, NGUIText.Alignment.Center);
        CreateLabel(resultRoot.transform, "MissionResultAction", new Vector3(300f, -15f, 0f), 280, 52,
            "清除 " + clears + "  ·  BOSS " + bosses, 19, TextColor, NGUIText.Alignment.Center);

        string evaluation = victory
            ? "任务评价  " + new string('★', mission.EarnedStars) + new string('☆', 3 - mission.EarnedStars) +
                "  · 难度倍率 " + multiplierText
            : "任务进度  " + mission.ObjectiveProgressText().Replace("任务进度：", string.Empty) +
                "  · 难度倍率 " + multiplierText;
        CreateLabel(resultRoot.transform, "MissionResultEvaluation", new Vector3(0f, -76f, 0f), 900, 36,
            evaluation, 22, resultColor, NGUIText.Alignment.Center);
        CreateLabel(resultRoot.transform, "MissionResultMechanics", new Vector3(0f, -128f, 0f), 940, 48,
            MechanicStatsText(score, mission), 16, TextColor, NGUIText.Alignment.Center);
        challengeDetailsLabel = CreateLabel(resultRoot.transform, "MissionResultChallenges", new Vector3(0f, -180f, 0f), 940, 30,
            victory ? mission.ChallengeText() : "挑战目标需通关后结算",
            15, victory ? resultColor : MutedColor, NGUIText.Alignment.Center);
        CreateLabel(resultRoot.transform, "MissionResultAdvice", new Vector3(0f, -219f, 0f), 940, 28,
            (victory ? "评级依据：生命、受击、" + mission.MistakeMetricLabel() + "与连击 · " : "建议：") + mission.ResultAdvice(),
            15, MutedColor, NGUIText.Alignment.Center);

        UIButton retry = CreateButton(resultRoot.transform, "MissionRetryButton", new Vector3(-390f, -293f, 0f),
            210, 58, "再来一次", AccentColor);
        retry.onClick.Add(new EventDelegate(Retry));
        UIButton select = CreateButton(resultRoot.transform, "MissionMenuButton", new Vector3(-130f, -293f, 0f),
            220, 58, "返回选关", TextColor);
        select.onClick.Add(new EventDelegate(ReturnToLevelSelect));
        UIButton details = CreateButton(resultRoot.transform, "MissionChallengeButton", new Vector3(130f, -293f, 0f),
            220, 58, "挑战详情", TextColor);
        details.onClick.Add(new EventDelegate(ToggleChallengeDetails));
        UIButton rank = CreateButton(resultRoot.transform, "MissionRankButton", new Vector3(390f, -293f, 0f),
            220, 58, "排行榜", MutedColor);
        rank.onClick.Add(new EventDelegate(OpenLeaderboard));
    }

    private void NextMission()
    {
        GameDataMgr.Instance.SetLevel(GameDataMgr.Instance.selectedLevelId + 1);
        StartCoroutine(LoadScene("GameScene"));
    }

    private void Retry()
    {
        StartCoroutine(LoadScene("GameScene"));
    }

    private void ReturnToMenu()
    {
        StartCoroutine(LoadScene("BeginScene"));
    }

    private void ReturnToLevelSelect()
    {
        MenuNavigationRequest.Pending = MenuNavigationTarget.LevelSelect;
        StartCoroutine(LoadScene("BeginScene"));
    }

    private void OpenLeaderboard()
    {
        MenuNavigationRequest.Pending = MenuNavigationTarget.Leaderboard;
        StartCoroutine(LoadScene("BeginScene"));
    }

    private void ToggleChallengeDetails()
    {
        if (challengeDetailsLabel == null || displayedMission == null)
            return;
        expandedChallengeDetails = !expandedChallengeDetails;
        ScoreManager score = ScoreManager.Instance;
        challengeDetailsLabel.height = expandedChallengeDetails ? 62 : 30;
        challengeDetailsLabel.text = expandedChallengeDetails
            ? "挑战详情：受击 " + (score != null ? score.PlayerHits : 0) +
                " · " + displayedMission.MistakeMetricLabel() + " " + displayedMission.ChallengeMistakeCount() +
                " · 最高连击 " + (score != null ? score.MaxMechanicCombo : 0) +
                "/" + displayedMission.ChallengeComboTarget() + "\n" + displayedMission.ResultAdvice()
            : (displayedMission.State == CampaignMissionState.Victory
                ? displayedMission.ChallengeText() : "挑战目标需通关后结算");
    }

    private IEnumerator LoadScene(string sceneName)
    {
        ObjectPoolManager pool = ObjectPoolManager.Instance;
        if (pool != null)
        {
            pool.usePool = false;
            pool.ClearPool();
        }
        yield return null;
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private void DestroyResult()
    {
        if (resultRoot == null)
            return;
        resultRoot.SetActive(false);
        Destroy(resultRoot);
        resultRoot = null;
        challengeDetailsLabel = null;
        displayedMission = null;
    }

    private void CacheTemplates(Transform parent)
    {
        panelTemplate = scrimTemplate = buttonTemplate = accentTemplate = null;
        foreach (UI2DSprite sprite in parent.GetComponentsInChildren<UI2DSprite>(true))
        {
            UIPanel owner = NGUITools.FindInParents<UIPanel>(sprite.gameObject);
            if (sprite.sprite2D == null || (owner != null && owner.name == "CampaignMissionResult"))
                continue;
            switch (sprite.sprite2D.name)
            {
                case "ui_panel": panelTemplate = sprite; break;
                case "ui_scrim": scrimTemplate = sprite; break;
                case "ui_button_primary": buttonTemplate = sprite; break;
                case "ui_accent": accentTemplate = sprite; break;
            }
        }
        if (GamePanel.Instance != null)
            labelTemplate = GamePanel.Instance.labTime;
    }

    private UI2DSprite CreateSprite(Transform parent, string name, Vector3 position, int width, int height,
        UI2DSprite template, Color color, bool collider, int depth)
    {
        GameObject go = NewObject(parent, name, position);
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

    private UILabel CreateLabel(Transform parent, string name, Vector3 position, int width, int height,
        string text, int size, Color color, NGUIText.Alignment alignment)
    {
        GameObject go = NewObject(parent, name, position);
        UILabel label = go.AddComponent<UILabel>();
        if (labelTemplate != null)
        {
            label.font = labelTemplate.font;
            label.trueTypeFont = labelTemplate.trueTypeFont;
            label.fontStyle = labelTemplate.fontStyle;
            label.effectStyle = labelTemplate.effectStyle;
            label.effectColor = labelTemplate.effectColor;
            label.effectDistance = labelTemplate.effectDistance;
        }
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

    private UIButton CreateButton(Transform parent, string name, Vector3 position, int width, int height,
        string text, Color color)
    {
        GameObject go = NewObject(parent, name, position);
        UI2DSprite sprite = go.AddComponent<UI2DSprite>();
        CopyStyle(sprite, buttonTemplate);
        if (sprite.sprite2D == null)
            sprite.sprite2D = FindLoadedSprite("ui_button_primary", "ui_button_secondary", "ui_input");
        sprite.type = UIBasicSprite.Type.Sliced;
        sprite.width = width;
        sprite.height = height;
        sprite.depth = 20;
        go.AddComponent<BoxCollider>().size = new Vector3(width, height, 0.1f);
        UIButton button = go.AddComponent<UIButton>();
        button.tweenTarget = go;
        button.hover = new Color(0.78f, 1f, 1f, 1f);
        button.pressed = new Color(0.68f, 0.84f, 0.9f, 1f);
        CreateLabel(go.transform, name + "Label", Vector3.zero, width - 20, height - 8,
            text, 20, color, NGUIText.Alignment.Center);
        return button;
    }

    private static GameObject NewObject(Transform parent, string name, Vector3 position)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.layer = parent.gameObject.layer;
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

    private static Sprite FindLoadedSprite(params string[] names)
    {
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i].name == names[nameIndex])
                    return sprites[i];
        return null;
    }

    private static string FormatTime(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        return (seconds / 60) + "m" + (seconds % 60).ToString("00") + "s";
    }

    private static string MechanicStatsText(ScoreManager score, CampaignMissionController mission)
    {
        if (score == null)
            return "暂无战斗统计";
        string mistakeLabel = mission != null ? mission.MistakeMetricLabel() : "失误";
        int mistakeCount = mission != null ? mission.ChallengeMistakeCount() : score.Mistakes;
        return "完美穿越 " + score.PerfectCorridorPasses + "  ·  连锁 " + score.ClusterWavesCleared +
            "  ·  完美闪避 " + score.PerfectDodges + "  ·  信标 " + score.RelayBeacons +
            "  ·  反弹 " + score.ReflectedCores + "\n" +
            mistakeLabel + " " + mistakeCount + "  ·  受击 " + score.PlayerHits +
            "  ·  最高连击 " + score.MaxMechanicCombo + "  ·  机制奖励 +" + score.MechanicBonusScore;
    }
}

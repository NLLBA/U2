using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CampaignMissionUI : MonoBehaviour
{
    private static CampaignMissionUI instance;

    private GameObject resultRoot;
    private Image panelTemplate;
    private Image scrimTemplate;
    private Button buttonTemplate;
    private Image accentTemplate;
    private Text labelTemplate;
    private Text challengeDetailsLabel;
    private Text navigationStatus;
    private Button challengeDetailsButton;
    private readonly List<Button> resultButtons = new List<Button>();
    private CampaignMissionController displayedMission;
    private bool expandedChallengeDetails;
    private bool navigating;
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
            ShowResultNow(mission);
        }
    }

    public void ShowResultNow(CampaignMissionController mission)
    {
        if (mission == null ||
            (mission.State != CampaignMissionState.Victory && mission.State != CampaignMissionState.Failed))
            return;
        displayedState = mission.State;
        BuildResult(mission);
    }

    private void BuildResult(CampaignMissionController mission)
    {
        DestroyResult();
        displayedMission = mission;
        expandedChallengeDetails = false;
        navigating = false;
        resultButtons.Clear();
        if (GameOverPanel.Instance != null && GameOverPanel.Instance.gameObject.activeSelf)
            GameOverPanel.Instance.Hideme();

        Transform parent = UGUIRuntime.Root;
        if (parent == null)
            return;
        UGUIRuntime.EnsureEventSystem();
        resultRoot = UGUIRuntime.CreateRect(parent, "CampaignMissionResult", Vector3.zero, 1920, 1080).gameObject;
        CacheTemplates(parent);

        LevelInfo level = mission.Level;
        bool victory = mission.State == CampaignMissionState.Victory;
        Color resultColor = victory ? AccentColor : FailureColor;
        string title = victory ? "任务完成" : "任务失败";
        string subtitle = victory
            ? (level != null && level.id == GameDataMgr.MaxLevelId ? "风暴挑战完成，可前往无尽模式冲榜" : "行动完成，可继续挑战下一关")
            : "调整策略后再次出击";

        CreateSprite(resultRoot.transform, "MissionResultScrim", Vector3.zero,
            4000, 2400, scrimTemplate, new Color(0f, 0.015f, 0.03f, 0.78f), true);
        CreateSurface(resultRoot.transform, "MissionResultPanel", Vector3.zero,
            1040, 660, CombatGraphic.Shape.Dialog, resultColor);
        CreateSprite(resultRoot.transform, "MissionResultAccent", new Vector3(0f, 296f, 0f),
            920, 3, accentTemplate, resultColor, false);

        CreateLabel(resultRoot.transform, "MissionResultEyebrow", new Vector3(-390f, 250f, 0f), 180, 28,
            "行动结算 / RESULT", 14, MutedColor, TextAnchor.MiddleLeft);
        CreateLabel(resultRoot.transform, "MissionResultTitle", new Vector3(-280f, 198f, 0f), 400, 60,
            title, 44, resultColor, TextAnchor.MiddleLeft);
        CreateLabel(resultRoot.transform, "MissionResultSubtitle", new Vector3(-280f, 154f, 0f), 400, 30,
            subtitle, 16, MutedColor, TextAnchor.MiddleLeft);

        string levelName = level != null ? level.name : "任务";
        CreateLabel(resultRoot.transform, "MissionResultLevelCaption", new Vector3(260f, 226f, 0f), 360, 24,
            "任务档案", 14, MutedColor, TextAnchor.MiddleLeft);
        CreateLabel(resultRoot.transform, "MissionResultLevel", new Vector3(260f, 188f, 0f), 360, 36,
            levelName, 23, TextColor, TextAnchor.MiddleLeft);
        CreateSprite(resultRoot.transform, "MissionResultColumnDivider", new Vector3(0f, 197f, 0f),
            1, 126, accentTemplate, new Color(MutedColor.r, MutedColor.g, MutedColor.b, 0.35f), false);

        ScoreManager score = ScoreManager.Instance;
        int totalScore = score != null ? score.TotalScore : 0;
        int runTime = score != null ? Mathf.FloorToInt(score.RunTime) : 0;
        int clears = score != null ? score.ClearCount : 0;
        int bosses = score != null ? score.BossRounds : 0;
        int difficultyId = GameDataMgr.Instance.GetDifficulty() != null ?
            GameDataMgr.Instance.GetDifficulty().id : 2;
        string multiplierText = (score != null ? score.ScoreRate : ScoreManager.DefaultRateFor(difficultyId))
            .ToString("0.00", CultureInfo.InvariantCulture) + "x";
        CreateSurface(resultRoot.transform, "MissionResultScoreCard", new Vector3(-310f, 55f, 0f),
            270, 116, CombatGraphic.Shape.Stat, resultColor);
        CreateSurface(resultRoot.transform, "MissionResultTimeCard", new Vector3(0f, 55f, 0f),
            270, 116, CombatGraphic.Shape.Stat, AccentColor);
        CreateSurface(resultRoot.transform, "MissionResultActionCard", new Vector3(310f, 55f, 0f),
            270, 116, CombatGraphic.Shape.Stat, AccentColor);
        CreateLabel(resultRoot.transform, "MissionResultScoreCaption", new Vector3(-310f, 78f, 0f), 230, 24,
            "最终分数 · " + multiplierText, 15, MutedColor, TextAnchor.MiddleCenter);
        CreateLabel(resultRoot.transform, "MissionResultScore", new Vector3(-310f, 39f, 0f), 230, 46,
            totalScore + " 分", 32, resultColor, TextAnchor.MiddleCenter);
        CreateLabel(resultRoot.transform, "MissionResultTimeCaption", new Vector3(0f, 78f, 0f), 230, 24,
            "生存时间", 15, MutedColor, TextAnchor.MiddleCenter);
        CreateLabel(resultRoot.transform, "MissionResultTime", new Vector3(0f, 39f, 0f), 230, 46,
            FormatTime(runTime), 30, TextColor, TextAnchor.MiddleCenter);
        CreateLabel(resultRoot.transform, "MissionResultActionCaption", new Vector3(310f, 78f, 0f), 230, 24,
            "行动记录", 15, MutedColor, TextAnchor.MiddleCenter);
        CreateLabel(resultRoot.transform, "MissionResultAction", new Vector3(310f, 39f, 0f), 230, 46,
            "清除 " + clears + "  ·  BOSS " + bosses, 19, TextColor, TextAnchor.MiddleCenter);

        string evaluation = victory
            ? "任务评价  " + new string('★', mission.EarnedStars) + new string('☆', 3 - mission.EarnedStars) +
                "  · 难度倍率 " + multiplierText
            : "任务进度  " + mission.ObjectiveProgressText().Replace("任务进度：", string.Empty) +
                "  · 难度倍率 " + multiplierText;
        CreateLabel(resultRoot.transform, "MissionResultEvaluation", new Vector3(0f, -36f, 0f), 900, 34,
            evaluation, 22, resultColor, TextAnchor.MiddleCenter);
        CreateLabel(resultRoot.transform, "MissionResultMechanics", new Vector3(0f, -78f, 0f), 900, 42,
            MechanicStatsText(score, mission), 16, TextColor, TextAnchor.MiddleCenter);
        CreateSurface(resultRoot.transform, "MissionChallengeDetails", new Vector3(0f, -157f, 0f),
            900, 96, CombatGraphic.Shape.Stat, resultColor);
        challengeDetailsLabel = CreateLabel(resultRoot.transform, "MissionResultChallenges", new Vector3(0f, -143f, 0f), 850, 30,
            victory ? mission.ChallengeText() : "挑战目标需通关后结算",
            15, victory ? resultColor : MutedColor, TextAnchor.MiddleCenter);
        CreateLabel(resultRoot.transform, "MissionResultAdvice", new Vector3(0f, -181f, 0f), 850, 24,
            (victory ? "评级依据：生命、受击、" + mission.MistakeMetricLabel() + "与连击 · " : "建议：") + mission.ResultAdvice(),
            15, MutedColor, TextAnchor.MiddleCenter);

        Button retry = CreateActionButton(resultRoot.transform, "MissionRetryButton", new Vector3(-365f, -260f, 0f),
            200, 56, "再来一次", AccentColor, false);
        retry.onClick.AddListener(Retry);
        Button select = CreateActionButton(resultRoot.transform, "MissionMenuButton", new Vector3(-122f, -260f, 0f),
            210, 56, "返回选关", AccentColor, false);
        select.onClick.AddListener(ReturnToLevelSelect);
        challengeDetailsButton = CreateActionButton(resultRoot.transform, "MissionChallengeButton", new Vector3(122f, -260f, 0f),
            210, 56, "展开详情", AccentColor, false);
        challengeDetailsButton.onClick.AddListener(ToggleChallengeDetails);
        Button rank = CreateActionButton(resultRoot.transform, "MissionRankButton", new Vector3(365f, -260f, 0f),
            210, 56, "排行榜", AccentColor, false);
        rank.onClick.AddListener(OpenLeaderboard);
        navigationStatus = CreateLabel(resultRoot.transform, "MissionNavigationStatus", new Vector3(0f, -305f, 0f),
            700, 20, string.Empty, 13, MutedColor, TextAnchor.MiddleCenter);
    }

    private void NextMission()
    {
        GameDataMgr.Instance.SetLevel(GameDataMgr.Instance.selectedLevelId + 1);
        BeginNavigation("GameScene", "正在载入下一任务...");
    }

    private void Retry()
    {
        BeginNavigation("GameScene", "正在重新部署...");
    }

    private void ReturnToMenu()
    {
        BeginNavigation("BeginScene", "正在返回主菜单...");
    }

    private void ReturnToLevelSelect()
    {
        MenuNavigationRequest.Pending = MenuNavigationTarget.LevelSelect;
        BeginNavigation("BeginScene", "正在返回选关...");
    }

    private void OpenLeaderboard()
    {
        MenuNavigationRequest.Pending = MenuNavigationTarget.Leaderboard;
        BeginNavigation("BeginScene", "正在打开排行榜...");
    }

    private void ToggleChallengeDetails()
    {
        if (challengeDetailsLabel == null || displayedMission == null)
            return;
        expandedChallengeDetails = !expandedChallengeDetails;
        ScoreManager score = ScoreManager.Instance;
        UGUIRuntime.SetSize(challengeDetailsLabel, challengeDetailsLabel.rectTransform.rect.width,
            expandedChallengeDetails ? 62f : 30f);
        challengeDetailsLabel.text = expandedChallengeDetails
            ? "挑战详情：受击 " + (score != null ? score.PlayerHits : 0) +
                " · " + displayedMission.MistakeMetricLabel() + " " + displayedMission.ChallengeMistakeCount() +
                " · 最高连击 " + (score != null ? score.MaxMechanicCombo : 0) +
                "/" + displayedMission.ChallengeComboTarget() + "\n" + displayedMission.ResultAdvice()
            : (displayedMission.State == CampaignMissionState.Victory
                ? displayedMission.ChallengeText() : "挑战目标需通关后结算");
        Text buttonLabel = UGUIRuntime.Label(challengeDetailsButton);
        if (buttonLabel != null)
            buttonLabel.text = expandedChallengeDetails ? "收起详情" : "展开详情";
    }

    private void BeginNavigation(string sceneName, string message)
    {
        if (navigating)
            return;
        navigating = true;
        foreach (Button button in resultButtons)
            if (button != null) button.interactable = false;
        if (navigationStatus != null)
            navigationStatus.text = message;
        StartCoroutine(LoadScene(sceneName));
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
        challengeDetailsButton = null;
        navigationStatus = null;
        resultButtons.Clear();
        displayedMission = null;
        navigating = false;
    }

    private void CacheTemplates(Transform parent)
    {
        panelTemplate = scrimTemplate = accentTemplate = null;
        buttonTemplate = null;
        foreach (Image image in parent.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null || image.transform.IsChildOf(resultRoot.transform))
                continue;
            switch (image.sprite.name)
            {
                case "ui_panel": panelTemplate = image; break;
                case "ui_scrim": scrimTemplate = image; break;
                case "ui_button_primary": buttonTemplate = image.GetComponent<Button>(); break;
                case "ui_accent": accentTemplate = image; break;
            }
        }
        if (GamePanel.Instance != null)
            labelTemplate = GamePanel.Instance.labTime;
    }

    private Image CreateSprite(Transform parent, string name, Vector3 position, int width, int height,
        Image template, Color color, bool raycastTarget)
    {
        return UGUIRuntime.CreateImage(parent, name, position, width, height,
            template, null, color, raycastTarget);
    }

    private Text CreateLabel(Transform parent, string name, Vector3 position, int width, int height,
        string text, int size, Color color, TextAnchor alignment)
    {
        return UGUIRuntime.CreateText(parent, name, position, width, height,
            text, size, color, alignment, labelTemplate);
    }

    private Button CreateButton(Transform parent, string name, Vector3 position, int width, int height,
        string text, Color color)
    {
        return UGUIRuntime.CreateButton(parent, name, position, width, height,
            text, color, buttonTemplate, labelTemplate);
    }

    private static CombatGraphic CreateSurface(Transform parent, string name, Vector3 position,
        int width, int height, CombatGraphic.Shape shape, Color accent)
    {
        RectTransform rect = UGUIRuntime.CreateRect(parent, name, position, width, height);
        CombatGraphic graphic = rect.gameObject.AddComponent<CombatGraphic>();
        graphic.Configure(shape, accent);
        graphic.raycastTarget = shape == CombatGraphic.Shape.Dialog;
        return graphic;
    }

    private Button CreateActionButton(Transform parent, string name, Vector3 position, int width, int height,
        string text, Color accent, bool danger)
    {
        CombatGraphic graphic = CreateSurface(parent, name, position, width, height,
            danger ? CombatGraphic.Shape.DangerButton : CombatGraphic.Shape.Button, accent);
        graphic.raycastTarget = true;
        Button button = graphic.gameObject.AddComponent<Button>();
        button.targetGraphic = graphic;
        button.colors = new ColorBlock {
            normalColor = Color.white, highlightedColor = new Color(1.18f, 1.18f, 1.18f),
            pressedColor = new Color(0.7f, 0.82f, 0.86f), selectedColor = Color.white,
            disabledColor = new Color(0.4f, 0.44f, 0.48f, 0.55f), colorMultiplier = 1f, fadeDuration = 0.08f
        };
        CreateLabel(graphic.transform, name + "Label", Vector3.zero, width - 24, height - 8,
            text, 20, TextColor, TextAnchor.MiddleCenter);
        resultButtons.Add(button);
        return button;
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

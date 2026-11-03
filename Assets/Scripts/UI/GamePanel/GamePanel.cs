using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class GamePanel : BasePanel<GamePanel>
{
    public Button btnBack;
    public Text labTime;
    public Text labLevel;
    public Text labDifficulty;
    public Text labSkill;
    public Text labShieldIcon;
    [FormerlySerializedAs("labHeroSkillIcon")]
    public Text labSkillIcon;
    public Text labBossWarning;
    [SerializeField] private Text labEncounter;
    [SerializeField] private Text labChallenges;
    [SerializeField] private Text labCombatFeedback;
    [SerializeField] private Text labHealthCaption;
    private readonly List<Color> hpFillColors = new List<Color>();
    private bool hpVisualsCached;

    public List<GameObject> hpObjs;

    public float nowTime = 0;

    private int displayedLevelId;
    private float levelNoticeUntil;
    private float feedbackUntil;
    private Color feedbackColor;
    private float nextClockRefresh;
    private float nextDynamicRefresh;
    private float nextBossTextRefresh;

    public void ConfigureHud(Text encounter, Text challenges, Text feedback, Text healthCaption)
    {
        labEncounter = encounter;
        labChallenges = challenges;
        labCombatFeedback = feedback;
        labHealthCaption = healthCaption;
    }

    public override void Init()
    {
        if (btnBack != null)
        {
            btnBack.onClick.AddListener(() =>
            {
                ShowPause();
            });
        }

        if (labLevel != null)
        {
            labLevel.fontSize = 24;
            UGUIRuntime.SetSize(labLevel, labLevel.rectTransform.rect.width, 64f);
            EnableBestFit(labLevel, 12, 24);
        }

        if (labDifficulty != null && labEncounter == null)
        {
            EnableBestFit(labDifficulty, 11, labDifficulty.fontSize);
            labEncounter = Instantiate(labDifficulty, labDifficulty.transform.parent);
            labEncounter.name = "labEncounter";
            labEncounter.rectTransform.anchoredPosition = labDifficulty.rectTransform.anchoredPosition + Vector2.down * 44f;
            UGUIRuntime.SetSize(labEncounter, labEncounter.rectTransform.rect.width, 52f);
            labEncounter.fontSize = 17;
            EnableBestFit(labEncounter, 10, 17);
            labEncounter.text = string.Empty;

            labChallenges = Instantiate(labDifficulty, labDifficulty.transform.parent);
            labChallenges.name = "labChallengeProgress";
            labChallenges.rectTransform.anchoredPosition = labDifficulty.rectTransform.anchoredPosition + Vector2.down * 102f;
            UGUIRuntime.SetSize(labChallenges, labChallenges.rectTransform.rect.width, 48f);
            labChallenges.fontSize = 16;
            EnableBestFit(labChallenges, 10, 16);
            labChallenges.text = string.Empty;
            labChallenges.gameObject.SetActive(false);
        }

        if (labBossWarning != null)
        {
            EnableBestFit(labBossWarning, 12, labBossWarning.fontSize);
            labBossWarning.gameObject.SetActive(false);
            if (labCombatFeedback == null)
            {
                labCombatFeedback = Instantiate(labBossWarning, labBossWarning.transform.parent);
                labCombatFeedback.name = "labCombatFeedback";
                labCombatFeedback.rectTransform.anchoredPosition = labBossWarning.rectTransform.anchoredPosition + Vector2.down * 92f;
                UGUIRuntime.SetSize(labCombatFeedback, labCombatFeedback.rectTransform.rect.width, 54f);
                labCombatFeedback.fontSize = 24;
                EnableBestFit(labCombatFeedback, 12, 24);
                labCombatFeedback.gameObject.SetActive(false);
            }
        }

        ChangeHP(5);
    }

    public void ChangeHP(int hp)
    {
        CacheHealthVisuals();
        int current = Mathf.Clamp(hp, 0, hpObjs.Count);
        for (int i = 0; i < hpObjs.Count; i++)
        {
            GameObject source = hpObjs[i];
            if (source == null)
                continue;
            source.SetActive(true);
            GameObject view = source;
            if (view == null)
                continue;
            view.SetActive(true);
            Graphic graphic = view.GetComponent<Graphic>();
            if (graphic == null)
                graphic = view.GetComponentInChildren<Graphic>(true);
            if (graphic != null)
            {
                graphic.enabled = true;
                graphic.color = i < current && i < hpFillColors.Count
                    ? hpFillColors[i]
                    : new Color(0.003f, 0.008f, 0.012f, 1f);
            }
        }

        if (labHealthCaption == null)
            labHealthCaption = UGUIRuntime.Find<Text>(UGUIRuntime.View(this), "Polish_HealthCaption");
        if (labHealthCaption != null)
            labHealthCaption.text = "机体完整度   " + current.ToString("00") + " / " + hpObjs.Count.ToString("00");
    }

    private void CacheHealthVisuals()
    {
        if (hpVisualsCached)
            return;
        hpVisualsCached = true;
        hpFillColors.Clear();
        for (int i = 0; i < hpObjs.Count; i++)
        {
            GameObject view = hpObjs[i];
            Graphic image = view != null ? view.GetComponentInChildren<Graphic>(true) : null;
            Color active = new Color(0.22f, 0.82f, 0.88f, 1f);
            hpFillColors.Add(active);
            if (image != null)
            {
                Image solid = image as Image;
                if (solid != null)
                {
                    Outline outline = solid.GetComponent<Outline>();
                    if (outline != null)
                        outline.enabled = false;
                    EnsureHealthSegmentFrame(solid.rectTransform);
                }
            }
        }
    }

    private static void EnsureHealthSegmentFrame(RectTransform segment)
    {
        if (segment == null)
            return;
        Transform existing = segment.Find("HealthSegmentFrame");
        CombatHealthSegmentFrame frame = existing != null
            ? existing.GetComponent<CombatHealthSegmentFrame>()
            : null;
        if (frame == null)
        {
            GameObject frameObject = new GameObject("HealthSegmentFrame", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(CombatHealthSegmentFrame));
            frameObject.transform.SetParent(segment, false);
            frame = frameObject.GetComponent<CombatHealthSegmentFrame>();
        }
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        frameRect.SetAsLastSibling();
        frame.color = Color.white;
        frame.raycastTarget = false;
    }

    private void Update()
    {
        CheckPause();

        if (GameFlowManager.Instance != null)
        {
            nowTime = GameFlowManager.Instance.RunTime;
        }
        else
        {
            nowTime += Time.deltaTime;
        }

        float unscaledNow = Time.unscaledTime;
        CombatTuningData tuning = GameDataMgr.Instance.Tuning;
        if (unscaledNow >= nextClockRefresh)
        {
            nextClockRefresh = unscaledNow + tuning.hudClockRefreshSeconds;
            UpdateTime();
        }
        UpdateLevelNotice();
        if (unscaledNow >= nextDynamicRefresh)
        {
            nextDynamicRefresh = unscaledNow + tuning.hudDynamicRefreshSeconds;
            UpdateInfo();
        }
        UpdateBoss();
        UpdateCombatFeedback();
    }

    public void ShowCombatFeedback(string text, Color color, float duration = 1.25f)
    {
        if (labCombatFeedback == null || string.IsNullOrEmpty(text))
            return;
        feedbackColor = color;
        feedbackColor.a = 1f;
        feedbackUntil = Time.unscaledTime + Mathf.Max(0.25f, duration);
        labCombatFeedback.text = text;
        labCombatFeedback.color = feedbackColor;
        labCombatFeedback.transform.localScale = Vector3.one * 1.08f;
        labCombatFeedback.gameObject.SetActive(true);
    }

    private void CheckPause()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || QuitPanel.Instance == null)
            return;

        if (GameFlowManager.Instance != null && GameFlowManager.Instance.State == GameFlowState.GameOver)
            return;

        if (QuitPanel.Instance.gameObject.activeSelf)
        {
            QuitPanel.Instance.Resume();
        }
        else
        {
            ShowPause();
        }
    }

    private void ShowPause()
    {
        if (NetworkBattleSession.Instance != null && NetworkBattleSession.Instance.IsPlaying)
        {
            return;
        }

        if (QuitPanel.Instance == null)
            return;

        QuitPanel.Instance.Showme();

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.Pause();
        }
        else
        {
            Time.timeScale = 0f;
        }
    }

    private void UpdateTime()
    {
        if (labTime == null)
            return;

        string value = string.Empty;

        if ((int)nowTime / 3600 > 0)
        {
            value += (int)nowTime / 3600 + "h";
        }
        if ((int)nowTime % 3600 / 60 > 0 || value.Length > 0)
        {
            value += (int)nowTime % 3600 / 60 + "m";
        }
        value += (int)nowTime % 60 + "s";
        SetText(labTime, value);
    }

    private void UpdateInfo()
    {
        if (labLevel != null)
        {
            string levelText = GameDataMgr.Instance.LevelName();
            CampaignMissionController mission = CampaignMissionController.Instance;
            if (mission != null && mission.IsActiveCampaign)
                levelText += "\n" + mission.ObjectiveProgressText();

            string status = string.Empty;
            if (GameFlowManager.Instance != null &&
                (GameDataMgr.Instance.IsEndless || GameDataMgr.Instance.GetLevel().bossStart < 9999f))
            {
                status += GameFlowManager.Instance.StatusText();
                if (GameFlowManager.Instance.State == GameFlowState.BossSurvival &&
                    BossController.Instance != null && BossController.Instance.BossActive)
                {
                    status += "  |  " + BossController.Instance.PatternName();
                }
            }
            if (status.Length > 0)
                levelText += (mission != null && mission.IsActiveCampaign ? "  |  " : "\n") + status;
            SetText(labLevel, levelText);
        }

        LevelEncounterDirector encounter = LevelEncounterDirector.Instance;
        CampaignMissionController activeMission = CampaignMissionController.Instance;
        if (labChallenges != null)
        {
            bool showChallenges = activeMission != null && activeMission.IsActiveCampaign &&
                activeMission.State == CampaignMissionState.Running;
            if (labChallenges.gameObject.activeSelf != showChallenges)
                labChallenges.gameObject.SetActive(showChallenges);
            if (showChallenges)
                SetText(labChallenges, "挑战  " + activeMission.LiveChallengeText());
        }
        if (labEncounter != null && GameDataMgr.Instance.IsEndless)
        {
            LevelInfo next = GameDataMgr.Instance.GetNextLevel(nowTime);
            labEncounter.color = new Color(0.8f, 0.55f, 1f);
            string wave = EndlessRunController.Instance != null ? EndlessRunController.Instance.StatusText() : "波次计算中";
            SetText(labEncounter, wave + "\n" + (next != null
                ? "渐进生存 · 距阶段" + next.id + "还有" + Mathf.CeilToInt(next.beginTime - nowTime) + "秒"
                : "极限生存 · 组合攻击持续升级"));
        }
        else if (labEncounter != null && encounter != null)
        {
            LevelInfo level = GameDataMgr.Instance.GetLevel();
            labEncounter.color = level.id == 4
                ? new Color(1f, 0.67f, 0.22f, 1f)
                : LevelEncounterDirector.Accent(level.id);
            string encounterText = "战术阶段 " + encounter.CurrentPhase.ToString("00") +
                "  ·  " + encounter.PhaseName + "  ·  " + level.featureTitle +
                "\n" + CompactEncounterStatus(encounter, level);
            if (ScoreManager.Instance != null && ScoreManager.Instance.MechanicCombo > 0)
                encounterText += " · 连击 " + ScoreManager.Instance.MechanicCombo;
            SetText(labEncounter, encounterText);
        }

        if (labDifficulty != null)
        {
            string difficultyText = GameDataMgr.Instance.IsTraining ? "训练：无伤害教学" : "难度：" + GameDataMgr.Instance.DifficultyName();
            if (ScoreManager.Instance != null)
            {
                difficultyText += "  |  威胁：" + ScoreManager.Instance.Threat +
                    "  |  分数：" + ScoreManager.Instance.TotalScore;
            }
            else if (DifficultyDirector.Instance != null)
            {
                difficultyText += "  |  威胁等级：" + DifficultyDirector.Instance.ThreatLevel;
            }
            if (ScoreManager.Instance != null)
                difficultyText += "  |  倍率 " +
                    ScoreManager.Instance.ScoreRate.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x";
            SetText(labDifficulty, difficultyText);
        }

        if (PlayerObject.Instance == null)
            return;

        if (labSkill != null)
        {
            SetText(labSkill, PlayerObject.Instance.SkillText());
        }

        if (labShieldIcon != null)
        {
            SetText(labShieldIcon, PlayerObject.Instance.ShieldIcon());
        }

        if (labSkillIcon != null)
        {
            SetText(labSkillIcon, PlayerObject.Instance.SkillIconText());
        }
    }

    private void UpdateLevelNotice()
    {
        LevelInfo level = GameDataMgr.Instance.GetLevel();
        if (displayedLevelId == level.id)
            return;

        displayedLevelId = level.id;
        // Use run time so a paused game also pauses the transition notice.
        levelNoticeUntil = nowTime + 4f;
    }

    private void UpdateBoss()
    {
        if (labBossWarning == null)
            return;

        GameFlowManager flow = GameFlowManager.Instance;
        bool showBoss = flow != null &&
            (flow.State == GameFlowState.BossWarning || flow.State == GameFlowState.BossSurvival);
        bool showLevel = nowTime < levelNoticeUntil &&
            (flow == null || (flow.State != GameFlowState.GameOver && flow.State != GameFlowState.Paused));
        bool shouldShow = showBoss || showLevel;

        bool visibilityChanged = labBossWarning.gameObject.activeSelf != shouldShow;
        if (visibilityChanged)
        {
            labBossWarning.gameObject.SetActive(shouldShow);
        }

        if (!shouldShow)
            return;

        bool refreshText = visibilityChanged || Time.unscaledTime >= nextBossTextRefresh;
        if (refreshText)
            nextBossTextRefresh = Time.unscaledTime + GameDataMgr.Instance.Tuning.hudClockRefreshSeconds;

        if (!showBoss)
        {
            LevelInfo level = GameDataMgr.Instance.GetLevel();
            if (refreshText)
                SetText(labBossWarning, GameDataMgr.Instance.IsEndless
                    ? "无尽模式 · 阶段 " + level.id + "\n弹幕逐步加强，持续生存挑战最高分"
                    : level.name + " · " + level.featureTitle + "\n" + level.featureHint);
            labBossWarning.transform.localScale = Vector3.one;
            labBossWarning.color = level.id == 4
                ? new Color(1f, 0.67f, 0.22f, 1f)
                : LevelEncounterDirector.Accent(level.id);
            return;
        }

        if (flow.State == GameFlowState.BossWarning)
        {
            if (refreshText)
                SetText(labBossWarning, "BOSS 接近  第" + (flow.BossRounds + 1) + "轮  " +
                    Mathf.CeilToInt(flow.Countdown()) + "秒");
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.08f;
            labBossWarning.transform.localScale = Vector3.one * pulse;
            labBossWarning.color = Color.Lerp(
                new Color(1f, 0.25f, 0.12f, 1f),
                new Color(1f, 0.85f, 0.2f, 1f),
                (Mathf.Sin(Time.unscaledTime * 8f) + 1f) * 0.5f);
        }
        else
        {
            if (refreshText)
                SetText(labBossWarning, !GameDataMgr.Instance.IsEndless && GameDataMgr.Instance.selectedLevelId == 5 &&
                    BossController.Instance != null
                    ? "BOSS核心反击  " + BossController.Instance.PhaseStatus()
                    : "第" + flow.BossRound + "轮 BOSS  坚持 " + Mathf.CeilToInt(flow.Countdown()) + "秒");
            bool shieldFlash = BossController.Instance != null && BossController.Instance.ShieldBreakFlashActive;
            float pulse = shieldFlash ? 1.12f + Mathf.Sin(Time.unscaledTime * 28f) * 0.1f : 1f;
            labBossWarning.transform.localScale = Vector3.one * pulse;
            labBossWarning.color = shieldFlash
                ? Color.Lerp(Color.white, new Color(1f, 0.2f, 0.08f, 1f),
                    (Mathf.Sin(Time.unscaledTime * 24f) + 1f) * 0.5f)
                : new Color(1f, 0.72f, 0.16f, 1f);
        }
    }

    private void UpdateCombatFeedback()
    {
        if (labCombatFeedback == null || !labCombatFeedback.gameObject.activeSelf)
            return;
        float remaining = feedbackUntil - Time.unscaledTime;
        if (remaining <= 0f)
        {
            labCombatFeedback.gameObject.SetActive(false);
            return;
        }
        float fade = Mathf.Clamp01(remaining / 0.3f);
        Color color = feedbackColor;
        color.a = fade;
        labCombatFeedback.color = color;
        labCombatFeedback.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.08f, fade);
    }

    private static void SetText(Text label, string value)
    {
        UGUIRuntime.SetText(label, value);
    }

    private static void EnableBestFit(Text label, int minimum, int maximum)
    {
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = minimum;
        label.resizeTextMaxSize = Mathf.Max(minimum, maximum);
    }

    private static string CompactEncounterStatus(LevelEncounterDirector encounter, LevelInfo level)
    {
        SignalRelayController relay = SignalRelayController.Instance;
        if (level != null && level.id == 4 && relay != null)
        {
            if (relay.ActivatedBeacons >= 3)
                return "信标 3 / 3  ·  全部激活";
            int energy = Mathf.RoundToInt(relay.CarriedEnergy * 100f);
            int charge = Mathf.RoundToInt(relay.BeaconCharge * 100f);
            return relay.CarriedEnergy < 0.05f
                ? "携带能量 00%  ·  前往中央蓝环取能"
                : "携带能量 " + energy + "%  ·  当前信标 " + charge + "%  ·  已激活 " + relay.ActivatedBeacons + " / 3";
        }
        return encounter.StatusText();
    }
}

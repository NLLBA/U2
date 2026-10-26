using UnityEngine;

public class EndlessRunController : MonoBehaviour
{
    private const string BestWaveKey = "U2.Endless.BestWave";
    public static EndlessRunController Instance { get; private set; }
    public int CurrentWave { get; private set; } = 1;
    public int HighestWave => Mathf.Max(PlayerPrefs.GetInt(BestWaveKey, 0), CurrentWave);
    public string FailureReason { get; private set; } = "尚未失败";
    public bool CampWarningActive { get; private set; }

    private Vector3 movementAnchor;
    private float stationaryTime;
    private int displayedTier = 1;
    private Color initialBackground;
    private bool hasBackground;
    private Camera sceneCamera;
    private CombatTuningData Tuning => GameDataMgr.Instance.Tuning;

    public static EndlessRunController Ensure(GameObject host)
    {
        if (Instance != null)
            return Instance;
        return host.AddComponent<EndlessRunController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        sceneCamera = Camera.main;
        if (sceneCamera != null)
        {
            initialBackground = sceneCamera.backgroundColor;
            hasBackground = true;
        }
    }

    private void Start()
    {
        if (PlayerObject.Instance != null)
            movementAnchor = PlayerObject.Instance.transform.position;
    }

    private void OnDestroy()
    {
        SaveBestWave();
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!GameDataMgr.Instance.IsEndless || GameFlowManager.Instance == null)
            return;

        float runTime = GameFlowManager.Instance.RunTime;
        int wave = Mathf.Max(1, Mathf.FloorToInt(runTime / Tuning.endlessWaveSeconds) + 1);
        if (wave != CurrentWave)
        {
            CurrentWave = wave;
            SaveBestWave();
            UpdateTierPresentation();
        }

        UpdateBackground();
        UpdateAntiCamp();
    }

    public void RegisterFailure(string reason)
    {
        if (FailureReason != "尚未失败" && reason == "弹幕命中导致生命耗尽")
        {
            SaveBestWave();
            return;
        }
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "生命耗尽" : reason.Trim();
        SaveBestWave();
    }

    public string StatusText()
    {
        int bossRound = GameFlowManager.Instance != null
            ? (GameFlowManager.Instance.State == GameFlowState.BossSurvival
                ? GameFlowManager.Instance.BossRound : GameFlowManager.Instance.BossRounds + 1)
            : 1;
        return "波次 " + CurrentWave + "  ·  最高 " + HighestWave + "  ·  Boss轮次 " + Mathf.Max(1, bossRound) +
            (CampWarningActive ? "  ·  [警告] 位置已暴露" : string.Empty);
    }

    private void UpdateAntiCamp()
    {
        PlayerObject player = PlayerObject.Instance;
        GameFlowManager flow = GameFlowManager.Instance;
        if (player == null || player.isDead || flow.State != GameFlowState.Normal ||
            CurrentWave < Tuning.endlessCampStartWave)
            return;

        Vector3 delta = player.transform.position - movementAnchor;
        delta.y = 0f;
        if (delta.sqrMagnitude >= 25f)
        {
            movementAnchor = player.transform.position;
            stationaryTime = 0f;
            CampWarningActive = false;
            return;
        }

        stationaryTime += Time.deltaTime;
        if (!CampWarningActive && stationaryTime >= Tuning.endlessCampWarningSeconds)
        {
            CampWarningActive = true;
            if (GamePanel.Instance != null)
                GamePanel.Instance.ShowCombatFeedback("[警告] 雷达锁定固定安全点 · 立即移动", new Color(1f, 0.35f, 0.18f), 2.5f);
        }
        if (stationaryTime < Tuning.endlessCampStrikeSeconds)
            return;

        FailureReason = "长时间停留触发雷达锁定";
        player.Wound();
        stationaryTime = 0f;
        CampWarningActive = false;
        movementAnchor = player.transform.position;
    }

    private void UpdateTierPresentation()
    {
        int tier = CurrentWave < Tuning.endlessTier2Wave ? 1 :
            (CurrentWave < Tuning.endlessTier3Wave ? 2 :
                (CurrentWave < Tuning.endlessTier4Wave ? 3 : 4));
        if (tier <= displayedTier)
            return;
        displayedTier = tier;
        if (GamePanel.Instance != null)
        {
            string text = tier == 2 ? "[升级] 双向攻击开始交替" :
                (tier == 3 ? "[升级] 追踪与扇形攻击开始组合" : "[升级] 极限阶段 · 安全区持续迁移");
            GamePanel.Instance.ShowCombatFeedback(text, new Color(0.85f, 0.55f, 1f), 2.4f);
        }
    }

    private void UpdateBackground()
    {
        if (!hasBackground || sceneCamera == null)
            return;
        float blend = Mathf.Clamp01((CurrentWave - 1f) /
            Mathf.Max(1f, Tuning.endlessBackgroundFinalWave - 1f));
        Color late = new Color(0.12f, 0.015f, 0.08f, 1f);
        sceneCamera.backgroundColor = Color.Lerp(initialBackground, late, blend);
    }

    private void SaveBestWave()
    {
        int previous = PlayerPrefs.GetInt(BestWaveKey, 0);
        if (CurrentWave <= previous)
            return;
        PlayerPrefs.SetInt(BestWaveKey, CurrentWave);
        PlayerPrefs.Save();
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public enum CampaignMissionState
{
    Inactive,
    Running,
    Victory,
    Failed,
}

public class CampaignMissionController : MonoBehaviour
{
    public const int NoHitChallenge = 1;
    public const int NoMistakeChallenge = 2;
    public const int FullComboChallenge = 4;

    private static CampaignMissionController instance;

    public static CampaignMissionController Instance => instance;
    public CampaignMissionState State { get; private set; }
    public LevelInfo Level { get; private set; }
    public int EarnedStars { get; private set; }
    public int EarnedChallengeMask { get; private set; }
    public bool IsActiveCampaign => State != CampaignMissionState.Inactive;
    public float AverageFrameRate => sampledFrameTime > 0f ? sampledFrames / sampledFrameTime : 0f;
    public int SampledFrames => sampledFrames;
    public int FramesBelow30 { get; private set; }
    public int FramesBelow45 { get; private set; }
    public string SessionId { get; private set; }

    private bool calibrationRecorded;
    private float sampledFrameTime;
    private int sampledFrames;

    public static CampaignMissionController Ensure(GameObject host = null)
    {
        if (instance != null)
            return instance;

        GameObject target = host != null ? host : new GameObject("CampaignMissionController");
        CampaignMissionController controller = target.GetComponent<CampaignMissionController>();
        return controller != null ? controller : target.AddComponent<CampaignMissionController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        SessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
        Level = GameDataMgr.Instance.GetLevel();
        bool networkMatch = NetworkBattleSession.Instance != null && NetworkBattleSession.Instance.IsBattle;
        State = networkMatch || GameDataMgr.Instance.IsEndless || GameDataMgr.Instance.IsTraining
            ? CampaignMissionState.Inactive : CampaignMissionState.Running;
    }

    private void OnDestroy()
    {
        if (State == CampaignMissionState.Running)
            RecordCalibration("scene_exit");
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (State != CampaignMissionState.Running || Level == null)
            return;

        if (Time.unscaledDeltaTime > 0f && Time.unscaledDeltaTime < 0.5f)
        {
            sampledFrameTime += Time.unscaledDeltaTime;
            sampledFrames++;
            float fps = 1f / Time.unscaledDeltaTime;
            if (fps < 45f)
                FramesBelow45++;
            if (fps < 30f)
                FramesBelow30++;
        }

        if (CurrentValue() >= TargetValue())
            CompleteMission();
    }

    public int CurrentValue()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        ScoreManager score = ScoreManager.Instance;
        switch (Level != null ? Level.objectiveType : string.Empty)
        {
            case "survive": return flow != null ? Mathf.FloorToInt(flow.RunTime) : 0;
            case "clear": return score != null ? score.ClusterWavesCleared : 0;
            case "score": return score != null ? score.RawScore : 0;
            case "dodge": return LevelEncounterDirector.Instance != null ? LevelEncounterDirector.Instance.SuccessfulDodges : 0;
            case "beacon": return SignalRelayController.Instance != null ? SignalRelayController.Instance.ActivatedBeacons : 0;
            case "reflect": return BossController.Instance != null ? BossController.Instance.ReflectedHits : 0;
            case "boss": return flow != null ? flow.BossRounds : 0;
            default: return flow != null ? Mathf.FloorToInt(flow.RunTime) : 0;
        }
    }

    public string ObjectiveProgressText()
    {
        if (Level == null)
            return string.Empty;
        string unit;
        switch (Level.objectiveType)
        {
            case "survive": unit = "秒"; break;
            case "clear": unit = "波"; break;
            case "score": unit = "分"; break;
            case "dodge": unit = "次"; break;
            case "beacon": unit = "座"; break;
            case "reflect": unit = "次"; break;
            case "boss": unit = "轮"; break;
            default: unit = string.Empty; break;
        }
        int target = TargetValue();
        return "任务进度：" + Mathf.Min(CurrentValue(), target) + "/" + target + unit;
    }

    public int TargetValue()
    {
        return Mathf.Max(1, GameDataMgr.Instance.MissionTarget(Level));
    }

    public void CompleteMission()
    {
        if (State != CampaignMissionState.Running || Level == null)
            return;

        State = CampaignMissionState.Victory;
        ScoreManager score = ScoreManager.Instance;
        if (score != null)
            score.FinishScore();
        EarnedStars = CalculateStars();
        EarnedChallengeMask = CalculateChallengeMask();
        CampaignProgress.CompleteLevel(Level.id, EarnedStars,
            score != null ? score.TotalScore : 0,
            score != null ? Mathf.FloorToInt(score.RunTime) : 0,
            EarnedChallengeMask);
        RecordCalibration("victory");
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.CompleteMission();
        CampaignMissionUI.Ensure().ShowResultNow(this);
    }

    public void FailMission()
    {
        if (State != CampaignMissionState.Running)
            return;

        State = CampaignMissionState.Failed;
        ScoreManager score = ScoreManager.Instance;
        if (score != null)
            score.FinishScore();
        if (Level != null)
        {
            CampaignProgress.RecordAttempt(Level.id,
                score != null ? score.TotalScore : 0,
                score != null ? Mathf.FloorToInt(score.RunTime) : 0);
        }
        RecordCalibration("player_death");
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.GameOver();
        else
            Time.timeScale = 0f;
        CampaignMissionUI.Ensure().ShowResultNow(this);
    }

    public int CalculateStars()
    {
        PlayerObject player = PlayerObject.Instance;
        ScoreManager score = ScoreManager.Instance;
        if (player == null || player.maxHp <= 0 || score == null)
            return 1;

        int difficulty = GameDataMgr.Instance.GetDifficulty().id;
        int hitAllowance = difficulty == 1 ? 2 : (difficulty == 2 ? 1 : 0);
        int mistakeAllowance = difficulty == 1 ? 1 : 0;
        int stageMistakes = ChallengeMistakeCount();
        int comboTarget = ChallengeComboTarget();
        int stars = 1;
        if (player.nowHp * 2 >= player.maxHp && score.PlayerHits <= hitAllowance &&
            stageMistakes <= mistakeAllowance && score.MaxMechanicCombo >= Mathf.CeilToInt(comboTarget * 0.5f))
            stars++;
        if (player.nowHp >= player.maxHp && score.PlayerHits == 0 && stageMistakes == 0 &&
            score.MaxMechanicCombo >= comboTarget)
            stars++;
        return Mathf.Clamp(stars, 1, 3);
    }

    public int CalculateChallengeMask()
    {
        ScoreManager score = ScoreManager.Instance;
        if (score == null)
            return 0;
        int mask = 0;
        int stageMistakes = ChallengeMistakeCount();
        if (score.PlayerHits == 0)
            mask |= NoHitChallenge;
        if (stageMistakes == 0)
            mask |= NoMistakeChallenge;
        if (score.PlayerHits == 0 && stageMistakes == 0 &&
            score.MechanicCombo >= ChallengeComboTarget())
            mask |= FullComboChallenge;
        return mask;
    }

    public int ChallengeComboTarget()
    {
        if (Level == null)
            return 1;
        return Level.id == 1 ? 5 : Mathf.Max(3, TargetValue());
    }

    public int ChallengeMistakeCount()
    {
        ScoreManager score = ScoreManager.Instance;
        return score == null || Level == null ? 0 : score.MissionMistakesForLevel(Level.id);
    }

    public string MistakeChallengeLabel()
    {
        switch (Level != null ? Level.id : 0)
        {
            case 1: return "零缺口碰撞";
            case 2: return "零误点";
            case 3: return "零提前闪避";
            case 4: return "零能量损失";
            case 5: return "零反弹误触";
            default: return "零失误";
        }
    }

    public string MistakeMetricLabel()
    {
        switch (Level != null ? Level.id : 0)
        {
            case 1: return "缺口碰撞";
            case 2: return "误点";
            case 3: return "提前闪避";
            case 4: return "能量损失";
            case 5: return "反弹误触";
            default: return "失误";
        }
    }

    public string ChallengeText()
    {
        int mask = EarnedChallengeMask;
        return ChallengeState(mask, NoHitChallenge) + " 零受击  ·  " +
            ChallengeState(mask, NoMistakeChallenge) + " " + MistakeChallengeLabel() + "  ·  " +
            ChallengeState(mask, FullComboChallenge) + " 全程不断连击";
    }

    public string LiveChallengeText()
    {
        ScoreManager score = ScoreManager.Instance;
        if (score == null)
            return string.Empty;

        bool noHit = score.PlayerHits == 0;
        bool noMistake = ChallengeMistakeCount() == 0;
        bool fullCombo = noHit && noMistake && score.MechanicCombo >= ChallengeComboTarget();
        int comboTarget = ChallengeComboTarget();
        return LiveChallengeState(noHit, "零受击") + "  " +
            LiveChallengeState(noMistake, MistakeChallengeLabel()) + "\n" +
            LiveChallengeState(fullCombo,
                "连续机制 " + Mathf.Min(score.MechanicCombo, comboTarget) + "/" + comboTarget);
    }

    public string ResultAdvice()
    {
        ScoreManager score = ScoreManager.Instance;
        if (score == null || Level == null)
            return "观察预警并调整移动节奏";
        int stageMistakes = ChallengeMistakeCount();
        if (stageMistakes >= 2)
        {
            switch (Level.id)
            {
                case 1: return "缺口碰撞过多：提前对准缺口，穿过后再换位";
                case 2: return "误点过多：只点击持续呼吸发光的核心";
                case 3: return "提前闪避过多：等锁定线固定后再横向换位";
                case 4: return "能量损失过多：携带能量时先清出路线";
                case 5: return "反弹误触过多：只点击金色核心，普通弹幕以躲避为主";
            }
        }
        if (Level.id == 3 && State == CampaignMissionState.Failed)
            return "闪避时机偏早：等锁定线固定后再横向换位";
        if (Level.id == 4 && score.PlayerHits > 0)
            return "携带能量时受击较多：先清出路线，再离开中央蓝环";
        if (Level.id == 5 && BossController.Instance != null && !BossController.Instance.ShieldBroken)
            return "反弹不足：优先点击金色核心，普通弹幕以躲避为主";
        if (score.PlayerHits >= 3)
            return "受击较多：先看预警方向，再使用护盾穿过密集区";
        if (score.MaxMechanicCombo < ChallengeComboTarget())
            return "连击尚未达标：受击或误点都会中断机制连击";
        return State == CampaignMissionState.Victory
            ? "表现稳定：可继续挑战零受击、" + MistakeChallengeLabel() + "与全程连击"
            : "接近完成：保持当前节奏并减少最后阶段的冒险操作";
    }

    private static string ChallengeState(int mask, int flag)
    {
        return (mask & flag) != 0 ? "[完成]" : "[未完成]";
    }

    private static string LiveChallengeState(bool active, string text)
    {
        return active ? "[72E6A1]● " + text + "[-]" : "[6F7D86]× " + text + "[-]";
    }

    private void RecordCalibration(string exitReason)
    {
        if (calibrationRecorded || Level == null)
            return;
        calibrationRecorded = true;
        CampaignBalanceTelemetry.Record(this, exitReason);
    }
}

public static class CampaignBalanceTelemetry
{
    private const string FileName = "CampaignBalanceRunsV7.csv";
    private const long MaxFileBytes = 2 * 1024 * 1024;
    private const string ConsentKey = "U2.Balance.Consent";
    private const string TesterKey = "U2.Balance.TesterId";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
    public static bool Enabled => PlayerPrefs.GetInt(ConsentKey, 0) == 1 ||
        Environment.CommandLine.IndexOf("-u2-balance-recording", StringComparison.OrdinalIgnoreCase) >= 0;
    public static string PrivacySummary =>
        "平衡数据仅在同意后保存在本机，不上传；记录匿名测试编号、关卡结果和性能区间，不读取设备唯一标识。";

    public static void SetConsent(bool enabled)
    {
        PlayerPrefs.SetInt(ConsentKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void Record(CampaignMissionController mission, string exitReason = "unknown")
    {
        if (!Enabled || mission == null || mission.Level == null)
            return;

        // Automated editor tests use synthetic runs and must never enter the
        // real-player balance dataset.
        if (Application.isEditor &&
            Environment.CommandLine.IndexOf("-runTests", StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        try
        {
            RotateOversizedFile();
            bool writeHeader = !File.Exists(FilePath) || new FileInfo(FilePath).Length == 0;
            StringBuilder line = new StringBuilder();
            if (writeHeader)
            {
                line.AppendLine("schema,game_version,build,recorded_at,session,tester,level,difficulty,multiplier,hero,result,exit_reason,duration,average_fps,below_30_pct,below_45_pct,resolution,device_profile,networked,phase,waves,hp,max_hp,hits,mistakes,stage_mistakes,max_combo,current_combo,objective,target,stars,challenges,raw_score,total_score,calibration_band");
            }

            ScoreManager score = ScoreManager.Instance;
            PlayerObject player = PlayerObject.Instance;
            LevelEncounterDirector encounter = LevelEncounterDirector.Instance;
            int difficultyId = GameDataMgr.Instance.GetDifficulty().id;
            float multiplier = score != null ? score.ScoreRate : ScoreManager.DefaultRateFor(difficultyId);
            float below30 = mission.SampledFrames > 0 ? mission.FramesBelow30 * 100f / mission.SampledFrames : 0f;
            float below45 = mission.SampledFrames > 0 ? mission.FramesBelow45 * 100f / mission.SampledFrames : 0f;
            bool networked = NetworkBattleSession.Instance != null && NetworkBattleSession.Instance.IsBattle;
            line.Append("7,")
                .Append(Application.version).Append(',')
                .Append(Application.buildGUID).Append(',')
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(',')
                .Append(mission.SessionId).Append(',')
                .Append(AnonymousTesterId()).Append(',')
                .Append(mission.Level.id).Append(',')
                .Append(difficultyId).Append(',')
                .Append(multiplier.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(GameDataMgr.Instance.heroIndex + 1).Append(',')
                .Append(mission.State == CampaignMissionState.Victory ? "victory" : "failed").Append(',')
                .Append(SafeField(exitReason)).Append(',')
                .Append(score != null ? Mathf.FloorToInt(score.RunTime) : 0).Append(',')
                .Append(mission.AverageFrameRate.ToString("0.0", CultureInfo.InvariantCulture)).Append(',')
                .Append(below30.ToString("0.0", CultureInfo.InvariantCulture)).Append(',')
                .Append(below45.ToString("0.0", CultureInfo.InvariantCulture)).Append(',')
                .Append(Screen.width).Append('x').Append(Screen.height).Append(',')
                .Append(DeviceProfile()).Append(',')
                .Append(networked ? "yes" : "no").Append(',')
                .Append(encounter != null ? encounter.CurrentPhase : 1).Append(',')
                .Append(encounter != null ? encounter.WavesFired : 0).Append(',')
                .Append(player != null ? player.nowHp : 0).Append(',')
                .Append(player != null ? player.maxHp : 0).Append(',')
                .Append(score != null ? score.PlayerHits : 0).Append(',')
                .Append(score != null ? score.Mistakes : 0).Append(',')
                .Append(mission.ChallengeMistakeCount()).Append(',')
                .Append(score != null ? score.MaxMechanicCombo : 0).Append(',')
                .Append(score != null ? score.MechanicCombo : 0).Append(',')
                .Append(mission.CurrentValue()).Append(',')
                .Append(mission.TargetValue()).Append(',')
                .Append(mission.EarnedStars).Append(',')
                .Append(mission.EarnedChallengeMask).Append(',')
                .Append(score != null ? score.RawScore : 0).Append(',')
                .Append(score != null ? score.TotalScore : 0).Append(',')
                .Append(CalibrationBand(mission, score))
                .AppendLine();
            File.AppendAllText(FilePath, line.ToString(), Encoding.UTF8);
        }
        catch (Exception exception)
        {
            GameLog.Warning(GameLogCategory.Performance, "BALANCE_WRITE", exception.Message);
        }
    }

    private static void RotateOversizedFile()
    {
        if (!File.Exists(FilePath) || new FileInfo(FilePath).Length < MaxFileBytes)
            return;

        string archive = Path.Combine(Application.persistentDataPath,
            "CampaignBalanceRunsV7-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv");
        File.Move(FilePath, archive);
    }

    private static string AnonymousTesterId()
    {
        string value = PlayerPrefs.GetString(TesterKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(value))
            return value;
        value = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
        PlayerPrefs.SetString(TesterKey, value);
        PlayerPrefs.Save();
        return value;
    }

    private static string DeviceProfile()
    {
        int memoryBucket = Mathf.CeilToInt(SystemInfo.systemMemorySize / 2048f) * 2;
        return SystemInfo.operatingSystemFamily + "-cpu" + SystemInfo.processorCount + "-ram" +
            Mathf.Max(2, memoryBucket) + "gb";
    }

    private static string SafeField(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Replace(',', '_').Replace('\n', ' ');
    }

    private static string CalibrationBand(CampaignMissionController mission, ScoreManager score)
    {
        if (mission.State != CampaignMissionState.Victory)
            return score != null && (score.PlayerHits >= 5 || score.Mistakes >= 4) ? "too_hard" : "failed";

        if (score != null && score.PlayerHits <= 1 && score.Mistakes == 0 &&
            score.MaxMechanicCombo >= mission.ChallengeComboTarget())
            return "too_easy";
        if (score != null && (score.PlayerHits >= 5 || score.Mistakes >= 4))
            return "too_hard";
        return "target";
    }
}

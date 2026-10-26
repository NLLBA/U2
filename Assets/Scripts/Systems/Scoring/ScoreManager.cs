using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum MissionMistakeKind
{
    Generic,
    CorridorContact,
    WrongClick,
    EarlyDodge,
    EnergyLoss,
    WrongReflection,
}

public class ScoreManager : MonoBehaviour
{
    private static ScoreManager instance;

    public static ScoreManager Instance => instance;

    [FormerlySerializedAs("survivalPointsPerSecond")]
    [Min(0)] public int timePoints = 10;
    [FormerlySerializedAs("manualClearPoints")]
    [Min(0)] public int clearPoints = 25;
    [FormerlySerializedAs("bossRoundBaseReward")]
    [Min(0)] public int bossPoints = 1000;
    [Min(0)] public int corridorPassPoints = 100;
    [Min(0)] public int clusterWavePoints = 120;
    [Min(0)] public int perfectDodgePoints = 150;
    [Min(0)] public int relayBeaconPoints = 250;
    [Min(0)] public int reflectedCorePoints = 200;
    [FormerlySerializedAs("easyDifficultyCoefficient")]
    [Min(0.01f)] public float easyRate = 1f;
    [FormerlySerializedAs("normalDifficultyCoefficient")]
    [Min(0.01f)] public float normalRate = 1.25f;
    [FormerlySerializedAs("hardDifficultyCoefficient")]
    [Min(0.01f)] public float hardRate = 1.6f;

    public float RunTime { get; private set; }
    public int TimeScore { get; private set; }
    public int ClearCount { get; private set; }
    public int ClusterWavesCleared { get; private set; }
    public int ClearScore { get; private set; }
    public int Threat { get; private set; }
    public int BossRounds { get; private set; }
    public int BossScore { get; private set; }
    public int PerfectCorridorPasses { get; private set; }
    public int PerfectDodges { get; private set; }
    public int RelayBeacons { get; private set; }
    public int ReflectedCores { get; private set; }
    public int Mistakes { get; private set; }
    public int CorridorMistakes { get; private set; }
    public int WrongClickMistakes { get; private set; }
    public int EarlyDodgeMistakes { get; private set; }
    public int EnergyLossMistakes { get; private set; }
    public int WrongReflectionMistakes { get; private set; }
    public int PlayerHits { get; private set; }
    public int MechanicCombo { get; private set; }
    public int MaxMechanicCombo { get; private set; }
    public int MechanicBonusScore { get; private set; }
    public float ScoreRate { get; private set; }
    public int RawScore { get; private set; }
    public int TotalScore { get; private set; }
    public bool Finished { get; private set; }

    public event Action<int> ScoreUpdated;

    private GameFlowManager flow;
    private DifficultyDirector director;
    private readonly HashSet<int> clearedClusterWaves = new HashSet<int>();
    private float nextScoreRefresh;

    public static ScoreManager Ensure(GameObject host = null)
    {
        if (instance != null)
            return instance;

        GameObject target = host != null ? host : new GameObject("ScoreManager");
        ScoreManager manager = target.GetComponent<ScoreManager>();
        return manager != null ? manager : target.AddComponent<ScoreManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            DestroyNow(this);
            return;
        }

        instance = this;
        ClampValues();
        Bind();
        Reset();
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        if (flow != null)
        {
            flow.StateUpdated -= OnFlowState;
        }

        instance = null;
    }

    private void OnValidate()
    {
        ClampValues();
    }

    private void Update()
    {
        if (flow == null || director == null)
        {
            Bind();
        }

        float interval = GameDataMgr.Instance.Tuning.scoreRefreshSeconds;
        if (Time.unscaledTime >= nextScoreRefresh)
        {
            nextScoreRefresh = Time.unscaledTime + interval;
            UpdateScore();
        }
    }

    public void Reset()
    {
        RunTime = 0f;
        TimeScore = 0;
        ClearCount = 0;
        ClusterWavesCleared = 0;
        clearedClusterWaves.Clear();
        ClearScore = 0;
        Threat = 1;
        BossRounds = 0;
        BossScore = 0;
        PerfectCorridorPasses = 0;
        PerfectDodges = 0;
        RelayBeacons = 0;
        ReflectedCores = 0;
        Mistakes = 0;
        CorridorMistakes = 0;
        WrongClickMistakes = 0;
        EarlyDodgeMistakes = 0;
        EnergyLossMistakes = 0;
        WrongReflectionMistakes = 0;
        PlayerHits = 0;
        MechanicCombo = 0;
        MaxMechanicCombo = 0;
        MechanicBonusScore = 0;
        ScoreRate = RateFor(DifficultyId());
        RawScore = 0;
        TotalScore = 0;
        Finished = false;
        nextScoreRefresh = 0f;
        UpdateScore();
    }

    public bool RegisterClear()
    {
        if (Finished)
            return false;

        ClearCount++;
        UpdateScore();
        return true;
    }

    public bool RegisterClusterWave(int waveId)
    {
        if (Finished || waveId <= 0 || !clearedClusterWaves.Add(waveId))
            return false;

        ClusterWavesCleared++;
        RegisterMechanicSuccess(clusterWavePoints);
        if (GamePanel.Instance != null)
            GamePanel.Instance.ShowCombatFeedback("连锁清除 +" + clusterWavePoints,
                new Color(0.25f, 1f, 0.7f));
        return true;
    }

    public bool RegisterCorridorPass()
    {
        if (Finished)
            return false;
        PerfectCorridorPasses++;
        RegisterMechanicSuccess(corridorPassPoints);
        return true;
    }

    public bool RegisterPerfectDodge()
    {
        if (Finished)
            return false;
        PerfectDodges++;
        RegisterMechanicSuccess(perfectDodgePoints);
        return true;
    }

    public bool RegisterRelayBeacon()
    {
        if (Finished)
            return false;
        RelayBeacons++;
        RegisterMechanicSuccess(relayBeaconPoints);
        return true;
    }

    public bool RegisterReflectedCore()
    {
        if (Finished)
            return false;
        ReflectedCores++;
        RegisterMechanicSuccess(reflectedCorePoints);
        return true;
    }

    public bool RegisterMistake()
    {
        // Existing callers use this overload for the level-2 decoy mistake.
        return RegisterMistake(MissionMistakeKind.WrongClick);
    }

    public bool RegisterMistake(MissionMistakeKind kind)
    {
        if (Finished)
            return false;
        Mistakes++;
        switch (kind)
        {
            case MissionMistakeKind.CorridorContact:
                CorridorMistakes++;
                break;
            case MissionMistakeKind.WrongClick:
                WrongClickMistakes++;
                break;
            case MissionMistakeKind.EarlyDodge:
                EarlyDodgeMistakes++;
                break;
            case MissionMistakeKind.EnergyLoss:
                EnergyLossMistakes++;
                break;
            case MissionMistakeKind.WrongReflection:
                WrongReflectionMistakes++;
                break;
        }
        MechanicCombo = 0;
        return true;
    }

    public int MissionMistakesForLevel(int levelId)
    {
        switch (levelId)
        {
            case 1:
                return CorridorMistakes;
            case 2:
                return WrongClickMistakes;
            case 3:
                return EarlyDodgeMistakes;
            case 4:
                return EnergyLossMistakes;
            case 5:
                return WrongReflectionMistakes;
            default:
                return Mistakes;
        }
    }

    public bool RegisterPlayerHit()
    {
        if (Finished)
            return false;
        PlayerHits++;
        MechanicCombo = 0;
        return true;
    }

    public void UpdateScore()
    {
        if (Finished)
            return;

        int oldScore = TotalScore;
        GameFlowManager flow = GetFlow();
        DifficultyDirector activeDirector = Director();

        RunTime = flow != null ? Mathf.Max(0f, flow.RunTime) : 0f;
        BossRounds = flow != null ? Mathf.Max(0, flow.BossRounds) : 0;
        Threat = activeDirector != null ? Mathf.Max(1, activeDirector.ThreatLevel) : 1;
        ScoreRate = RateFor(DifficultyId());

        TimeScore = SafeInt(Math.Floor(RunTime * timePoints));
        ClearScore = SafeInt((long)ClearCount * clearPoints);
        BossScore = BossScoreFor(BossRounds);

        RawScore = SafeInt((long)TimeScore + ClearScore + BossScore + MechanicBonusScore);
        TotalScore = SafeInt(Math.Round(RawScore * ScoreRate, MidpointRounding.AwayFromZero));

        if (TotalScore != oldScore)
        {
            ScoreUpdated?.Invoke(TotalScore);
        }
    }

    public void FinishScore()
    {
        if (Finished)
            return;

        UpdateScore();
        Finished = true;
    }

    public float RateFor(int difficultyId)
    {
        switch (difficultyId)
        {
            case 1:
                return easyRate;
            case 3:
                return hardRate;
            case 2:
            default:
                return normalRate;
        }
    }

    // Used by menus before the gameplay score component exists. Gameplay uses
    // the serialized fields above, so a project can still tune the live rate.
    public static float DefaultRateFor(int difficultyId)
    {
        switch (difficultyId)
        {
            case 1: return 1f;
            case 3: return 1.6f;
            case 2:
            default: return 1.25f;
        }
    }

    public int BossScoreFor(int completedRounds)
    {
        long rounds = Mathf.Max(0, completedRounds);
        long reward = (long)bossPoints * rounds * (rounds + 1L) / 2L;
        return SafeInt(reward);
    }

    private void Bind()
    {
        GameFlowManager nextFlow = GetComponent<GameFlowManager>();
        if (nextFlow == null)
        {
            nextFlow = GameFlowManager.Instance;
        }

        if (flow != nextFlow)
        {
            if (flow != null)
            {
                flow.StateUpdated -= OnFlowState;
            }

            flow = nextFlow;
            if (flow != null)
            {
                flow.StateUpdated += OnFlowState;
            }
        }

        director = GetComponent<DifficultyDirector>();
        if (director == null)
        {
            director = DifficultyDirector.Instance;
        }
    }

    private void OnFlowState(GameFlowState state)
    {
        if (state == GameFlowState.GameOver || state == GameFlowState.MissionComplete)
        {
            FinishScore();
            return;
        }

        if (state == GameFlowState.Normal && flow != null && flow.RunTime <= 0.0001f)
        {
            Reset();
        }
    }

    private GameFlowManager GetFlow()
    {
        if (flow == null)
        {
            Bind();
        }

        return flow;
    }

    private DifficultyDirector Director()
    {
        if (director == null)
        {
            Bind();
        }

        return director;
    }

    private int DifficultyId()
    {
        DifficultyInfo difficulty = GameDataMgr.Instance.GetDifficulty();
        return difficulty != null ? difficulty.id : 2;
    }

    private void ClampValues()
    {
        timePoints = Mathf.Max(0, timePoints);
        clearPoints = Mathf.Max(0, clearPoints);
        bossPoints = Mathf.Max(0, bossPoints);
        corridorPassPoints = Mathf.Max(0, corridorPassPoints);
        clusterWavePoints = Mathf.Max(0, clusterWavePoints);
        perfectDodgePoints = Mathf.Max(0, perfectDodgePoints);
        relayBeaconPoints = Mathf.Max(0, relayBeaconPoints);
        reflectedCorePoints = Mathf.Max(0, reflectedCorePoints);
        easyRate = Mathf.Max(0.01f, easyRate);
        normalRate = Mathf.Max(0.01f, normalRate);
        hardRate = Mathf.Max(0.01f, hardRate);
    }

    private void RegisterMechanicSuccess(int points)
    {
        MechanicCombo++;
        MaxMechanicCombo = Mathf.Max(MaxMechanicCombo, MechanicCombo);
        MechanicBonusScore = SafeInt((long)MechanicBonusScore + Mathf.Max(0, points));
        UpdateScore();
    }

    private static int SafeInt(double value)
    {
        if (value >= int.MaxValue)
            return int.MaxValue;
        if (value <= 0d)
            return 0;
        return (int)value;
    }

    private static void DestroyNow(Component target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}

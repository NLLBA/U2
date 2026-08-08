using System;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    private static ScoreManager instance;

    public static ScoreManager Instance => instance;

    [Min(0)] public int survivalPointsPerSecond = 10;
    [Min(0)] public int manualClearPoints = 25;
    [Min(0)] public int bossRoundBaseReward = 1000;
    [Min(0.01f)] public float easyDifficultyCoefficient = 1f;
    [Min(0.01f)] public float normalDifficultyCoefficient = 1.25f;
    [Min(0.01f)] public float hardDifficultyCoefficient = 1.6f;

    public float SurvivalTime { get; private set; }
    public int SurvivalScore { get; private set; }
    public int ManualClearedBullets { get; private set; }
    public int ManualClearScore { get; private set; }
    public int CurrentThreatLevel { get; private set; }
    public int CompletedBossRounds { get; private set; }
    public int BossStageReward { get; private set; }
    public float DifficultyCoefficient { get; private set; }
    public int FinalScore { get; private set; }
    public bool IsFinalized { get; private set; }

    public event Action<int> ScoreChanged;

    private GameFlowManager flowManager;
    private DifficultyDirector difficultyDirector;

    public static ScoreManager EnsureInstance(GameObject host = null)
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
            DestroyComponent(this);
            return;
        }

        instance = this;
        ClampSettings();
        BindDependencies();
        ResetScore();
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        if (flowManager != null)
        {
            flowManager.StateChanged -= HandleFlowStateChanged;
        }

        instance = null;
    }

    private void OnValidate()
    {
        ClampSettings();
    }

    private void Update()
    {
        if (flowManager == null || difficultyDirector == null)
        {
            BindDependencies();
        }

        RecalculateScore();
    }

    public void ResetScore()
    {
        SurvivalTime = 0f;
        SurvivalScore = 0;
        ManualClearedBullets = 0;
        ManualClearScore = 0;
        CurrentThreatLevel = 1;
        CompletedBossRounds = 0;
        BossStageReward = 0;
        DifficultyCoefficient = GetDifficultyCoefficient(GetCurrentDifficultyId());
        FinalScore = 0;
        IsFinalized = false;
        RecalculateScore();
    }

    public bool RegisterManualBulletClear()
    {
        if (IsFinalized)
            return false;

        ManualClearedBullets++;
        RecalculateScore();
        return true;
    }

    public void RecalculateScore()
    {
        if (IsFinalized)
            return;

        int previousScore = FinalScore;
        GameFlowManager flow = GetFlowManager();
        DifficultyDirector director = GetDifficultyDirector();

        SurvivalTime = flow != null ? Mathf.Max(0f, flow.SurvivalTime) : 0f;
        CompletedBossRounds = flow != null ? Mathf.Max(0, flow.CompletedBossRounds) : 0;
        CurrentThreatLevel = director != null ? Mathf.Max(1, director.ThreatLevel) : 1;
        DifficultyCoefficient = GetDifficultyCoefficient(GetCurrentDifficultyId());

        SurvivalScore = ClampToInt(Math.Floor(SurvivalTime * survivalPointsPerSecond));
        ManualClearScore = ClampToInt((long)ManualClearedBullets * manualClearPoints);
        BossStageReward = CalculateBossStageReward(CompletedBossRounds);

        long rawScore = (long)SurvivalScore + ManualClearScore + BossStageReward;
        FinalScore = ClampToInt(Math.Round(rawScore * DifficultyCoefficient, MidpointRounding.AwayFromZero));

        if (FinalScore != previousScore)
        {
            ScoreChanged?.Invoke(FinalScore);
        }
    }

    public void FinalizeScore()
    {
        if (IsFinalized)
            return;

        RecalculateScore();
        IsFinalized = true;
    }

    public float GetDifficultyCoefficient(int difficultyId)
    {
        switch (difficultyId)
        {
            case 1:
                return easyDifficultyCoefficient;
            case 3:
                return hardDifficultyCoefficient;
            case 2:
            default:
                return normalDifficultyCoefficient;
        }
    }

    public int CalculateBossStageReward(int completedRounds)
    {
        long rounds = Mathf.Max(0, completedRounds);
        long reward = (long)bossRoundBaseReward * rounds * (rounds + 1L) / 2L;
        return ClampToInt(reward);
    }

    private void BindDependencies()
    {
        GameFlowManager nextFlow = GetComponent<GameFlowManager>();
        if (nextFlow == null)
        {
            nextFlow = GameFlowManager.Instance;
        }

        if (flowManager != nextFlow)
        {
            if (flowManager != null)
            {
                flowManager.StateChanged -= HandleFlowStateChanged;
            }

            flowManager = nextFlow;
            if (flowManager != null)
            {
                flowManager.StateChanged += HandleFlowStateChanged;
            }
        }

        difficultyDirector = GetComponent<DifficultyDirector>();
        if (difficultyDirector == null)
        {
            difficultyDirector = DifficultyDirector.Instance;
        }
    }

    private void HandleFlowStateChanged(GameFlowState state)
    {
        if (state == GameFlowState.GameOver)
        {
            FinalizeScore();
            return;
        }

        if (state == GameFlowState.Normal && flowManager != null && flowManager.SurvivalTime <= 0.0001f)
        {
            ResetScore();
        }
    }

    private GameFlowManager GetFlowManager()
    {
        if (flowManager == null)
        {
            BindDependencies();
        }

        return flowManager;
    }

    private DifficultyDirector GetDifficultyDirector()
    {
        if (difficultyDirector == null)
        {
            BindDependencies();
        }

        return difficultyDirector;
    }

    private int GetCurrentDifficultyId()
    {
        DifficultyInfo difficulty = GameDataMgr.Instance.GetNowDifficultyInfo();
        return difficulty != null ? difficulty.id : 2;
    }

    private void ClampSettings()
    {
        survivalPointsPerSecond = Mathf.Max(0, survivalPointsPerSecond);
        manualClearPoints = Mathf.Max(0, manualClearPoints);
        bossRoundBaseReward = Mathf.Max(0, bossRoundBaseReward);
        easyDifficultyCoefficient = Mathf.Max(0.01f, easyDifficultyCoefficient);
        normalDifficultyCoefficient = Mathf.Max(0.01f, normalDifficultyCoefficient);
        hardDifficultyCoefficient = Mathf.Max(0.01f, hardDifficultyCoefficient);
    }

    private static int ClampToInt(double value)
    {
        if (value >= int.MaxValue)
            return int.MaxValue;
        if (value <= 0d)
            return 0;
        return (int)value;
    }

    private static void DestroyComponent(Component target)
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

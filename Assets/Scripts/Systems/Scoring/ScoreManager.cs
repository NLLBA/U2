using System;
using UnityEngine;
using UnityEngine.Serialization;

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
    [FormerlySerializedAs("easyDifficultyCoefficient")]
    [Min(0.01f)] public float easyRate = 1f;
    [FormerlySerializedAs("normalDifficultyCoefficient")]
    [Min(0.01f)] public float normalRate = 1.25f;
    [FormerlySerializedAs("hardDifficultyCoefficient")]
    [Min(0.01f)] public float hardRate = 1.6f;

    public float RunTime { get; private set; }
    public int TimeScore { get; private set; }
    public int ClearCount { get; private set; }
    public int ClearScore { get; private set; }
    public int Threat { get; private set; }
    public int BossRounds { get; private set; }
    public int BossScore { get; private set; }
    public float ScoreRate { get; private set; }
    public int TotalScore { get; private set; }
    public bool Finished { get; private set; }

    public event Action<int> ScoreUpdated;

    private GameFlowManager flow;
    private DifficultyDirector director;

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

        UpdateScore();
    }

    public void Reset()
    {
        RunTime = 0f;
        TimeScore = 0;
        ClearCount = 0;
        ClearScore = 0;
        Threat = 1;
        BossRounds = 0;
        BossScore = 0;
        ScoreRate = RateFor(DifficultyId());
        TotalScore = 0;
        Finished = false;
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

        long raw = (long)TimeScore + ClearScore + BossScore;
        TotalScore = SafeInt(Math.Round(raw * ScoreRate, MidpointRounding.AwayFromZero));

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
        if (state == GameFlowState.GameOver)
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
        easyRate = Mathf.Max(0.01f, easyRate);
        normalRate = Mathf.Max(0.01f, normalRate);
        hardRate = Mathf.Max(0.01f, hardRate);
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

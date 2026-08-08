using System;
using UnityEngine;

public enum GameFlowState
{
    Normal,
    BossWarning,
    BossSurvival,
    Paused,
    GameOver,
}

public class GameFlowManager : MonoBehaviour
{
    private static GameFlowManager instance;

    public static GameFlowManager Instance => instance;

    [Min(1f)] public float firstBossTime = 120f;
    [Min(0f)] public float bossWarningTime = 3f;
    [Min(1f)] public float bossDuration = 30f;
    [Min(1f)] public float normalInterval = 90f;

    public GameFlowState State { get; private set; }
    public float SurvivalTime { get; private set; }
    public float StateElapsedTime { get; private set; }
    public float NextBossTime { get; private set; }
    public int CurrentBossRound { get; private set; }
    public int CompletedBossRounds { get; private set; }

    public event Action<GameFlowState> StateChanged;

    private GameFlowState stateBeforePause;
    private float stateElapsedBeforePause;

    public static GameFlowManager EnsureInstance(GameObject host = null)
    {
        if (instance != null)
            return instance;

        GameObject target = host != null ? host : new GameObject("GameFlowManager");
        GameFlowManager manager = target.GetComponent<GameFlowManager>();
        return manager != null ? manager : target.AddComponent<GameFlowManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        ResetFlow();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        AdvanceFlow(Time.deltaTime);
    }

    public void ResetFlow()
    {
        firstBossTime = Mathf.Max(1f, firstBossTime);
        bossWarningTime = Mathf.Clamp(bossWarningTime, 0f, firstBossTime);
        bossDuration = Mathf.Max(1f, bossDuration);
        normalInterval = Mathf.Max(bossWarningTime + 1f, normalInterval);

        SurvivalTime = 0f;
        StateElapsedTime = 0f;
        NextBossTime = firstBossTime;
        CurrentBossRound = 0;
        CompletedBossRounds = 0;
        stateBeforePause = GameFlowState.Normal;
        stateElapsedBeforePause = 0f;
        SetState(GameFlowState.Normal);
    }

    public void AdvanceFlow(float deltaTime)
    {
        if (State == GameFlowState.Paused || State == GameFlowState.GameOver)
            return;

        float remainingTime = Mathf.Max(0f, deltaTime);
        int transitionGuard = 0;

        while (remainingTime > 0f && transitionGuard++ < 8)
        {
            float timeToTransition = GetTimeToTransition();
            float step = Mathf.Min(remainingTime, timeToTransition);

            SurvivalTime += step;
            StateElapsedTime += step;
            remainingTime -= step;

            if (timeToTransition - step > 0.0001f)
                break;

            MoveToNextState();
        }
    }

    public void PauseGame()
    {
        if (State == GameFlowState.Paused || State == GameFlowState.GameOver)
            return;

        stateBeforePause = State;
        stateElapsedBeforePause = StateElapsedTime;
        Time.timeScale = 0f;
        SetState(GameFlowState.Paused);
    }

    public void ResumeGame()
    {
        if (State != GameFlowState.Paused)
            return;

        Time.timeScale = 1f;
        SetState(stateBeforePause);
        StateElapsedTime = stateElapsedBeforePause;
    }

    public void EnterGameOver()
    {
        if (State == GameFlowState.GameOver)
            return;

        Time.timeScale = 0f;
        SetState(GameFlowState.GameOver);
    }

    public float GetCurrentCountdown()
    {
        switch (State)
        {
            case GameFlowState.Normal:
            case GameFlowState.BossWarning:
                return Mathf.Max(0f, NextBossTime - SurvivalTime);
            case GameFlowState.BossSurvival:
                return Mathf.Max(0f, bossDuration - StateElapsedTime);
            default:
                return 0f;
        }
    }

    public string GetStatusText()
    {
        int seconds = Mathf.CeilToInt(GetCurrentCountdown());
        switch (State)
        {
            case GameFlowState.Normal:
                return "距离第" + (CompletedBossRounds + 1) + "轮BOSS：" + seconds + "秒";
            case GameFlowState.BossWarning:
                return "BOSS预警：第" + (CompletedBossRounds + 1) + "轮 " + seconds + "秒";
            case GameFlowState.BossSurvival:
                return "第" + CurrentBossRound + "轮BOSS：" + seconds + "秒";
            case GameFlowState.Paused:
                return "游戏已暂停";
            case GameFlowState.GameOver:
                return "游戏结束";
            default:
                return string.Empty;
        }
    }

    private float GetTimeToTransition()
    {
        switch (State)
        {
            case GameFlowState.Normal:
                return Mathf.Max(0f, NextBossTime - bossWarningTime - SurvivalTime);
            case GameFlowState.BossWarning:
                return Mathf.Max(0f, NextBossTime - SurvivalTime);
            case GameFlowState.BossSurvival:
                return Mathf.Max(0f, bossDuration - StateElapsedTime);
            default:
                return float.PositiveInfinity;
        }
    }

    private void MoveToNextState()
    {
        switch (State)
        {
            case GameFlowState.Normal:
                SetState(GameFlowState.BossWarning);
                break;
            case GameFlowState.BossWarning:
                CurrentBossRound = CompletedBossRounds + 1;
                SetState(GameFlowState.BossSurvival);
                break;
            case GameFlowState.BossSurvival:
                CompletedBossRounds = CurrentBossRound;
                NextBossTime = SurvivalTime + normalInterval;
                SetState(GameFlowState.Normal);
                break;
        }
    }

    private void SetState(GameFlowState newState)
    {
        GameFlowState previousState = State;
        State = newState;
        StateElapsedTime = 0f;
        StateChanged?.Invoke(State);

        if (!Application.isPlaying)
            return;

        switch (newState)
        {
            case GameFlowState.BossWarning:
                SoundManager.Play(SoundCue.BossWarning);
                break;
            case GameFlowState.BossSurvival:
                SoundManager.Play(SoundCue.BossAppear);
                break;
            case GameFlowState.Normal:
                if (previousState == GameFlowState.BossSurvival)
                {
                    SoundManager.Play(SoundCue.BossCleared);
                }
                break;
            case GameFlowState.GameOver:
                SoundManager.Play(SoundCue.GameOver);
                break;
        }
    }
}

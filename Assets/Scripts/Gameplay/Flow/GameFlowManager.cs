using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using System;

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

    [FormerlySerializedAs("firstBossTime")]
    [Min(1f)] public float bossStart = 120f;
    [FormerlySerializedAs("bossWarningTime")]
    [Min(0f)] public float warnTime = 3f;
    [FormerlySerializedAs("bossDuration")]
    [Min(1f)] public float bossTime = 30f;
    [FormerlySerializedAs("normalInterval")]
    [Min(1f)] public float bossGap = 90f;

    public GameFlowState State { get; private set; }
    public float RunTime { get; private set; }
    public float StateTime { get; private set; }
    public float NextBoss { get; private set; }
    public int BossRound { get; private set; }
    public int BossRounds { get; private set; }

    public event Action<GameFlowState> StateUpdated;

    private GameFlowState pausedState;
    private float pausedTime;

    public static GameFlowManager Ensure(GameObject host = null)
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
        Reset();
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
        Advance(Time.deltaTime);
    }

    public void Reset()
    {
        bossStart = Mathf.Max(1f, bossStart);
        warnTime = Mathf.Clamp(warnTime, 0f, bossStart);
        bossTime = Mathf.Max(1f, bossTime);
        bossGap = Mathf.Max(warnTime + 1f, bossGap);

        RunTime = 0f;
        StateTime = 0f;
        NextBoss = bossStart;
        BossRound = 0;
        BossRounds = 0;
        pausedState = GameFlowState.Normal;
        pausedTime = 0f;
        SetState(GameFlowState.Normal);
    }

    public void Advance(float deltaTime)
    {
        if (State == GameFlowState.Paused || State == GameFlowState.GameOver)
            return;

        float remaining = Mathf.Max(0f, deltaTime);
        int guard = 0;

        while (remaining > 0f && guard++ < 8)
        {
            float timeLeft = TimeLeft();
            float step = Mathf.Min(remaining, timeLeft);

            RunTime += step;
            StateTime += step;
            remaining -= step;

            if (timeLeft - step > 0.0001f)
                break;

            NextState();
        }
    }

    public void Pause()
    {
        if (State == GameFlowState.Paused || State == GameFlowState.GameOver)
            return;

        pausedState = State;
        pausedTime = StateTime;
        Time.timeScale = 0f;
        SetState(GameFlowState.Paused);
    }

    public void Resume()
    {
        if (State != GameFlowState.Paused)
            return;

        Time.timeScale = 1f;
        SetState(pausedState);
        StateTime = pausedTime;
    }

    public void GameOver()
    {
        if (State == GameFlowState.GameOver)
            return;

        Time.timeScale = 0f;
        SetState(GameFlowState.GameOver);
    }

    public float Countdown()
    {
        switch (State)
        {
            case GameFlowState.Normal:
            case GameFlowState.BossWarning:
                return Mathf.Max(0f, NextBoss - RunTime);
            case GameFlowState.BossSurvival:
                return Mathf.Max(0f, bossTime - StateTime);
            default:
                return 0f;
        }
    }

    public string StatusText()
    {
        int seconds = Mathf.CeilToInt(Countdown());
        switch (State)
        {
            case GameFlowState.Normal:
                return "距离第" + (BossRounds + 1) + "轮BOSS：" + seconds + "秒";
            case GameFlowState.BossWarning:
                return "BOSS预警：第" + (BossRounds + 1) + "轮 " + seconds + "秒";
            case GameFlowState.BossSurvival:
                return "第" + BossRound + "轮BOSS：" + seconds + "秒";
            case GameFlowState.Paused:
                return "游戏已暂停";
            case GameFlowState.GameOver:
                return "游戏结束";
            default:
                return string.Empty;
        }
    }

    private float TimeLeft()
    {
        switch (State)
        {
            case GameFlowState.Normal:
                return Mathf.Max(0f, NextBoss - warnTime - RunTime);
            case GameFlowState.BossWarning:
                return Mathf.Max(0f, NextBoss - RunTime);
            case GameFlowState.BossSurvival:
                return Mathf.Max(0f, bossTime - StateTime);
            default:
                return float.PositiveInfinity;
        }
    }

    private void NextState()
    {
        switch (State)
        {
            case GameFlowState.Normal:
                SetState(GameFlowState.BossWarning);
                break;
            case GameFlowState.BossWarning:
                BossRound = BossRounds + 1;
                SetState(GameFlowState.BossSurvival);
                break;
            case GameFlowState.BossSurvival:
                BossRounds = BossRound;
                NextBoss = RunTime + bossGap;
                SetState(GameFlowState.Normal);
                break;
        }
    }

    private void SetState(GameFlowState newState)
    {
        GameFlowState oldState = State;
        State = newState;
        StateTime = 0f;
        StateUpdated?.Invoke(State);

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
                if (oldState == GameFlowState.BossSurvival)
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

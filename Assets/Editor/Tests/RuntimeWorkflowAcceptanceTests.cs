using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class RuntimeWorkflowAcceptanceTests
{
    private const string BeginScenePath = "Assets/Scenes/BeginScene.unity";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string BeginSceneName = "BeginScene";
    private const string GameSceneName = "GameScene";
    private const float DefaultTimeout = 15f;
    private int previousLevelId;
    private bool previousEndless;

    [SetUp]
    public void SetUp()
    {
        previousLevelId = GameDataMgr.Instance.selectedLevelId;
        previousEndless = GameDataMgr.Instance.IsEndless;
        GameDataMgr.Instance.SetLevel(1);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            yield return new ExitPlayMode();
        }

        GameDataMgr.Instance.SetLevel(previousLevelId);
        if (previousEndless)
            GameDataMgr.Instance.SelectEndless();
    }

    [UnityTest]
    public IEnumerator DifficultySelectionLoadsGameSceneAndRuntimeSystems()
    {
        OpenSceneForPlayMode(BeginScenePath);
        yield return new EnterPlayMode();
        yield return WaitFor(
            () => SceneManager.GetActiveScene().name == BeginSceneName &&
                DifficultyPanel.Instance != null &&
                DifficultyPanel.Instance.btnStart != null &&
                DifficultyPanel.Instance.btnStart.onClick.Count > 0,
            "DifficultyPanel did not initialize in BeginScene.");

        DifficultyPanel.Instance.Showme();
        DifficultyPanel.Instance.Select(2);
        EventDelegate.Execute(DifficultyPanel.Instance.btnStart.onClick);

        yield return WaitFor(
            () => GameObject.Find("CampaignLevelSelect") != null &&
                GameObject.Find("LevelSelectStart") != null,
            "Difficulty confirmation did not open the campaign level selector.");

        UIButton startMission = GameObject.Find("LevelSelectStart").GetComponent<UIButton>();
        Assert.IsNotNull(startMission);
        EventDelegate.Execute(startMission.onClick);

        yield return WaitFor(
            () => SceneManager.GetActiveScene().name == GameSceneName &&
                GameFlowManager.Instance != null &&
                ScoreManager.Instance != null &&
                PlayerObject.Instance != null,
            "Starting from DifficultyPanel did not produce a ready GameScene.");

        Assert.IsNotNull(GameObject.Find("Main"));
        Assert.AreEqual(2, GameDataMgr.Instance.GetDifficulty().id);
        Assert.AreEqual(GameFlowState.Normal, GameFlowManager.Instance.State);
        Assert.AreEqual(1f, Time.timeScale, 0.001f);
    }

    [UnityTest]
    public IEnumerator PauseButtonFreezesFlowAndCloseButtonResumesIt()
    {
        OpenSceneForPlayMode(GameScenePath);
        yield return new EnterPlayMode();

        float deadline = Time.realtimeSinceStartup + DefaultTimeout;
        while (!IsGameSceneReady())
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Assert.Fail("GameScene runtime systems did not initialize.");
            }

            yield return null;
        }

        GameFlowManager flow = GameFlowManager.Instance;
        EventDelegate.Execute(GamePanel.Instance.btnBack.onClick);

        deadline = Time.realtimeSinceStartup + DefaultTimeout;
        while (flow.State != GameFlowState.Paused ||
            QuitPanel.Instance == null ||
            !QuitPanel.Instance.gameObject.activeSelf)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Assert.Fail("Pause button did not pause the flow and show QuitPanel.");
            }

            yield return null;
        }

        float pausedSurvivalTime = flow.RunTime;
        yield return new WaitForSecondsRealtime(0.2f);

        Assert.AreEqual(0f, Time.timeScale, 0.001f);
        Assert.AreEqual(pausedSurvivalTime, flow.RunTime, 0.01f);

        EventDelegate.Execute(QuitPanel.Instance.btnClose.onClick);
        deadline = Time.realtimeSinceStartup + DefaultTimeout;
        while (flow.State == GameFlowState.Paused || Time.timeScale <= 0.99f)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Assert.Fail("Close button did not resume the game.");
            }

            yield return null;
        }

        Assert.IsFalse(QuitPanel.Instance.gameObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator EndlessModeContinuesSurvivalAndScoringAcrossBossRounds()
    {
        OpenSceneForPlayMode(BeginScenePath);
        yield return new EnterPlayMode();
        yield return WaitFor(() => DifficultyPanel.Instance != null && DifficultyPanel.Instance.btnStart.onClick.Count > 0,
            "Difficulty panel did not initialize.");
        DifficultyPanel.Instance.Showme();
        EventDelegate.Execute(DifficultyPanel.Instance.btnStart.onClick);
        yield return WaitFor(() => GameObject.Find("EndlessModeButton") != null, "Endless option did not appear.");
        EventDelegate.Execute(GameObject.Find("EndlessModeButton").GetComponent<UIButton>().onClick);
        yield return null;
        EventDelegate.Execute(GameObject.Find("LevelSelectStart").GetComponent<UIButton>().onClick);
        yield return WaitFor(
            () => IsGameSceneReady() && GameDataMgr.Instance.IsEndless &&
                CampaignMissionController.Instance != null &&
                CampaignMissionController.Instance.State == CampaignMissionState.Inactive,
            "GameScene did not initialize the independent endless mode.");

        // Freeze frame-driven movement while advancing the real game flow explicitly.
        GameFlowManager flow = GameFlowManager.Instance;
        flow.Reset();
        Time.timeScale = 0f;
        ScoreManager.Instance.Reset();
        Assert.IsNull(LevelEncounterDirector.Instance, "Endless must use the original progressive emitters.");
        float initialSpeed = GameDataMgr.Instance.BulletSpeed();
        flow.Advance(150f);
        yield return null;

        Assert.AreEqual(5, GameDataMgr.Instance.GetLevel().id);
        Assert.IsTrue(GameDataMgr.Instance.IsEndless);
        StringAssert.Contains("无尽模式", GamePanel.Instance.labLevel.text);
        Assert.IsTrue(GamePanel.Instance.labBossWarning.gameObject.activeSelf);
        ScoreManager.Instance.UpdateScore();
        int scoreAtFifthStage = ScoreManager.Instance.TotalScore;
        Assert.Greater(GameDataMgr.Instance.BulletSpeed(), initialSpeed);
        Assert.AreEqual(8, DifficultyDirector.Instance.ThreatLevel);

        flow.Advance(60f);
        yield return null;
        ScoreManager.Instance.UpdateScore();

        Assert.AreEqual(210f, flow.RunTime, 0.02f);
        Assert.AreEqual(5, GameDataMgr.Instance.GetLevel().id);
        Assert.AreEqual(2, flow.BossRounds);
        Assert.AreEqual(270f, flow.NextBoss, 0.001f);
        Assert.Greater(ScoreManager.Instance.TotalScore, scoreAtFifthStage);
        Assert.IsFalse(ScoreManager.Instance.Finished);

        PlayerObject.Instance.Dead();
        yield return null;
        Assert.AreEqual(CampaignMissionState.Inactive, CampaignMissionController.Instance.State);
        Assert.IsNull(GameObject.Find("CampaignMissionResult"));
        Assert.IsTrue(GameOverPanel.Instance.gameObject.activeSelf);
        StringAssert.Contains("无尽模式", GameOverPanel.Instance.labTime.text);
        Assert.IsTrue(ScoreManager.Instance.Finished);

        string rankPath = Path.Combine(Application.persistentDataPath, GameDataMgr.EndlessRankFile + ".xml");
        byte[] previousFile = File.Exists(rankPath) ? File.ReadAllBytes(rankPath) : null;
        RankData previousRanks = GameDataMgr.Instance.rankData;
        int expectedScore = ScoreManager.Instance.TotalScore;
        try
        {
            GameDataMgr.Instance.rankData = new RankData();
            GameOverPanel.Instance.inputName.value = " Nova ";
            EventDelegate.Execute(GameOverPanel.Instance.btnSure.onClick);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == BeginSceneName,
                "Endless settlement did not return to the menu.");
            RankData saved = XmlDataMgr.Instance.LoadPaths(typeof(RankData), rankPath, string.Empty) as RankData;
            Assert.IsNotNull(saved);
            Assert.AreEqual(1, saved.ranks.Count);
            Assert.AreEqual("Nova", saved.ranks[0].name);
            Assert.AreEqual(expectedScore, saved.ranks[0].score);
            Assert.AreEqual(210, saved.ranks[0].runTime);
            Assert.AreEqual(2, saved.ranks[0].bosses);
        }
        finally
        {
            GameDataMgr.Instance.rankData = previousRanks;
            if (previousFile != null) File.WriteAllBytes(rankPath, previousFile);
            else if (File.Exists(rankPath)) File.Delete(rankPath);
        }
    }

    [UnityTest]
    public IEnumerator PlayerDeathFinalizesScoreAndShowsSettlement()
    {
        GameDataMgr.Instance.SetLevel(1);
        OpenSceneForPlayMode(GameScenePath);
        yield return new EnterPlayMode();
        yield return WaitFor(
            IsGameSceneReady,
            "GameScene runtime systems did not initialize.");

        GameFlowManager.Instance.Advance(4f);
        PlayerObject.Instance.Dead();
        yield return null;

        Assert.AreEqual(GameFlowState.GameOver, GameFlowManager.Instance.State);
        Assert.AreEqual(0f, Time.timeScale, 0.001f);
        Assert.IsTrue(ScoreManager.Instance.Finished);
        Assert.Greater(ScoreManager.Instance.TotalScore, 0);
        Assert.AreEqual(CampaignMissionState.Failed, CampaignMissionController.Instance.State);
        Assert.IsNotNull(GameObject.Find("CampaignMissionResult"));
        Assert.IsNotNull(GameObject.Find("MissionResultAdvice"));
        Assert.IsFalse(string.IsNullOrEmpty(GameObject.Find("MissionResultAdvice").GetComponent<UILabel>().text));
        Assert.IsNotNull(GameObject.Find("MissionRetryButton"));
        Assert.IsNotNull(GameObject.Find("MissionMenuButton"));
    }

    [UnityTest]
    public IEnumerator FinalizedScoreRoundTripsThroughTemporaryRankXml()
    {
        OpenSceneForPlayMode(GameScenePath);
        yield return new EnterPlayMode();
        yield return WaitFor(
            IsGameSceneReady,
            "GameScene runtime systems did not initialize.");

        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "U2RuntimeAcceptance-" + Guid.NewGuid().ToString("N"));
        string rankPath = Path.Combine(temporaryDirectory, "RankData.xml");

        try
        {
            GameFlowManager.Instance.Advance(12f);
            ScoreManager.Instance.RegisterClear();
            PlayerObject.Instance.Dead();
            yield return null;

            RankData source = new RankData();
            source.ranks.Add(new RankInfo
            {
                name = "RuntimeAcceptancePilot",
                score = ScoreManager.Instance.TotalScore,
                runTime = Mathf.FloorToInt(ScoreManager.Instance.RunTime),
                difficulty = GameDataMgr.Instance.DifficultyName(),
                bosses = ScoreManager.Instance.BossRounds,
                clears = ScoreManager.Instance.ClearCount,
                date = "2026-08-05 23:30:00",
            });
            source.Sort();

            XmlDataMgr.Instance.SavePath(source, rankPath);
            RankData reloaded = XmlDataMgr.Instance.LoadPaths(
                typeof(RankData),
                rankPath,
                string.Empty) as RankData;

            Assert.IsNotNull(reloaded);
            reloaded.Sort();
            Assert.AreEqual(1, reloaded.ranks.Count);

            RankInfo saved = reloaded.ranks[0];
            Assert.AreEqual("RuntimeAcceptancePilot", saved.name);
            Assert.AreEqual(ScoreManager.Instance.TotalScore, saved.score);
            Assert.AreEqual(12, saved.runTime);
            Assert.AreEqual(1, saved.clears);
            Assert.AreEqual("2026-08-05 23:30:00", saved.date);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }
    }

    [UnityTest]
    public IEnumerator RestartButtonReloadsFreshGameState()
    {
        OpenSceneForPlayMode(GameScenePath);
        yield return new EnterPlayMode();

        float deadline = Time.realtimeSinceStartup + DefaultTimeout;
        while (!IsGameSceneReady())
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Assert.Fail("GameScene runtime systems did not initialize.");
            }

            yield return null;
        }

        GameFlowManager.Instance.Advance(5f);
        EventDelegate.Execute(GamePanel.Instance.btnBack.onClick);

        deadline = Time.realtimeSinceStartup + DefaultTimeout;
        while (GameFlowManager.Instance.State != GameFlowState.Paused ||
            QuitPanel.Instance == null ||
            !QuitPanel.Instance.gameObject.activeSelf)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Assert.Fail("Pause button did not show QuitPanel before restart.");
            }

            yield return null;
        }

        int previousPlayerId = PlayerObject.Instance.GetInstanceID();

        EventDelegate.Execute(QuitPanel.Instance.btnRestart.onClick);

        deadline = Time.realtimeSinceStartup + DefaultTimeout;
        while (SceneManager.GetActiveScene().name != GameSceneName ||
            PlayerObject.Instance == null ||
            PlayerObject.Instance.GetInstanceID() == previousPlayerId ||
            GameFlowManager.Instance == null ||
            ScoreManager.Instance == null)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Assert.Fail("Restart button did not reload a fresh GameScene.");
            }

            yield return null;
        }

        Assert.AreEqual(GameFlowState.Normal, GameFlowManager.Instance.State);
        Assert.Less(GameFlowManager.Instance.RunTime, 1.5f);
        Assert.IsFalse(ScoreManager.Instance.Finished);
        Assert.AreEqual(1f, Time.timeScale, 0.001f);
    }

    private static bool IsGameSceneReady()
    {
        return SceneManager.GetActiveScene().name == GameSceneName &&
            GameFlowManager.Instance != null &&
            ScoreManager.Instance != null &&
            PlayerObject.Instance != null &&
            GamePanel.Instance != null &&
            GamePanel.Instance.btnBack != null &&
            GamePanel.Instance.btnBack.onClick.Count > 0 &&
            QuitPanel.Instance != null &&
            QuitPanel.Instance.btnRestart != null &&
            QuitPanel.Instance.btnRestart.onClick.Count > 0 &&
            GameOverPanel.Instance != null &&
            GameOverPanel.Instance.btnSure != null &&
            GameOverPanel.Instance.btnSure.onClick.Count > 0;
    }

    private static void OpenSceneForPlayMode(string scenePath)
    {
        Assert.IsFalse(EditorApplication.isPlaying, "Test must start in Edit Mode.");
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
    }

    private static IEnumerator WaitFor(
        Func<bool> condition,
        string failureMessage,
        float timeoutSeconds = DefaultTimeout)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!condition())
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Assert.Fail(failureMessage);
            }

            yield return null;
        }
    }
}

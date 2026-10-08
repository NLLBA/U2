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

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            yield return new ExitPlayMode();
        }
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
    public IEnumerator PlayerDeathFinalizesScoreAndShowsSettlement()
    {
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
        Assert.IsNotNull(GameOverPanel.Instance);
        Assert.IsTrue(GameOverPanel.Instance.gameObject.activeSelf);
        Assert.IsFalse(string.IsNullOrEmpty(GameOverPanel.Instance.labTime.text));
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Stage9ValidationTests
{
    private GameObject host;
    private DifficultyDirector director;
    private BossController boss;
    private int originalHeroIndex;
    private int originalDifficultyIndex;
    private string temporaryDirectory;

    [SetUp]
    public void SetUp()
    {
        Time.timeScale = 1f;
        originalHeroIndex = GameDataMgr.Instance.heroIndex;
        originalDifficultyIndex = GameDataMgr.Instance.difficultyIndex;
        temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "U2Stage9-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        host = new GameObject("Stage9ValidationTests");
        director = host.AddComponent<DifficultyDirector>();
        boss = host.AddComponent<BossController>();
        boss.bossPath = string.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        GameDataMgr.Instance.heroIndex = originalHeroIndex;
        GameDataMgr.Instance.SetDifficulty(originalDifficultyIndex);
        if (host != null)
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }

    [Test]
    public void BalanceReportCoversThreeDifficultiesAndFiveAircraft()
    {
        Stage9BalanceReport report = BuildReport(60);

        Assert.AreEqual(3, GameDataMgr.Instance.difficultyData.difficulties.Count);
        Assert.AreEqual(5, GameDataMgr.Instance.roleData.roles.Count);
        Assert.AreEqual(12, report.PressureSnapshots.Count);
        Assert.AreEqual(15, report.SimulationResults.Count);

        for (int difficultyId = 1; difficultyId <= 3; difficultyId++)
        {
            Assert.AreEqual(
                5,
                report.SimulationResults.Count(value => value.DifficultyId == difficultyId));
        }

        for (int hero = 1; hero <= 5; hero++)
        {
            Assert.AreEqual(
                3,
                report.SimulationResults.Count(value => value.HeroIndex == hero));
        }

        Assert.IsTrue(report.SimulationResults.All(IsFiniteAndBounded));
    }

    [Test]
    public void PressureIncreasesByDifficultyAndBossRound()
    {
        Stage9BalanceReport report = BuildReport(10);
        string[] labels = { "60s", "120s / Boss 1 start", "Boss 1 peak", "Boss 2 peak" };

        for (int i = 0; i < labels.Length; i++)
        {
            Stage9PressureSnapshot easy = FindSnapshot(report, 1, labels[i]);
            Stage9PressureSnapshot normal = FindSnapshot(report, 2, labels[i]);
            Stage9PressureSnapshot hard = FindSnapshot(report, 3, labels[i]);
            Assert.Less(easy.CombinedPressure, normal.CombinedPressure, labels[i]);
            Assert.Less(normal.CombinedPressure, hard.CombinedPressure, labels[i]);
        }

        Stage9PressureSnapshot boss1 = FindSnapshot(report, 2, "Boss 1 peak");
        Stage9PressureSnapshot boss2 = FindSnapshot(report, 2, "Boss 2 peak");
        Assert.AreEqual(GameFlowState.BossSurvival, boss1.State);
        Assert.AreEqual(GameFlowState.BossSurvival, boss2.State);
        Assert.Greater(boss2.BossBulletsPerSecond, boss1.BossBulletsPerSecond);
        Assert.Greater(boss2.BossPeakBulletsPerSecond, boss1.BossPeakBulletsPerSecond);
        Assert.Greater(boss2.BossBulletSpeedMultiplier, boss1.BossBulletSpeedMultiplier);
    }

    [Test]
    public void FixedSeedSimulationIsRepeatableAndDifficultyReducesSurvival()
    {
        Stage9BalanceReport first = BuildReport(400);
        Stage9BalanceReport second = BuildReport(400);

        for (int i = 0; i < first.SimulationResults.Count; i++)
        {
            Stage9SimulationResult left = first.SimulationResults[i];
            Stage9SimulationResult right = second.SimulationResults[i];
            Assert.AreEqual(left.AverageSurvivalTime, right.AverageSurvivalTime, 0.0001f);
            Assert.AreEqual(left.AverageScore, right.AverageScore, 0.0001f);
            Assert.AreEqual(left.FirstBossDeathRate, right.FirstBossDeathRate, 0.0001f);
            Assert.AreEqual(left.SecondBossDeathRate, right.SecondBossDeathRate, 0.0001f);
        }

        float easyAverage = AverageSurvival(first, 1);
        float normalAverage = AverageSurvival(first, 2);
        float hardAverage = AverageSurvival(first, 3);
        Assert.Greater(easyAverage, normalAverage);
        Assert.Greater(normalAverage, hardAverage);
    }

    [Test]
    public void ScenesExposeTheCompleteSelectionAndSettlementWorkflow()
    {
        Scene beginScene = EditorSceneManager.OpenScene(
            "Assets/Scenes/BeginScene.unity",
            OpenSceneMode.Single);
        ChoosePanel choose = FindSceneComponent<ChoosePanel>(beginScene, "ChoosePanel");
        DifficultyPanel difficulty = FindSceneComponent<DifficultyPanel>(beginScene, "DifficultyPanel");

        Assert.IsNotNull(choose);
        Assert.IsNotNull(choose.btnStart);
        Assert.IsNotNull(difficulty);
        Assert.IsNotNull(difficulty.btnEasy);
        Assert.IsNotNull(difficulty.btnNormal);
        Assert.IsNotNull(difficulty.btnHard);
        Assert.IsNotNull(difficulty.btnStart);
        Assert.IsNotNull(difficulty.btnClose);
        Assert.IsNotNull(difficulty.labSelected);
        Assert.IsNotNull(difficulty.labDetail);

        Scene gameScene = EditorSceneManager.OpenScene(
            "Assets/Scenes/GameScene.unity",
            OpenSceneMode.Single);
        Assert.IsNotNull(FindSceneComponent<Main>(gameScene, "Main"));
        GameOverPanel gameOver = FindSceneComponent<GameOverPanel>(gameScene, "GameOverPanel");
        Assert.IsNotNull(gameOver);
        Assert.IsNotNull(gameOver.btnSure);
        Assert.IsNotNull(gameOver.btnRestart);

        for (int i = 0; i < GameDataMgr.Instance.roleData.roles.Count; i++)
        {
            Assert.IsNotNull(
                Resources.Load<GameObject>(GameDataMgr.Instance.roleData.roles[i].asset),
                "Missing aircraft resource " + (i + 1));
        }
    }

    [Test]
    public void SelectionToSecondBossSettlementPersistsAfterXmlReload()
    {
        GameDataMgr data = GameDataMgr.Instance;
        data.heroIndex = 4;
        data.SetDifficulty(3);
        Assert.AreEqual("Airplane/Airplane5", data.GetHero().asset);
        Assert.AreEqual(3, data.GetDifficulty().id);

        GameFlowManager flow = host.AddComponent<GameFlowManager>();
        ScoreManager score = host.AddComponent<ScoreManager>();
        flow.Reset();

        flow.Advance(150f);
        Assert.AreEqual(1, flow.BossRounds);
        Assert.AreEqual(GameFlowState.Normal, flow.State);
        Assert.Greater(director.ThreatLevel, 1);

        flow.Advance(100f);
        Assert.AreEqual(GameFlowState.BossSurvival, flow.State);
        Assert.AreEqual(2, flow.BossRound);
        for (int i = 0; i < 12; i++)
        {
            Assert.IsTrue(score.RegisterClear());
        }

        flow.GameOver();
        Assert.IsTrue(score.Finished);
        Assert.Greater(score.TotalScore, 0);
        Assert.IsFalse(score.RegisterClear());

        RankData rankData = new RankData();
        rankData.ranks.Add(new RankInfo
        {
            name = "Stage9Pilot",
            score = score.TotalScore,
            runTime = Mathf.FloorToInt(score.RunTime),
            difficulty = data.DifficultyName(),
            bosses = score.BossRounds,
            clears = score.ClearCount,
            date = "2026-08-02 22:30:00",
        });
        rankData.Sort();

        string rankPath = Path.Combine(temporaryDirectory, "RankData.xml");
        XmlDataMgr.Instance.SavePath(rankData, rankPath);
        RankData reloaded = XmlDataMgr.Instance.LoadPaths(
            typeof(RankData),
            rankPath,
            string.Empty) as RankData;

        Assert.IsNotNull(reloaded);
        reloaded.Sort();
        Assert.AreEqual(1, reloaded.ranks.Count);
        RankInfo saved = reloaded.ranks[0];
        Assert.AreEqual("Stage9Pilot", saved.name);
        Assert.AreEqual(score.TotalScore, saved.score);
        Assert.AreEqual(250, saved.runTime);
        Assert.AreEqual("困难", saved.difficulty);
        Assert.AreEqual(1, saved.bosses);
        Assert.AreEqual(12, saved.clears);
    }

    private Stage9BalanceReport BuildReport(int trials)
    {
        return Stage9BalanceModel.BuildReport(
            GameDataMgr.Instance,
            director,
            boss,
            trials,
            Stage9BalanceModel.DefaultMaximumSeconds);
    }

    private static bool IsFiniteAndBounded(Stage9SimulationResult result)
    {
        return !float.IsNaN(result.AverageSurvivalTime) &&
            !float.IsInfinity(result.AverageSurvivalTime) &&
            result.AverageSurvivalTime > 0f &&
            result.AverageSurvivalTime <= Stage9BalanceModel.DefaultMaximumSeconds &&
            result.AverageScore >= 0f &&
            IsRate(result.FirstBossReachRate) &&
            IsRate(result.FirstBossDeathRate) &&
            IsRate(result.SecondBossReachRate) &&
            IsRate(result.SecondBossDeathRate);
    }

    private static bool IsRate(float value)
    {
        return value >= 0f && value <= 1f;
    }

    private static float AverageSurvival(Stage9BalanceReport report, int difficultyId)
    {
        return report.SimulationResults
            .Where(value => value.DifficultyId == difficultyId)
            .Average(value => value.AverageSurvivalTime);
    }

    private static Stage9PressureSnapshot FindSnapshot(
        Stage9BalanceReport report,
        int difficultyId,
        string label)
    {
        return report.PressureSnapshots.Single(
            value => value.DifficultyId == difficultyId && value.Label == label);
    }

    private static T FindSceneComponent<T>(Scene scene, string objectName) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j].name == objectName)
                {
                    return transforms[j].GetComponent<T>();
                }
            }
        }

        return null;
    }
}

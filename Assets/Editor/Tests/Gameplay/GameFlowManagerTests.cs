using NUnit.Framework;
using UnityEngine;

public class GameFlowManagerTests
{
    private GameObject host;
    private GameFlowManager manager;

    [SetUp]
    public void SetUp()
    {
        Time.timeScale = 1f;
        host = new GameObject("GameFlowManagerTests");
        manager = host.AddComponent<GameFlowManager>();
        manager.bossStart = 10f;
        manager.warnTime = 2f;
        manager.bossTime = 3f;
        manager.bossGap = 8f;
        manager.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        Object.DestroyImmediate(host);
    }

    [Test]
    public void FlowCyclesFromNormalThroughBossAndBack()
    {
        manager.Advance(8f);
        Assert.AreEqual(GameFlowState.BossWarning, manager.State);

        manager.Advance(2f);
        Assert.AreEqual(GameFlowState.BossSurvival, manager.State);
        Assert.AreEqual(1, manager.BossRound);

        manager.Advance(3f);
        Assert.AreEqual(GameFlowState.Normal, manager.State);
        Assert.AreEqual(1, manager.BossRounds);
        Assert.AreEqual(18f, manager.NextBoss, 0.001f);

        manager.Advance(3f);
        Assert.AreEqual(GameFlowState.BossWarning, manager.State);
    }

    [Test]
    public void BossStartsEveryNinetySecondsIncludingAfterTheFifthLevel()
    {
        manager.bossStart = 90f;
        manager.warnTime = 3f;
        manager.bossTime = 30f;
        manager.bossGap = 90f;
        manager.Reset();

        manager.Advance(87f);
        Assert.AreEqual(GameFlowState.BossWarning, manager.State);
        manager.Advance(3f);
        Assert.AreEqual(GameFlowState.BossSurvival, manager.State);
        Assert.AreEqual(1, manager.BossRound);

        manager.Advance(30f);
        Assert.AreEqual(GameFlowState.Normal, manager.State);
        Assert.AreEqual(180f, manager.NextBoss, 0.001f);
        manager.Advance(57f);
        Assert.AreEqual(GameFlowState.BossWarning, manager.State);
        manager.Advance(3f);
        Assert.AreEqual(GameFlowState.BossSurvival, manager.State);
        Assert.AreEqual(2, manager.BossRound);

        manager.Advance(30f);
        manager.Advance(60f);
        Assert.AreEqual(GameFlowState.BossSurvival, manager.State);
        Assert.AreEqual(3, manager.BossRound);
        Assert.AreEqual(270f, manager.RunTime, 0.001f);
    }

    [TestCase(0f, 1)]
    [TestCase(29.9f, 1)]
    [TestCase(30f, 2)]
    [TestCase(60f, 3)]
    [TestCase(100f, 4)]
    [TestCase(149.9f, 4)]
    [TestCase(150f, 5)]
    [TestCase(210f, 5)]
    [TestCase(900f, 5)]
    public void StageProgressionContinuesAtTheFifthStage(float runTime, int expectedLevel)
    {
        Assert.AreEqual(expectedLevel, GameDataMgr.Instance.GetLevel(runTime).id);
    }

    [Test]
    public void FifthStageHasNoFurtherStageToUnlock()
    {
        Assert.AreEqual(5, GameDataMgr.Instance.GetNextLevel(149f).id);
        Assert.IsNull(GameDataMgr.Instance.GetNextLevel(150f));
        Assert.IsNull(GameDataMgr.Instance.GetNextLevel(900f));
    }

    [Test]
    public void PausePreservesBossCountdown()
    {
        manager.Advance(11f);
        Assert.AreEqual(GameFlowState.BossSurvival, manager.State);
        Assert.AreEqual(1f, manager.StateTime, 0.001f);

        manager.Pause();
        manager.Advance(20f);
        Assert.AreEqual(GameFlowState.Paused, manager.State);
        Assert.AreEqual(11f, manager.RunTime, 0.001f);

        manager.Resume();
        Assert.AreEqual(GameFlowState.BossSurvival, manager.State);
        Assert.AreEqual(1f, manager.StateTime, 0.001f);

        manager.Advance(2f);
        Assert.AreEqual(GameFlowState.Normal, manager.State);
        Assert.AreEqual(1, manager.BossRounds);
    }

    [Test]
    public void GameOverStopsFlow()
    {
        manager.Advance(4f);
        manager.GameOver();
        manager.Advance(20f);

        Assert.AreEqual(GameFlowState.GameOver, manager.State);
        Assert.AreEqual(4f, manager.RunTime, 0.001f);
        Assert.AreEqual(0f, Time.timeScale, 0.001f);
    }

    [Test]
    public void MissionCompleteStopsFlowAndUsesItsOwnState()
    {
        manager.Advance(4f);
        manager.CompleteMission();
        manager.Advance(20f);

        Assert.AreEqual(GameFlowState.MissionComplete, manager.State);
        Assert.AreEqual(4f, manager.RunTime, 0.001f);
        Assert.AreEqual(0f, Time.timeScale, 0.001f);
        Assert.AreEqual("任务完成", manager.StatusText());
    }

    [Test]
    public void CampaignDefinesFiveFiniteMissionsAndAllAreOpen()
    {
        string[] expectedTypes = { "survive", "clear", "dodge", "beacon", "reflect" };
        for (int levelId = 1; levelId <= GameDataMgr.MaxLevelId; levelId++)
        {
            LevelInfo level = GameDataMgr.Instance.GetLevelById(levelId);
            Assert.IsNotNull(level);
            Assert.AreEqual(levelId, level.id);
            Assert.AreEqual(expectedTypes[levelId - 1], level.objectiveType);
            Assert.IsFalse(string.IsNullOrEmpty(level.objectiveText));
            Assert.Greater(level.objectiveTarget, 0);
            Assert.IsTrue(CampaignProgress.IsUnlocked(levelId));
        }

        LevelInfo relayMission = GameDataMgr.Instance.GetLevelById(4);
        Assert.AreEqual(3, relayMission.objectiveTarget);
        Assert.AreEqual(9999f, relayMission.bossStart, 0.001f);
        LevelInfo finalMission = GameDataMgr.Instance.GetLevelById(5);
        Assert.AreEqual(6, finalMission.objectiveTarget);
        Assert.AreEqual(45f, finalMission.bossStart, 0.001f);
        Assert.AreEqual(30f, finalMission.bossDuration, 0.001f);
        Assert.AreEqual(35f, finalMission.bossGap, 0.001f);
        Assert.AreEqual(1.7f, finalMission.bulletRate, 0.001f);
        Assert.AreEqual(GameDataMgr.MaxLevelId, CampaignProgress.MaxUnlocked);
        Assert.IsFalse(CampaignProgress.IsUnlocked(0));
        Assert.IsFalse(CampaignProgress.IsUnlocked(6));
    }

    [Test]
    public void EndlessRestoresOriginalBossScheduleAfterCampaignConfiguration()
    {
        manager.Configure(GameDataMgr.Instance.GetLevelById(5));
        manager.ConfigureEndless();
        manager.Advance(87f);
        Assert.AreEqual(GameFlowState.BossWarning, manager.State);
        manager.Advance(3f);
        Assert.AreEqual(GameFlowState.BossSurvival, manager.State);
        manager.Advance(30f);
        Assert.AreEqual(1, manager.BossRounds);
        Assert.AreEqual(180f, manager.NextBoss);
        manager.Advance(60f);
        Assert.AreEqual(GameFlowState.BossSurvival, manager.State);
        Assert.AreEqual(2, manager.BossRound);
    }

    [Test]
    public void FifthMissionRunsTwoBossStagesAndFinishesItsScheduleAtOneHundredTenSeconds()
    {
        manager.Configure(GameDataMgr.Instance.GetLevelById(5));

        manager.Advance(75f);
        Assert.AreEqual(GameFlowState.Normal, manager.State);
        Assert.AreEqual(1, manager.BossRounds);
        Assert.AreEqual(80f, manager.NextBoss, 0.001f);

        manager.Advance(5f);
        Assert.AreEqual(GameFlowState.BossSurvival, manager.State);
        Assert.AreEqual(2, manager.BossRound);

        manager.Advance(30f);
        Assert.AreEqual(GameFlowState.Normal, manager.State);
        Assert.AreEqual(2, manager.BossRounds);
        Assert.AreEqual(110f, manager.RunTime, 0.001f);
    }

    [Test]
    public void CompletedChallengeGoalsAreMergedAndPersistedPerLevel()
    {
        string[] keys =
        {
            "U2.Campaign.Level1.Stars",
            "U2.Campaign.Level1.BestScore",
            "U2.Campaign.Level1.BestTime",
            "U2.Campaign.Level1.Challenges",
        };
        bool[] existed = new bool[keys.Length];
        int[] values = new int[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            existed[i] = PlayerPrefs.HasKey(keys[i]);
            values[i] = PlayerPrefs.GetInt(keys[i]);
        }

        try
        {
            foreach (string key in keys)
                PlayerPrefs.DeleteKey(key);
            CampaignProgress.CompleteLevel(1, 1, 100, 10, CampaignMissionController.NoHitChallenge);
            CampaignProgress.CompleteLevel(1, 2, 200, 20, CampaignMissionController.NoMistakeChallenge);

            Assert.AreEqual(3, CampaignProgress.ChallengeMask(1));
            Assert.AreEqual(2, CampaignProgress.ChallengeCount(1));
            Assert.AreEqual(2, CampaignProgress.Stars(1));
        }
        finally
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (existed[i]) PlayerPrefs.SetInt(keys[i], values[i]);
                else PlayerPrefs.DeleteKey(keys[i]);
            }
            PlayerPrefs.Save();
        }
    }
}

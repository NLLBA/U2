using NUnit.Framework;
using UnityEngine;

public class DifficultyDirectorTests
{
    private GameObject host;
    private GameFlowManager flow;
    private DifficultyDirector director;
    private int originalDifficulty;

    [SetUp]
    public void SetUp()
    {
        Time.timeScale = 1f;
        originalDifficulty = GameDataMgr.Instance.difficultyIndex;
        GameDataMgr.Instance.SetDifficulty(2);
        host = new GameObject("DifficultyDirectorTests");
        flow = host.AddComponent<GameFlowManager>();
        director = host.AddComponent<DifficultyDirector>();

        flow.bossStart = 1000f;
        flow.warnTime = 2f;
        flow.bossTime = 3f;
        flow.bossGap = 8f;
        flow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        GameDataMgr.Instance.SetDifficulty(originalDifficulty);
        Object.DestroyImmediate(host);
    }

    [Test]
    public void ThreatLevelIncreasesEveryThirtySeconds()
    {
        Assert.AreEqual(1, director.ThreatLevel);

        flow.Advance(29.9f);
        Assert.AreEqual(1, director.ThreatLevel);

        flow.Advance(0.1f);
        Assert.AreEqual(2, director.ThreatLevel);

        flow.Advance(60f);
        Assert.AreEqual(4, director.ThreatLevel);
    }

    [Test]
    public void CompletedBossAddsThreatAndNextBossGetsStronger()
    {
        flow.bossStart = 10f;
        flow.Reset();

        flow.Advance(13f);
        Assert.AreEqual(1, flow.BossRounds);
        Assert.AreEqual(3, director.ThreatLevel);

        flow.Advance(5f);
        Assert.AreEqual(GameFlowState.BossSurvival, flow.State);
        Assert.AreEqual(2, director.BossRound);
        Assert.AreEqual(1.12f, director.BossBulletRate, 0.001f);
        Assert.AreEqual(0.94f, director.BossFireRate, 0.001f);
    }

    [Test]
    public void DynamicMultipliersRespectCaps()
    {
        director.threatStep = 1f;
        flow.Advance(100f);

        Assert.AreEqual(2.2f, director.BulletRate, 0.001f);
        Assert.AreEqual(2.2f, director.ScaleBullet(1.6f), 0.001f);
        Assert.AreEqual(0.45f, director.FireRate, 0.001f);
    }

    [Test]
    public void SelectedDifficultyChangesDensityWarningsTrackingAndMissionTargets()
    {
        LevelInfo first = GameDataMgr.Instance.GetLevelById(1);
        LevelInfo second = GameDataMgr.Instance.GetLevelById(2);
        LevelInfo third = GameDataMgr.Instance.GetLevelById(3);
        LevelInfo fourth = GameDataMgr.Instance.GetLevelById(4);
        LevelInfo fifth = GameDataMgr.Instance.GetLevelById(5);

        GameDataMgr.Instance.SetDifficulty(1);
        Assert.AreEqual(100, GameDataMgr.Instance.EncounterDensityPercent());
        Assert.AreEqual(1.15f, GameDataMgr.Instance.EncounterWarningTime(1f), 0.001f);
        Assert.AreEqual(1f, GameDataMgr.Instance.EncounterTrackingTime(), 0.001f);
        Assert.AreEqual(45, GameDataMgr.Instance.MissionTarget(first));
        Assert.AreEqual(8, GameDataMgr.Instance.MissionTarget(second));
        Assert.AreEqual(5, GameDataMgr.Instance.MissionTarget(third));
        Assert.AreEqual(3, GameDataMgr.Instance.MissionTarget(fourth));
        Assert.AreEqual(6, GameDataMgr.Instance.MissionTarget(fifth));

        GameDataMgr.Instance.SetDifficulty(2);
        Assert.AreEqual(130, GameDataMgr.Instance.EncounterDensityPercent());
        Assert.AreEqual(55, GameDataMgr.Instance.MissionTarget(first));
        Assert.AreEqual(16, GameDataMgr.Instance.MissionTarget(second));
        Assert.AreEqual(8, GameDataMgr.Instance.MissionTarget(third));
        Assert.AreEqual(3, GameDataMgr.Instance.MissionTarget(fourth));
        Assert.AreEqual(9, GameDataMgr.Instance.MissionTarget(fifth));

        GameDataMgr.Instance.SetDifficulty(3);
        Assert.AreEqual(165, GameDataMgr.Instance.EncounterDensityPercent());
        Assert.AreEqual(0.75f, GameDataMgr.Instance.EncounterWarningTime(1f), 0.001f);
        Assert.AreEqual(1.45f, GameDataMgr.Instance.EncounterTrackingTime(), 0.001f);
        Assert.AreEqual(65, GameDataMgr.Instance.MissionTarget(first));
        Assert.AreEqual(26, GameDataMgr.Instance.MissionTarget(second));
        Assert.AreEqual(12, GameDataMgr.Instance.MissionTarget(third));
        Assert.AreEqual(3, GameDataMgr.Instance.MissionTarget(fourth));
        Assert.AreEqual(12, GameDataMgr.Instance.MissionTarget(fifth));
        Assert.AreEqual(2, GameDataMgr.Instance.PlayerHp(new RoleInfo { hp = 2 }));
    }
}

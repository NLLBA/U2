using NUnit.Framework;
using UnityEngine;

public class DifficultyDirectorTests
{
    private GameObject host;
    private GameFlowManager flow;
    private DifficultyDirector director;

    [SetUp]
    public void SetUp()
    {
        Time.timeScale = 1f;
        host = new GameObject("DifficultyDirectorTests");
        flow = host.AddComponent<GameFlowManager>();
        director = host.AddComponent<DifficultyDirector>();

        flow.firstBossTime = 1000f;
        flow.bossWarningTime = 2f;
        flow.bossDuration = 3f;
        flow.normalInterval = 5f;
        flow.ResetFlow();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        Object.DestroyImmediate(host);
    }

    [Test]
    public void ThreatLevelIncreasesEveryThirtySeconds()
    {
        Assert.AreEqual(1, director.ThreatLevel);

        flow.AdvanceFlow(29.9f);
        Assert.AreEqual(1, director.ThreatLevel);

        flow.AdvanceFlow(0.1f);
        Assert.AreEqual(2, director.ThreatLevel);

        flow.AdvanceFlow(60f);
        Assert.AreEqual(4, director.ThreatLevel);
    }

    [Test]
    public void CompletedBossAddsThreatAndNextBossGetsStronger()
    {
        flow.firstBossTime = 10f;
        flow.ResetFlow();

        flow.AdvanceFlow(13f);
        Assert.AreEqual(1, flow.CompletedBossRounds);
        Assert.AreEqual(3, director.ThreatLevel);

        flow.AdvanceFlow(5f);
        Assert.AreEqual(GameFlowState.BossSurvival, flow.State);
        Assert.AreEqual(2, director.ScalingBossRound);
        Assert.AreEqual(1.12f, director.BossBulletSpeedMultiplier, 0.001f);
        Assert.AreEqual(0.94f, director.BossFireIntervalMultiplier, 0.001f);
    }

    [Test]
    public void DynamicMultipliersRespectCaps()
    {
        director.threatStepSeconds = 1f;
        flow.AdvanceFlow(100f);

        Assert.AreEqual(2.2f, director.NormalBulletSpeedMultiplier, 0.001f);
        Assert.AreEqual(2.2f, director.ApplyNormalBulletSpeed(1.6f), 0.001f);
        Assert.AreEqual(0.45f, director.NormalFireIntervalMultiplier, 0.001f);
    }
}

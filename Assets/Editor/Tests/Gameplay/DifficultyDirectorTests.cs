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

        flow.bossStart = 1000f;
        flow.warnTime = 2f;
        flow.bossTime = 3f;
        flow.bossGap = 5f;
        flow.Reset();
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
}

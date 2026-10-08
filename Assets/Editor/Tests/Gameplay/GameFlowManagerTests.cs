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
        manager.bossGap = 5f;
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
}

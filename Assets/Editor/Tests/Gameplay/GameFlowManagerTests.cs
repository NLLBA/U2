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
        manager.firstBossTime = 10f;
        manager.bossWarningTime = 2f;
        manager.bossDuration = 3f;
        manager.normalInterval = 5f;
        manager.ResetFlow();
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
        manager.AdvanceFlow(8f);
        Assert.AreEqual(GameFlowState.BossWarning, manager.State);

        manager.AdvanceFlow(2f);
        Assert.AreEqual(GameFlowState.BossSurvival, manager.State);
        Assert.AreEqual(1, manager.CurrentBossRound);

        manager.AdvanceFlow(3f);
        Assert.AreEqual(GameFlowState.Normal, manager.State);
        Assert.AreEqual(1, manager.CompletedBossRounds);
        Assert.AreEqual(18f, manager.NextBossTime, 0.001f);

        manager.AdvanceFlow(3f);
        Assert.AreEqual(GameFlowState.BossWarning, manager.State);
    }

    [Test]
    public void PausePreservesBossCountdown()
    {
        manager.AdvanceFlow(11f);
        Assert.AreEqual(GameFlowState.BossSurvival, manager.State);
        Assert.AreEqual(1f, manager.StateElapsedTime, 0.001f);

        manager.PauseGame();
        manager.AdvanceFlow(20f);
        Assert.AreEqual(GameFlowState.Paused, manager.State);
        Assert.AreEqual(11f, manager.SurvivalTime, 0.001f);

        manager.ResumeGame();
        Assert.AreEqual(GameFlowState.BossSurvival, manager.State);
        Assert.AreEqual(1f, manager.StateElapsedTime, 0.001f);

        manager.AdvanceFlow(2f);
        Assert.AreEqual(GameFlowState.Normal, manager.State);
        Assert.AreEqual(1, manager.CompletedBossRounds);
    }

    [Test]
    public void GameOverStopsFlow()
    {
        manager.AdvanceFlow(4f);
        manager.EnterGameOver();
        manager.AdvanceFlow(20f);

        Assert.AreEqual(GameFlowState.GameOver, manager.State);
        Assert.AreEqual(4f, manager.SurvivalTime, 0.001f);
        Assert.AreEqual(0f, Time.timeScale, 0.001f);
    }
}

using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class ScoreManagerTests
{
    private GameObject host;
    private GameFlowManager flow;
    private ScoreManager score;
    private int previousDifficultyId;

    [SetUp]
    public void SetUp()
    {
        Time.timeScale = 1f;
        previousDifficultyId = GameDataMgr.Instance.nowDifficultyIndex;
        GameDataMgr.Instance.SetDifficulty(2);

        host = new GameObject("ScoreManagerTests");
        flow = host.AddComponent<GameFlowManager>();
        DifficultyDirector director = host.AddComponent<DifficultyDirector>();
        score = host.AddComponent<ScoreManager>();
        InvokeAwake(flow);
        InvokeAwake(director);
        InvokeAwake(score);

        flow.firstBossTime = 10f;
        flow.bossWarningTime = 2f;
        flow.bossDuration = 4f;
        flow.normalInterval = 5f;
        flow.ResetFlow();
        score.ResetScore();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        GameDataMgr.Instance.SetDifficulty(previousDifficultyId);
        BulletObject.ClearAllBullets();
        if (host != null)
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void SurvivalAndManualClearsUseOneDifficultyCoefficient()
    {
        flow.AdvanceFlow(5f);
        score.RegisterManualBulletClear();
        score.RegisterManualBulletClear();
        score.RecalculateScore();

        Assert.AreEqual(5f, score.SurvivalTime, 0.001f);
        Assert.AreEqual(50, score.SurvivalScore);
        Assert.AreEqual(2, score.ManualClearedBullets);
        Assert.AreEqual(50, score.ManualClearScore);
        Assert.AreEqual(1, score.CurrentThreatLevel);
        Assert.AreEqual(1.25f, score.DifficultyCoefficient, 0.001f);
        Assert.AreEqual(125, score.FinalScore);
    }

    [Test]
    public void CompletedBossAddsThreatRoundAndReward()
    {
        flow.AdvanceFlow(14f);
        score.RecalculateScore();

        Assert.AreEqual(1, score.CompletedBossRounds);
        Assert.AreEqual(3, score.CurrentThreatLevel);
        Assert.AreEqual(1000, score.BossStageReward);
        Assert.AreEqual(1425, score.FinalScore);
        Assert.AreEqual(3000, score.CalculateBossStageReward(2));
    }

    [Test]
    public void OnlyDirectPlayerClearIncrementsTheCounter()
    {
        GameObject manualBullet = new GameObject("ManualBullet");
        BulletObject manualObject = manualBullet.AddComponent<BulletObject>();
        manualObject.InitInfo(new BulletInfo { lifeTime = 30f });
        Assert.IsTrue(manualObject.ClearByPlayer());

        GameObject skillBullet = new GameObject("SkillBullet");
        BulletObject skillObject = skillBullet.AddComponent<BulletObject>();
        skillObject.InitInfo(new BulletInfo { lifeTime = 30f });
        BulletObject.ClearAllBullets();

        Assert.AreEqual(1, score.ManualClearedBullets);
    }

    [Test]
    public void GameOverFreezesTheFinalScore()
    {
        flow.AdvanceFlow(7f);
        score.RegisterManualBulletClear();
        flow.EnterGameOver();

        Assert.IsTrue(score.IsFinalized);
        Assert.AreEqual(119, score.FinalScore);
        Assert.IsFalse(score.RegisterManualBulletClear());

        flow.AdvanceFlow(100f);
        score.RecalculateScore();
        Assert.AreEqual(7f, score.SurvivalTime, 0.001f);
        Assert.AreEqual(119, score.FinalScore);
    }

    [Test]
    public void DifficultyCoefficientsMatchTheThreeModes()
    {
        Assert.AreEqual(1f, score.GetDifficultyCoefficient(1), 0.001f);
        Assert.AreEqual(1.25f, score.GetDifficultyCoefficient(2), 0.001f);
        Assert.AreEqual(1.6f, score.GetDifficultyCoefficient(3), 0.001f);
    }

    private static void InvokeAwake(object target)
    {
        MethodInfo awake = target.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(awake);
        awake.Invoke(target, null);
    }
}

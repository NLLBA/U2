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
        previousDifficultyId = GameDataMgr.Instance.difficultyIndex;
        GameDataMgr.Instance.SetDifficulty(2);

        host = new GameObject("ScoreManagerTests");
        flow = host.AddComponent<GameFlowManager>();
        DifficultyDirector director = host.AddComponent<DifficultyDirector>();
        score = host.AddComponent<ScoreManager>();
        InvokeAwake(flow);
        InvokeAwake(director);
        InvokeAwake(score);

        flow.bossStart = 10f;
        flow.warnTime = 2f;
        flow.bossTime = 4f;
        flow.bossGap = 5f;
        flow.Reset();
        score.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        GameDataMgr.Instance.SetDifficulty(previousDifficultyId);
        BulletObject.ClearAll();
        if (host != null)
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void SurvivalAndManualClearsUseOneDifficultyCoefficient()
    {
        flow.Advance(5f);
        score.RegisterClear();
        score.RegisterClear();
        score.UpdateScore();

        Assert.AreEqual(5f, score.RunTime, 0.001f);
        Assert.AreEqual(50, score.TimeScore);
        Assert.AreEqual(2, score.ClearCount);
        Assert.AreEqual(50, score.ClearScore);
        Assert.AreEqual(1, score.Threat);
        Assert.AreEqual(1.25f, score.ScoreRate, 0.001f);
        Assert.AreEqual(125, score.TotalScore);
    }

    [Test]
    public void CompletedBossAddsThreatRoundAndReward()
    {
        flow.Advance(14f);
        score.UpdateScore();

        Assert.AreEqual(1, score.BossRounds);
        Assert.AreEqual(3, score.Threat);
        Assert.AreEqual(1000, score.BossScore);
        Assert.AreEqual(1425, score.TotalScore);
        Assert.AreEqual(3000, score.BossScoreFor(2));
    }

    [Test]
    public void OnlyDirectPlayerClearIncrementsTheCounter()
    {
        GameObject manualBullet = new GameObject("ManualBullet");
        BulletObject manualObject = manualBullet.AddComponent<BulletObject>();
        manualObject.Init(new BulletInfo { life = 30f });
        Assert.IsTrue(manualObject.PlayerClear());

        GameObject skillBullet = new GameObject("SkillBullet");
        BulletObject skillObject = skillBullet.AddComponent<BulletObject>();
        skillObject.Init(new BulletInfo { life = 30f });
        BulletObject.ClearAll();

        Assert.AreEqual(1, score.ClearCount);
    }

    [Test]
    public void GameOverFreezesTheFinalScore()
    {
        flow.Advance(7f);
        score.RegisterClear();
        flow.GameOver();

        Assert.IsTrue(score.Finished);
        Assert.AreEqual(119, score.TotalScore);
        Assert.IsFalse(score.RegisterClear());

        flow.Advance(100f);
        score.UpdateScore();
        Assert.AreEqual(7f, score.RunTime, 0.001f);
        Assert.AreEqual(119, score.TotalScore);
    }

    [Test]
    public void DifficultyCoefficientsMatchTheThreeModes()
    {
        Assert.AreEqual(1f, score.RateFor(1), 0.001f);
        Assert.AreEqual(1.25f, score.RateFor(2), 0.001f);
        Assert.AreEqual(1.6f, score.RateFor(3), 0.001f);
    }

    private static void InvokeAwake(object target)
    {
        MethodInfo awake = target.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(awake);
        awake.Invoke(target, null);
    }
}

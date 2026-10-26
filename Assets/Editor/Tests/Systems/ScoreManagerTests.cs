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
        flow.bossGap = 8f;
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
        Assert.AreEqual(100, score.RawScore);
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
    public void ChainClearTraversesConnectedBulletsWithoutClearingUnrelatedShots()
    {
        BulletObject first = CreateBullet("ClusterFirst", Vector3.zero, 10f, 1);
        first.ConfigureInteraction(BulletInteraction.ClusterCore);
        CreateBullet("ClusterMiddle", Vector3.right * 8f, 10f, 1);
        CreateBullet("ClusterLast", Vector3.right * 16f, 10f, 1);
        BulletObject nextWave = CreateBullet("NextWave", Vector3.right * 4f, 10f, 2);
        BulletObject remote = CreateBullet("RemoteCluster", Vector3.right * 40f, 10f, 2);
        BulletObject ordinary = CreateBullet("OrdinaryShot", Vector3.right * 4f, 0f);

        Assert.IsTrue(first.PlayerClear());
        Assert.AreEqual(3, score.ClearCount, "A connected chain must count every bullet once.");
        Assert.AreEqual(1, score.ClusterWavesCleared, "A completed cluster must count as one mission wave.");
        Assert.IsFalse(nextWave.IsDead, "A chain must not skip ahead into the next mission wave.");
        Assert.IsFalse(remote.IsDead);
        Assert.IsFalse(ordinary.IsDead);

        BulletObject.ClearAll();
        Assert.AreEqual(3, score.ClearCount, "System cleanup must not award player clears.");
    }

    [Test]
    public void ScreenClearAwardsEachBulletOnceEvenWhenClustersOverlap()
    {
        CreateBullet("ClusterFirst", Vector3.zero, 10f);
        CreateBullet("ClusterSecond", Vector3.right * 8f, 10f);
        CreateBullet("OrdinaryShot", Vector3.right * 4f, 0f);

        BulletObject.ClearByPlayer();
        BulletObject.ClearByPlayer();

        Assert.AreEqual(3, score.ClearCount);
    }

    private static BulletObject CreateBullet(string name, Vector3 position, float chainRadius, int waveId = 0)
    {
        GameObject host = new GameObject(name);
        host.transform.position = position;
        BulletObject bullet = host.AddComponent<BulletObject>();
        bullet.OnAcquire();
        bullet.Init(new BulletInfo { life = 30f }, BulletSource.Normal, 1f, chainRadius);
        bullet.AssignEncounterWave(waveId);
        return bullet;
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

    [Test]
    public void MissionRawScoreDoesNotUseTheLeaderboardDifficultyCoefficient()
    {
        flow.Advance(5f);
        score.RegisterClear();
        score.RegisterClear();
        score.UpdateScore();
        Assert.AreEqual(100, score.RawScore);
        Assert.AreEqual(125, score.TotalScore);

        GameDataMgr.Instance.SetDifficulty(3);
        score.UpdateScore();
        Assert.AreEqual(100, score.RawScore);
        Assert.AreEqual(160, score.TotalScore);
    }

    [Test]
    public void MissionMechanicsAwardBonusesAndTrackTheFinalStatistics()
    {
        Assert.IsTrue(score.RegisterCorridorPass());
        Assert.IsTrue(score.RegisterClusterWave(7));
        Assert.IsTrue(score.RegisterPerfectDodge());
        Assert.IsTrue(score.RegisterRelayBeacon());
        Assert.IsTrue(score.RegisterReflectedCore());

        Assert.AreEqual(1, score.PerfectCorridorPasses);
        Assert.AreEqual(1, score.ClusterWavesCleared);
        Assert.AreEqual(1, score.PerfectDodges);
        Assert.AreEqual(1, score.RelayBeacons);
        Assert.AreEqual(1, score.ReflectedCores);
        Assert.AreEqual(820, score.MechanicBonusScore);
        Assert.AreEqual(5, score.MechanicCombo);
        Assert.AreEqual(5, score.MaxMechanicCombo);
        Assert.AreEqual(820, score.RawScore);
        Assert.AreEqual(1025, score.TotalScore);

        Assert.IsTrue(score.RegisterMistake());
        Assert.IsTrue(score.RegisterPlayerHit());
        Assert.AreEqual(1, score.Mistakes);
        Assert.AreEqual(1, score.PlayerHits);
        Assert.AreEqual(0, score.MechanicCombo);
        Assert.AreEqual(5, score.MaxMechanicCombo);
    }

    [Test]
    public void ClusterCoreHasAForgivingVisualClickAreaWithoutALargerCollider()
    {
        GameObject cameraHost = new GameObject("CoreClickCamera");
        Camera camera = cameraHost.AddComponent<Camera>();
        BulletObject core = CreateBullet("ForgivingCore", new Vector3(0f, 0f, 10f), 10f, 3);
        core.ConfigureInteraction(BulletInteraction.ClusterCore);
        Vector2 click = (Vector2)camera.WorldToScreenPoint(core.transform.position) + Vector2.right * 50f;

        Assert.IsTrue(BulletObject.TryPlayerClearNearScreen(camera, click));
        Assert.IsTrue(core == null || core.IsDead);
        Assert.AreEqual(1, score.ClusterWavesCleared);

        Object.DestroyImmediate(cameraHost);
    }

    [TestCase(0f, 1)]
    [TestCase(0.3334f, 2)]
    [TestCase(0.6667f, 3)]
    [TestCase(1f, 3)]
    public void EncounterProgressUsesThreeClearPhases(float progress, int expectedPhase)
    {
        Assert.AreEqual(expectedPhase, LevelEncounterDirector.PhaseForProgress(progress));
    }

    [TestCase(0, 2)]
    [TestCase(1, 4)]
    [TestCase(2, 3)]
    [TestCase(3, 5)]
    [TestCase(4, 1)]
    public void EveryHeroHasOneStageAffinity(int heroIndex, int expectedLevel)
    {
        Assert.AreEqual(expectedLevel, PlayerObject.AffinityLevelForHero(heroIndex));
        Assert.IsFalse(string.IsNullOrEmpty(PlayerObject.StageAffinityText(heroIndex)));
    }

    private static void InvokeAwake(object target)
    {
        MethodInfo awake = target.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(awake);
        awake.Invoke(target, null);
    }
}

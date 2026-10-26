using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class BossControllerTests
{
    private GameObject host;
    private GameFlowManager flow;
    private BossController controller;
    private int originalDifficulty;

    [SetUp]
    public void SetUp()
    {
        Time.timeScale = 1f;
        FireObject.SetIntensity(1f);
        originalDifficulty = GameDataMgr.Instance.difficultyIndex;
        GameDataMgr.Instance.SetDifficulty(2);

        host = new GameObject("BossControllerTests");
        flow = host.AddComponent<GameFlowManager>();
        DifficultyDirector director = host.AddComponent<DifficultyDirector>();
        controller = host.AddComponent<BossController>();
        InvokeAwake(flow);
        InvokeAwake(director);
        InvokeAwake(controller);
        controller.bossPath = string.Empty;

        flow.bossStart = 10f;
        flow.warnTime = 2f;
        flow.bossTime = 4f;
        flow.bossGap = 8f;
        flow.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        FireObject.SetIntensity(1f);
        GameDataMgr.Instance.SetDifficulty(originalDifficulty);
        if (host != null)
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BossLifecycleReducesNormalFireAndClearsBossBullets()
    {
        flow.Advance(8f);
        Assert.AreEqual(GameFlowState.BossWarning, flow.State);
        Assert.AreEqual(0.25f, FireObject.Intensity, 0.001f);

        flow.Advance(2f);
        Assert.IsTrue(controller.BossActive);
        Assert.AreEqual(1, controller.BossRound);

        GameObject bullet = new GameObject("BossBulletTest");
        BulletObject bulletObject = bullet.AddComponent<BulletObject>();
        bulletObject.OnAcquire();
        bulletObject.Init(new BulletInfo { life = 30f }, BulletSource.Boss, 1f);

        flow.Advance(4f);
        Assert.AreEqual(GameFlowState.Normal, flow.State);
        Assert.AreEqual(1, flow.BossRounds);
        Assert.IsFalse(controller.BossActive);
        Assert.AreEqual(1f, FireObject.Intensity, 0.001f);
        Assert.IsTrue(bullet == null);
    }

    [Test]
    public void PauseKeepsTheActiveBossAndRound()
    {
        flow.Advance(10f);
        Assert.IsTrue(controller.BossActive);

        flow.Pause();
        Assert.AreEqual(GameFlowState.Paused, flow.State);
        Assert.IsTrue(controller.BossActive);
        Assert.AreEqual(1, controller.BossRound);

        flow.Resume();
        Assert.AreEqual(GameFlowState.BossSurvival, flow.State);
        Assert.IsTrue(controller.BossActive);
    }

    [Test]
    public void BossUsesFourPhasesAndLaterRoundsAddBullets()
    {
        Assert.AreEqual(BossAttackPattern.Fan, controller.PatternAt(0f));
        Assert.AreEqual(BossAttackPattern.Spiral, controller.PatternAt(0.25f));
        Assert.AreEqual(BossAttackPattern.Cross, controller.PatternAt(0.5f));
        Assert.AreEqual(BossAttackPattern.Homing, controller.PatternAt(0.75f));

        Assert.Greater(
            controller.VolleySize(BossAttackPattern.Fan, 3),
            controller.VolleySize(BossAttackPattern.Fan, 1));
        Assert.Greater(
            controller.VolleySize(BossAttackPattern.Homing, 3),
            controller.VolleySize(BossAttackPattern.Homing, 1));
    }

    [Test]
    public void FifthLevelUsesTwoDistinctFinalBossStages()
    {
        Assert.AreEqual(BossAttackPattern.Ring, controller.CampaignPatternAt(0f, 1, 5));
        Assert.AreEqual(BossAttackPattern.Spiral, controller.CampaignPatternAt(0.5f, 1, 5));
        Assert.AreEqual(BossAttackPattern.Cross, controller.CampaignPatternAt(0f, 2, 5));
        Assert.AreEqual(BossAttackPattern.Homing, controller.CampaignPatternAt(0.5f, 2, 5));
        Assert.AreEqual(BossAttackPattern.Fan, controller.CampaignPatternAt(0f, 1, 4));
    }

    [Test]
    public void BossVolleySizeAlsoFollowsSelectedDifficulty()
    {
        GameDataMgr.Instance.SetDifficulty(1);
        int easyFan = controller.VolleySize(BossAttackPattern.Fan, 1);
        int easyHoming = controller.VolleySize(BossAttackPattern.Homing, 1);
        GameDataMgr.Instance.SetDifficulty(2);
        int normalFan = controller.VolleySize(BossAttackPattern.Fan, 1);
        int normalHoming = controller.VolleySize(BossAttackPattern.Homing, 1);
        GameDataMgr.Instance.SetDifficulty(3);
        int hardFan = controller.VolleySize(BossAttackPattern.Fan, 1);
        int hardHoming = controller.VolleySize(BossAttackPattern.Homing, 1);

        Assert.Less(easyFan, normalFan);
        Assert.Less(normalFan, hardFan);
        Assert.Less(easyHoming, normalHoming);
        Assert.Less(normalHoming, hardHoming);
    }

    [Test]
    public void BossVisualsCycleAcrossRoundsAndKeepFallbackSupport()
    {
        controller.bossPath = "Airplane/Airplane5";
        controller.bossPaths = new[] { "Bosses/A", "Bosses/B", "Bosses/C" };

        Assert.AreEqual("Bosses/A", controller.BossPath(1));
        Assert.AreEqual("Bosses/B", controller.BossPath(2));
        Assert.AreEqual("Bosses/C", controller.BossPath(3));
        Assert.AreEqual("Bosses/A", controller.BossPath(4));

        controller.bossPaths = new string[0];
        Assert.AreEqual("Airplane/Airplane5", controller.BossPath(2));

        controller.bossPath = string.Empty;
        Assert.AreEqual(string.Empty, controller.BossPath(1));
    }

    [Test]
    public void DedicatedBossVisualPrefabsContainArtAndAnimation()
    {
        string[] resourcePaths =
        {
            "Bosses/CrimsonManta",
            "Bosses/AzureBastion",
            "Bosses/SolarSeraph",
        };

        for (int i = 0; i < resourcePaths.Length; i++)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePaths[i]);
            Assert.IsNotNull(prefab, "Missing boss visual: " + resourcePaths[i]);

            BossVisualAnimator animator = prefab.GetComponent<BossVisualAnimator>();
            Assert.IsNotNull(animator, "Missing visual animator: " + resourcePaths[i]);
            Assert.Greater(prefab.GetComponentsInChildren<MeshRenderer>(true).Length, 10);
            Assert.AreEqual(1, prefab.GetComponentsInChildren<Light>(true).Length);
            Assert.AreEqual(0, prefab.GetComponentsInChildren<Collider>(true).Length);
            Assert.Greater(
                animator.spinParts.Length +
                animator.reverseParts.Length +
                animator.pulseParts.Length,
                0,
                "Boss visual has no animated parts: " + resourcePaths[i]);
        }
    }

    private static void InvokeAwake(object target)
    {
        MethodInfo awake = target.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(awake);
        awake.Invoke(target, null);
    }
}

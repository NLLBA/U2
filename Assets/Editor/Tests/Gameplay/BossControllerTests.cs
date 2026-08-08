using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class BossControllerTests
{
    private GameObject host;
    private GameFlowManager flow;
    private BossController controller;

    [SetUp]
    public void SetUp()
    {
        Time.timeScale = 1f;
        FireObject.SetGlobalIntensityScale(1f);

        host = new GameObject("BossControllerTests");
        flow = host.AddComponent<GameFlowManager>();
        DifficultyDirector director = host.AddComponent<DifficultyDirector>();
        controller = host.AddComponent<BossController>();
        InvokeAwake(flow);
        InvokeAwake(director);
        InvokeAwake(controller);
        controller.bossVisualResource = string.Empty;

        flow.firstBossTime = 10f;
        flow.bossWarningTime = 2f;
        flow.bossDuration = 4f;
        flow.normalInterval = 5f;
        flow.ResetFlow();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        FireObject.SetGlobalIntensityScale(1f);
        if (host != null)
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BossLifecycleReducesNormalFireAndClearsBossBullets()
    {
        flow.AdvanceFlow(8f);
        Assert.AreEqual(GameFlowState.BossWarning, flow.State);
        Assert.AreEqual(0.25f, FireObject.GlobalIntensityScale, 0.001f);

        flow.AdvanceFlow(2f);
        Assert.IsTrue(controller.IsBossActive);
        Assert.AreEqual(1, controller.ActiveRound);

        GameObject bullet = new GameObject("BossBulletTest");
        BulletObject bulletObject = bullet.AddComponent<BulletObject>();
        bulletObject.OnPoolAcquire();
        bulletObject.InitInfo(new BulletInfo { lifeTime = 30f }, BulletSource.Boss, 1f);

        flow.AdvanceFlow(4f);
        Assert.AreEqual(GameFlowState.Normal, flow.State);
        Assert.AreEqual(1, flow.CompletedBossRounds);
        Assert.IsFalse(controller.IsBossActive);
        Assert.AreEqual(1f, FireObject.GlobalIntensityScale, 0.001f);
        Assert.IsTrue(bullet == null);
    }

    [Test]
    public void PauseKeepsTheActiveBossAndRound()
    {
        flow.AdvanceFlow(10f);
        Assert.IsTrue(controller.IsBossActive);

        flow.PauseGame();
        Assert.AreEqual(GameFlowState.Paused, flow.State);
        Assert.IsTrue(controller.IsBossActive);
        Assert.AreEqual(1, controller.ActiveRound);

        flow.ResumeGame();
        Assert.AreEqual(GameFlowState.BossSurvival, flow.State);
        Assert.IsTrue(controller.IsBossActive);
    }

    [Test]
    public void BossUsesFourPhasesAndLaterRoundsAddBullets()
    {
        Assert.AreEqual(BossAttackPattern.Fan, controller.GetPatternAtNormalizedTime(0f));
        Assert.AreEqual(BossAttackPattern.Spiral, controller.GetPatternAtNormalizedTime(0.25f));
        Assert.AreEqual(BossAttackPattern.Cross, controller.GetPatternAtNormalizedTime(0.5f));
        Assert.AreEqual(BossAttackPattern.Homing, controller.GetPatternAtNormalizedTime(0.75f));

        Assert.Greater(
            controller.GetPrimaryVolleyBulletCount(BossAttackPattern.Fan, 3),
            controller.GetPrimaryVolleyBulletCount(BossAttackPattern.Fan, 1));
        Assert.Greater(
            controller.GetPrimaryVolleyBulletCount(BossAttackPattern.Homing, 3),
            controller.GetPrimaryVolleyBulletCount(BossAttackPattern.Homing, 1));
    }

    [Test]
    public void BossVisualsCycleAcrossRoundsAndKeepFallbackSupport()
    {
        controller.bossVisualResource = "Airplane/Airplane5";
        controller.bossVisualResources = new[] { "Bosses/A", "Bosses/B", "Bosses/C" };

        Assert.AreEqual("Bosses/A", controller.GetBossVisualResource(1));
        Assert.AreEqual("Bosses/B", controller.GetBossVisualResource(2));
        Assert.AreEqual("Bosses/C", controller.GetBossVisualResource(3));
        Assert.AreEqual("Bosses/A", controller.GetBossVisualResource(4));

        controller.bossVisualResources = new string[0];
        Assert.AreEqual("Airplane/Airplane5", controller.GetBossVisualResource(2));

        controller.bossVisualResource = string.Empty;
        Assert.AreEqual(string.Empty, controller.GetBossVisualResource(1));
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
                animator.clockwiseParts.Length +
                animator.counterClockwiseParts.Length +
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

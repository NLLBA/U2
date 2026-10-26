using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ObjectPoolManagerTests
{
    private GameObject host;
    private GameObject prefab;
    private ObjectPoolManager pool;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("ObjectPoolManagerTests");
        pool = host.AddComponent<ObjectPoolManager>();
        pool.poolLimit = 2500;
        pool.usePool = true;

        prefab = new GameObject("BulletPrefab");
        prefab.SetActive(false);
        prefab.AddComponent<BulletObject>();
    }

    [TearDown]
    public void TearDown()
    {
        BulletObject.ClearAll();
        if (host != null)
        {
            Object.DestroyImmediate(host);
        }

        if (prefab != null)
        {
            Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void ReleasedBulletIsReusedWithResetStateAndTransform()
    {
        GameObject first = pool.Spawn(
            prefab,
            "test:bullet",
            new Vector3(3f, 4f, 5f),
            Quaternion.Euler(0f, 45f, 0f));
        BulletObject firstBullet = first.GetComponent<BulletObject>();
        firstBullet.Init(CreateInfo(), BulletSource.Boss, 2f);

        first.transform.rotation = Quaternion.Euler(20f, 80f, 10f);
        firstBullet.Dead(false);

        Assert.IsFalse(first.activeSelf);
        Assert.IsTrue(firstBullet.IsDead);
        Assert.AreEqual(Quaternion.identity, first.transform.localRotation);

        Quaternion spawnRotation = Quaternion.LookRotation(Vector3.left);
        GameObject second = pool.Spawn(
            prefab,
            "test:bullet",
            new Vector3(-2f, 0f, 8f),
            spawnRotation);
        BulletObject secondBullet = second.GetComponent<BulletObject>();
        secondBullet.Init(CreateInfo(), BulletSource.Normal, 1f);

        Assert.AreSame(first, second);
        Assert.IsFalse(secondBullet.IsDead);
        Assert.AreEqual(BulletSource.Normal, secondBullet.Source);
        Assert.Less(Quaternion.Angle(spawnRotation, second.transform.rotation), 0.001f);

        PoolStats stats = pool.Stats();
        Assert.AreEqual(2, stats.SpawnCount);
        Assert.AreEqual(1, stats.NewCount);
        Assert.AreEqual(1, stats.ReuseCount);
        Assert.AreEqual(1, stats.ActiveCount);
        Assert.AreEqual(1, stats.ReleaseCount);
    }

    [Test]
    public void BossCleanupRecyclesOnlyBossBullets()
    {
        GameObject normalObject = pool.Spawn(
            prefab,
            "test:bullet",
            Vector3.zero,
            Quaternion.identity);
        BulletObject normalBullet = normalObject.GetComponent<BulletObject>();
        normalBullet.Init(CreateInfo(), BulletSource.Normal);

        GameObject bossObject = pool.Spawn(
            prefab,
            "test:bullet",
            Vector3.one,
            Quaternion.identity);
        BulletObject bossBullet = bossObject.GetComponent<BulletObject>();
        bossBullet.Init(CreateInfo(), BulletSource.Boss);

        Assert.AreEqual(1, BulletObject.ClearBoss());
        Assert.IsTrue(normalObject.activeSelf);
        Assert.IsFalse(bossObject.activeSelf);

        normalBullet.Dead(false);
    }

    [Test]
    public void DisabledPoolingCreatesAndDiscardsEachInstance()
    {
        pool.usePool = false;

        GameObject first = pool.Spawn(
            prefab,
            "test:no-pool",
            Vector3.zero,
            Quaternion.identity);
        first.GetComponent<BulletObject>().Init(CreateInfo());
        first.GetComponent<BulletObject>().Dead(false);

        GameObject second = pool.Spawn(
            prefab,
            "test:no-pool",
            Vector3.zero,
            Quaternion.identity);
        second.GetComponent<BulletObject>().Init(CreateInfo());

        PoolStats stats = pool.Stats();
        Assert.AreEqual(2, stats.NewCount);
        Assert.AreEqual(0, stats.ReuseCount);
        Assert.AreEqual(1, stats.DropCount);

        second.GetComponent<BulletObject>().Dead(false);
    }

    [TestCase(500)]
    [TestCase(1000)]
    [TestCase(2000)]
    public void WarmPoolReusesEveryObjectAtBenchmarkScale(int count)
    {
        List<GameObject> objects = SpawnMany(count, "test:scale");
        ReleaseMany(objects);
        pool.ResetStats();

        objects = SpawnMany(count, "test:scale");
        PoolStats activeStats = pool.Stats();
        Assert.AreEqual(count, activeStats.SpawnCount);
        Assert.AreEqual(0, activeStats.NewCount);
        Assert.AreEqual(count, activeStats.ReuseCount);
        Assert.AreEqual(count, activeStats.ActiveCount);

        ReleaseMany(objects);
        PoolStats finalStats = pool.Stats();
        Assert.AreEqual(count, finalStats.ReleaseCount);
        Assert.AreEqual(0, finalStats.ActiveCount);
        Assert.AreEqual(count, finalStats.InactiveCount);
    }

    [Test]
    public void RepeatedCombatCyclesReachAStableAllocationPlateau()
    {
        const int objectsPerCycle = 200;
        List<GameObject> objects = SpawnMany(objectsPerCycle, "test:soak");
        ReleaseMany(objects);
        pool.ResetStats();

        for (int cycle = 0; cycle < 50; cycle++)
        {
            objects = SpawnMany(objectsPerCycle, "test:soak");
            ReleaseMany(objects);
        }

        PoolStats stats = pool.Stats();
        Assert.AreEqual(objectsPerCycle * 50, stats.SpawnCount);
        Assert.AreEqual(0, stats.NewCount, "A warmed combat pool must not allocate in later cycles.");
        Assert.AreEqual(stats.SpawnCount, stats.ReuseCount);
        Assert.AreEqual(0, stats.ActiveCount);
        Assert.AreEqual(objectsPerCycle, stats.InactiveCount);
    }

    private List<GameObject> SpawnMany(int count, string key)
    {
        List<GameObject> objects = new List<GameObject>(count);
        for (int i = 0; i < count; i++)
        {
            objects.Add(pool.Spawn(prefab, key, Vector3.zero, Quaternion.identity));
        }

        return objects;
    }

    private void ReleaseMany(List<GameObject> objects)
    {
        for (int i = 0; i < objects.Count; i++)
        {
            pool.Release(objects[i]);
        }
    }

    private static BulletInfo CreateInfo()
    {
        return new BulletInfo
        {
            type = 1,
            moveRate = 60f,
            life = 30f,
        };
    }
}

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
        pool.maxInactivePerResource = 2500;
        pool.poolingEnabled = true;

        prefab = new GameObject("BulletPrefab");
        prefab.SetActive(false);
        prefab.AddComponent<BulletObject>();
    }

    [TearDown]
    public void TearDown()
    {
        BulletObject.ClearAllBullets();
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
        firstBullet.InitInfo(CreateInfo(), BulletSource.Boss, 2f);

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
        secondBullet.InitInfo(CreateInfo(), BulletSource.Normal, 1f);

        Assert.AreSame(first, second);
        Assert.IsFalse(secondBullet.IsDead);
        Assert.AreEqual(BulletSource.Normal, secondBullet.Source);
        Assert.Less(Quaternion.Angle(spawnRotation, second.transform.rotation), 0.001f);

        ObjectPoolStatistics stats = pool.GetStatistics();
        Assert.AreEqual(2, stats.SpawnRequests);
        Assert.AreEqual(1, stats.InstancesCreated);
        Assert.AreEqual(1, stats.InstancesReused);
        Assert.AreEqual(1, stats.ActiveCount);
        Assert.AreEqual(1, stats.RecycleRequests);
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
        normalBullet.InitInfo(CreateInfo(), BulletSource.Normal);

        GameObject bossObject = pool.Spawn(
            prefab,
            "test:bullet",
            Vector3.one,
            Quaternion.identity);
        BulletObject bossBullet = bossObject.GetComponent<BulletObject>();
        bossBullet.InitInfo(CreateInfo(), BulletSource.Boss);

        Assert.AreEqual(1, BulletObject.ClearBossBullets());
        Assert.IsTrue(normalObject.activeSelf);
        Assert.IsFalse(bossObject.activeSelf);

        normalBullet.Dead(false);
    }

    [Test]
    public void DisabledPoolingCreatesAndDiscardsEachInstance()
    {
        pool.poolingEnabled = false;

        GameObject first = pool.Spawn(
            prefab,
            "test:no-pool",
            Vector3.zero,
            Quaternion.identity);
        first.GetComponent<BulletObject>().InitInfo(CreateInfo());
        first.GetComponent<BulletObject>().Dead(false);

        GameObject second = pool.Spawn(
            prefab,
            "test:no-pool",
            Vector3.zero,
            Quaternion.identity);
        second.GetComponent<BulletObject>().InitInfo(CreateInfo());

        ObjectPoolStatistics stats = pool.GetStatistics();
        Assert.AreEqual(2, stats.InstancesCreated);
        Assert.AreEqual(0, stats.InstancesReused);
        Assert.AreEqual(1, stats.DiscardedCount);

        second.GetComponent<BulletObject>().Dead(false);
    }

    [TestCase(500)]
    [TestCase(1000)]
    [TestCase(2000)]
    public void WarmPoolReusesEveryObjectAtBenchmarkScale(int count)
    {
        List<GameObject> objects = SpawnMany(count, "test:scale");
        ReleaseMany(objects);
        pool.ResetStatistics();

        objects = SpawnMany(count, "test:scale");
        ObjectPoolStatistics activeStats = pool.GetStatistics();
        Assert.AreEqual(count, activeStats.SpawnRequests);
        Assert.AreEqual(0, activeStats.InstancesCreated);
        Assert.AreEqual(count, activeStats.InstancesReused);
        Assert.AreEqual(count, activeStats.ActiveCount);

        ReleaseMany(objects);
        ObjectPoolStatistics finalStats = pool.GetStatistics();
        Assert.AreEqual(count, finalStats.RecycleRequests);
        Assert.AreEqual(0, finalStats.ActiveCount);
        Assert.AreEqual(count, finalStats.InactiveCount);
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
            forwardSpeed = 60f,
            lifeTime = 30f,
        };
    }
}

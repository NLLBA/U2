using System.Collections.Generic;
using UnityEngine;

public interface IPoolableObject
{
    void OnPoolAcquire();
    void OnPoolRelease();
}

public struct ObjectPoolStatistics
{
    public int SpawnRequests;
    public int InstancesCreated;
    public int InstancesReused;
    public int ActiveCount;
    public int RecycleRequests;
    public int InactiveCount;
    public int DiscardedCount;
}

[DisallowMultipleComponent]
public sealed class PooledObject : MonoBehaviour
{
    internal ObjectPoolManager Owner;
    internal string PoolKey;
    internal bool IsSpawned;
    internal Vector3 OriginalLocalScale;

    public void ReleaseAfter(float delay)
    {
        CancelInvoke(nameof(ReleaseNow));
        if (delay <= 0f)
        {
            ReleaseNow();
            return;
        }

        Invoke(nameof(ReleaseNow), delay);
    }

    internal void NotifyAcquire()
    {
        CancelInvoke(nameof(ReleaseNow));
        IPoolableObject[] callbacks = GetComponents<IPoolableObject>();
        for (int i = 0; i < callbacks.Length; i++)
        {
            callbacks[i].OnPoolAcquire();
        }
    }

    internal void NotifyRelease()
    {
        CancelInvoke(nameof(ReleaseNow));
        IPoolableObject[] callbacks = GetComponents<IPoolableObject>();
        for (int i = 0; i < callbacks.Length; i++)
        {
            callbacks[i].OnPoolRelease();
        }
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(ReleaseNow));
    }

    private void ReleaseNow()
    {
        if (Owner != null)
        {
            Owner.Release(gameObject);
        }
    }
}

[DisallowMultipleComponent]
public sealed class PooledEffectObject : MonoBehaviour, IPoolableObject
{
    private ParticleSystem[] particleSystems;

    public void Play(float lifetime)
    {
        CacheParticleSystems();
        RestartParticles();

        PooledObject pooledObject = GetComponent<PooledObject>();
        if (pooledObject != null)
        {
            pooledObject.ReleaseAfter(lifetime);
        }
    }

    public void OnPoolAcquire()
    {
        CacheParticleSystems();
        RestartParticles();
    }

    public void OnPoolRelease()
    {
        CacheParticleSystems();
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void CacheParticleSystems()
    {
        if (particleSystems == null)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }
    }

    private void RestartParticles()
    {
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Clear(true);
            particleSystems[i].Play(true);
        }
    }
}

public class ObjectPoolManager : MonoBehaviour
{
    private static ObjectPoolManager instance;

    public static ObjectPoolManager Instance => instance;

    [Min(1)] public int maxInactivePerResource = 512;
    public bool poolingEnabled = true;
    public bool logStatisticsOnDestroy;

    private readonly Dictionary<string, Stack<PooledObject>> pools =
        new Dictionary<string, Stack<PooledObject>>();
    private readonly Dictionary<string, GameObject> resourceCache =
        new Dictionary<string, GameObject>();

    private Transform inactiveRoot;
    private int spawnRequests;
    private int instancesCreated;
    private int instancesReused;
    private int activeCount;
    private int recycleRequests;
    private int inactiveCount;
    private int discardedCount;

    public static ObjectPoolManager EnsureInstance(GameObject host = null)
    {
        if (instance != null)
            return instance;

        GameObject target = host != null ? host : new GameObject("ObjectPoolManager");
        ObjectPoolManager manager = target.GetComponent<ObjectPoolManager>();
        return manager != null ? manager : target.AddComponent<ObjectPoolManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            DestroyComponent(this);
            return;
        }

        instance = this;
        maxInactivePerResource = Mathf.Max(1, maxInactivePerResource);
        EnsureInactiveRoot();
    }

    private void OnValidate()
    {
        maxInactivePerResource = Mathf.Max(1, maxInactivePerResource);
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        if (logStatisticsOnDestroy)
        {
            Debug.Log(FormatStatistics());
        }

        instance = null;
    }

    public GameObject SpawnResource(
        string resourcePath,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            Debug.LogError("Cannot spawn an empty resource path.");
            return null;
        }

        GameObject prefab;
        if (!resourceCache.TryGetValue(resourcePath, out prefab) || prefab == null)
        {
            prefab = Resources.Load<GameObject>(resourcePath);
            resourceCache[resourcePath] = prefab;
        }

        if (prefab == null)
        {
            Debug.LogError("Pooled resource not found: " + resourcePath);
            return null;
        }

        return Spawn(prefab, "resource:" + resourcePath, position, rotation, parent);
    }

    public GameObject Spawn(
        GameObject prefab,
        string poolKey,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null)
    {
        if (prefab == null)
            return null;

        string safeKey = string.IsNullOrEmpty(poolKey)
            ? "prefab:" + prefab.GetInstanceID()
            : poolKey;

        spawnRequests++;
        PooledObject pooledObject = TakeInactive(safeKey);
        if (pooledObject == null)
        {
            GameObject created = Instantiate(prefab);
            pooledObject = created.GetComponent<PooledObject>();
            if (pooledObject == null)
            {
                pooledObject = created.AddComponent<PooledObject>();
            }

            pooledObject.Owner = this;
            pooledObject.PoolKey = safeKey;
            pooledObject.OriginalLocalScale = created.transform.localScale;
            instancesCreated++;
        }
        else
        {
            instancesReused++;
        }

        Transform target = pooledObject.transform;
        target.SetParent(parent, false);
        target.position = position;
        target.rotation = rotation;
        target.localScale = pooledObject.OriginalLocalScale;

        pooledObject.IsSpawned = true;
        activeCount++;
        pooledObject.gameObject.SetActive(true);
        pooledObject.NotifyAcquire();
        return pooledObject.gameObject;
    }

    public GameObject SpawnEffect(
        string resourcePath,
        Vector3 position,
        Quaternion rotation,
        float lifetime)
    {
        GameObject effect = SpawnResource(resourcePath, position, rotation);
        if (effect == null)
            return null;

        PooledEffectObject pooledEffect = effect.GetComponent<PooledEffectObject>();
        if (pooledEffect == null)
        {
            pooledEffect = effect.AddComponent<PooledEffectObject>();
        }

        pooledEffect.Play(Mathf.Max(0f, lifetime));
        return effect;
    }

    public bool Release(GameObject target)
    {
        if (target == null)
            return false;

        PooledObject pooledObject = target.GetComponent<PooledObject>();
        if (pooledObject == null || pooledObject.Owner != this || !pooledObject.IsSpawned)
            return false;

        pooledObject.IsSpawned = false;
        pooledObject.NotifyRelease();
        recycleRequests++;
        activeCount = Mathf.Max(0, activeCount - 1);

        Stack<PooledObject> pool = GetOrCreatePool(pooledObject.PoolKey);
        if (!poolingEnabled || pool.Count >= maxInactivePerResource)
        {
            discardedCount++;
            DestroyObject(target);
            return true;
        }

        target.SetActive(false);
        Transform targetTransform = target.transform;
        targetTransform.SetParent(EnsureInactiveRoot(), false);
        targetTransform.localPosition = Vector3.zero;
        targetTransform.localRotation = Quaternion.identity;
        targetTransform.localScale = pooledObject.OriginalLocalScale;
        pool.Push(pooledObject);
        inactiveCount++;
        return true;
    }

    public static bool TryRelease(GameObject target)
    {
        if (target == null)
            return false;

        PooledObject pooledObject = target.GetComponent<PooledObject>();
        return pooledObject != null && pooledObject.Owner != null && pooledObject.Owner.Release(target);
    }

    public void ClearInactive()
    {
        foreach (KeyValuePair<string, Stack<PooledObject>> pair in pools)
        {
            Stack<PooledObject> pool = pair.Value;
            while (pool.Count > 0)
            {
                PooledObject pooledObject = pool.Pop();
                if (pooledObject != null)
                {
                    DestroyObject(pooledObject.gameObject);
                }
            }
        }

        pools.Clear();
        inactiveCount = 0;
    }

    public void ResetStatistics()
    {
        spawnRequests = 0;
        instancesCreated = 0;
        instancesReused = 0;
        recycleRequests = 0;
        discardedCount = 0;
    }

    public ObjectPoolStatistics GetStatistics()
    {
        return new ObjectPoolStatistics
        {
            SpawnRequests = spawnRequests,
            InstancesCreated = instancesCreated,
            InstancesReused = instancesReused,
            ActiveCount = activeCount,
            RecycleRequests = recycleRequests,
            InactiveCount = inactiveCount,
            DiscardedCount = discardedCount,
        };
    }

    public string FormatStatistics()
    {
        ObjectPoolStatistics stats = GetStatistics();
        return "ObjectPool requests=" + stats.SpawnRequests +
            " created=" + stats.InstancesCreated +
            " reused=" + stats.InstancesReused +
            " active=" + stats.ActiveCount +
            " recycled=" + stats.RecycleRequests +
            " inactive=" + stats.InactiveCount +
            " discarded=" + stats.DiscardedCount;
    }

    private PooledObject TakeInactive(string poolKey)
    {
        Stack<PooledObject> pool;
        if (!poolingEnabled || !pools.TryGetValue(poolKey, out pool))
            return null;

        while (pool.Count > 0)
        {
            PooledObject pooledObject = pool.Pop();
            inactiveCount = Mathf.Max(0, inactiveCount - 1);
            if (pooledObject != null)
            {
                return pooledObject;
            }
        }

        return null;
    }

    private Stack<PooledObject> GetOrCreatePool(string poolKey)
    {
        Stack<PooledObject> pool;
        if (!pools.TryGetValue(poolKey, out pool))
        {
            pool = new Stack<PooledObject>();
            pools.Add(poolKey, pool);
        }

        return pool;
    }

    private Transform EnsureInactiveRoot()
    {
        if (inactiveRoot != null)
            return inactiveRoot;

        GameObject root = new GameObject("ObjectPool_Inactive");
        root.transform.SetParent(transform, false);
        root.SetActive(false);
        inactiveRoot = root.transform;
        return inactiveRoot;
    }

    private static void DestroyObject(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static void DestroyComponent(Component target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}

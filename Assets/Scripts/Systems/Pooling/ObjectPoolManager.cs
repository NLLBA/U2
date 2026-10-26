using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public interface IPoolable
{
    void OnAcquire();
    void OnRelease();
}

public struct PoolStats
{
    public int SpawnCount;
    public int NewCount;
    public int ReuseCount;
    public int ActiveCount;
    public int ReleaseCount;
    public int InactiveCount;
    public int DropCount;
}

[DisallowMultipleComponent]
public sealed class PooledObject : MonoBehaviour
{
    internal ObjectPoolManager Owner;
    internal string PoolKey;
    internal bool IsSpawned;
    internal Vector3 BaseScale;
    private IPoolable[] callbacks;

    private void Awake()
    {
        RefreshCallbacks();
    }

    public void RefreshCallbacks()
    {
        callbacks = GetComponents<IPoolable>();
    }

    public void ReleaseIn(float delay)
    {
        CancelInvoke(nameof(ReleaseSelf));
        if (delay <= 0f)
        {
            ReleaseSelf();
            return;
        }

        Invoke(nameof(ReleaseSelf), delay);
    }

    internal void OnGet()
    {
        CancelInvoke(nameof(ReleaseSelf));
        if (callbacks == null)
            RefreshCallbacks();
        for (int i = 0; i < callbacks.Length; i++)
        {
            callbacks[i].OnAcquire();
        }
    }

    internal void OnPut()
    {
        CancelInvoke(nameof(ReleaseSelf));
        if (callbacks == null)
            RefreshCallbacks();
        for (int i = 0; i < callbacks.Length; i++)
        {
            callbacks[i].OnRelease();
        }
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(ReleaseSelf));
    }

    private void ReleaseSelf()
    {
        if (Owner != null)
        {
            Owner.Release(gameObject);
        }
    }
}

[DisallowMultipleComponent]
public sealed class PooledEffect : MonoBehaviour, IPoolable
{
    private ParticleSystem[] particles;

    public void Play(float lifetime)
    {
        Cache();
        Restart();

        PooledObject pooledObject = GetComponent<PooledObject>();
        if (pooledObject != null)
        {
            pooledObject.ReleaseIn(lifetime);
        }
    }

    public void OnAcquire()
    {
        Cache();
        Restart();
    }

    public void OnRelease()
    {
        Cache();
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Cache()
    {
        if (particles == null)
        {
            particles = GetComponentsInChildren<ParticleSystem>(true);
        }
    }

    private void Restart()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Clear(true);
            particles[i].Play(true);
        }
    }
}

public class ObjectPoolManager : MonoBehaviour
{
    private static ObjectPoolManager instance;

    public static ObjectPoolManager Instance => instance;

    [FormerlySerializedAs("maxInactivePerResource")]
    [Min(1)] public int poolLimit = 512;
    [FormerlySerializedAs("poolingEnabled")]
    public bool usePool = true;
    [FormerlySerializedAs("logStatisticsOnDestroy")]
    public bool logStats;

    private readonly Dictionary<string, Stack<PooledObject>> pools =
        new Dictionary<string, Stack<PooledObject>>();
    private readonly Dictionary<string, GameObject> cache =
        new Dictionary<string, GameObject>();

    private Transform poolRoot;
    private int spawnCount;
    private int newCount;
    private int reuseCount;
    private int activeCount;
    private int releaseCount;
    private int inactiveCount;
    private int dropCount;

    public static ObjectPoolManager Ensure(GameObject host = null)
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
            DestroyNow(this);
            return;
        }

        instance = this;
        poolLimit = Mathf.Max(1, poolLimit);
        Root();
    }

    private void OnValidate()
    {
        poolLimit = Mathf.Max(1, poolLimit);
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        if (logStats)
        {
            GameLog.Debug(GameLogCategory.Performance, StatsText());
        }

        instance = null;
    }

    public GameObject SpawnAsset(
        string assetPath,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            GameLog.Error(GameLogCategory.Data, "POOL_EMPTY_PATH", "Cannot spawn an empty resource path.");
            return null;
        }

        GameObject prefab = LoadAsset(assetPath);

        if (prefab == null)
        {
            GameLog.Error(GameLogCategory.Data, "POOL_RESOURCE_MISSING", "Pooled resource not found: " + assetPath);
            return null;
        }

        return Spawn(prefab, "resource:" + assetPath, position, rotation, parent);
    }

    public GameObject LoadAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;

        GameObject prefab;
        if (!cache.TryGetValue(assetPath, out prefab) || prefab == null)
        {
            prefab = Resources.Load<GameObject>(assetPath);
            cache[assetPath] = prefab;
        }
        return prefab;
    }

    public int PreloadAssets(IEnumerable<string> assetPaths)
    {
        if (assetPaths == null)
            return 0;
        int loaded = 0;
        foreach (string path in assetPaths)
        {
            if (LoadAsset(path) != null)
                loaded++;
        }
        return loaded;
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

        string key = string.IsNullOrEmpty(poolKey)
            ? "prefab:" + prefab.GetInstanceID()
            : poolKey;

        spawnCount++;
        PooledObject pooledObject = Take(key);
        if (pooledObject == null)
        {
            GameObject created = Instantiate(prefab);
            pooledObject = created.GetComponent<PooledObject>();
            if (pooledObject == null)
            {
                pooledObject = created.AddComponent<PooledObject>();
            }

            pooledObject.Owner = this;
            pooledObject.PoolKey = key;
            pooledObject.BaseScale = created.transform.localScale;
            newCount++;
        }
        else
        {
            reuseCount++;
        }

        Transform target = pooledObject.transform;
        target.SetParent(parent, false);
        target.position = position;
        target.rotation = rotation;
        target.localScale = pooledObject.BaseScale;

        pooledObject.IsSpawned = true;
        activeCount++;
        pooledObject.gameObject.SetActive(true);
        pooledObject.OnGet();
        return pooledObject.gameObject;
    }

    public GameObject SpawnFx(
        string assetPath,
        Vector3 position,
        Quaternion rotation,
        float lifetime)
    {
        GameObject effect = SpawnAsset(assetPath, position, rotation);
        if (effect == null)
            return null;

        PooledEffect pooledEffect = effect.GetComponent<PooledEffect>();
        if (pooledEffect == null)
        {
            pooledEffect = effect.AddComponent<PooledEffect>();
            PooledObject pooledObject = effect.GetComponent<PooledObject>();
            if (pooledObject != null)
                pooledObject.RefreshCallbacks();
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
        pooledObject.OnPut();
        releaseCount++;
        activeCount = Mathf.Max(0, activeCount - 1);

        Stack<PooledObject> pool = GetPool(pooledObject.PoolKey);
        if (!usePool || pool.Count >= poolLimit)
        {
            dropCount++;
            DestroyNow(target);
            return true;
        }

        target.SetActive(false);
        Transform targetRoot = target.transform;
        targetRoot.SetParent(Root(), false);
        targetRoot.localPosition = Vector3.zero;
        targetRoot.localRotation = Quaternion.identity;
        targetRoot.localScale = pooledObject.BaseScale;
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

    public void ClearPool()
    {
        foreach (KeyValuePair<string, Stack<PooledObject>> pair in pools)
        {
            Stack<PooledObject> pool = pair.Value;
            while (pool.Count > 0)
            {
                PooledObject pooledObject = pool.Pop();
                if (pooledObject != null)
                {
                    DestroyNow(pooledObject.gameObject);
                }
            }
        }

        pools.Clear();
        inactiveCount = 0;
    }

    public void ResetStats()
    {
        spawnCount = 0;
        newCount = 0;
        reuseCount = 0;
        releaseCount = 0;
        dropCount = 0;
    }

    public PoolStats Stats()
    {
        return new PoolStats
        {
            SpawnCount = spawnCount,
            NewCount = newCount,
            ReuseCount = reuseCount,
            ActiveCount = activeCount,
            ReleaseCount = releaseCount,
            InactiveCount = inactiveCount,
            DropCount = dropCount,
        };
    }

    public string StatsText()
    {
        PoolStats stats = Stats();
        return "ObjectPool requests=" + stats.SpawnCount +
            " created=" + stats.NewCount +
            " reused=" + stats.ReuseCount +
            " active=" + stats.ActiveCount +
            " recycled=" + stats.ReleaseCount +
            " inactive=" + stats.InactiveCount +
            " discarded=" + stats.DropCount;
    }

    private PooledObject Take(string poolKey)
    {
        Stack<PooledObject> pool;
        if (!usePool || !pools.TryGetValue(poolKey, out pool))
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

    private Stack<PooledObject> GetPool(string poolKey)
    {
        Stack<PooledObject> pool;
        if (!pools.TryGetValue(poolKey, out pool))
        {
            pool = new Stack<PooledObject>();
            pools.Add(poolKey, pool);
        }

        return pool;
    }

    private Transform Root()
    {
        if (poolRoot != null)
            return poolRoot;

        GameObject root = new GameObject("ObjectPool_Inactive");
        root.transform.SetParent(transform, false);
        root.SetActive(false);
        poolRoot = root.transform;
        return poolRoot;
    }

    private static void DestroyNow(GameObject target)
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

    private static void DestroyNow(Component target)
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

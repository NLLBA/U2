using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

public static class ObjectPoolBenchmark
{
    private static readonly int[] Counts = { 500, 1000, 2000 };

    private struct Result
    {
        public int Count;
        public string Mode;
        public double ElapsedMilliseconds;
        public long ManagedMemoryBefore;
        public long ManagedMemoryAtPeak;
        public long ManagedMemoryAfter;
        public long ManagedPeakIncrease;
        public long TotalAllocatedAtPeak;
        public double ThroughputPerSecond;
        public int Gen0Collections;
        public int Gen1Collections;
        public int Gen2Collections;
        public ObjectPoolStatistics Statistics;
    }

    [MenuItem("Tools/U2/Run Object Pool Benchmark")]
    public static void RunFromMenu()
    {
        RunBenchmark();
    }

    public static void RunFromCommandLine()
    {
        RunBenchmark();
    }

    private static void RunBenchmark()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Run the object pool benchmark outside Play Mode.");
            return;
        }

        GameObject host = null;
        GameObject prefab = null;
        try
        {
            host = new GameObject("ObjectPoolBenchmarkHost");
            ObjectPoolManager pool = host.AddComponent<ObjectPoolManager>();
            pool.maxInactivePerResource = 2500;

            prefab = new GameObject("ObjectPoolBenchmarkPrefab");
            prefab.SetActive(false);

            List<Result> results = new List<Result>();
            for (int i = 0; i < Counts.Length; i++)
            {
                int count = Counts[i];
                pool.ClearInactive();
                results.Add(Measure(pool, prefab, count, false));

                pool.ClearInactive();
                WarmPool(pool, prefab, count);
                results.Add(Measure(pool, prefab, count, true));
            }

            string path = WriteCsv(results);
            Debug.Log("Object pool benchmark complete: " + path);
        }
        finally
        {
            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
            }

            if (prefab != null)
            {
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }
    }

    private static void WarmPool(ObjectPoolManager pool, GameObject prefab, int count)
    {
        pool.poolingEnabled = true;
        List<GameObject> objects = SpawnMany(pool, prefab, count);
        ReleaseMany(pool, objects);
    }

    private static Result Measure(
        ObjectPoolManager pool,
        GameObject prefab,
        int count,
        bool poolingEnabled)
    {
        pool.poolingEnabled = poolingEnabled;
        pool.ResetStatistics();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long managedBefore = GC.GetTotalMemory(false);
        int gen0Before = GC.CollectionCount(0);
        int gen1Before = GC.CollectionCount(1);
        int gen2Before = GC.CollectionCount(2);
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<GameObject> objects = SpawnMany(pool, prefab, count);
        long managedAtPeak = GC.GetTotalMemory(false);
        long totalAllocatedAtPeak = Profiler.GetTotalAllocatedMemoryLong();
        ReleaseMany(pool, objects);
        stopwatch.Stop();
        long managedAfter = GC.GetTotalMemory(false);

        return new Result
        {
            Count = count,
            Mode = poolingEnabled ? "pool" : "instantiate-destroy",
            ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            ManagedMemoryBefore = managedBefore,
            ManagedMemoryAtPeak = managedAtPeak,
            ManagedMemoryAfter = managedAfter,
            ManagedPeakIncrease = Math.Max(0L, managedAtPeak - managedBefore),
            TotalAllocatedAtPeak = totalAllocatedAtPeak,
            ThroughputPerSecond = stopwatch.Elapsed.TotalSeconds <= 0d
                ? 0d
                : count / stopwatch.Elapsed.TotalSeconds,
            Gen0Collections = GC.CollectionCount(0) - gen0Before,
            Gen1Collections = GC.CollectionCount(1) - gen1Before,
            Gen2Collections = GC.CollectionCount(2) - gen2Before,
            Statistics = pool.GetStatistics(),
        };
    }

    private static List<GameObject> SpawnMany(
        ObjectPoolManager pool,
        GameObject prefab,
        int count)
    {
        List<GameObject> objects = new List<GameObject>(count);
        for (int i = 0; i < count; i++)
        {
            objects.Add(pool.Spawn(
                prefab,
                "benchmark:object",
                Vector3.zero,
                Quaternion.identity));
        }

        return objects;
    }

    private static void ReleaseMany(ObjectPoolManager pool, List<GameObject> objects)
    {
        for (int i = 0; i < objects.Count; i++)
        {
            pool.Release(objects[i]);
        }
    }

    private static string WriteCsv(List<Result> results)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string logDirectory = Path.Combine(projectRoot, "Logs");
        Directory.CreateDirectory(logDirectory);
        string path = Path.Combine(logDirectory, "ObjectPoolBenchmark.csv");

        StringBuilder csv = new StringBuilder();
        csv.AppendLine(
            "count,mode,elapsed_ms,throughput_per_second,managed_before,managed_peak," +
            "managed_peak_increase,managed_after,total_allocated_peak,gc_gen0,gc_gen1,gc_gen2," +
            "spawn_requests,instances_created,instances_reused," +
            "recycle_requests,inactive_count,discarded_count");

        for (int i = 0; i < results.Count; i++)
        {
            Result result = results[i];
            ObjectPoolStatistics stats = result.Statistics;
            csv.Append(result.Count).Append(',')
                .Append(result.Mode).Append(',')
                .Append(result.ElapsedMilliseconds.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                .Append(result.ThroughputPerSecond.ToString("F1", CultureInfo.InvariantCulture)).Append(',')
                .Append(result.ManagedMemoryBefore).Append(',')
                .Append(result.ManagedMemoryAtPeak).Append(',')
                .Append(result.ManagedPeakIncrease).Append(',')
                .Append(result.ManagedMemoryAfter).Append(',')
                .Append(result.TotalAllocatedAtPeak).Append(',')
                .Append(result.Gen0Collections).Append(',')
                .Append(result.Gen1Collections).Append(',')
                .Append(result.Gen2Collections).Append(',')
                .Append(stats.SpawnRequests).Append(',')
                .Append(stats.InstancesCreated).Append(',')
                .Append(stats.InstancesReused).Append(',')
                .Append(stats.RecycleRequests).Append(',')
                .Append(stats.InactiveCount).Append(',')
                .Append(stats.DiscardedCount).AppendLine();
        }

        File.WriteAllText(path, csv.ToString(), Encoding.UTF8);
        return path;
    }
}

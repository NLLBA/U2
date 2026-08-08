using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class Stage9ReportGenerator
{
    private const string MarkdownFileName = "Stage9BalanceReport.md";
    private const string CsvFileName = "Stage9BalanceResults.csv";

    [MenuItem("Tools/U2/Run Stage 9 Validation")]
    public static void RunFromMenu()
    {
        RunAll();
    }

    public static void RunFromCommandLine()
    {
        RunAll();
    }

    public static void RunAllFromCommandLine()
    {
        ObjectPoolBenchmark.RunFromCommandLine();
        RunAll();
    }

    private static void RunAll()
    {
        GameObject host = null;
        try
        {
            host = new GameObject("Stage9ReportGenerator");
            DifficultyDirector director = host.AddComponent<DifficultyDirector>();
            BossController boss = host.AddComponent<BossController>();
            boss.bossVisualResource = string.Empty;

            Stage9BalanceReport report = Stage9BalanceModel.BuildReport(
                GameDataMgr.Instance,
                director,
                boss,
                Stage9BalanceModel.DefaultTrialCount,
                Stage9BalanceModel.DefaultMaximumSeconds);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string logDirectory = Path.Combine(projectRoot, "Logs");
            Directory.CreateDirectory(logDirectory);
            string csvPath = Path.Combine(logDirectory, CsvFileName);
            string markdownPath = Path.Combine(logDirectory, MarkdownFileName);

            File.WriteAllText(csvPath, BuildCsv(report), new UTF8Encoding(true));
            File.WriteAllText(
                markdownPath,
                BuildMarkdown(report, logDirectory),
                new UTF8Encoding(true));

            Debug.Log("STAGE9_VALIDATION_OK\nReport: " + markdownPath + "\nData: " + csvPath);
        }
        finally
        {
            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }

    private static string BuildCsv(Stage9BalanceReport report)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine(
            "hero,difficulty,resource,hp,speed,volume,skill,trials,average_survival_seconds," +
            "median_survival_seconds,p90_survival_seconds,average_score,first_boss_reach_rate," +
            "first_boss_death_rate,second_boss_reach_rate,second_boss_death_rate");

        for (int i = 0; i < report.SimulationResults.Count; i++)
        {
            Stage9SimulationResult result = report.SimulationResults[i];
            csv.Append(result.HeroIndex).Append(',')
                .Append(result.DifficultyId).Append(',')
                .Append(EscapeCsv(result.HeroResource)).Append(',')
                .Append(result.StartingHp).Append(',')
                .Append(result.Speed).Append(',')
                .Append(result.Volume).Append(',')
                .Append(EscapeCsv(result.SkillName)).Append(',')
                .Append(result.TrialCount).Append(',')
                .Append(Format(result.AverageSurvivalTime, "F2")).Append(',')
                .Append(Format(result.MedianSurvivalTime, "F2")).Append(',')
                .Append(Format(result.P90SurvivalTime, "F2")).Append(',')
                .Append(Format(result.AverageScore, "F0")).Append(',')
                .Append(Format(result.FirstBossReachRate, "F4")).Append(',')
                .Append(Format(result.FirstBossDeathRate, "F4")).Append(',')
                .Append(Format(result.SecondBossReachRate, "F4")).Append(',')
                .Append(Format(result.SecondBossDeathRate, "F4")).AppendLine();
        }

        return csv.ToString();
    }

    private static string BuildMarkdown(Stage9BalanceReport report, string logDirectory)
    {
        StringBuilder markdown = new StringBuilder();
        markdown.AppendLine("# Stage 9 Balance and Acceptance Report");
        markdown.AppendLine();
        markdown.AppendLine("生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        markdown.AppendLine();
        markdown.AppendLine("## 范围与方法");
        markdown.AppendLine();
        markdown.AppendLine(
            "这是固定随机种子的自动平衡模拟，不是真人试玩数据。每个“战机 x 难度”组合执行 400 次，" +
            "单次最多模拟 480 秒。模型直接读取项目中的战机、难度、关卡、发射模式、动态威胁、" +
            "Boss 弹幕和计分公式；移动、体积、护盾及战机技能按下列概率假设折算。");
        markdown.AppendLine();

        AppendModelAssumptions(markdown);

        AppendPressureTable(markdown, report.PressureSnapshots);
        AppendCombinationTable(markdown, report.SimulationResults);
        AppendDifficultySummary(markdown, report.SimulationResults);
        AppendAircraftSummary(markdown, report.SimulationResults);
        AppendPoolBenchmark(markdown, Path.Combine(logDirectory, "ObjectPoolBenchmark.csv"));

        markdown.AppendLine("## 验收证据");
        markdown.AppendLine();
        markdown.AppendLine(
            "Unity EditMode 测试中的 `Stage9ValidationTests` 覆盖：3 档难度 x 5 架战机、压力递增、" +
            "固定种子复现、场景接线，以及“开始选择 -> 难度 -> 普通阶段增强 -> 第一轮 Boss -> " +
            "更难普通阶段 -> 第二轮 Boss -> 死亡结算 -> 计分 -> XML 排行榜重载”的完整流程。");
        markdown.AppendLine();
        markdown.AppendLine(
            "批处理模型不测量真实渲染帧率或真人躲避行为。对象池部分是 Editor 微基准，记录耗时、" +
            "吞吐、托管内存增长和 GC 次数，不把这些数据表述为游戏运行帧率。");

        return markdown.ToString();
    }

    private static void AppendModelAssumptions(StringBuilder markdown)
    {
        markdown.AppendLine("## 模型假设");
        markdown.AppendLine();
        markdown.AppendLine("- 综合压力 = 普通弹量/秒 x 普通弹速 x 0.6 + Boss 平均弹量/秒 x Boss 弹速。");
        markdown.AppendLine("- 每秒受击概率 = 1 - exp(-综合压力 x 暴露系数 x 减伤系数 x 0.0048)。");
        markdown.AppendLine("- 暴露系数 = (0.52 + 体积 x 0.055) / (0.78 + 速度 x 0.075)。");
        markdown.AppendLine("- 通用护盾按“护盾持续时间 / 技能冷却”的覆盖率折算 68% 防护效果。");
        markdown.AppendLine("- 1 号机清屏折算为 0.86 倍受击；2 号机按“技能冷却 + 3 秒”每次修复 2 HP。");
        markdown.AppendLine("- 3/4/5 号机分别按干扰、脉冲、推进技能覆盖率折算受击概率，最低总减伤系数为 0.35。");
        markdown.AppendLine("- 自动清除量仅用于分数估算，按综合压力在每秒 0.13 至 0.32 次之间线性取值。");
        markdown.AppendLine();
    }

    private static void AppendPressureTable(
        StringBuilder markdown,
        List<Stage9PressureSnapshot> snapshots)
    {
        markdown.AppendLine("## 压力快照");
        markdown.AppendLine();
        markdown.AppendLine("| 难度 | 时间点 | 状态 | 威胁 | 普通弹/秒 | 普通弹速 | Boss 平均弹/秒 | Boss 峰值弹/秒 | Boss 弹速 | 综合压力 |");
        markdown.AppendLine("|---|---|---|---:|---:|---:|---:|---:|---:|---:|");
        for (int i = 0; i < snapshots.Count; i++)
        {
            Stage9PressureSnapshot value = snapshots[i];
            markdown.Append("| ").Append(value.DifficultyName)
                .Append(" | ").Append(value.Label)
                .Append(" | ").Append(value.State)
                .Append(" | ").Append(value.ThreatLevel)
                .Append(" | ").Append(Format(value.NormalBulletsPerSecond, "F2"))
                .Append(" | ").Append(Format(value.NormalBulletSpeedMultiplier, "F2"))
                .Append(" | ").Append(Format(value.BossBulletsPerSecond, "F2"))
                .Append(" | ").Append(Format(value.BossPeakBulletsPerSecond, "F2"))
                .Append(" | ").Append(Format(value.BossBulletSpeedMultiplier, "F2"))
                .Append(" | ").Append(Format(value.CombinedPressure, "F2"))
                .AppendLine(" |");
        }
        markdown.AppendLine();
    }

    private static void AppendCombinationTable(
        StringBuilder markdown,
        List<Stage9SimulationResult> results)
    {
        markdown.AppendLine("## 战机 x 难度模拟");
        markdown.AppendLine();
        markdown.AppendLine("| 战机 | 难度 | HP | 速度 | 体积 | 技能 | 平均生存 | 中位数 | P90 | 平均分 | Boss 1 到达/死亡 | Boss 2 到达/死亡 |");
        markdown.AppendLine("|---:|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|");
        for (int i = 0; i < results.Count; i++)
        {
            Stage9SimulationResult value = results[i];
            markdown.Append("| ").Append(value.HeroIndex)
                .Append(" | ").Append(value.DifficultyName)
                .Append(" | ").Append(value.StartingHp)
                .Append(" | ").Append(value.Speed)
                .Append(" | ").Append(value.Volume)
                .Append(" | ").Append(value.SkillName)
                .Append(" | ").Append(Format(value.AverageSurvivalTime, "F1")).Append("s")
                .Append(" | ").Append(Format(value.MedianSurvivalTime, "F0")).Append("s")
                .Append(" | ").Append(Format(value.P90SurvivalTime, "F0")).Append("s")
                .Append(" | ").Append(Format(value.AverageScore, "F0"))
                .Append(" | ").Append(Percent(value.FirstBossReachRate)).Append(" / ")
                .Append(Percent(value.FirstBossDeathRate))
                .Append(" | ").Append(Percent(value.SecondBossReachRate)).Append(" / ")
                .Append(Percent(value.SecondBossDeathRate))
                .AppendLine(" |");
        }
        markdown.AppendLine();
    }

    private static void AppendDifficultySummary(
        StringBuilder markdown,
        List<Stage9SimulationResult> results)
    {
        markdown.AppendLine("## 难度平均值");
        markdown.AppendLine();
        markdown.AppendLine("| 难度 | 平均生存 | 平均分 | Boss 1 死亡率 | Boss 2 死亡率 |");
        markdown.AppendLine("|---|---:|---:|---:|---:|");
        foreach (IGrouping<int, Stage9SimulationResult> group in results.GroupBy(value => value.DifficultyId))
        {
            Stage9SimulationResult first = group.First();
            markdown.Append("| ").Append(first.DifficultyName)
                .Append(" | ").Append(Format(group.Average(value => value.AverageSurvivalTime), "F1")).Append("s")
                .Append(" | ").Append(Format(group.Average(value => value.AverageScore), "F0"))
                .Append(" | ").Append(Percent(group.Average(value => value.FirstBossDeathRate)))
                .Append(" | ").Append(Percent(group.Average(value => value.SecondBossDeathRate)))
                .AppendLine(" |");
        }
        markdown.AppendLine();
    }

    private static void AppendAircraftSummary(
        StringBuilder markdown,
        List<Stage9SimulationResult> results)
    {
        markdown.AppendLine("## 各战机跨难度平均值");
        markdown.AppendLine();
        markdown.AppendLine("| 战机 | 资源 | 技能 | 平均生存 | 平均分 |");
        markdown.AppendLine("|---:|---|---|---:|---:|");
        foreach (IGrouping<int, Stage9SimulationResult> group in results.GroupBy(value => value.HeroIndex))
        {
            Stage9SimulationResult first = group.First();
            markdown.Append("| ").Append(first.HeroIndex)
                .Append(" | ").Append(first.HeroResource)
                .Append(" | ").Append(first.SkillName)
                .Append(" | ").Append(Format(group.Average(value => value.AverageSurvivalTime), "F1")).Append("s")
                .Append(" | ").Append(Format(group.Average(value => value.AverageScore), "F0"))
                .AppendLine(" |");
        }
        markdown.AppendLine();
    }

    private static void AppendPoolBenchmark(StringBuilder markdown, string benchmarkPath)
    {
        markdown.AppendLine("## 对象池 Editor 微基准");
        markdown.AppendLine();
        if (!File.Exists(benchmarkPath))
        {
            markdown.AppendLine("生成报告时未找到 `ObjectPoolBenchmark.csv`。");
            markdown.AppendLine();
            return;
        }

        string[] lines = File.ReadAllLines(benchmarkPath);
        if (lines.Length < 2)
        {
            markdown.AppendLine("基准文件没有结果行。");
            markdown.AppendLine();
            return;
        }

        string[] headers = lines[0].Split(',');
        Dictionary<string, int> indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < headers.Length; i++)
        {
            indexes[headers[i]] = i;
        }

        markdown.AppendLine("| 数量 | 模式 | 耗时 | 吞吐/秒 | 托管内存峰值增量 | GC (0/1/2) | 创建 | 复用 | 丢弃 |");
        markdown.AppendLine("|---:|---|---:|---:|---:|---:|---:|---:|---:|");
        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            string[] fields = lines[lineIndex].Split(',');
            markdown.Append("| ").Append(GetField(fields, indexes, "count"))
                .Append(" | ").Append(GetField(fields, indexes, "mode"))
                .Append(" | ").Append(GetField(fields, indexes, "elapsed_ms")).Append("ms")
                .Append(" | ").Append(GetField(fields, indexes, "throughput_per_second"))
                .Append(" | ").Append(GetField(fields, indexes, "managed_peak_increase")).Append(" B")
                .Append(" | ").Append(GetField(fields, indexes, "gc_gen0")).Append('/')
                .Append(GetField(fields, indexes, "gc_gen1")).Append('/')
                .Append(GetField(fields, indexes, "gc_gen2"))
                .Append(" | ").Append(GetField(fields, indexes, "instances_created"))
                .Append(" | ").Append(GetField(fields, indexes, "instances_reused"))
                .Append(" | ").Append(GetField(fields, indexes, "discarded_count"))
                .AppendLine(" |");
        }
        markdown.AppendLine();
        AppendPoolComparisons(markdown, lines, indexes);
    }

    private static void AppendPoolComparisons(
        StringBuilder markdown,
        string[] lines,
        Dictionary<string, int> indexes)
    {
        Dictionary<string, string[]> baseline = new Dictionary<string, string[]>();
        Dictionary<string, string[]> pooled = new Dictionary<string, string[]>();
        for (int i = 1; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',');
            string count = GetField(fields, indexes, "count");
            string mode = GetField(fields, indexes, "mode");
            if (mode == "instantiate-destroy")
            {
                baseline[count] = fields;
            }
            else if (mode == "pool")
            {
                pooled[count] = fields;
            }
        }

        markdown.AppendLine("| 数量 | 耗时加速 | 托管内存峰值增量下降 | GC 变化 (0/1/2) |");
        markdown.AppendLine("|---:|---:|---:|---:|");
        foreach (string count in baseline.Keys.OrderBy(value => int.Parse(value, CultureInfo.InvariantCulture)))
        {
            string[] poolFields;
            if (!pooled.TryGetValue(count, out poolFields))
                continue;

            string[] baseFields = baseline[count];
            double baseTime = ParseDouble(GetField(baseFields, indexes, "elapsed_ms"));
            double poolTime = ParseDouble(GetField(poolFields, indexes, "elapsed_ms"));
            double baseMemory = ParseDouble(GetField(baseFields, indexes, "managed_peak_increase"));
            double poolMemory = ParseDouble(GetField(poolFields, indexes, "managed_peak_increase"));
            double speedup = poolTime > 0d ? baseTime / poolTime : 0d;
            double memoryReduction = baseMemory > 0d
                ? (baseMemory - poolMemory) / baseMemory
                : 0d;

            markdown.Append("| ").Append(count)
                .Append(" | ").Append(Format(speedup, "F2")).Append("x")
                .Append(" | ").Append(Percent(memoryReduction))
                .Append(" | ")
                .Append(GetField(baseFields, indexes, "gc_gen0")).Append('/')
                .Append(GetField(baseFields, indexes, "gc_gen1")).Append('/')
                .Append(GetField(baseFields, indexes, "gc_gen2")).Append(" -> ")
                .Append(GetField(poolFields, indexes, "gc_gen0")).Append('/')
                .Append(GetField(poolFields, indexes, "gc_gen1")).Append('/')
                .Append(GetField(poolFields, indexes, "gc_gen2"))
                .AppendLine(" |");
        }
        markdown.AppendLine();
    }

    private static double ParseDouble(string value)
    {
        double result;
        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result)
            ? result
            : 0d;
    }

    private static string GetField(
        string[] fields,
        Dictionary<string, int> indexes,
        string name)
    {
        int index;
        return indexes.TryGetValue(name, out index) && index >= 0 && index < fields.Length
            ? fields[index]
            : "n/a";
    }

    private static string Percent(double value)
    {
        return (value * 100d).ToString("F1", CultureInfo.InvariantCulture) + "%";
    }

    private static string Format(double value, string format)
    {
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string EscapeCsv(string value)
    {
        string safe = value ?? string.Empty;
        if (safe.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            return safe;

        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}

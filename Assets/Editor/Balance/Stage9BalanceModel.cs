using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class Stage9PressureSnapshot
{
    public int DifficultyId;
    public string DifficultyName;
    public string Label;
    public float SurvivalTime;
    public GameFlowState State;
    public int BossRound;
    public int CompletedBossRounds;
    public int ThreatLevel;
    public string LevelName;
    public float NormalBulletsPerSecond;
    public float NormalBulletSpeedMultiplier;
    public float BossBulletsPerSecond;
    public float BossPeakBulletsPerSecond;
    public float BossBulletSpeedMultiplier;

    public float CombinedPressure
    {
        get
        {
            return NormalBulletsPerSecond * NormalBulletSpeedMultiplier * 0.6f +
                BossBulletsPerSecond * BossBulletSpeedMultiplier;
        }
    }
}

public sealed class Stage9SimulationResult
{
    public int HeroIndex;
    public string HeroResource;
    public int DifficultyId;
    public string DifficultyName;
    public int StartingHp;
    public int Speed;
    public int Volume;
    public string SkillName;
    public int TrialCount;
    public float AverageSurvivalTime;
    public float MedianSurvivalTime;
    public float P90SurvivalTime;
    public float AverageScore;
    public float FirstBossReachRate;
    public float FirstBossDeathRate;
    public float SecondBossReachRate;
    public float SecondBossDeathRate;
}

public sealed class Stage9BalanceReport
{
    public readonly List<Stage9PressureSnapshot> PressureSnapshots = new List<Stage9PressureSnapshot>();
    public readonly List<Stage9SimulationResult> SimulationResults = new List<Stage9SimulationResult>();
}

public static class Stage9BalanceModel
{
    public const int EmitterCount = 8;
    public const int DefaultTrialCount = 400;
    public const int DefaultMaximumSeconds = 480;

    private const float FirstBossTime = 120f;
    private const float BossWarningTime = 3f;
    private const float BossDuration = 30f;
    private const float NormalInterval = 90f;

    private struct FlowPoint
    {
        public GameFlowState State;
        public int BossRound;
        public int CompletedBossRounds;
    }

    public static Stage9BalanceReport BuildReport(
        GameDataMgr data,
        DifficultyDirector director,
        BossController boss,
        int trialCount = DefaultTrialCount,
        int maximumSeconds = DefaultMaximumSeconds)
    {
        Stage9BalanceReport report = new Stage9BalanceReport();
        float[] snapshotTimes = { 60f, 120f, 144f, 264f };
        string[] snapshotLabels = { "60s", "120s / Boss 1 start", "Boss 1 peak", "Boss 2 peak" };

        for (int difficultyIndex = 0; difficultyIndex < data.difficultyData.difficultyInfos.Count; difficultyIndex++)
        {
            DifficultyInfo difficulty = data.difficultyData.difficultyInfos[difficultyIndex];
            for (int snapshotIndex = 0; snapshotIndex < snapshotTimes.Length; snapshotIndex++)
            {
                report.PressureSnapshots.Add(CalculateSnapshot(
                    data,
                    director,
                    boss,
                    difficulty,
                    snapshotTimes[snapshotIndex],
                    snapshotLabels[snapshotIndex]));
            }

            for (int heroIndex = 0; heroIndex < data.roleData.roleDatas.Count; heroIndex++)
            {
                report.SimulationResults.Add(SimulateCombination(
                    data,
                    director,
                    boss,
                    data.roleData.roleDatas[heroIndex],
                    heroIndex,
                    difficulty,
                    trialCount,
                    maximumSeconds));
            }
        }

        return report;
    }

    public static Stage9PressureSnapshot CalculateSnapshot(
        GameDataMgr data,
        DifficultyDirector director,
        BossController boss,
        DifficultyInfo difficulty,
        float survivalTime,
        string label)
    {
        FlowPoint flow = GetFlowPoint(survivalTime);
        LevelInfo level = GetLevelAtTime(data, survivalTime);
        int growthSteps = Mathf.Max(0,
            Mathf.FloorToInt(survivalTime / director.threatStepSeconds) +
            flow.CompletedBossRounds * director.bossThreatLevelBonus);

        float dynamicSpeed = Mathf.Min(
            1f + growthSteps * director.bulletSpeedGrowthPerLevel,
            director.maxBulletSpeedMultiplier);
        float normalSpeed = Mathf.Min(
            difficulty.bulletSpeedMul * level.bulletSpeedMul * dynamicSpeed,
            director.maxBulletSpeedMultiplier);

        float dynamicInterval = Mathf.Max(
            1f - growthSteps * director.fireIntervalReductionPerLevel,
            director.minFireIntervalMultiplier);
        float normalIntensity = flow.State == GameFlowState.BossWarning ||
            flow.State == GameFlowState.BossSurvival
            ? boss.normalFireIntensityDuringBoss
            : 1f;
        float normalRate = CalculateNormalBulletRate(
            data,
            difficulty,
            level,
            dynamicInterval) * normalIntensity;

        float bossRate = 0f;
        float bossPeakRate = 0f;
        float bossSpeed = 0f;
        if (flow.State == GameFlowState.BossSurvival)
        {
            int round = Mathf.Max(1, flow.BossRound);
            float roundSpeed = Mathf.Min(
                1f + (round - 1) * director.bossBulletSpeedGrowthPerRound,
                director.maxBossBulletSpeedMultiplier);
            bossSpeed = Mathf.Min(
                difficulty.bulletSpeedMul * roundSpeed,
                director.maxBossBulletSpeedMultiplier);

            float roundInterval = Mathf.Max(
                1f - (round - 1) * director.bossFireIntervalReductionPerRound,
                director.minBossFireIntervalMultiplier);
            float intervalMultiplier = Mathf.Max(
                difficulty.fireCdMul * roundInterval,
                director.minBossFireIntervalMultiplier);
            float attackInterval = Mathf.Max(0.16f, boss.baseAttackInterval * intervalMultiplier);

            float totalVolleyCount = 0f;
            float peakVolleyCount = 0f;
            BossAttackPattern[] patterns =
            {
                BossAttackPattern.Fan,
                BossAttackPattern.Spiral,
                BossAttackPattern.Cross,
                BossAttackPattern.Homing,
            };

            for (int i = 0; i < patterns.Length; i++)
            {
                float volleyCount = boss.GetPrimaryVolleyBulletCount(patterns[i], round);
                if (round >= 2 && patterns[i] != BossAttackPattern.Cross)
                {
                    volleyCount += 2f;
                }
                if (round >= 3 && patterns[i] != BossAttackPattern.Homing)
                {
                    volleyCount += 1f / 3f;
                }

                totalVolleyCount += volleyCount;
                peakVolleyCount = Mathf.Max(peakVolleyCount, volleyCount);
            }

            bossRate = totalVolleyCount / patterns.Length / attackInterval;
            bossPeakRate = peakVolleyCount / attackInterval;
        }

        return new Stage9PressureSnapshot
        {
            DifficultyId = difficulty.id,
            DifficultyName = difficulty.name,
            Label = label,
            SurvivalTime = survivalTime,
            State = flow.State,
            BossRound = flow.BossRound,
            CompletedBossRounds = flow.CompletedBossRounds,
            ThreatLevel = growthSteps + 1,
            LevelName = level.name,
            NormalBulletsPerSecond = normalRate,
            NormalBulletSpeedMultiplier = normalSpeed,
            BossBulletsPerSecond = bossRate,
            BossPeakBulletsPerSecond = bossPeakRate,
            BossBulletSpeedMultiplier = bossSpeed,
        };
    }

    public static Stage9SimulationResult SimulateCombination(
        GameDataMgr data,
        DifficultyDirector director,
        BossController boss,
        RoleInfo role,
        int heroIndex,
        DifficultyInfo difficulty,
        int trialCount,
        int maximumSeconds)
    {
        int safeTrials = Mathf.Max(1, trialCount);
        int safeMaximumSeconds = Mathf.Max(1, maximumSeconds);
        List<float> survivalTimes = new List<float>(safeTrials);
        double totalScore = 0d;
        int firstBossReached = 0;
        int firstBossDeaths = 0;
        int secondBossReached = 0;
        int secondBossDeaths = 0;

        // Pressure depends only on time and difficulty, so calculate it once per
        // combination instead of allocating fire-pattern collections per trial.
        float[] pressureBySecond = new float[safeMaximumSeconds];
        for (int second = 0; second < safeMaximumSeconds; second++)
        {
            Stage9PressureSnapshot snapshot = CalculateSnapshot(
                data, director, boss, difficulty, second + 0.5f, string.Empty);
            pressureBySecond[second] = snapshot.CombinedPressure;
        }

        for (int trial = 0; trial < safeTrials; trial++)
        {
            int seed = 7919 + heroIndex * 100003 + difficulty.id * 1009 + trial * 37;
            System.Random random = new System.Random(seed);
            int maximumHp = Mathf.Max(1, role.hp + difficulty.hpAdd);
            int hp = maximumHp;
            float survivalTime = safeMaximumSeconds;
            float pressureSum = 0f;

            for (int second = 0; second < safeMaximumSeconds; second++)
            {
                float pressure = pressureBySecond[second];
                pressureSum += pressure;

                ApplyRepairSkill(heroIndex, difficulty, second, maximumHp, ref hp);
                float mitigation = CalculateMitigation(role, heroIndex, difficulty, second);
                float exposure = (0.52f + Mathf.Clamp(role.volume, 1, 10) * 0.055f) /
                    (0.78f + Mathf.Clamp(role.speed, 1, 10) * 0.075f);
                float hitProbability = 1f - Mathf.Exp(-pressure * exposure * mitigation * 0.0048f);

                if (random.NextDouble() < hitProbability)
                {
                    hp--;
                    if (hp <= 0)
                    {
                        survivalTime = second + 1f;
                        break;
                    }
                }
            }

            FlowPoint deathFlow = GetFlowPoint(survivalTime);
            int completedRounds = deathFlow.CompletedBossRounds;
            float averagePressure = pressureSum / Mathf.Max(1f, survivalTime);
            int manualClears = Mathf.FloorToInt(
                survivalTime * Mathf.Clamp(0.13f + averagePressure * 0.006f, 0.13f, 0.32f));
            int rawScore = Mathf.FloorToInt(survivalTime) * 10 + manualClears * 25 +
                1000 * completedRounds * (completedRounds + 1) / 2;
            totalScore += Mathf.Round(rawScore * GetScoreCoefficient(difficulty.id));

            survivalTimes.Add(survivalTime);
            if (survivalTime >= FirstBossTime)
            {
                firstBossReached++;
            }
            if (survivalTime >= FirstBossTime && survivalTime < FirstBossTime + BossDuration)
            {
                firstBossDeaths++;
            }
            if (survivalTime >= FirstBossTime + BossDuration + NormalInterval)
            {
                secondBossReached++;
            }
            if (survivalTime >= FirstBossTime + BossDuration + NormalInterval &&
                survivalTime < FirstBossTime + BossDuration + NormalInterval + BossDuration)
            {
                secondBossDeaths++;
            }
        }

        survivalTimes.Sort();
        return new Stage9SimulationResult
        {
            HeroIndex = heroIndex + 1,
            HeroResource = role.resName,
            DifficultyId = difficulty.id,
            DifficultyName = difficulty.name,
            StartingHp = Mathf.Max(1, role.hp + difficulty.hpAdd),
            Speed = role.speed,
            Volume = role.volume,
            SkillName = GetSkillName(heroIndex),
            TrialCount = safeTrials,
            AverageSurvivalTime = survivalTimes.Average(),
            MedianSurvivalTime = Percentile(survivalTimes, 0.5f),
            P90SurvivalTime = Percentile(survivalTimes, 0.9f),
            AverageScore = (float)(totalScore / safeTrials),
            FirstBossReachRate = (float)firstBossReached / safeTrials,
            FirstBossDeathRate = firstBossReached > 0 ? (float)firstBossDeaths / firstBossReached : 0f,
            SecondBossReachRate = (float)secondBossReached / safeTrials,
            SecondBossDeathRate = secondBossReached > 0 ? (float)secondBossDeaths / secondBossReached : 0f,
        };
    }

    private static float CalculateNormalBulletRate(
        GameDataMgr data,
        DifficultyInfo difficulty,
        LevelInfo level,
        float dynamicInterval)
    {
        HashSet<int> unlockedFireIds = ParseIds(level.fireIds);
        List<FireInfo> candidates = data.fireData.fireInfoList
            .Where(info => unlockedFireIds.Count == 0 || unlockedFireIds.Contains(info.id))
            .ToList();
        if (candidates.Count == 0)
        {
            candidates.AddRange(data.fireData.fireInfoList);
        }
        if (candidates.Count == 0)
            return 0f;

        float totalRate = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            FireInfo fire = candidates[i];
            float cd = fire.cd <= 0f
                ? 0f
                : Mathf.Max(0.08f, fire.cd * difficulty.fireCdMul * level.fireCdMul * dynamicInterval);
            float delay = fire.delay <= 0f
                ? 0f
                : Mathf.Max(0.5f, fire.delay * difficulty.fireDelayMul * level.fireDelayMul * dynamicInterval);
            float cycleDuration = Mathf.Max(0.08f, delay + fire.num * cd);
            totalRate += Mathf.Max(0, fire.num) / cycleDuration;
        }

        return totalRate / candidates.Count * EmitterCount;
    }

    private static FlowPoint GetFlowPoint(float survivalTime)
    {
        float safeTime = Mathf.Max(0f, survivalTime);
        if (safeTime < FirstBossTime - BossWarningTime)
        {
            return new FlowPoint { State = GameFlowState.Normal };
        }
        if (safeTime < FirstBossTime)
        {
            return new FlowPoint { State = GameFlowState.BossWarning, BossRound = 1 };
        }

        float cycleLength = BossDuration + NormalInterval;
        int cycle = Mathf.FloorToInt((safeTime - FirstBossTime) / cycleLength);
        float cycleElapsed = safeTime - (FirstBossTime + cycle * cycleLength);
        if (cycleElapsed < BossDuration)
        {
            return new FlowPoint
            {
                State = GameFlowState.BossSurvival,
                BossRound = cycle + 1,
                CompletedBossRounds = cycle,
            };
        }

        int completed = cycle + 1;
        if (cycleElapsed >= cycleLength - BossWarningTime)
        {
            return new FlowPoint
            {
                State = GameFlowState.BossWarning,
                BossRound = completed + 1,
                CompletedBossRounds = completed,
            };
        }

        return new FlowPoint
        {
            State = GameFlowState.Normal,
            CompletedBossRounds = completed,
        };
    }

    private static LevelInfo GetLevelAtTime(GameDataMgr data, float survivalTime)
    {
        LevelInfo result = data.levelData.levelInfos[0];
        for (int i = 0; i < data.levelData.levelInfos.Count; i++)
        {
            if (survivalTime >= data.levelData.levelInfos[i].beginTime)
            {
                result = data.levelData.levelInfos[i];
            }
        }

        return result;
    }

    private static HashSet<int> ParseIds(string value)
    {
        HashSet<int> ids = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(value))
            return ids;

        string[] parts = value.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            int id;
            if (int.TryParse(parts[i], out id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static float CalculateMitigation(
        RoleInfo role,
        int heroIndex,
        DifficultyInfo difficulty,
        int second)
    {
        float shieldUptime = Mathf.Clamp01(difficulty.shieldTime / Mathf.Max(1f, difficulty.skillCD));
        float mitigation = 1f - shieldUptime * 0.68f;

        switch (heroIndex)
        {
            case 0:
                mitigation *= 0.86f;
                break;
            case 2:
                mitigation *= 1f - Mathf.Clamp01(5f / (difficulty.skillCD + 1f)) * 0.36f;
                break;
            case 3:
                mitigation *= 1f - Mathf.Clamp01(4f / (difficulty.skillCD + 4f)) * 0.48f;
                break;
            case 4:
                float boostUptime = Mathf.Clamp01(5f / Mathf.Max(1f, difficulty.skillCD));
                mitigation *= 1f - boostUptime * 0.24f;
                break;
        }

        return Mathf.Clamp(mitigation, 0.35f, 1f);
    }

    private static void ApplyRepairSkill(
        int heroIndex,
        DifficultyInfo difficulty,
        int second,
        int maximumHp,
        ref int hp)
    {
        if (heroIndex != 1 || second <= 0)
            return;

        int interval = Mathf.Max(1, Mathf.RoundToInt(difficulty.skillCD + 3f));
        if (second % interval == 0)
        {
            hp = Mathf.Min(maximumHp, hp + 2);
        }
    }

    private static string GetSkillName(int heroIndex)
    {
        string[] names = { "爆破清屏", "紧急维修", "电磁干扰", "能量脉冲", "疾航推进" };
        return names[Mathf.Clamp(heroIndex, 0, names.Length - 1)];
    }

    private static float GetScoreCoefficient(int difficultyId)
    {
        switch (difficultyId)
        {
            case 1:
                return 1f;
            case 3:
                return 1.6f;
            default:
                return 1.25f;
        }
    }

    private static float Percentile(List<float> sortedValues, float percentile)
    {
        if (sortedValues.Count == 0)
            return 0f;

        int index = Mathf.Clamp(
            Mathf.CeilToInt(percentile * sortedValues.Count) - 1,
            0,
            sortedValues.Count - 1);
        return sortedValues[index];
    }
}

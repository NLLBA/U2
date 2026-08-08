using UnityEngine;

public class DifficultyDirector : MonoBehaviour
{
    private static DifficultyDirector instance;

    public static DifficultyDirector Instance => instance;

    private GameFlowManager flowManager;

    [Min(1f)] public float threatStepSeconds = 30f;
    [Min(0)] public int bossThreatLevelBonus = 2;

    [Min(0f)] public float bulletSpeedGrowthPerLevel = 0.04f;
    [Min(1f)] public float maxBulletSpeedMultiplier = 2.2f;
    [Min(0f)] public float fireIntervalReductionPerLevel = 0.03f;
    [Range(0.05f, 1f)] public float minFireIntervalMultiplier = 0.45f;

    [Min(0f)] public float bossBulletSpeedGrowthPerRound = 0.12f;
    [Min(1f)] public float maxBossBulletSpeedMultiplier = 2.5f;
    [Min(0f)] public float bossFireIntervalReductionPerRound = 0.06f;
    [Range(0.05f, 1f)] public float minBossFireIntervalMultiplier = 0.45f;

    public int ThreatLevel => GetThreatGrowthSteps() + 1;

    public float NormalBulletSpeedMultiplier
    {
        get
        {
            float multiplier = 1f + GetThreatGrowthSteps() * bulletSpeedGrowthPerLevel;
            return Mathf.Min(multiplier, maxBulletSpeedMultiplier);
        }
    }

    public float NormalFireIntervalMultiplier
    {
        get
        {
            float multiplier = 1f - GetThreatGrowthSteps() * fireIntervalReductionPerLevel;
            return Mathf.Max(multiplier, minFireIntervalMultiplier);
        }
    }

    public int ScalingBossRound
    {
        get
        {
            GameFlowManager flow = GetFlowManager();
            if (flow == null)
                return 1;

            if (flow.State == GameFlowState.BossSurvival)
                return Mathf.Max(1, flow.CurrentBossRound);

            return Mathf.Max(1, flow.CompletedBossRounds + 1);
        }
    }

    public float BossBulletSpeedMultiplier
    {
        get
        {
            float multiplier = 1f + (ScalingBossRound - 1) * bossBulletSpeedGrowthPerRound;
            return Mathf.Min(multiplier, maxBossBulletSpeedMultiplier);
        }
    }

    public float BossFireIntervalMultiplier
    {
        get
        {
            float multiplier = 1f - (ScalingBossRound - 1) * bossFireIntervalReductionPerRound;
            return Mathf.Max(multiplier, minBossFireIntervalMultiplier);
        }
    }

    public static DifficultyDirector EnsureInstance(GameObject host = null)
    {
        if (instance != null)
            return instance;

        GameObject target = host != null ? host : new GameObject("DifficultyDirector");
        DifficultyDirector director = target.GetComponent<DifficultyDirector>();
        return director != null ? director : target.AddComponent<DifficultyDirector>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        flowManager = GetComponent<GameFlowManager>();
        ClampSettings();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnValidate()
    {
        ClampSettings();
    }

    public float ApplyNormalBulletSpeed(float baseMultiplier)
    {
        return Mathf.Min(baseMultiplier * NormalBulletSpeedMultiplier, maxBulletSpeedMultiplier);
    }

    public float ApplyNormalFireInterval(float baseMultiplier)
    {
        return baseMultiplier * NormalFireIntervalMultiplier;
    }

    public float ApplyBossBulletSpeed(float baseMultiplier)
    {
        return Mathf.Min(baseMultiplier * BossBulletSpeedMultiplier, maxBossBulletSpeedMultiplier);
    }

    public float ApplyBossFireInterval(float baseMultiplier)
    {
        return Mathf.Max(baseMultiplier * BossFireIntervalMultiplier, minBossFireIntervalMultiplier);
    }

    private int GetThreatGrowthSteps()
    {
        GameFlowManager flow = GetFlowManager();
        if (flow == null)
            return 0;

        int timeLevels = Mathf.FloorToInt(flow.SurvivalTime / threatStepSeconds);
        int bossLevels = flow.CompletedBossRounds * bossThreatLevelBonus;
        return Mathf.Max(0, timeLevels + bossLevels);
    }

    private GameFlowManager GetFlowManager()
    {
        if (flowManager == null)
        {
            flowManager = GetComponent<GameFlowManager>();
        }

        return flowManager != null ? flowManager : GameFlowManager.Instance;
    }

    private void ClampSettings()
    {
        threatStepSeconds = Mathf.Max(1f, threatStepSeconds);
        bossThreatLevelBonus = Mathf.Max(0, bossThreatLevelBonus);
        bulletSpeedGrowthPerLevel = Mathf.Max(0f, bulletSpeedGrowthPerLevel);
        maxBulletSpeedMultiplier = Mathf.Max(1f, maxBulletSpeedMultiplier);
        fireIntervalReductionPerLevel = Mathf.Max(0f, fireIntervalReductionPerLevel);
        minFireIntervalMultiplier = Mathf.Clamp(minFireIntervalMultiplier, 0.05f, 1f);
        bossBulletSpeedGrowthPerRound = Mathf.Max(0f, bossBulletSpeedGrowthPerRound);
        maxBossBulletSpeedMultiplier = Mathf.Max(1f, maxBossBulletSpeedMultiplier);
        bossFireIntervalReductionPerRound = Mathf.Max(0f, bossFireIntervalReductionPerRound);
        minBossFireIntervalMultiplier = Mathf.Clamp(minBossFireIntervalMultiplier, 0.05f, 1f);
    }
}

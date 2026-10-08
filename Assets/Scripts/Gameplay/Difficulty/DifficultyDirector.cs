using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class DifficultyDirector : MonoBehaviour
{
    private static DifficultyDirector instance;

    public static DifficultyDirector Instance => instance;

    private GameFlowManager flow;

    [FormerlySerializedAs("threatStepSeconds")]
    [Min(1f)] public float threatStep = 30f;
    [FormerlySerializedAs("bossThreatLevelBonus")]
    [Min(0)] public int bossThreat = 2;

    [FormerlySerializedAs("bulletSpeedGrowthPerLevel")]
    [Min(0f)] public float bulletGrowth = 0.04f;
    [FormerlySerializedAs("maxBulletSpeedMultiplier")]
    [Min(1f)] public float maxBulletSpeed = 2.2f;
    [FormerlySerializedAs("fireIntervalReductionPerLevel")]
    [Min(0f)] public float fireDrop = 0.03f;
    [FormerlySerializedAs("minFireIntervalMultiplier")]
    [Range(0.05f, 1f)] public float minFireRate = 0.45f;

    [FormerlySerializedAs("bossBulletSpeedGrowthPerRound")]
    [Min(0f)] public float bossGrowth = 0.12f;
    [FormerlySerializedAs("maxBossBulletSpeedMultiplier")]
    [Min(1f)] public float maxBossSpeed = 2.5f;
    [FormerlySerializedAs("bossFireIntervalReductionPerRound")]
    [Min(0f)] public float bossFireDrop = 0.06f;
    [FormerlySerializedAs("minBossFireIntervalMultiplier")]
    [Range(0.05f, 1f)] public float minBossRate = 0.45f;

    public int ThreatLevel => Growth() + 1;

    public float BulletRate
    {
        get
        {
            float rate = 1f + Growth() * bulletGrowth;
            return Mathf.Min(rate, maxBulletSpeed);
        }
    }

    public float FireRate
    {
        get
        {
            float rate = 1f - Growth() * fireDrop;
            return Mathf.Max(rate, minFireRate);
        }
    }

    public int BossRound
    {
        get
        {
            GameFlowManager flow = GetFlow();
            if (flow == null)
                return 1;

            if (flow.State == GameFlowState.BossSurvival)
                return Mathf.Max(1, flow.BossRound);

            return Mathf.Max(1, flow.BossRounds + 1);
        }
    }

    public float BossBulletRate
    {
        get
        {
            float rate = 1f + (BossRound - 1) * bossGrowth;
            return Mathf.Min(rate, maxBossSpeed);
        }
    }

    public float BossFireRate
    {
        get
        {
            float rate = 1f - (BossRound - 1) * bossFireDrop;
            return Mathf.Max(rate, minBossRate);
        }
    }

    public static DifficultyDirector Ensure(GameObject host = null)
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
        flow = GetComponent<GameFlowManager>();
        ClampValues();
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
        ClampValues();
    }

    public float ScaleBullet(float baseScale)
    {
        return Mathf.Min(baseScale * BulletRate, maxBulletSpeed);
    }

    public float ScaleFire(float baseScale)
    {
        return baseScale * FireRate;
    }

    public float ScaleBossBullet(float baseScale)
    {
        return Mathf.Min(baseScale * BossBulletRate, maxBossSpeed);
    }

    public float ScaleBossFire(float baseScale)
    {
        return Mathf.Max(baseScale * BossFireRate, minBossRate);
    }

    private int Growth()
    {
        GameFlowManager flow = GetFlow();
        if (flow == null)
            return 0;

        int timeLevel = Mathf.FloorToInt(flow.RunTime / threatStep);
        int bossLevel = flow.BossRounds * bossThreat;
        return Mathf.Max(0, timeLevel + bossLevel);
    }

    private GameFlowManager GetFlow()
    {
        if (flow == null)
        {
            flow = GetComponent<GameFlowManager>();
        }

        return flow != null ? flow : GameFlowManager.Instance;
    }

    private void ClampValues()
    {
        threatStep = Mathf.Max(1f, threatStep);
        bossThreat = Mathf.Max(0, bossThreat);
        bulletGrowth = Mathf.Max(0f, bulletGrowth);
        maxBulletSpeed = Mathf.Max(1f, maxBulletSpeed);
        fireDrop = Mathf.Max(0f, fireDrop);
        minFireRate = Mathf.Clamp(minFireRate, 0.05f, 1f);
        bossGrowth = Mathf.Max(0f, bossGrowth);
        maxBossSpeed = Mathf.Max(1f, maxBossSpeed);
        bossFireDrop = Mathf.Max(0f, bossFireDrop);
        minBossRate = Mathf.Clamp(minBossRate, 0.05f, 1f);
    }
}

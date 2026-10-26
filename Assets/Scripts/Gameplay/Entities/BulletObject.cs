using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BulletSource
{
    Normal,
    Boss,
}

public enum BulletInteraction
{
    Normal,
    ClusterCore,
    ClusterDecoy,
    ReflectCore,
}

public class BulletObject : MonoBehaviour, IPoolable
{
    private static readonly HashSet<BulletObject> activeBullets = new HashSet<BulletObject>();

    private BulletInfo info;
    private float time;
    private bool isDead;
    private float speedRate = 1f;
    private float chainRadius;
    private float trackingTime = float.PositiveInfinity;
    private Vector3 baseScale;
    private MaterialPropertyBlock visualBlock;
    private bool rageVisual;
    private bool registered;
    private Renderer[] cachedRenderers;

    public BulletSource Source { get; private set; }
    public bool IsDead => isDead;
    public float ChainRadius => chainRadius;
    public int EncounterWaveId { get; private set; }
    public BulletInteraction Interaction { get; private set; }
    public bool RageVisual => rageVisual;

    public static bool TryPlayerClearNearScreen(Camera camera, Vector2 screenPosition)
    {
        if (camera == null)
            return false;

        BulletObject nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (BulletObject bullet in activeBullets)
        {
            if (bullet == null || bullet.isDead ||
                (bullet.Interaction != BulletInteraction.ClusterCore &&
                 bullet.Interaction != BulletInteraction.ReflectCore))
                continue;

            Vector3 point = camera.WorldToScreenPoint(bullet.transform.position);
            if (point.z <= 0f)
                continue;
            float radius = bullet.Interaction == BulletInteraction.ClusterCore ? 54f : 46f;
            if (bullet.Interaction == BulletInteraction.ReflectCore && PlayerObject.HasStageAffinity(5))
                radius *= 1.3f;
            float distance = ((Vector2)point - screenPosition).sqrMagnitude;
            if (distance <= radius * radius && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = bullet;
            }
        }

        return nearest != null && nearest.PlayerClear();
    }

    public static void ClearByPlayer()
    {
        BulletObject[] snapshot = Snapshot();
        int awardedWave = 0;
        foreach (BulletObject bullet in snapshot)
        {
            if (bullet == null || bullet.isDead)
                continue;
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.RegisterClear();
            if (awardedWave == 0 && bullet.EncounterWaveId > 0)
                awardedWave = bullet.EncounterWaveId;
            bullet.Dead();
        }
        if (awardedWave > 0 && ScoreManager.Instance != null)
            ScoreManager.Instance.RegisterClusterWave(awardedWave);
    }

    private void Awake()
    {
        baseScale = transform.localScale;
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        Register();
    }

    private void OnDestroy()
    {
        activeBullets.Remove(this);
        registered = false;
    }

    private void OnDisable()
    {
        activeBullets.Remove(this);
        registered = false;
    }

    public static void ClearAll()
    {
        BulletObject[] bullets = Snapshot();
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null)
            {
                bullets[i].Dead();
            }
        }
    }

    public static int ClearBoss()
    {
        int count = 0;
        BulletObject[] bullets = Snapshot();
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null && bullets[i].Source == BulletSource.Boss)
            {
                bullets[i].Dead(false);
                count++;
            }
        }

        return count;
    }

    public void Init(BulletInfo info, BulletSource source = BulletSource.Normal, float speedRate = 1f,
        float chainRadius = 0f, float trackingTime = float.PositiveInfinity)
    {
        this.info = info;
        Source = source;
        this.speedRate = Mathf.Max(0f, source == BulletSource.Normal
            ? GameDataMgr.Instance.BulletSpeed() * speedRate
            : speedRate);
        this.chainRadius = Mathf.Max(0f, chainRadius);
        this.trackingTime = Mathf.Max(0f, trackingTime);
        EncounterWaveId = 0;
        ResetInteraction();
        time = 0f;
        isDead = false;
        CancelInvoke();

        if (info == null)
        {
            GameLog.Error(GameLogCategory.Data, "BULLET_NULL", "Bullet info is null.");
            Dead(false);
            return;
        }

        if (Application.isPlaying)
        {
            Invoke(nameof(AutoDestroy), info.life);
        }
    }

    private void AutoDestroy()
    {
        Dead();
    }

    public void Dead()
    {
        Dead(true);
    }

    public bool PlayerClear()
    {
        if (isDead)
            return false;

        if (Interaction == BulletInteraction.ReflectCore)
        {
            if (BossController.Instance != null)
                BossController.Instance.RegisterReflectedHit(transform.position);
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.RegisterClear();
            Dead();
            return true;
        }

        if (Interaction == BulletInteraction.ClusterDecoy)
        {
            if (LevelEncounterDirector.Instance != null)
                LevelEncounterDirector.Instance.SplitClusterDecoy(transform.position);
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.RegisterClear();
                ScoreManager.Instance.RegisterMistake(MissionMistakeKind.WrongClick);
            }
            Dead();
            return true;
        }

        CampaignMissionController mission = CampaignMissionController.Instance;
        if (Interaction == BulletInteraction.Normal && mission != null && mission.IsActiveCampaign &&
            mission.Level != null && mission.Level.id == 5 && ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RegisterMistake(MissionMistakeKind.WrongReflection);
        }

        // Collect the connected cluster before releasing any pooled objects.
        List<BulletObject> connected = new List<BulletObject> { this };
        BulletObject[] snapshot = Snapshot();
        for (int i = 0; i < connected.Count; i++)
        {
            BulletObject origin = connected[i];
            if (origin.chainRadius <= 0f || Interaction != BulletInteraction.ClusterCore)
                continue;
            foreach (BulletObject other in snapshot)
            {
                if (other == null || other.isDead || other.chainRadius <= 0f || connected.Contains(other))
                    continue;
                if (origin.EncounterWaveId > 0 && other.EncounterWaveId > 0 &&
                    origin.EncounterWaveId != other.EncounterWaveId)
                    continue;
                if ((origin.transform.position - other.transform.position).sqrMagnitude <=
                    origin.chainRadius * origin.chainRadius)
                    connected.Add(other);
            }
        }

        HashSet<int> completedWaves = new HashSet<int>();
        foreach (BulletObject bullet in connected)
        {
            int waveId = bullet.EncounterWaveId;
            if (waveId <= 0 || completedWaves.Contains(waveId))
                continue;

            bool hasUnclearedBullet = false;
            foreach (BulletObject other in snapshot)
            {
                if (other != null && !other.isDead && other.EncounterWaveId == waveId &&
                    !connected.Contains(other))
                {
                    hasUnclearedBullet = true;
                    break;
                }
            }
            if (!hasUnclearedBullet)
                completedWaves.Add(waveId);
        }

        foreach (BulletObject bullet in connected)
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.RegisterClear();
            bullet.Dead();
        }
        if (ScoreManager.Instance != null)
        {
            foreach (int waveId in completedWaves)
                ScoreManager.Instance.RegisterClusterWave(waveId);
        }
        return true;
    }

    public void AssignEncounterWave(int waveId)
    {
        EncounterWaveId = Mathf.Max(0, waveId);
    }

    public void ConfigureInteraction(BulletInteraction interaction)
    {
        Interaction = interaction;
        ApplyInteractionVisual();
    }

    public void ConfigureRageVisual(bool enabled)
    {
        rageVisual = enabled;
        ApplyInteractionVisual();
    }

    public void Dead(bool showFx)
    {
        if (isDead)
            return;

        isDead = true;
        CancelInvoke();

        if (showFx && info != null && !string.IsNullOrEmpty(info.deathFx))
        {
            ObjectPoolManager.Ensure().SpawnFx(
                info.deathFx,
                transform.position,
                Quaternion.identity,
                1f);
        }

        if (ObjectPoolManager.TryRelease(gameObject))
            return;

        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    public void OnAcquire()
    {
        CancelInvoke();
        info = null;
        time = 0f;
        isDead = false;
        Source = BulletSource.Normal;
        speedRate = 1f;
        chainRadius = 0f;
        trackingTime = float.PositiveInfinity;
        EncounterWaveId = 0;
        ResetInteraction();
        Register();
    }

    public void OnRelease()
    {
        CancelInvoke();
        info = null;
        time = 0f;
        isDead = true;
        Source = BulletSource.Normal;
        speedRate = 1f;
        chainRadius = 0f;
        trackingTime = float.PositiveInfinity;
        EncounterWaveId = 0;
        ResetInteraction();
    }

    private void Register()
    {
        if (!registered)
            registered = activeBullets.Add(this);
    }

    private void ResetInteraction()
    {
        Interaction = BulletInteraction.Normal;
        rageVisual = false;
        if (baseScale == Vector3.zero)
            baseScale = transform.localScale;
        transform.localScale = baseScale;
        CacheRenderers();
        for (int i = 0; i < cachedRenderers.Length; i++)
            if (cachedRenderers[i] != null)
                cachedRenderers[i].SetPropertyBlock(null);
    }

    private void ApplyInteractionVisual()
    {
        if (Interaction != BulletInteraction.ClusterCore && Interaction != BulletInteraction.ReflectCore && !rageVisual)
            return;

        if (visualBlock == null)
            visualBlock = new MaterialPropertyBlock();
        Color color = Interaction == BulletInteraction.ReflectCore
            ? new Color(1f, 0.72f, 0.08f, 1f)
            : (Interaction == BulletInteraction.ClusterCore
                ? new Color(0.2f, 1f, 0.95f, 1f)
                : new Color(1f, 0.08f, 0.28f, 1f));
        visualBlock.Clear();
        visualBlock.SetColor("_Color", color);
        visualBlock.SetColor("_BaseColor", color);
        CacheRenderers();
        for (int i = 0; i < cachedRenderers.Length; i++)
            if (cachedRenderers[i] != null)
                cachedRenderers[i].SetPropertyBlock(visualBlock);
        float scale = Interaction == BulletInteraction.ReflectCore ? 1.5f :
            (Interaction == BulletInteraction.ClusterCore ? 1.3f : 1.08f);
        transform.localScale = baseScale * scale;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead)
            return;

        if (other.gameObject.CompareTag("Player"))
        {
            PlayerObject obj = other.GetComponent<PlayerObject>();
            if (obj != null)
            {
                obj.Wound();
            }

            Dead();
        }
    }

    void Update()
    {
        if (info == null || isDead)
            return;

        float speed = speedRate * PlayerObject.BulletRate;
        time += Time.deltaTime;
        if (Interaction == BulletInteraction.ClusterCore || Interaction == BulletInteraction.ReflectCore)
        {
            float baseMultiplier = Interaction == BulletInteraction.ReflectCore ? 1.5f : 1.3f;
            float pulse = 1f + Mathf.Sin(time * 8f) * 0.12f;
            transform.localScale = baseScale * baseMultiplier * pulse;
        }
        transform.Translate(Vector3.forward * info.moveRate * speed * Time.deltaTime);

        switch (info.type)
        {
            case 2:
                transform.Translate(Vector3.right * Time.deltaTime * Mathf.Sin(time * info.turnSpeed) * info.sideSpeed);
                break;
            case 3:
                transform.rotation *= Quaternion.AngleAxis(info.turnSpeed * Time.deltaTime, Vector3.up);
                break;
            case 4:
                transform.rotation *= Quaternion.AngleAxis(-info.turnSpeed * Time.deltaTime, Vector3.up);
                break;
            case 5:
                if (time <= trackingTime && PlayerObject.Instance != null && !PlayerObject.Instance.isDead)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(PlayerObject.Instance.transform.position - transform.position),
                        info.turnSpeed * Time.deltaTime);
                }
                break;
        }
    }

    private void CacheRenderers()
    {
        if (cachedRenderers == null)
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private static BulletObject[] Snapshot()
    {
        BulletObject[] snapshot = new BulletObject[activeBullets.Count];
        activeBullets.CopyTo(snapshot);
        return snapshot;
    }
}

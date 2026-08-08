using UnityEngine;

public enum BossAttackPattern
{
    Fan,
    Spiral,
    Cross,
    Homing,
}

public class BossController : MonoBehaviour
{
    private static BossController instance;

    public static BossController Instance => instance;

    [Range(0f, 1f)] public float normalFireIntensityDuringBoss = 0.25f;
    [Min(0.1f)] public float baseAttackInterval = 1.2f;
    [Min(1f)] public float bossVisualScale = 2.5f;
    public string bossVisualResource = "Airplane/Airplane5";
    public string[] bossVisualResources =
    {
        "Bosses/CrimsonManta",
        "Bosses/AzureBastion",
        "Bosses/SolarSeraph",
    };

    public bool IsBossActive { get; private set; }
    public int ActiveRound { get; private set; }
    public BossAttackPattern CurrentPattern { get; private set; }
    public int SpawnedBulletCount { get; private set; }

    private GameFlowManager flowManager;
    private GameObject bossObject;
    private float attackTimer;
    private float spiralAngle;
    private int volleyIndex;

    public static BossController EnsureInstance(GameObject host = null)
    {
        if (instance != null)
            return instance;

        GameObject target = host != null ? host : new GameObject("BossController");
        BossController controller = target.GetComponent<BossController>();
        return controller != null ? controller : target.AddComponent<BossController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            DestroyComponent(this);
            return;
        }

        instance = this;
        normalFireIntensityDuringBoss = Mathf.Clamp01(normalFireIntensityDuringBoss);
        baseAttackInterval = Mathf.Max(0.1f, baseAttackInterval);
        bossVisualScale = Mathf.Max(1f, bossVisualScale);
        BindFlowManager();

        if (flowManager != null)
        {
            HandleFlowStateChanged(flowManager.State);
        }
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        if (flowManager != null)
        {
            flowManager.StateChanged -= HandleFlowStateChanged;
        }

        EndBoss();
        FireObject.SetGlobalIntensityScale(1f);

        instance = null;
    }

    private void OnValidate()
    {
        normalFireIntensityDuringBoss = Mathf.Clamp01(normalFireIntensityDuringBoss);
        baseAttackInterval = Mathf.Max(0.1f, baseAttackInterval);
        bossVisualScale = Mathf.Max(1f, bossVisualScale);
    }

    private void Update()
    {
        if (flowManager == null)
        {
            BindFlowManager();
        }

        if (flowManager == null || flowManager.State != GameFlowState.BossSurvival || !IsBossActive)
            return;

        UpdateBossPosition();

        float progress = flowManager.bossDuration <= 0f
            ? 0f
            : flowManager.StateElapsedTime / flowManager.bossDuration;
        BossAttackPattern nextPattern = GetPatternAtNormalizedTime(progress);
        if (nextPattern != CurrentPattern)
        {
            CurrentPattern = nextPattern;
            attackTimer = 0f;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f)
            return;

        FireCurrentPattern();
        attackTimer = GameDataMgr.Instance.GetBossFireInterval(baseAttackInterval);
    }

    public BossAttackPattern GetPatternAtNormalizedTime(float normalizedTime)
    {
        float progress = Mathf.Clamp01(normalizedTime);
        if (progress < 0.25f)
            return BossAttackPattern.Fan;
        if (progress < 0.5f)
            return BossAttackPattern.Spiral;
        if (progress < 0.75f)
            return BossAttackPattern.Cross;
        return BossAttackPattern.Homing;
    }

    public int GetPrimaryVolleyBulletCount(BossAttackPattern pattern, int round)
    {
        int safeRound = Mathf.Max(1, round);
        switch (pattern)
        {
            case BossAttackPattern.Fan:
                return Mathf.Min(5 + (safeRound - 1) * 2, 13);
            case BossAttackPattern.Spiral:
                return Mathf.Min(4 + safeRound, 10);
            case BossAttackPattern.Cross:
                return safeRound >= 2 ? 8 : 4;
            case BossAttackPattern.Homing:
                return Mathf.Min(2 + safeRound, 6);
            default:
                return 1;
        }
    }

    public string GetBossVisualResource(int round)
    {
        if (string.IsNullOrEmpty(bossVisualResource))
            return string.Empty;

        if (bossVisualResources != null && bossVisualResources.Length > 0)
        {
            int index = (Mathf.Max(1, round) - 1) % bossVisualResources.Length;
            string selectedResource = bossVisualResources[index];
            if (!string.IsNullOrEmpty(selectedResource))
            {
                return selectedResource;
            }
        }

        return bossVisualResource;
    }

    public string GetPatternName()
    {
        switch (CurrentPattern)
        {
            case BossAttackPattern.Fan:
                return "扇形";
            case BossAttackPattern.Spiral:
                return "旋转";
            case BossAttackPattern.Cross:
                return "交叉";
            case BossAttackPattern.Homing:
                return "追踪";
            default:
                return string.Empty;
        }
    }

    private void BindFlowManager()
    {
        GameFlowManager nextFlow = GetComponent<GameFlowManager>();
        if (nextFlow == null)
        {
            nextFlow = GameFlowManager.Instance;
        }

        if (flowManager == nextFlow)
            return;

        if (flowManager != null)
        {
            flowManager.StateChanged -= HandleFlowStateChanged;
        }

        flowManager = nextFlow;
        if (flowManager != null)
        {
            flowManager.StateChanged += HandleFlowStateChanged;
        }
    }

    private void HandleFlowStateChanged(GameFlowState state)
    {
        switch (state)
        {
            case GameFlowState.BossWarning:
                FireObject.SetGlobalIntensityScale(normalFireIntensityDuringBoss);
                break;
            case GameFlowState.BossSurvival:
                FireObject.SetGlobalIntensityScale(normalFireIntensityDuringBoss);
                if (!IsBossActive)
                {
                    BeginBoss();
                }
                break;
            case GameFlowState.Normal:
                EndBoss();
                FireObject.SetGlobalIntensityScale(1f);
                break;
            case GameFlowState.GameOver:
                EndBoss();
                break;
        }
    }

    private void BeginBoss()
    {
        IsBossActive = true;
        ActiveRound = flowManager != null ? Mathf.Max(1, flowManager.CurrentBossRound) : 1;
        CurrentPattern = BossAttackPattern.Fan;
        SpawnedBulletCount = 0;
        attackTimer = 0f;
        spiralAngle = 0f;
        volleyIndex = 0;
        CreateBossVisual();
    }

    private void EndBoss()
    {
        BulletObject.ClearBossBullets();

        if (bossObject != null)
        {
            if (!ObjectPoolManager.TryRelease(bossObject))
            {
                DestroyObject(bossObject);
            }
            bossObject = null;
        }

        IsBossActive = false;
        ActiveRound = 0;
        attackTimer = 0f;
    }

    private void CreateBossVisual()
    {
        string visualResource = GetBossVisualResource(ActiveRound);
        GameObject prefab = string.IsNullOrEmpty(visualResource)
            ? null
            : Resources.Load<GameObject>(visualResource);

        if (prefab != null)
        {
            bossObject = ObjectPoolManager.EnsureInstance().SpawnResource(
                visualResource,
                Vector3.zero,
                Quaternion.identity);
        }
        else
        {
            bossObject = new GameObject("BossVisual");
        }

        bossObject.name = "SurvivalBoss_Round" + ActiveRound;
        bossObject.tag = "Untagged";
        bossObject.transform.localScale = Vector3.one * bossVisualScale;
        bossObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        Collider[] colliders = bossObject.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] rigidbodies = bossObject.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
        }

        UpdateBossPosition();
    }

    private void UpdateBossPosition()
    {
        if (bossObject == null)
            return;

        float horizontalOffset = Mathf.Sin(Time.time * 0.7f) * 0.13f;
        bossObject.transform.position = GetArenaPosition(0.5f + horizontalOffset, 0.82f);
    }

    private Vector3 GetArenaPosition(float viewportX, float viewportY)
    {
        if (Camera.main == null)
            return new Vector3(0f, 0f, 80f);

        float distance = Mathf.Abs(Camera.main.transform.position.y);
        Vector3 screenPosition = new Vector3(
            Screen.width * Mathf.Clamp01(viewportX),
            Screen.height * Mathf.Clamp01(viewportY),
            distance);
        return Camera.main.ScreenToWorldPoint(screenPosition);
    }

    private void FireCurrentPattern()
    {
        switch (CurrentPattern)
        {
            case BossAttackPattern.Fan:
                FireFan(GetPrimaryVolleyBulletCount(CurrentPattern, ActiveRound));
                break;
            case BossAttackPattern.Spiral:
                FireSpiral(GetPrimaryVolleyBulletCount(CurrentPattern, ActiveRound));
                break;
            case BossAttackPattern.Cross:
                FireCross(GetPrimaryVolleyBulletCount(CurrentPattern, ActiveRound));
                break;
            case BossAttackPattern.Homing:
                FireHoming(GetPrimaryVolleyBulletCount(CurrentPattern, ActiveRound));
                break;
        }

        volleyIndex++;
        if (ActiveRound >= 2 && CurrentPattern != BossAttackPattern.Cross && volleyIndex % 2 == 0)
        {
            FireCross(4);
        }

        if (ActiveRound >= 3 && CurrentPattern != BossAttackPattern.Homing && volleyIndex % 3 == 0)
        {
            FireHoming(1);
        }
    }

    private void FireFan(int count)
    {
        Vector3 aimDirection = GetPlayerDirection();
        float span = Mathf.Min(70f + (ActiveRound - 1) * 8f, 120f);
        for (int i = 0; i < count; i++)
        {
            float angle = count <= 1 ? 0f : Mathf.Lerp(-span * 0.5f, span * 0.5f, i / (count - 1f));
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * aimDirection;
            CreateBossBullet(GetStraightBulletId(), direction);
        }
    }

    private void FireSpiral(int count)
    {
        float angleStep = 360f / count;
        for (int i = 0; i < count; i++)
        {
            Vector3 direction = Quaternion.AngleAxis(spiralAngle + angleStep * i, Vector3.up) * Vector3.forward;
            int bulletId = i % 2 == 0 ? 9 + GetColorIndex() : 13 + GetColorIndex();
            CreateBossBullet(bulletId, direction);
        }

        spiralAngle = (spiralAngle + 18f + ActiveRound * 3f) % 360f;
    }

    private void FireCross(int count)
    {
        float angleStep = 360f / count;
        float baseAngle = volleyIndex * (7f + ActiveRound);
        for (int i = 0; i < count; i++)
        {
            Vector3 direction = Quaternion.AngleAxis(baseAngle + angleStep * i, Vector3.up) * Vector3.forward;
            CreateBossBullet(GetStraightBulletId(), direction);
        }
    }

    private void FireHoming(int count)
    {
        Vector3 aimDirection = GetPlayerDirection();
        for (int i = 0; i < count; i++)
        {
            float center = (count - 1) * 0.5f;
            float angle = (i - center) * 12f;
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * aimDirection;
            CreateBossBullet(17 + GetColorIndex(), direction);
        }
    }

    private Vector3 GetPlayerDirection()
    {
        if (bossObject != null && PlayerObject.Instance != null)
        {
            Vector3 direction = PlayerObject.Instance.transform.position - bossObject.transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }
        }

        return Vector3.back;
    }

    private int GetStraightBulletId()
    {
        return 1 + GetColorIndex();
    }

    private int GetColorIndex()
    {
        return (Mathf.Max(1, ActiveRound) - 1) % 4;
    }

    private void CreateBossBullet(int bulletId, Vector3 direction)
    {
        if (bossObject == null || direction.sqrMagnitude < 0.001f)
            return;

        BulletInfo info = GameDataMgr.Instance.GetBulletInfoById(bulletId);
        if (info == null || string.IsNullOrEmpty(info.resName))
            return;

        GameObject bullet = ObjectPoolManager.EnsureInstance().SpawnResource(
            info.resName,
            bossObject.transform.position,
            Quaternion.identity);
        if (bullet == null)
            return;

        BulletObject bulletObject = bullet.GetComponent<BulletObject>();
        if (bulletObject == null)
        {
            bulletObject = bullet.AddComponent<BulletObject>();
        }

        Vector3 normalizedDirection = direction.normalized;
        bullet.transform.position = bossObject.transform.position + normalizedDirection * 7f;
        bullet.transform.rotation = Quaternion.LookRotation(normalizedDirection);
        bulletObject.InitInfo(info, BulletSource.Boss, GameDataMgr.Instance.GetBossBulletSpeedMul());
        SpawnedBulletCount++;
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

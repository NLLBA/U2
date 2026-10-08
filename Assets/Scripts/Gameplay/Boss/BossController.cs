using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

    [FormerlySerializedAs("normalFireIntensityDuringBoss")]
    [Range(0f, 1f)] public float bossFireScale = 0.25f;
    [FormerlySerializedAs("baseAttackInterval")]
    [Min(0.1f)] public float fireGap = 1.2f;
    [FormerlySerializedAs("bossVisualScale")]
    [Min(1f)] public float bossScale = 2.5f;
    [FormerlySerializedAs("bossVisualResource")]
    public string bossPath = "Airplane/Airplane5";
    [FormerlySerializedAs("bossVisualResources")]
    public string[] bossPaths =
    {
        "Bosses/CrimsonManta",
        "Bosses/AzureBastion",
        "Bosses/SolarSeraph",
    };

    public bool BossActive { get; private set; }
    public int BossRound { get; private set; }
    public BossAttackPattern Pattern { get; private set; }
    public int BulletCount { get; private set; }

    private GameFlowManager flow;
    private GameObject boss;
    private float fireTimer;
    private float spin;
    private int volley;

    public static BossController Ensure(GameObject host = null)
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
            DestroyNow(this);
            return;
        }

        instance = this;
        ClampValues();
        BindFlow();

        if (flow != null)
        {
            OnFlowState(flow.State);
        }
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        if (flow != null)
        {
            flow.StateUpdated -= OnFlowState;
        }

        StopBoss();
        FireObject.SetIntensity(1f);

        instance = null;
    }

    private void OnValidate()
    {
        ClampValues();
    }

    private void Update()
    {
        if (flow == null)
        {
            BindFlow();
        }

        if (flow == null || flow.State != GameFlowState.BossSurvival || !BossActive)
            return;

        MoveBoss();

        float progress = flow.bossTime <= 0f
            ? 0f
            : flow.StateTime / flow.bossTime;
        BossAttackPattern next = PatternAt(progress);
        if (next != Pattern)
        {
            Pattern = next;
            fireTimer = 0f;
        }

        fireTimer -= Time.deltaTime;
        if (fireTimer > 0f)
            return;

        FirePattern();
        fireTimer = GameDataMgr.Instance.BossFireGap(fireGap);
    }

    public BossAttackPattern PatternAt(float progress)
    {
        float time = Mathf.Clamp01(progress);
        if (time < 0.25f)
            return BossAttackPattern.Fan;
        if (time < 0.5f)
            return BossAttackPattern.Spiral;
        if (time < 0.75f)
            return BossAttackPattern.Cross;
        return BossAttackPattern.Homing;
    }

    public int VolleySize(BossAttackPattern pattern, int round)
    {
        int level = Mathf.Max(1, round);
        if (pattern == BossAttackPattern.Fan)
            return Mathf.Min(3 + level * 2, 13);
        if (pattern == BossAttackPattern.Spiral)
            return Mathf.Min(4 + level, 10);
        if (pattern == BossAttackPattern.Cross)
            return level >= 2 ? 8 : 4;
        if (pattern == BossAttackPattern.Homing)
            return Mathf.Min(2 + level, 6);
        return 1;
    }

    public string BossPath(int round)
    {
        if (string.IsNullOrEmpty(bossPath))
            return string.Empty;

        if (bossPaths != null && bossPaths.Length > 0)
        {
            int index = (Mathf.Max(1, round) - 1) % bossPaths.Length;
            string selected = bossPaths[index];
            if (!string.IsNullOrEmpty(selected))
            {
                return selected;
            }
        }

        return bossPath;
    }

    public string PatternName()
    {
        switch (Pattern)
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

    private void ClampValues()
    {
        bossFireScale = Mathf.Clamp01(bossFireScale);
        fireGap = Mathf.Max(0.1f, fireGap);
        bossScale = Mathf.Max(1f, bossScale);
    }

    private void BindFlow()
    {
        GameFlowManager next = GetComponent<GameFlowManager>();
        if (next == null)
        {
            next = GameFlowManager.Instance;
        }

        if (flow == next)
            return;

        if (flow != null)
        {
            flow.StateUpdated -= OnFlowState;
        }

        flow = next;
        if (flow != null)
        {
            flow.StateUpdated += OnFlowState;
        }
    }

    private void OnFlowState(GameFlowState state)
    {
        if (state == GameFlowState.BossWarning || state == GameFlowState.BossSurvival)
        {
            FireObject.SetIntensity(bossFireScale);
        }

        if (state == GameFlowState.BossSurvival && !BossActive)
        {
            StartBoss();
        }
        else if (state == GameFlowState.Normal)
        {
            StopBoss();
            FireObject.SetIntensity(1f);
        }
        else if (state == GameFlowState.GameOver)
        {
            StopBoss();
        }
    }

    private void StartBoss()
    {
        BossActive = true;
        BossRound = flow != null ? Mathf.Max(1, flow.BossRound) : 1;
        Pattern = BossAttackPattern.Fan;
        BulletCount = 0;
        fireTimer = 0f;
        spin = 0f;
        volley = 0;
        SpawnVisual();
    }

    private void StopBoss()
    {
        BulletObject.ClearBoss();

        if (boss != null)
        {
            if (!ObjectPoolManager.TryRelease(boss))
            {
                DestroyNow(boss);
            }
            boss = null;
        }

        BossActive = false;
        BossRound = 0;
        fireTimer = 0f;
    }

    private void SpawnVisual()
    {
        string path = BossPath(BossRound);
        GameObject prefab = string.IsNullOrEmpty(path)
            ? null
            : Resources.Load<GameObject>(path);

        if (prefab != null)
        {
            boss = ObjectPoolManager.Ensure().SpawnAsset(
                path,
                Vector3.zero,
                Quaternion.identity);
        }
        else
        {
            boss = new GameObject("BossVisual");
        }

        boss.name = "SurvivalBoss_Round" + BossRound;
        boss.tag = "Untagged";
        boss.transform.localScale = Vector3.one * bossScale;
        boss.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        Collider[] colliders = boss.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] bodies = boss.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].isKinematic = true;
            bodies[i].detectCollisions = false;
        }

        MoveBoss();
    }

    private void MoveBoss()
    {
        if (boss == null)
            return;

        float offset = Mathf.Sin(Time.time * 0.7f) * 0.13f;
        boss.transform.position = ArenaPos(0.5f + offset, 0.82f);
    }

    private Vector3 ArenaPos(float x, float y)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return new Vector3(0f, 0f, 80f);

        float distance = Mathf.Abs(camera.transform.position.y);
        Vector3 point = new Vector3(
            Screen.width * Mathf.Clamp01(x),
            Screen.height * Mathf.Clamp01(y),
            distance);
        return camera.ScreenToWorldPoint(point);
    }

    private void FirePattern()
    {
        int count = VolleySize(Pattern, BossRound);
        if (Pattern == BossAttackPattern.Fan)
            ShootFan(count);
        else if (Pattern == BossAttackPattern.Spiral)
            ShootSpiral(count);
        else if (Pattern == BossAttackPattern.Cross)
            ShootCross(count);
        else if (Pattern == BossAttackPattern.Homing)
            ShootHoming(count);

        volley++;
        if (BossRound >= 2 && Pattern != BossAttackPattern.Cross && volley % 2 == 0)
        {
            ShootCross(4);
        }

        if (BossRound >= 3 && Pattern != BossAttackPattern.Homing && volley % 3 == 0)
        {
            ShootHoming(1);
        }
    }

    private void ShootFan(int count)
    {
        Vector3 aim = PlayerDir();
        float span = Mathf.Min(70f + (BossRound - 1) * 8f, 120f);
        for (int i = 0; i < count; i++)
        {
            float angle = count <= 1 ? 0f : Mathf.Lerp(-span * 0.5f, span * 0.5f, i / (count - 1f));
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * aim;
            SpawnBullet(StraightId(), dir);
        }
    }

    private void ShootSpiral(int count)
    {
        float step = 360f / count;
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(spin + step * i, Vector3.up) * Vector3.forward;
            int id = i % 2 == 0 ? 9 + ColorIndex() : 13 + ColorIndex();
            SpawnBullet(id, dir);
        }

        spin = (spin + 18f + BossRound * 3f) % 360f;
    }

    private void ShootCross(int count)
    {
        float step = 360f / count;
        float start = volley * (7f + BossRound);
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(start + step * i, Vector3.up) * Vector3.forward;
            SpawnBullet(StraightId(), dir);
        }
    }

    private void ShootHoming(int count)
    {
        Vector3 aim = PlayerDir();
        for (int i = 0; i < count; i++)
        {
            float center = (count - 1) * 0.5f;
            float angle = (i - center) * 12f;
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * aim;
            SpawnBullet(17 + ColorIndex(), dir);
        }
    }

    private Vector3 PlayerDir()
    {
        if (boss != null && PlayerObject.Instance != null)
        {
            Vector3 dir = PlayerObject.Instance.transform.position - boss.transform.position;
            if (dir.sqrMagnitude > 0.001f)
            {
                return dir.normalized;
            }
        }

        return Vector3.back;
    }

    private int StraightId()
    {
        return 1 + ColorIndex();
    }

    private int ColorIndex()
    {
        return (Mathf.Max(1, BossRound) - 1) % 4;
    }

    private void SpawnBullet(int id, Vector3 dir)
    {
        if (boss == null || dir.sqrMagnitude < 0.001f)
            return;

        BulletInfo info = GameDataMgr.Instance.GetBullet(id);
        if (info == null || string.IsNullOrEmpty(info.asset))
            return;

        GameObject bullet = ObjectPoolManager.Ensure().SpawnAsset(
            info.asset,
            boss.transform.position,
            Quaternion.identity);
        if (bullet == null)
            return;

        BulletObject shot = bullet.GetComponent<BulletObject>();
        if (shot == null)
        {
            shot = bullet.AddComponent<BulletObject>();
        }

        dir.Normalize();
        bullet.transform.position = boss.transform.position + dir * 7f;
        bullet.transform.rotation = Quaternion.LookRotation(dir);
        shot.Init(info, BulletSource.Boss, GameDataMgr.Instance.BossBulletSpeed());
        BulletCount++;
    }

    private static void DestroyNow(Object target)
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

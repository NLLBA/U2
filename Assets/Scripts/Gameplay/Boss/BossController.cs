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
    Ring,
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
    public int ReflectedHits { get; private set; }
    public int ShieldHitsRequired
    {
        get
        {
            CombatTuningData tuning = GameDataMgr.Instance.Tuning;
            return GameDataMgr.Instance.EncounterCount(
                tuning.bossShieldEasy, tuning.bossShieldNormal, tuning.bossShieldHard);
        }
    }
    public bool ShieldBroken => ReflectedHits >= ShieldHitsRequired;
    public bool ShieldBreakFlashActive => Time.unscaledTime < shieldBreakFlashUntil;
    public bool RageVisualActive { get; private set; }

    private GameFlowManager flow;
    private GameObject boss;
    private float fireTimer;
    private float spin;
    private int volley;
    private Material reflectionMaterial;
    private float shieldBreakShakeUntil;
    private float shieldBreakFlashUntil;
    private Renderer[] bossRenderers = new Renderer[0];
    private Light[] bossLights = new Light[0];
    private Color[] bossLightColors = new Color[0];
    private float[] bossLightIntensities = new float[0];
    private MaterialPropertyBlock bossVisualBlock;
    private GameObject reflectedBeam;
    private LineRenderer reflectedBeamLine;

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
        ApplyTuning();
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
        if (reflectionMaterial != null)
            DestroyNow(reflectionMaterial);
        if (reflectedBeam != null)
            DestroyNow(reflectedBeam);

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
        BossAttackPattern next = ActivePatternAt(progress);
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
        CombatTuningData tuning = GameDataMgr.Instance.Tuning;
        if (time < tuning.bossPhase2)
            return BossAttackPattern.Fan;
        if (time < tuning.bossPhase3)
            return BossAttackPattern.Spiral;
        if (time < tuning.bossPhase4)
            return BossAttackPattern.Cross;
        return BossAttackPattern.Homing;
    }

    public BossAttackPattern CampaignPatternAt(float progress, int round, int levelId)
    {
        if (levelId != 5)
            return PatternAt(progress);

        float time = Mathf.Clamp01(progress);
        float split = GameDataMgr.Instance.Tuning.fifthBossPhaseSplit;
        if (round <= 1)
            return time < split ? BossAttackPattern.Ring : BossAttackPattern.Spiral;
        return time < split ? BossAttackPattern.Cross : BossAttackPattern.Homing;
    }

    public int VolleySize(BossAttackPattern pattern, int round)
    {
        int level = Mathf.Max(1, round);
        int difficulty = GameDataMgr.Instance.GetDifficulty().id - 1;
        CombatTuningData tuning = GameDataMgr.Instance.Tuning;
        if (pattern == BossAttackPattern.Fan)
            return Mathf.Min(tuning.bossFanBase + level * tuning.bossFanPerRound +
                difficulty * tuning.bossFanPerDifficulty, tuning.bossFanMax);
        if (pattern == BossAttackPattern.Spiral)
            return Mathf.Min(tuning.bossSpiralBase + level * tuning.bossSpiralPerRound +
                difficulty * tuning.bossSpiralPerDifficulty, tuning.bossSpiralMax);
        if (pattern == BossAttackPattern.Cross)
            return (level >= 2 ? tuning.bossCrossAdvanced : tuning.bossCrossBase) +
                difficulty * tuning.bossCrossPerDifficulty;
        if (pattern == BossAttackPattern.Homing)
            return Mathf.Min(tuning.bossHomingBase + level * tuning.bossHomingPerRound +
                difficulty * tuning.bossHomingPerDifficulty, tuning.bossHomingMax);
        if (pattern == BossAttackPattern.Ring)
            return Mathf.Min(tuning.bossRingBase + level * tuning.bossRingPerRound +
                difficulty * tuning.bossRingPerDifficulty, tuning.bossRingMax);
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

    public IEnumerable<string> ResourcePaths()
    {
        if (!string.IsNullOrWhiteSpace(bossPath))
            yield return bossPath;
        if (bossPaths == null)
            yield break;
        for (int i = 0; i < bossPaths.Length; i++)
            if (!string.IsNullOrWhiteSpace(bossPaths[i]))
                yield return bossPaths[i];
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
            case BossAttackPattern.Ring:
                return "星环";
            default:
                return string.Empty;
        }
    }

    public string PhaseStatus()
    {
        int target = GameDataMgr.Instance.MissionTarget(GameDataMgr.Instance.GetLevel());
        if (!ShieldBroken)
            return "护盾 " + Mathf.Min(ReflectedHits, ShieldHitsRequired) + "/" + ShieldHitsRequired + " · 点击金色核心反弹";
        return "狂暴阶段 " + Mathf.Min(ReflectedHits, target) + "/" + target + " · " + PatternName();
    }

    public void RegisterReflectedHit(Vector3 origin)
    {
        if (!BossActive || GameDataMgr.Instance.IsEndless || GameDataMgr.Instance.selectedLevelId != 5)
            return;
        bool wasBroken = ShieldBroken;
        ReflectedHits++;
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RegisterReflectedCore();
        SoundManager.Play(SoundCue.Skill);
        DrawReflection(origin);
        if (!wasBroken && ShieldBroken)
        {
            if (PlayerObject.Instance != null)
                PlayerObject.Instance.GrantProtection(GameDataMgr.Instance.Tuning.fifthBossProtectionSeconds);
            shieldBreakShakeUntil = Time.unscaledTime + 1.1f;
            shieldBreakFlashUntil = Time.unscaledTime + 1.4f;
            if (BKMusic.Instance != null)
                BKMusic.Instance.SetCombatIntensity(true);
            ApplyBossRageVisual(true);
            if (GamePanel.Instance != null)
                GamePanel.Instance.ShowCombatFeedback("护盾击破 · 1.5秒保护 · BOSS进入狂暴", new Color(1f, 0.35f, 0.12f), 2f);
            SoundManager.Play(SoundCue.BossAppear);
        }
        else if (GamePanel.Instance != null)
        {
            GamePanel.Instance.ShowCombatFeedback("核心反弹 +" +
                (ScoreManager.Instance != null ? ScoreManager.Instance.reflectedCorePoints : 200),
                new Color(1f, 0.78f, 0.15f));
        }
    }

    private void ClampValues()
    {
        bossFireScale = Mathf.Clamp01(bossFireScale);
        fireGap = Mathf.Max(0.1f, fireGap);
        bossScale = Mathf.Max(1f, bossScale);
    }

    private void ApplyTuning()
    {
        CombatTuningData tuning = GameDataMgr.Instance.Tuning;
        bossFireScale = tuning.bossNormalFireScale;
        fireGap = tuning.bossAttackInterval;
        bossScale = tuning.bossVisualScale;
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
        else if (state == GameFlowState.GameOver || state == GameFlowState.MissionComplete)
        {
            StopBoss();
        }
    }

    private void StartBoss()
    {
        BossActive = true;
        BossRound = flow != null ? Mathf.Max(1, flow.BossRound) : 1;
        Pattern = ActivePatternAt(0f);
        BulletCount = 0;
        fireTimer = 0f;
        spin = 0f;
        volley = 0;
        if (BKMusic.Instance != null)
            BKMusic.Instance.SetCombatIntensity(ShieldBroken);
        SpawnVisual();
    }

    private void StopBoss()
    {
        BulletObject.ClearBoss();
        ApplyBossRageVisual(false);

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
        shieldBreakShakeUntil = 0f;
        shieldBreakFlashUntil = 0f;
        if (BKMusic.Instance != null)
            BKMusic.Instance.SetCombatIntensity(false);
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

        CacheBossVisuals();
        ApplyBossRageVisual(ShieldBroken);

        MoveBoss();
    }

    private void MoveBoss()
    {
        if (boss == null)
            return;

        float offset = Mathf.Sin(Time.time * 0.7f) * 0.13f;
        if (Time.unscaledTime < shieldBreakShakeUntil)
            offset += Mathf.Sin(Time.unscaledTime * 62f) * 0.055f;
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
        else if (Pattern == BossAttackPattern.Ring)
            ShootRing(count);

        volley++;
        CombatTuningData tuning = GameDataMgr.Instance.Tuning;
        int coreCadence = ShieldBroken ? tuning.bossRageCoreCadence : tuning.bossNormalCoreCadence;
        if (IsFifthCampaign() && volley % coreCadence == 1)
            ShootReflectCore();

        if ((BossRound >= tuning.bossExtraCrossRound || ShieldBroken) &&
            Pattern != BossAttackPattern.Cross && volley % 2 == 0)
        {
            ShootCross(4);
        }

        if ((BossRound >= tuning.bossExtraHomingRound || ShieldBroken) &&
            Pattern != BossAttackPattern.Homing && volley % 3 == 0)
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

    private void ShootRing(int count)
    {
        float step = 360f / count;
        float start = volley % 2 == 0 ? 0f : step * 0.5f;
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(start + step * i, Vector3.up) * Vector3.forward;
            SpawnBullet(9 + ColorIndex(), dir);
        }
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

    private BossAttackPattern ActivePatternAt(float progress)
    {
        int levelId = GameDataMgr.Instance.IsEndless ? 0 : GameDataMgr.Instance.selectedLevelId;
        int phase = levelId == 5 && !ShieldBroken ? 1 : BossRound;
        if (levelId == 5 && ShieldBroken)
            phase = Mathf.Max(2, phase);
        return CampaignPatternAt(progress, phase, levelId);
    }

    private void ShootReflectCore()
    {
        SpawnBullet(4, PlayerDir(), BulletInteraction.ReflectCore, 0.65f);
    }

    private bool IsFifthCampaign()
    {
        return !GameDataMgr.Instance.IsEndless && GameDataMgr.Instance.selectedLevelId == 5;
    }

    private void SpawnBullet(int id, Vector3 dir, BulletInteraction interaction = BulletInteraction.Normal,
        float speedMultiplier = 1f)
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
            PooledObject pooled = bullet.GetComponent<PooledObject>();
            if (pooled != null)
                pooled.RefreshCallbacks();
        }

        dir.Normalize();
        bullet.transform.position = boss.transform.position + dir * 7f;
        bullet.transform.rotation = Quaternion.LookRotation(dir);
        shot.Init(info, BulletSource.Boss, GameDataMgr.Instance.BossBulletSpeed() * speedMultiplier);
        shot.ConfigureInteraction(interaction);
        shot.ConfigureRageVisual(ShieldBroken && interaction == BulletInteraction.Normal);
        BulletCount++;
    }

    private void CacheBossVisuals()
    {
        bossRenderers = boss != null ? boss.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        bossLights = boss != null ? boss.GetComponentsInChildren<Light>(true) : new Light[0];
        bossLightColors = new Color[bossLights.Length];
        bossLightIntensities = new float[bossLights.Length];
        for (int i = 0; i < bossLights.Length; i++)
        {
            bossLightColors[i] = bossLights[i] != null ? bossLights[i].color : Color.white;
            bossLightIntensities[i] = bossLights[i] != null ? bossLights[i].intensity : 1f;
        }
    }

    private void ApplyBossRageVisual(bool active)
    {
        RageVisualActive = active && boss != null;
        if (bossVisualBlock == null)
            bossVisualBlock = new MaterialPropertyBlock();
        for (int i = 0; i < bossRenderers.Length; i++)
        {
            Renderer renderer = bossRenderers[i];
            if (renderer == null)
                continue;
            if (!RageVisualActive)
            {
                renderer.SetPropertyBlock(null);
                continue;
            }
            bossVisualBlock.Clear();
            bossVisualBlock.SetColor("_Color", new Color(1f, 0.08f, 0.2f, 1f));
            bossVisualBlock.SetColor("_BaseColor", new Color(1f, 0.08f, 0.2f, 1f));
            bossVisualBlock.SetColor("_EmissionColor", new Color(1f, 0.02f, 0.08f, 1f) * 3f);
            renderer.SetPropertyBlock(bossVisualBlock);
        }
        for (int i = 0; i < bossLights.Length; i++)
        {
            if (bossLights[i] == null)
                continue;
            bossLights[i].color = RageVisualActive ? new Color(1f, 0.05f, 0.12f) :
                (i < bossLightColors.Length ? bossLightColors[i] : Color.white);
            bossLights[i].intensity = RageVisualActive ? 3.2f :
                (i < bossLightIntensities.Length ? bossLightIntensities[i] : 1f);
        }
    }

    private void DrawReflection(Vector3 origin)
    {
        if (boss == null)
            return;
        if (reflectionMaterial == null)
            reflectionMaterial = new Material(Shader.Find("Sprites/Default"));
        if (reflectedBeam == null)
        {
            reflectedBeam = new GameObject("ReflectedCoreBeam");
            reflectedBeamLine = reflectedBeam.AddComponent<LineRenderer>();
            reflectedBeamLine.sharedMaterial = reflectionMaterial;
            reflectedBeamLine.useWorldSpace = true;
            reflectedBeamLine.positionCount = 2;
            reflectedBeamLine.startWidth = 2.2f;
            reflectedBeamLine.endWidth = 0.7f;
            reflectedBeamLine.startColor = new Color(1f, 0.75f, 0.1f, 1f);
            reflectedBeamLine.endColor = new Color(0.2f, 1f, 1f, 0.8f);
            reflectedBeamLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            reflectedBeamLine.receiveShadows = false;
        }
        CancelInvoke(nameof(HideReflection));
        reflectedBeam.SetActive(true);
        reflectedBeamLine.SetPosition(0, origin);
        reflectedBeamLine.SetPosition(1, boss.transform.position);
        Invoke(nameof(HideReflection), 0.2f);
    }

    private void HideReflection()
    {
        if (reflectedBeam != null)
            reflectedBeam.SetActive(false);
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

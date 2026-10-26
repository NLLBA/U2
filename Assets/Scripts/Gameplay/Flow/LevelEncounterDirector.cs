using System.Collections;
using UnityEngine;

public enum LevelAttack
{
    Corridor,
    Cluster,
    LockOn,
    Crossfire,
    Spiral,
}

// Uses the run clock, so pauses freeze both telegraphs and attacks. No random
// emitter choices are needed: both peers receive the same authored wave order.
public class LevelEncounterDirector : MonoBehaviour
{
    public static LevelEncounterDirector Instance { get; private set; }
    public LevelAttack CurrentAttack { get; private set; }
    public int WavesFired { get; private set; }
    public int BulletsFired { get; private set; }
    public int SuccessfulDodges { get; private set; }
    public bool HasWarning => pending;
    public int CurrentPhase => PhaseForProgress(MissionProgress());
    public string PhaseName => CurrentPhase == 1 ? "适应" : (CurrentPhase == 2 ? "交锋" : "决战");

    public string PhaseRuleText
    {
        get
        {
            int levelId = level != null ? level.id : 0;
            switch (levelId)
            {
                case 1: return CurrentPhase == 1 ? "单线缺口" : (CurrentPhase == 2 ? "横扫叠加" : "交替反向");
                case 2: return CurrentPhase == 1 ? "识别核心" : (CurrentPhase == 2 ? "双层诱饵" : "精英分裂");
                case 3: return CurrentPhase == 1 ? "单点锁定" : (CurrentPhase == 2 ? "侧翼齐射" : "假线追踪");
                case 4: return CurrentPhase == 1 ? "双侧交叉" : (CurrentPhase == 2 ? "窄角齐射" : "加速封锁");
                case 5: return CurrentPhase == 1 ? "螺旋风暴" : (CurrentPhase == 2 ? "交叉风暴" : "追踪狂暴");
                default: return "阶段规则";
            }
        }
    }

    private GameFlowManager flow;
    private LevelInfo level;
    private float nextWave = 1f;
    private float releaseAt;
    private float previousTime;
    private bool pending;
    private bool dodgePending;
    private float dodgeResolveAt;
    private int dodgeStartHits;
    private float dodgeStartTime;
    private Vector3 dodgeStartPosition;
    private bool dodgeMovedEarly;
    private bool corridorPending;
    private float corridorResolveAt;
    private int corridorStartHits;
    private int displayedPhase = 1;
    private Vector3 lockedTarget;
    private readonly LineRenderer[] guides = new LineRenderer[10];
    private Material guideMaterial;
    private LineRenderer burstWarning;

    public static LevelEncounterDirector Ensure(GameObject host)
    {
        if (Instance != null)
            return Instance;
        return host.AddComponent<LevelEncounterDirector>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        flow = GameFlowManager.Instance;
        level = GameDataMgr.Instance.GetLevel();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;
        Instance = null;
        if (guideMaterial != null)
            Destroy(guideMaterial);
    }

    private void OnDisable()
    {
        pending = false;
        dodgePending = false;
        dodgeMovedEarly = false;
        HideGuides();
    }

    private void Update()
    {
        if (flow != null)
            Tick(flow.RunTime);
    }

    public void Tick(float runTime)
    {
        if (flow == null || level == null || Camera.main == null || PlayerObject.Instance == null)
            return;
        if (runTime < previousTime)
        {
            nextWave = 1f;
            WavesFired = BulletsFired = 0;
            SuccessfulDodges = 0;
            pending = false;
            dodgePending = false;
            corridorPending = false;
            dodgeMovedEarly = false;
            displayedPhase = 1;
            HideGuides();
        }
        previousTime = runTime;
        TrackEarlyDodge(runTime);
        ResolveDodge(runTime);
        ResolveCorridor(runTime);
        UpdatePhaseFeedback();
        if (flow.State == GameFlowState.Paused)
            return;
        if (flow.State != GameFlowState.Normal || PlayerObject.Instance.isDead)
        {
            pending = false;
            HideGuides();
            nextWave = runTime + 1.5f;
            return;
        }

        if (IsRecovery(level.id, runTime))
        {
            pending = false;
            HideGuides();
            return;
        }

        if (pending)
        {
            if (runTime < releaseAt)
                return;
            FireWave();
            pending = false;
            HideGuides();
            WavesFired++;
            nextWave = runTime + WaveInterval();
            return;
        }

        if (runTime < nextWave)
            return;
        CurrentAttack = AttackForRun(level.id, runTime);
        lockedTarget = PlayerObject.Instance.transform.position;
        float warning = CurrentAttack == LevelAttack.LockOn ? 1.1f : 0.8f;
        releaseAt = runTime + GameDataMgr.Instance.EncounterWarningTime(warning * PhaseWarningRate());
        pending = true;
        ShowGuides();
    }

    public static LevelAttack AttackAt(int levelId, float runTime)
    {
        if (levelId == 1) return LevelAttack.Corridor;
        if (levelId == 2) return LevelAttack.Cluster;
        if (levelId == 3) return LevelAttack.LockOn;
        if (levelId == 4) return LevelAttack.Crossfire;
        int phase = Mathf.FloorToInt(Mathf.Max(0f, runTime) / 12f) % 3;
        return phase == 0 ? LevelAttack.Spiral : (phase == 1 ? LevelAttack.Crossfire : LevelAttack.LockOn);
    }

    private LevelAttack AttackForRun(int levelId, float runTime)
    {
        LevelAttack attack = AttackAt(levelId, runTime);
        if (levelId == 5 && CampaignMissionController.Instance != null &&
            CampaignMissionController.Instance.IsActiveCampaign)
        {
            if (CurrentPhase >= 3)
                return LevelAttack.LockOn;
            if (CurrentPhase >= 2)
                return LevelAttack.Crossfire;
        }
        return attack;
    }

    public static bool IsRecovery(int levelId, float runTime)
    {
        return levelId == 5 && Mathf.Repeat(runTime, 12f) < 2f;
    }

    public static int PhaseForProgress(float progress)
    {
        float value = Mathf.Clamp01(progress);
        if (value < 1f / 3f)
            return 1;
        return value < 2f / 3f ? 2 : 3;
    }

    public string StatusText()
    {
        if (level != null && level.id == 4 && SignalRelayController.Instance != null)
            return SignalRelayController.Instance.StatusText();
        if (flow != null && flow.State == GameFlowState.BossSurvival)
            return level != null && level.id == 5 && BossController.Instance != null
                ? BossController.Instance.PhaseStatus()
                : "观察BOSS招式，保留护盾应急";
        if (level.id == 5 && IsRecovery(5, previousTime))
            return "风暴间歇 · 调整站位";
        switch (AttackForRun(level.id, previousTime))
        {
            case LevelAttack.Corridor: return "沿青色缺口穿行";
            case LevelAttack.Cluster: return "点击呼吸发光的青色核心";
            case LevelAttack.LockOn: return pending ? "瞄准已锁定 · 立即换位" : "成功诱导 " + SuccessfulDodges + " 次";
            case LevelAttack.Crossfire: return "交叉齐射 · 避开红色预警线";
            default: return "双螺旋 · 沿弹幕间隙绕行";
        }
    }

    public static Color Accent(int levelId)
    {
        switch (levelId)
        {
            case 1: return new Color(0.25f, 0.9f, 1f);
            case 2: return new Color(0.35f, 1f, 0.55f);
            case 3: return new Color(1f, 0.7f, 0.2f);
            case 4: return new Color(1f, 0.35f, 0.3f);
            default: return new Color(0.8f, 0.55f, 1f);
        }
    }

    private float WaveInterval()
    {
        float interval = CurrentAttack == LevelAttack.Spiral ? 1.3f :
            (CurrentAttack == LevelAttack.LockOn ? 3.6f : 2.5f);
        // Difficulty changes both wave density and the time between waves.
        float phaseRate = CurrentPhase == 1 ? 1f : (CurrentPhase == 2 ? 0.9f : 0.78f);
        return GameDataMgr.Instance.FireDelay(interval) * phaseRate;
    }

    private float GapCenter()
    {
        float[] centers = { 0.5f, 0.28f, 0.5f, 0.72f };
        return centers[WavesFired % centers.Length];
    }

    private Vector3 ClusterCenter(int index = 0)
    {
        if (UsesDoubleCore())
            return ArenaPoint(index == 0 ? 0.3f : 0.7f, 0.86f);
        return ArenaPoint(0.25f + (WavesFired % 3) * 0.25f, 0.86f);
    }

    private Vector3 Origin(int index, int count)
    {
        if (CurrentAttack == LevelAttack.Crossfire)
            return ArenaPoint(index == 0 ? 0.03f : 0.97f, 0.76f);
        float x = count <= 1 ? 0.5f : Mathf.Lerp(0.12f, 0.88f, index / (count - 1f));
        return ArenaPoint(x, 0.94f);
    }

    private void FireWave()
    {
        if (CurrentAttack == LevelAttack.Corridor)
        {
            Vector3 down = (ArenaPoint(0.5f, 0.1f) - ArenaPoint(0.5f, 0.9f)).normalized;
            int lanes = GameDataMgr.Instance.EncounterCount(19, 23, 27) + (CurrentPhase - 1) * 2;
            float gap = GameDataMgr.Instance.GetDifficulty().id == 1 ? 0.14f :
                (GameDataMgr.Instance.GetDifficulty().id == 3 ? 0.1f : 0.12f);
            gap = Mathf.Max(0.075f, gap - (CurrentPhase - 1) * 0.01f);
            for (int i = 0; i < lanes; i++)
            {
                float x = lanes <= 1 ? 0.5f : 0.03f + i * (0.94f / (lanes - 1f));
                if (Mathf.Abs(x - GapCenter()) < gap)
                    continue;
                Spawn(ArenaPoint(x, 0.94f), down, 1, 1, 46f);
            }
            if (ShouldCombine())
            {
                bool fromLeft = FireSideSweep(IsEliteWave());
                if (GameDataMgr.Instance.GetDifficulty().id == 3 && CurrentPhase >= 2 &&
                    (WavesFired + 1) % 2 == 0)
                    StartCoroutine(FireReverseSweep(fromLeft, IsEliteWave()));
            }
            corridorPending = true;
            corridorStartHits = PlayerObject.Instance != null ? PlayerObject.Instance.HitsTaken : 0;
            corridorResolveAt = previousTime + WaveInterval();
        }
        else if (CurrentAttack == LevelAttack.Cluster)
        {
            Vector3 down = (ArenaPoint(0.5f, 0.1f) - ArenaPoint(0.5f, 0.9f)).normalized;
            int waveId = WavesFired + 1;
            int coreCount = UsesDoubleCore() ? 2 : 1;
            int totalDecoys = GameDataMgr.Instance.EncounterCount(8, 11, 14) +
                (CurrentPhase - 1) * 2 + (IsEliteWave() ? 4 : 0);
            int decoysPerCore = Mathf.CeilToInt((float)totalDecoys / coreCount);
            float chainRadius = 18f * (PlayerObject.HasStageAffinity(2) ? 1.25f : 1f);
            for (int coreIndex = 0; coreIndex < coreCount; coreIndex++)
            {
                Vector3 center = ClusterCenter(coreIndex);
                BulletObject core = Spawn(center, down, 2, 1, 24f, chainRadius, waveId);
                if (core != null)
                    core.ConfigureInteraction(BulletInteraction.ClusterCore);
                for (int i = 0; i < decoysPerCore; i++)
                {
                    Vector3 offset = Quaternion.AngleAxis(i * (360f / decoysPerCore), Vector3.up) *
                        Vector3.right * 9f;
                    BulletObject decoy = Spawn(center + offset, down, 2, 1, 24f, chainRadius, waveId);
                    if (decoy != null)
                        decoy.ConfigureInteraction(BulletInteraction.ClusterDecoy);
                }
            }
        }
        else if (CurrentAttack == LevelAttack.LockOn || CurrentAttack == LevelAttack.Crossfire)
        {
            int origins = CurrentAttack == LevelAttack.LockOn
                ? GameDataMgr.Instance.EncounterCount(3, 4, 5) : 2;
            int shots = CurrentAttack == LevelAttack.Crossfire
                ? GameDataMgr.Instance.EncounterCount(3, 4, 5) : 3;
            shots += CurrentPhase - 1;
            if (IsEliteWave())
                shots += 2;
            float angleStep = CurrentAttack == LevelAttack.Crossfire && CurrentPhase >= 2 ? 7f : 9f;
            bool tracking = CurrentAttack == LevelAttack.LockOn;
            for (int i = 0; i < origins; i++)
            {
                Vector3 origin = Origin(i, origins);
                Vector3 aim = (lockedTarget - origin).normalized;
                for (int j = 0; j < shots; j++)
                {
                    float angle = (j - (shots - 1) * 0.5f) * angleStep;
                    Spawn(origin, Quaternion.AngleAxis(angle, Vector3.up) * aim,
                        tracking ? 3 : 4, tracking ? 5 : 1, tracking ? 52f : 62f);
                }
            }
            if (tracking)
            {
                dodgePending = true;
                dodgeStartHits = PlayerObject.Instance.HitsTaken;
                dodgeStartTime = previousTime;
                dodgeStartPosition = PlayerObject.Instance.transform.position;
                dodgeMovedEarly = false;
                dodgeResolveAt = previousTime + TrackingTime() + 0.35f;
                if (ShouldCombine())
                    FireCrossVolley(IsEliteWave());
            }
            else if (level != null && level.id == 4 && CurrentPhase >= 3 &&
                (WavesFired + 1) % 3 == 0)
            {
                FireSideSweep(IsEliteWave(), null, 0.1f);
            }
        }
        else
        {
            for (int side = 0; side < 2; side++)
            {
                Vector3 origin = ArenaPoint(side == 0 ? 0.22f : 0.78f, 0.85f);
                int count = GameDataMgr.Instance.EncounterCount(6, 8, 10) + (IsEliteWave() ? 3 : 0);
                for (int i = 0; i < count; i++)
                {
                    float angle = i * (360f / count) + WavesFired * (side == 0 ? 19f : -19f);
                    Spawn(origin, Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward,
                        side == 0 ? 2 : 3, side == 0 ? 3 : 4, 48f);
                }
            }
        }
    }

    public void SplitClusterDecoy(Vector3 position)
    {
        if (GamePanel.Instance != null)
            GamePanel.Instance.ShowCombatFeedback("误点诱饵 · 红色分裂弹幕", new Color(1f, 0.25f, 0.2f));
        StartCoroutine(AnimateBurstWarning(position));
        for (int i = 0; i < 4; i++)
        {
            Vector3 direction = Quaternion.AngleAxis(45f + i * 90f, Vector3.up) * Vector3.forward;
            Spawn(position, direction, 2, 1, 38f);
        }
    }

    private BulletObject Spawn(Vector3 position, Vector3 direction, int assetId, int movement, float speed,
        float chainRadius = 0f, int encounterWaveId = 0)
    {
        if (direction.sqrMagnitude < 0.001f)
            return null;
        BulletInfo source = GameDataMgr.Instance.GetBullet(assetId);
        if (source == null || string.IsNullOrEmpty(source.asset))
            return null;
        // Per-wave movement is local data; the shared XML bullet definitions stay immutable.
        BulletInfo info = new BulletInfo
        {
            id = source.id, asset = source.asset, deathFx = source.deathFx,
            type = movement, moveRate = speed, turnSpeed = movement == 5 ? 0.9f : 13f,
            sideSpeed = 0f, life = chainRadius > 0f ? 12f : 8f,
        };
        GameObject bullet = ObjectPoolManager.Ensure().SpawnAsset(info.asset, position, Quaternion.LookRotation(direction));
        if (bullet == null)
            return null;
        BulletObject shot = bullet.GetComponent<BulletObject>();
        if (shot == null)
        {
            shot = bullet.AddComponent<BulletObject>();
            PooledObject pooled = bullet.GetComponent<PooledObject>();
            if (pooled != null)
                pooled.RefreshCallbacks();
        }
        shot.Init(info, BulletSource.Normal, 1f, chainRadius,
            movement == 5 ? TrackingTime() : float.PositiveInfinity);
        shot.AssignEncounterWave(encounterWaveId);
        BulletsFired++;
        return shot;
    }

    private bool ShouldCombine()
    {
        int difficulty = GameDataMgr.Instance.GetDifficulty().id;
        if (difficulty == 3)
            return true;
        if (difficulty == 2 && CurrentPhase >= 2)
            return true;
        // Easy mode keeps the first two thirds readable, then introduces one
        // additional rule so the final third still feels like a climax.
        return difficulty == 1 && CurrentPhase >= 3 &&
            level != null && (level.id == 1 || level.id == 3);
    }

    private bool IsEliteWave()
    {
        int difficulty = GameDataMgr.Instance.GetDifficulty().id;
        if (difficulty == 3)
            return (WavesFired + 1) % 4 == 0;
        if (difficulty == 1)
            return false;
        if (CurrentPhase < 3)
            return false;
        return (WavesFired + 1) % 4 == 0;
    }

    private bool FireSideSweep(bool elite, bool? forcedFromLeft = null, float yOffset = 0f)
    {
        bool fromLeft = forcedFromLeft ?? (WavesFired % 2 == 0);
        int count = GameDataMgr.Instance.EncounterCount(5, 7, 9) + (elite ? 2 : 0);
        Vector3 direction = fromLeft ? Vector3.right : Vector3.left;
        for (int i = 0; i < count; i++)
        {
            float y = count <= 1 ? 0.5f : Mathf.Lerp(0.16f, 0.78f, i / (count - 1f));
            y = Mathf.Clamp(y + yOffset, 0.1f, 0.9f);
            Spawn(ArenaPoint(fromLeft ? 0.03f : 0.97f, y), direction, 3, 1, 40f);
        }
        return fromLeft;
    }

    private IEnumerator FireReverseSweep(bool firstFromLeft, bool elite)
    {
        yield return new WaitForSeconds(0.55f);
        if (flow == null || flow.State != GameFlowState.Normal || PlayerObject.Instance == null ||
            PlayerObject.Instance.isDead)
            yield break;
        FireSideSweep(elite, !firstFromLeft, 0.06f);
        if (GamePanel.Instance != null)
            GamePanel.Instance.ShowCombatFeedback("困难变式 · 反向横扫", new Color(1f, 0.38f, 0.2f));
    }

    private void FireCrossVolley(bool elite)
    {
        int shots = GameDataMgr.Instance.EncounterCount(2, 3, 4) + (elite ? 2 : 0);
        for (int side = 0; side < 2; side++)
        {
            Vector3 origin = ArenaPoint(side == 0 ? 0.03f : 0.97f, 0.58f);
            Vector3 aim = (lockedTarget - origin).normalized;
            for (int i = 0; i < shots; i++)
            {
                float angle = (i - (shots - 1) * 0.5f) * 10f;
                Spawn(origin, Quaternion.AngleAxis(angle, Vector3.up) * aim, 4, 1, 58f);
            }
        }
    }

    private void ResolveDodge(float runTime)
    {
        if (!dodgePending || runTime < dodgeResolveAt || PlayerObject.Instance == null)
            return;
        bool avoided = !PlayerObject.Instance.isDead && PlayerObject.Instance.HitsTaken == dodgeStartHits;
        if (dodgeMovedEarly && ScoreManager.Instance != null)
            ScoreManager.Instance.RegisterMistake(MissionMistakeKind.EarlyDodge);

        if (avoided)
        {
            SuccessfulDodges++;
            if (dodgeMovedEarly)
            {
                if (GamePanel.Instance != null)
                    GamePanel.Instance.ShowCombatFeedback("提前闪避 · 本轮不计完美",
                        new Color(1f, 0.65f, 0.25f));
            }
            else
            {
                if (ScoreManager.Instance != null)
                    ScoreManager.Instance.RegisterPerfectDodge();
                if (GamePanel.Instance != null)
                    GamePanel.Instance.ShowCombatFeedback("完美闪避 +" +
                        (ScoreManager.Instance != null ? ScoreManager.Instance.perfectDodgePoints : 150),
                        new Color(0.3f, 1f, 0.75f));
            }
        }
        dodgePending = false;
        dodgeMovedEarly = false;
    }

    private void ResolveCorridor(float runTime)
    {
        if (!corridorPending || runTime < corridorResolveAt || PlayerObject.Instance == null)
            return;
        if (!PlayerObject.Instance.isDead && PlayerObject.Instance.HitsTaken == corridorStartHits)
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.RegisterCorridorPass();
            if (GamePanel.Instance != null)
                GamePanel.Instance.ShowCombatFeedback("完美穿越 +" +
                    (ScoreManager.Instance != null ? ScoreManager.Instance.corridorPassPoints : 100),
                    new Color(0.25f, 0.9f, 1f));
        }
        else if (PlayerObject.Instance.HitsTaken > corridorStartHits && ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RegisterMistake(MissionMistakeKind.CorridorContact);
        }
        corridorPending = false;
    }

    private void TrackEarlyDodge(float runTime)
    {
        if (!dodgePending || dodgeMovedEarly || PlayerObject.Instance == null)
            return;

        float trackingTime = Mathf.Max(0.1f, TrackingTime());
        if (runTime - dodgeStartTime > trackingTime * 0.35f)
            return;

        Vector3 delta = PlayerObject.Instance.transform.position - dodgeStartPosition;
        if (delta.sqrMagnitude >= 9f)
            dodgeMovedEarly = true;
    }

    private float MissionProgress()
    {
        CampaignMissionController mission = CampaignMissionController.Instance;
        if (mission == null || !mission.IsActiveCampaign)
            return 0f;
        return Mathf.Clamp01((float)mission.CurrentValue() / Mathf.Max(1, mission.TargetValue()));
    }

    private float PhaseWarningRate()
    {
        return CurrentPhase == 1 ? 1f : (CurrentPhase == 2 ? 0.9f : 0.7f);
    }

    private float TrackingTime()
    {
        float time = GameDataMgr.Instance.EncounterTrackingTime();
        return PlayerObject.HasStageAffinity(3) ? time * 0.8f : time;
    }

    private bool UsesDoubleCore()
    {
        return level != null && level.id == 2 && GameDataMgr.Instance.GetDifficulty().id == 3 &&
            CurrentPhase >= 2;
    }

    private bool UsesHardFeint()
    {
        return GameDataMgr.Instance.GetDifficulty().id == 3 && (WavesFired + 1) % 3 == 0;
    }

    private void UpdatePhaseFeedback()
    {
        int phase = CurrentPhase;
        if (phase <= displayedPhase)
            return;
        displayedPhase = phase;
        if (GamePanel.Instance != null)
            GamePanel.Instance.ShowCombatFeedback("阶段" + phase + " · " + PhaseName + " · " + PhaseRuleText,
                Accent(level != null ? level.id : 1), 1.8f);
    }

    private IEnumerator AnimateBurstWarning(Vector3 center)
    {
        if (guideMaterial == null)
            guideMaterial = new Material(Shader.Find("Sprites/Default"));
        if (burstWarning == null)
        {
            GameObject marker = new GameObject("ClusterMistakeWarning");
            marker.transform.SetParent(transform, false);
            burstWarning = marker.AddComponent<LineRenderer>();
            burstWarning.sharedMaterial = guideMaterial;
            burstWarning.useWorldSpace = true;
            burstWarning.loop = true;
            burstWarning.positionCount = 32;
            burstWarning.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            burstWarning.receiveShadows = false;
        }
        LineRenderer line = burstWarning;
        line.enabled = true;
        float elapsed = 0f;
        while (elapsed < 0.45f)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / 0.45f);
            float radius = Mathf.Lerp(4f, 18f, progress);
            Color color = new Color(1f, 0.12f, 0.08f, 1f - progress);
            line.startColor = line.endColor = color;
            line.widthMultiplier = Mathf.Lerp(2f, 0.4f, progress);
            for (int i = 0; i < line.positionCount; i++)
                line.SetPosition(i, center + Quaternion.AngleAxis(i * 360f / line.positionCount, Vector3.up) *
                    Vector3.right * radius);
            yield return null;
        }
        line.enabled = false;
    }

    private Vector3 ArenaPoint(float x, float y)
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(x, y, 0f));
        float height = PlayerObject.Instance != null ? PlayerObject.Instance.transform.position.y : 0f;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
        float distance;
        return plane.Raycast(ray, out distance) ? ray.GetPoint(distance) : ray.GetPoint(293f);
    }

    private void ShowGuides()
    {
        HideGuides();
        Color color = Accent(level.id);
        if (CurrentAttack == LevelAttack.Corridor)
        {
            for (int i = 0; i < 2; i++)
            {
                float gap = GameDataMgr.Instance.GetDifficulty().id == 1 ? 0.14f :
                    (GameDataMgr.Instance.GetDifficulty().id == 3 ? 0.1f : 0.12f);
                gap = Mathf.Max(0.075f, gap - (CurrentPhase - 1) * 0.01f);
                float x = GapCenter() + (i == 0 ? -gap : gap);
                DrawLine(i, ArenaPoint(x, 0.12f), ArenaPoint(x, 0.94f), color);
            }
            if (ShouldCombine())
                DrawLine(2, ArenaPoint(0.03f, 0.48f), ArenaPoint(0.97f, 0.48f), color);
        }
        else if (CurrentAttack == LevelAttack.LockOn || CurrentAttack == LevelAttack.Crossfire)
        {
            int count = CurrentAttack == LevelAttack.LockOn
                ? GameDataMgr.Instance.EncounterCount(3, 4, 5) : 2;
            for (int i = 0; i < count; i++)
                DrawLine(i, Origin(i, count), lockedTarget, color);
            if (CurrentAttack == LevelAttack.LockOn && ShouldCombine())
            {
                DrawLine(count, ArenaPoint(0.03f, 0.58f), lockedTarget, color);
                DrawLine(count + 1, ArenaPoint(0.97f, 0.58f), lockedTarget, color);
            }
        }
        else
        {
            int count = CurrentAttack == LevelAttack.Cluster ? (UsesDoubleCore() ? 2 : 1) : 2;
            for (int i = 0; i < count; i++)
            {
                LineRenderer line = GetGuide(i, color);
                Vector3 center = CurrentAttack == LevelAttack.Cluster ? ClusterCenter(i) :
                    ArenaPoint(i == 0 ? 0.22f : 0.78f, 0.85f);
                line.positionCount = 33;
                for (int j = 0; j <= 32; j++)
                    line.SetPosition(j, center + Quaternion.AngleAxis(j * 360f / 32f, Vector3.up) * Vector3.right * 12f);
            }
        }
        if (UsesHardFeint())
        {
            LineRenderer feint = GetGuide(9, new Color(1f, 0.25f, 0.55f));
            feint.widthMultiplier = 0.45f;
            feint.startColor = feint.endColor = new Color(1f, 0.25f, 0.55f, 0.32f);
            feint.positionCount = 2;
            feint.SetPosition(0, ArenaPoint(0.08f, 0.24f));
            feint.SetPosition(1, ArenaPoint(0.92f, 0.7f));
        }
    }

    private void DrawLine(int index, Vector3 start, Vector3 end, Color color)
    {
        LineRenderer line = GetGuide(index, color);
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private LineRenderer GetGuide(int index, Color color)
    {
        if (guides[index] == null)
        {
            if (guideMaterial == null)
                guideMaterial = new Material(Shader.Find("Sprites/Default"));
            GameObject marker = new GameObject("EncounterWarning" + index);
            marker.transform.SetParent(transform, false);
            guides[index] = marker.AddComponent<LineRenderer>();
            guides[index].sharedMaterial = guideMaterial;
            guides[index].useWorldSpace = true;
            guides[index].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            guides[index].receiveShadows = false;
        }
        guides[index].enabled = true;
        bool easy = GameDataMgr.Instance.GetDifficulty().id == 1;
        guides[index].widthMultiplier = easy ? 1.35f : 0.8f;
        float alpha = easy ? 0.95f : 0.65f;
        guides[index].startColor = guides[index].endColor = new Color(color.r, color.g, color.b, alpha);
        return guides[index];
    }

    private void HideGuides()
    {
        foreach (LineRenderer line in guides)
            if (line != null)
                line.enabled = false;
    }
}

using UnityEngine;

public class SignalRelayController : MonoBehaviour
{
    public static SignalRelayController Instance { get; private set; }
    public int ActivatedBeacons { get; private set; }
    public float CarriedEnergy { get; private set; }
    public float BeaconCharge { get; private set; }
    public Vector3 EnergySourcePosition => ArenaPoint(0.5f, 0.5f);
    public Vector3 CurrentBeaconPosition
    {
        get
        {
            int index = Mathf.Clamp(ActivatedBeacons, 0, beaconViewports.Length - 1);
            return ArenaPoint(beaconViewports[index].x, beaconViewports[index].y);
        }
    }

    private readonly Vector2[] beaconViewports =
    {
        new Vector2(0.18f, 0.28f),
        new Vector2(0.8f, 0.55f),
        new Vector2(0.28f, 0.78f),
    };
    private readonly LineRenderer[] beaconRings = new LineRenderer[3];
    private readonly LineRenderer[] beaconChargeRings = new LineRenderer[3];
    private LineRenderer sourceRing;
    private Material ringMaterial;
    private PlayerObject player;

    public static SignalRelayController Ensure(GameObject host)
    {
        if (Instance != null)
            return Instance;
        return host.AddComponent<SignalRelayController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (player != null)
            player.Damaged -= OnPlayerDamaged;
        if (ringMaterial != null)
            Destroy(ringMaterial);
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    public void Tick(float deltaTime)
    {
        if (Camera.main == null || PlayerObject.Instance == null || PlayerObject.Instance.isDead)
            return;
        BindPlayer();
        EnsureVisuals();
        UpdateVisuals();

        GameFlowManager flow = GameFlowManager.Instance;
        if (flow == null || flow.State != GameFlowState.Normal || ActivatedBeacons >= beaconRings.Length)
            return;

        Vector3 playerPosition = player.transform.position;
        float affinityRate = PlayerObject.HasStageAffinity(4) ? 1.25f : 1f;
        bool collectingAtSource = DistanceOnArena(playerPosition, ArenaPoint(0.5f, 0.5f)) <= 15f;
        if (collectingAtSource)
            CarriedEnergy = Mathf.Clamp01(CarriedEnergy + Mathf.Max(0f, deltaTime) * 0.5f * affinityRate);

        Vector2 target = beaconViewports[ActivatedBeacons];
        if (!collectingAtSource && CarriedEnergy > 0f &&
            DistanceOnArena(playerPosition, ArenaPoint(target.x, target.y)) <= 16f)
        {
            float seconds = GameDataMgr.Instance.EncounterCount(3, 4, 5);
            float deposited = Mathf.Min(CarriedEnergy, Mathf.Max(0f, deltaTime) * affinityRate / seconds);
            CarriedEnergy -= deposited;
            BeaconCharge = Mathf.Clamp01(BeaconCharge + deposited);
            if (BeaconCharge >= 0.999f)
            {
                ActivatedBeacons++;
                BeaconCharge = 0f;
                CarriedEnergy = 0f;
                if (ScoreManager.Instance != null)
                    ScoreManager.Instance.RegisterRelayBeacon();
                if (GamePanel.Instance != null)
                    GamePanel.Instance.ShowCombatFeedback("信标激活 +" +
                        (ScoreManager.Instance != null ? ScoreManager.Instance.relayBeaconPoints : 250),
                        new Color(0.2f, 1f, 0.45f));
                SoundManager.Play(SoundCue.Skill);
            }
        }
    }

    public string StatusText()
    {
        if (ActivatedBeacons >= beaconViewports.Length)
            return "三座信标已全部激活";
        if (CarriedEnergy < 0.05f)
            return "能量 " + ProgressBar(CarriedEnergy) + " · 前往中央蓝环取能";
        return "能量 " + ProgressBar(CarriedEnergy) + " · 信标 " + ProgressBar(BeaconCharge);
    }

    private void BindPlayer()
    {
        if (player == PlayerObject.Instance)
            return;
        if (player != null)
            player.Damaged -= OnPlayerDamaged;
        player = PlayerObject.Instance;
        player.Damaged += OnPlayerDamaged;
    }

    private void OnPlayerDamaged()
    {
        bool lostEnergy = CarriedEnergy > 0f || BeaconCharge > 0f;
        float lossRate = PlayerObject.HasStageAffinity(4) ? 0.5f : 1f;
        CarriedEnergy = Mathf.Max(0f, CarriedEnergy - 0.35f * lossRate);
        BeaconCharge = Mathf.Max(0f, BeaconCharge - 0.1f * lossRate);
        CampaignMissionController mission = CampaignMissionController.Instance;
        if (lostEnergy && mission != null && mission.IsActiveCampaign && mission.Level != null &&
            mission.Level.id == 4 && ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RegisterMistake(MissionMistakeKind.EnergyLoss);
        }
        if (lostEnergy && GamePanel.Instance != null)
            GamePanel.Instance.ShowCombatFeedback("能量受损 · 已激活信标保持", new Color(1f, 0.45f, 0.2f));
    }

    private void EnsureVisuals()
    {
        if (ringMaterial == null)
            ringMaterial = new Material(Shader.Find("Sprites/Default"));
        if (sourceRing == null)
            sourceRing = CreateRing("EnergySource");
        for (int i = 0; i < beaconRings.Length; i++)
        {
            if (beaconRings[i] == null)
                beaconRings[i] = CreateRing("SignalBeacon" + (i + 1));
            if (beaconChargeRings[i] == null)
                beaconChargeRings[i] = CreateRing("SignalBeaconCharge" + (i + 1));
        }
    }

    private LineRenderer CreateRing(string objectName)
    {
        GameObject marker = new GameObject(objectName);
        marker.transform.SetParent(transform, false);
        LineRenderer ring = marker.AddComponent<LineRenderer>();
        ring.sharedMaterial = ringMaterial;
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = 48;
        ring.widthMultiplier = 1.1f;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        return ring;
    }

    private void UpdateVisuals()
    {
        float pulse = 0.65f + Mathf.Sin(Time.time * 4f) * 0.2f;
        DrawRing(sourceRing, ArenaPoint(0.5f, 0.5f), 15f,
            new Color(0.15f, 0.85f, 1f, pulse), CarriedEnergy);
        for (int i = 0; i < beaconRings.Length; i++)
        {
            Color color = i < ActivatedBeacons
                ? new Color(0.2f, 1f, 0.45f, 0.85f)
                : (i == ActivatedBeacons
                    ? new Color(1f, 0.55f, 0.12f, pulse)
                    : new Color(0.3f, 0.35f, 0.42f, 0.35f));
            float progress = i < ActivatedBeacons ? 1f : (i == ActivatedBeacons ? BeaconCharge : 0f);
            Vector3 center = ArenaPoint(beaconViewports[i].x, beaconViewports[i].y);
            DrawRing(beaconRings[i], center, 16f, color, 0f);
            DrawChargeArc(beaconChargeRings[i], center, 18f, progress,
                i < ActivatedBeacons ? new Color(0.25f, 1f, 0.6f, 1f) : new Color(1f, 0.8f, 0.2f, 1f));
        }
    }

    private static void DrawRing(LineRenderer ring, Vector3 center, float radius, Color color, float progress)
    {
        ring.startColor = ring.endColor = color;
        ring.widthMultiplier = Mathf.Lerp(0.8f, 2f, Mathf.Clamp01(progress));
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i * 360f / ring.positionCount;
            ring.SetPosition(i, center + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.right * radius);
        }
    }

    private static void DrawChargeArc(LineRenderer ring, Vector3 center, float radius, float progress, Color color)
    {
        float value = Mathf.Clamp01(progress);
        int pointCount = Mathf.Max(2, Mathf.CeilToInt(48f * value) + 1);
        ring.loop = value >= 0.999f;
        ring.positionCount = pointCount;
        ring.startColor = ring.endColor = new Color(color.r, color.g, color.b, value > 0f ? color.a : 0f);
        ring.widthMultiplier = 2.2f;
        for (int i = 0; i < pointCount; i++)
        {
            float normalized = pointCount <= 1 ? 0f : (float)i / (pointCount - 1);
            float angle = normalized * 360f * value;
            ring.SetPosition(i, center + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.right * radius);
        }
    }

    private static string ProgressBar(float value)
    {
        int filled = Mathf.RoundToInt(Mathf.Clamp01(value) * 10f);
        return "[" + new string('#', filled) + new string('-', 10 - filled) + "] " +
            Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }

    private static float DistanceOnArena(Vector3 a, Vector3 b)
    {
        a.y = b.y;
        return Vector3.Distance(a, b);
    }

    private static Vector3 ArenaPoint(float x, float y)
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(x, y, 0f));
        float height = PlayerObject.Instance != null ? PlayerObject.Instance.transform.position.y : 0f;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
        float distance;
        return plane.Raycast(ray, out distance) ? ray.GetPoint(distance) : ray.GetPoint(293f);
    }
}

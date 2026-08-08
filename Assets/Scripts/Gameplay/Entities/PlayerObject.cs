using System.Collections.Generic;
using UnityEngine;

public enum ShieldRotationPattern
{
    SingleOrbit,
    CounterRotating,
    Gyroscope,
    LockingOscillation,
    PrecessingSpiral,
}

public enum ShieldPulsePattern
{
    CalmWave,
    DoubleBeat,
    TravelingRipple,
    HeavyBeat,
    RapidSurge,
}

public sealed class ShieldVisualProfile
{
    public readonly int StyleId;
    public readonly Color ShellColor;
    public readonly Color ShellEmissionColor;
    public readonly Color RingColor;
    public readonly Color RingEmissionColor;
    public readonly Color NodeColor;
    public readonly Color NodeEmissionColor;
    public readonly int RingCount;
    public readonly ShieldRotationPattern RotationPattern;
    public readonly float RotationSpeed;
    public readonly ShieldPulsePattern PulsePattern;
    public readonly float PulseFrequency;
    public readonly float PulseAmount;
    public readonly int EnergyNodeCount;
    public readonly float EnergyNodeSize;

    public ShieldVisualProfile(
        int styleId,
        Color shellColor,
        Color shellEmissionColor,
        Color ringColor,
        Color ringEmissionColor,
        Color nodeColor,
        Color nodeEmissionColor,
        int ringCount,
        ShieldRotationPattern rotationPattern,
        float rotationSpeed,
        ShieldPulsePattern pulsePattern,
        float pulseFrequency,
        float pulseAmount,
        int energyNodeCount,
        float energyNodeSize)
    {
        StyleId = styleId;
        ShellColor = shellColor;
        ShellEmissionColor = shellEmissionColor;
        RingColor = ringColor;
        RingEmissionColor = ringEmissionColor;
        NodeColor = nodeColor;
        NodeEmissionColor = nodeEmissionColor;
        RingCount = ringCount;
        RotationPattern = rotationPattern;
        RotationSpeed = rotationSpeed;
        PulsePattern = pulsePattern;
        PulseFrequency = pulseFrequency;
        PulseAmount = pulseAmount;
        EnergyNodeCount = energyNodeCount;
        EnergyNodeSize = energyNodeSize;
    }
}

public static class ShieldVisualProfileCatalog
{
    private static readonly ShieldVisualProfile[] Profiles =
    {
        new ShieldVisualProfile(
            1,
            new Color(0.02f, 0.35f, 1f, 0.28f),
            new Color(0f, 0.55f, 1.6f, 1f),
            new Color(0.2f, 0.85f, 1f, 0.95f),
            new Color(0.1f, 0.9f, 2f, 1f),
            new Color(0.75f, 0.97f, 1f, 1f),
            new Color(0.35f, 1.2f, 2.5f, 1f),
            1,
            ShieldRotationPattern.SingleOrbit,
            72f,
            ShieldPulsePattern.CalmWave,
            0.8f,
            0.035f,
            3,
            0.065f),
        new ShieldVisualProfile(
            2,
            new Color(0.02f, 0.72f, 0.3f, 0.27f),
            new Color(0f, 1.15f, 0.42f, 1f),
            new Color(0.2f, 1f, 0.58f, 0.96f),
            new Color(0.08f, 1.65f, 0.55f, 1f),
            new Color(0.82f, 1f, 0.68f, 1f),
            new Color(0.45f, 2.1f, 0.72f, 1f),
            2,
            ShieldRotationPattern.CounterRotating,
            118f,
            ShieldPulsePattern.DoubleBeat,
            1.05f,
            0.055f,
            4,
            0.058f),
        new ShieldVisualProfile(
            3,
            new Color(0.55f, 0.08f, 1f, 0.26f),
            new Color(0.85f, 0.12f, 1.75f, 1f),
            new Color(0.92f, 0.3f, 1f, 0.96f),
            new Color(1.35f, 0.25f, 2.1f, 1f),
            new Color(1f, 0.78f, 1f, 1f),
            new Color(1.8f, 0.55f, 2.4f, 1f),
            3,
            ShieldRotationPattern.Gyroscope,
            148f,
            ShieldPulsePattern.TravelingRipple,
            1.45f,
            0.045f,
            6,
            0.052f),
        new ShieldVisualProfile(
            4,
            new Color(1f, 0.42f, 0.02f, 0.3f),
            new Color(1.8f, 0.58f, 0.03f, 1f),
            new Color(1f, 0.78f, 0.18f, 0.98f),
            new Color(2.2f, 1.1f, 0.12f, 1f),
            new Color(1f, 0.94f, 0.62f, 1f),
            new Color(2.5f, 1.65f, 0.35f, 1f),
            4,
            ShieldRotationPattern.LockingOscillation,
            54f,
            ShieldPulsePattern.HeavyBeat,
            0.62f,
            0.075f,
            8,
            0.05f),
        new ShieldVisualProfile(
            5,
            new Color(1f, 0.04f, 0.09f, 0.25f),
            new Color(1.9f, 0.06f, 0.12f, 1f),
            new Color(1f, 0.28f, 0.08f, 0.97f),
            new Color(2.4f, 0.32f, 0.08f, 1f),
            new Color(1f, 0.82f, 0.42f, 1f),
            new Color(2.8f, 0.8f, 0.16f, 1f),
            5,
            ShieldRotationPattern.PrecessingSpiral,
            212f,
            ShieldPulsePattern.RapidSurge,
            2.2f,
            0.04f,
            10,
            0.045f),
    };

    public static int Count => Profiles.Length;

    public static ShieldVisualProfile Get(int styleId)
    {
        int index = Mathf.Clamp(styleId, 1, Profiles.Length) - 1;
        return Profiles[index];
    }

    public static float EvaluatePulse(
        ShieldPulsePattern pattern,
        float time,
        float frequency,
        float phaseOffset = 0f)
    {
        float cycle = Mathf.Repeat(time * frequency + phaseOffset, 1f);
        float phase = cycle * Mathf.PI * 2f;

        switch (pattern)
        {
            case ShieldPulsePattern.CalmWave:
                return 0.5f + Mathf.Sin(phase) * 0.5f;
            case ShieldPulsePattern.DoubleBeat:
                return Mathf.Clamp01(
                    PulsePeak(cycle, 0.16f, 0.13f) +
                    PulsePeak(cycle, 0.4f, 0.1f) * 0.7f);
            case ShieldPulsePattern.TravelingRipple:
                return 0.5f + Mathf.Sin(phase + Mathf.Sin(phase * 0.5f) * 0.8f) * 0.5f;
            case ShieldPulsePattern.HeavyBeat:
                return Mathf.Pow(0.5f + Mathf.Sin(phase) * 0.5f, 4f);
            case ShieldPulsePattern.RapidSurge:
                return Mathf.Clamp01(
                    0.5f + Mathf.Sin(phase) * 0.32f + Mathf.Sin(phase * 3f) * 0.18f);
        }

        return 0f;
    }

    private static float PulsePeak(float cycle, float center, float halfWidth)
    {
        float distance = Mathf.Abs(cycle - center);
        distance = Mathf.Min(distance, 1f - distance);
        float strength = 1f - Mathf.Clamp01(distance / halfWidth);
        return strength * strength;
    }
}

public class PlayerObject : MonoBehaviour
{
    private sealed class ShieldRingState
    {
        public GameObject Root;
        public Quaternion BaseRotation;
        public float Radius;
        public float Phase;
        public int Index;
    }

    private sealed class ShieldNodeState
    {
        public Transform Transform;
        public int RingIndex;
        public float NormalizedAngle;
        public float Phase;
    }

    private static PlayerObject instance;
    private static float bulletSpeedScale = 1f;

    public static PlayerObject Instance => instance;
    public static float BulletSpeedScale => bulletSpeedScale;

    public int nowHp;
    public int maxHp;
    public int speed;
    public int roundSpeed;
    public bool isDead;

    private Quaternion targetQ;
    private Vector3 nowPos;
    private Vector3 frontPos;
    private float hValue;
    private float vValue;

    private bool isShield;
    private float shieldEndTime;
    private float nextShieldTime;
    private float nextHeroSkillTime;
    private float slowEndTime;
    private float pulseEndTime;
    private float nextPulseTime;
    private float speedBoostEndTime;

    private GameObject shieldRoot;
    private GameObject shieldShell;
    private Material shieldShellMaterial;
    private Material shieldRingMaterial;
    private Material shieldNodeMaterial;
    private ShieldVisualProfile shieldVisualProfile;
    private readonly List<ShieldRingState> shieldRings = new List<ShieldRingState>();
    private readonly List<ShieldNodeState> shieldEnergyNodes = new List<ShieldNodeState>();
    private Vector3 shieldBaseScale = Vector3.one;
    private float shieldNodeBaseScale = 1f;

    private void Awake()
    {
        instance = this;
        bulletSpeedScale = 1f;
    }

    private void OnDestroy()
    {
        bulletSpeedScale = 1f;

        if (shieldShellMaterial != null)
        {
            DestroyShieldObject(shieldShellMaterial);
        }

        if (shieldRingMaterial != null)
        {
            DestroyShieldObject(shieldRingMaterial);
        }

        if (shieldNodeMaterial != null)
        {
            DestroyShieldObject(shieldNodeMaterial);
        }
    }

    public void Dead()
    {
        if (isDead)
            return;

        isDead = true;
        HideShieldView();

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.EnterGameOver();
        }
        else
        {
            Time.timeScale = 0;
        }

        GameOverPanel.Instance.Showme();
        ScorePanelPresenter.ShowFinalScore(GameOverPanel.Instance);
    }

    public void Wound()
    {
        if (isDead || isShield)
            return;

        nowHp -= 1;
        SoundManager.Play(SoundCue.PlayerHit);
        GamePanel.Instance.ChangeHP(nowHp);

        if (nowHp <= 0)
            Dead();
    }

    void Update()
    {
        if (isDead)
            return;

        UpdateShield();
        UpdateHeroSkillState();
        UpdateMove();
        UpdateClickBullet();
        UpdateSkills();
        UpdateShieldView();
    }

    private void UpdateMove()
    {
        hValue = Input.GetAxisRaw("Horizontal");
        vValue = Input.GetAxisRaw("Vertical");

        if (hValue == 0)
            targetQ = Quaternion.identity;
        else
            targetQ = hValue < 0 ? Quaternion.AngleAxis(20, Vector3.forward) : Quaternion.AngleAxis(-20, Vector3.forward);

        transform.rotation = Quaternion.Slerp(transform.rotation, targetQ, roundSpeed * Time.deltaTime);

        float moveSpeed = speed;
        if (Time.time < speedBoostEndTime)
        {
            moveSpeed *= 1.55f;
        }

        frontPos = transform.position;
        transform.Translate(Vector3.forward * vValue * moveSpeed * Time.deltaTime);
        transform.Translate(Vector3.right * hValue * moveSpeed * Time.deltaTime, Space.World);

        if (Camera.main == null)
            return;

        nowPos = Camera.main.WorldToScreenPoint(transform.position);
        if (nowPos.x <= 0 || nowPos.x >= Screen.width)
        {
            transform.position = new Vector3(frontPos.x, transform.position.y, transform.position.z);
        }

        if (nowPos.y <= 0 || nowPos.y >= Screen.height)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, frontPos.z);
        }
    }

    private void UpdateClickBullet()
    {
        if (!Input.GetMouseButtonDown(0) || Camera.main == null)
            return;

        RaycastHit hitInfo;
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hitInfo, 1000, 1 << LayerMask.NameToLayer("Bullent")))
        {
            BulletObject bulletObj = hitInfo.transform.GetComponent<BulletObject>();
            if (bulletObj != null)
            {
                bulletObj.ClearByPlayer();
            }
        }
    }

    private void UpdateSkills()
    {
        float skillCD = GameDataMgr.Instance.GetSkillCD();

        if (Input.GetKeyDown(KeyCode.E) && Time.time >= nextShieldTime)
        {
            isShield = true;
            shieldEndTime = Time.time + GameDataMgr.Instance.GetShieldTime();
            nextShieldTime = Time.time + skillCD;
            ShowShieldView();
            SoundManager.Play(SoundCue.Skill);
        }

        if (Input.GetKeyDown(KeyCode.Q) && Time.time >= nextHeroSkillTime)
        {
            UseHeroSkill();
            nextHeroSkillTime = Time.time + GetHeroSkillCD();
            SoundManager.Play(SoundCue.Skill);
        }
    }

    private void UseHeroSkill()
    {
        switch (GetHeroSkillType())
        {
            case 1:
                BulletObject.ClearAllBullets();
                break;
            case 2:
                nowHp = Mathf.Min(maxHp, nowHp + 2);
                GamePanel.Instance.ChangeHP(nowHp);
                break;
            case 3:
                slowEndTime = Time.time + 5f;
                bulletSpeedScale = 0.35f;
                break;
            case 4:
                pulseEndTime = Time.time + 4f;
                nextPulseTime = 0f;
                break;
            case 5:
                speedBoostEndTime = Time.time + 5f;
                break;
        }
    }

    private void UpdateHeroSkillState()
    {
        if (Time.time >= slowEndTime)
        {
            bulletSpeedScale = 1f;
        }

        if (Time.time < pulseEndTime && Time.time >= nextPulseTime)
        {
            BulletObject.ClearAllBullets();
            nextPulseTime = Time.time + 0.75f;
        }
    }

    private void UpdateShield()
    {
        if (isShield && Time.time >= shieldEndTime)
        {
            isShield = false;
            HideShieldView();
        }
    }

    private int GetHeroSkillType()
    {
        return Mathf.Clamp(GameDataMgr.Instance.nowSelHeroIndex + 1, 1, 5);
    }

    private float GetHeroSkillCD()
    {
        float baseCD = GameDataMgr.Instance.GetSkillCD();
        switch (GetHeroSkillType())
        {
            case 1:
                return baseCD + 2f;
            case 2:
                return baseCD + 3f;
            case 3:
                return baseCD + 1f;
            case 4:
                return baseCD + 4f;
            case 5:
                return baseCD;
        }

        return baseCD;
    }

    public string GetHeroSkillName()
    {
        switch (GetHeroSkillType())
        {
            case 1:
                return "爆破清屏";
            case 2:
                return "紧急维修";
            case 3:
                return "电磁干扰";
            case 4:
                return "能量脉冲";
            case 5:
                return "疾航推进";
        }

        return "专属技能";
    }

    public string GetHeroSkillIcon()
    {
        switch (GetHeroSkillType())
        {
            case 1:
                return "爆";
            case 2:
                return "修";
            case 3:
                return "扰";
            case 4:
                return "脉";
            case 5:
                return "速";
        }

        return "技";
    }

    public string GetSkillText()
    {
        string shieldText = isShield ? "E 护盾中" : Time.time >= nextShieldTime ? "E 护盾可用" : "E 护盾冷却 " + Mathf.CeilToInt(nextShieldTime - Time.time) + "s";
        string heroText = Time.time >= nextHeroSkillTime ? "Q " + GetHeroSkillName() + " 可用" : "Q " + GetHeroSkillName() + " 冷却 " + Mathf.CeilToInt(nextHeroSkillTime - Time.time) + "s";
        return shieldText + "    " + heroText;
    }

    public string GetShieldIconText()
    {
        if (isShield)
            return "盾";
        return Time.time >= nextShieldTime ? "盾" : Mathf.CeilToInt(nextShieldTime - Time.time).ToString();
    }

    public string GetHeroSkillIconText()
    {
        if (Time.time >= nextHeroSkillTime)
            return GetHeroSkillIcon();
        return Mathf.CeilToInt(nextHeroSkillTime - Time.time).ToString();
    }

    private void ShowShieldView()
    {
        if (shieldRoot == null)
        {
            CreateShieldView();
        }

        FitShieldToPlane();
        shieldRoot.SetActive(true);
    }

    private void CreateShieldView()
    {
        shieldVisualProfile = GetShieldVisualProfile();
        shieldRoot = new GameObject("ShieldViewRoot");
        shieldRoot.transform.SetParent(transform, false);
        shieldRoot.transform.localPosition = Vector3.zero;
        shieldRoot.transform.localRotation = Quaternion.identity;
        shieldRoot.transform.localScale = Vector3.one;

        shieldShell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shieldShell.name = "ShieldShell";
        shieldShell.transform.SetParent(shieldRoot.transform, false);
        DestroyShieldCollider(shieldShell);

        shieldShellMaterial = CreateShieldMaterial(
            "RuntimeShieldShellMaterial",
            shieldVisualProfile.ShellColor,
            shieldVisualProfile.ShellEmissionColor,
            0.78f,
            0.08f);
        shieldShell.GetComponent<Renderer>().sharedMaterial = shieldShellMaterial;

        shieldRingMaterial = CreateShieldMaterial(
            "RuntimeShieldRingMaterial",
            shieldVisualProfile.RingColor,
            shieldVisualProfile.RingEmissionColor,
            0.9f,
            0.12f);
        shieldNodeMaterial = CreateShieldMaterial(
            "RuntimeShieldNodeMaterial",
            shieldVisualProfile.NodeColor,
            shieldVisualProfile.NodeEmissionColor,
            0.95f,
            0.18f);

        shieldRings.Clear();
        for (int i = 0; i < shieldVisualProfile.RingCount; i++)
        {
            shieldRings.Add(CreateShieldRing(i));
        }

        shieldEnergyNodes.Clear();
        CreateShieldEnergyNodes();

        shieldRoot.SetActive(false);
    }

    private ShieldVisualProfile GetShieldVisualProfile()
    {
        RoleInfo roleInfo = GameDataMgr.Instance.GetNowSelHeroInfo();
        int fallbackStyle = Mathf.Clamp(GameDataMgr.Instance.nowSelHeroIndex + 1, 1, 5);
        int styleId = roleInfo != null && roleInfo.shieldStyle > 0
            ? roleInfo.shieldStyle
            : fallbackStyle;
        return ShieldVisualProfileCatalog.Get(styleId);
    }

    private Material CreateShieldMaterial(
        string materialName,
        Color color,
        Color emissionColor,
        float smoothness,
        float metallic)
    {
        Material material = new Material(Shader.Find("Standard"));
        material.name = materialName;
        material.color = color;
        material.SetColor("_EmissionColor", emissionColor);
        material.SetFloat("_Glossiness", smoothness);
        material.SetFloat("_Metallic", metallic);
        material.EnableKeyword("_EMISSION");
        SetTransparentMaterial(material);
        return material;
    }

    private void DestroyShieldCollider(GameObject obj)
    {
        Collider collider = obj.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            DestroyShieldObject(collider);
        }
    }

    private void DestroyShieldObject(Object obj)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(obj);
            return;
        }
#endif
        Destroy(obj);
    }

    private ShieldRingState CreateShieldRing(int index)
    {
        GameObject ring = new GameObject("ShieldRing" + (index + 1));
        ring.transform.SetParent(shieldRoot.transform, false);
        ring.transform.localPosition = Vector3.zero;
        ring.transform.localScale = Vector3.one;

        ShieldRingState state = new ShieldRingState
        {
            Root = ring,
            BaseRotation = GetShieldRingBaseRotation(index),
            Phase = (float)index / Mathf.Max(1, shieldVisualProfile.RingCount),
            Index = index,
        };
        ring.transform.localRotation = state.BaseRotation;

        LineRenderer line = ring.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 96;
        line.widthMultiplier = 0.16f;
        line.sharedMaterial = shieldRingMaterial;
        line.startColor = shieldVisualProfile.RingColor;
        line.endColor = shieldVisualProfile.RingColor;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        return state;
    }

    private void CreateShieldEnergyNodes()
    {
        int baseNodeCount = shieldVisualProfile.EnergyNodeCount / shieldRings.Count;
        int extraNodeCount = shieldVisualProfile.EnergyNodeCount % shieldRings.Count;
        int createdNodeCount = 0;

        for (int ringIndex = 0; ringIndex < shieldRings.Count; ringIndex++)
        {
            int ringNodeCount = baseNodeCount + (ringIndex < extraNodeCount ? 1 : 0);
            for (int nodeIndex = 0; nodeIndex < ringNodeCount; nodeIndex++)
            {
                GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                node.name = "ShieldEnergyNode" + (createdNodeCount + 1);
                node.transform.SetParent(shieldRings[ringIndex].Root.transform, false);
                node.transform.localRotation = Quaternion.identity;
                DestroyShieldCollider(node);

                Renderer renderer = node.GetComponent<Renderer>();
                renderer.sharedMaterial = shieldNodeMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                float normalizedAngle = (float)nodeIndex / ringNodeCount + ringIndex * 0.07f;
                shieldEnergyNodes.Add(new ShieldNodeState
                {
                    Transform = node.transform,
                    RingIndex = ringIndex,
                    NormalizedAngle = Mathf.Repeat(normalizedAngle, 1f),
                    Phase = (float)createdNodeCount / shieldVisualProfile.EnergyNodeCount,
                });
                createdNodeCount++;
            }
        }
    }

    private Quaternion GetShieldRingBaseRotation(int index)
    {
        switch (shieldVisualProfile.RotationPattern)
        {
            case ShieldRotationPattern.SingleOrbit:
                return Quaternion.Euler(12f, 0f, 0f);
            case ShieldRotationPattern.CounterRotating:
                return index == 0
                    ? Quaternion.Euler(24f, 0f, 18f)
                    : Quaternion.Euler(72f, 0f, 92f);
            case ShieldRotationPattern.Gyroscope:
                return Quaternion.Euler(18f + index * 52f, index * 37f, index * 24f);
            case ShieldRotationPattern.LockingOscillation:
                return Quaternion.Euler(12f + index * 38f, index * 45f, index * 90f);
            case ShieldRotationPattern.PrecessingSpiral:
                return Quaternion.Euler(index * 27f, index * 36f, 10f + index * 19f);
        }

        return Quaternion.identity;
    }

    private void SetTransparentMaterial(Material material)
    {
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

    private void FitShieldToPlane()
    {
        Bounds localBounds;
        if (!TryGetLocalRenderBounds(out localBounds))
        {
            localBounds = new Bounds(Vector3.zero, new Vector3(12f, 8f, 18f));
        }

        Vector3 size = localBounds.size;
        size.x = Mathf.Max(size.x, 4f);
        size.y = Mathf.Max(size.y, 4f);
        size.z = Mathf.Max(size.z, 4f);

        Vector3 padding = new Vector3(1.25f, 1.45f, 1.2f);
        shieldBaseScale = new Vector3(size.x * padding.x, size.y * padding.y, size.z * padding.z);

        shieldRoot.transform.localPosition = localBounds.center;
        shieldRoot.transform.localRotation = Quaternion.identity;

        shieldShell.transform.localPosition = Vector3.zero;
        shieldShell.transform.localRotation = Quaternion.identity;
        shieldShell.transform.localScale = shieldBaseScale;

        float ringRadius = Mathf.Max(shieldBaseScale.x, shieldBaseScale.z) * 0.54f;
        float lineWidth = Mathf.Max(0.08f, ringRadius * 0.025f);

        for (int i = 0; i < shieldRings.Count; i++)
        {
            ShieldRingState ring = shieldRings[i];
            ring.Radius = ringRadius * (1f - i * 0.055f);
            ring.Root.transform.localPosition = Vector3.zero;
            ring.Root.transform.localRotation = ring.BaseRotation;
            ring.Root.transform.localScale = Vector3.one;
            UpdateRingRenderer(ring, ring.Radius, lineWidth * (1f - i * 0.07f));
        }

        shieldNodeBaseScale = Mathf.Max(0.1f, ringRadius * shieldVisualProfile.EnergyNodeSize);
        for (int i = 0; i < shieldEnergyNodes.Count; i++)
        {
            ShieldNodeState node = shieldEnergyNodes[i];
            float angle = node.NormalizedAngle * Mathf.PI * 2f;
            float nodeRadius = shieldRings[node.RingIndex].Radius;
            node.Transform.localPosition = new Vector3(
                Mathf.Cos(angle) * nodeRadius,
                0f,
                Mathf.Sin(angle) * nodeRadius);
            node.Transform.localScale = Vector3.one * shieldNodeBaseScale;
        }
    }

    private void UpdateRingRenderer(ShieldRingState ring, float radius, float lineWidth)
    {
        LineRenderer line = ring.Root.GetComponent<LineRenderer>();
        if (line == null)
            return;

        line.widthMultiplier = lineWidth;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = (float)i / line.positionCount * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    private bool TryGetLocalRenderBounds(out Bounds localBounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        localBounds = new Bounds(Vector3.zero, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || (shieldRoot != null && renderers[i].transform.IsChildOf(shieldRoot.transform)))
                continue;

            Bounds worldBounds = renderers[i].bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;

            EncapsulateLocalPoint(ref localBounds, ref hasBounds, new Vector3(min.x, min.y, min.z));
            EncapsulateLocalPoint(ref localBounds, ref hasBounds, new Vector3(min.x, min.y, max.z));
            EncapsulateLocalPoint(ref localBounds, ref hasBounds, new Vector3(min.x, max.y, min.z));
            EncapsulateLocalPoint(ref localBounds, ref hasBounds, new Vector3(min.x, max.y, max.z));
            EncapsulateLocalPoint(ref localBounds, ref hasBounds, new Vector3(max.x, min.y, min.z));
            EncapsulateLocalPoint(ref localBounds, ref hasBounds, new Vector3(max.x, min.y, max.z));
            EncapsulateLocalPoint(ref localBounds, ref hasBounds, new Vector3(max.x, max.y, min.z));
            EncapsulateLocalPoint(ref localBounds, ref hasBounds, new Vector3(max.x, max.y, max.z));
        }

        return hasBounds;
    }

    private void EncapsulateLocalPoint(ref Bounds localBounds, ref bool hasBounds, Vector3 worldPoint)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        if (!hasBounds)
        {
            localBounds = new Bounds(localPoint, Vector3.zero);
            hasBounds = true;
        }
        else
        {
            localBounds.Encapsulate(localPoint);
        }
    }

    private void UpdateShieldView()
    {
        if (shieldRoot == null || !shieldRoot.activeSelf)
            return;

        float time = Time.time;
        float shellPulse = ShieldVisualProfileCatalog.EvaluatePulse(
            shieldVisualProfile.PulsePattern,
            time,
            shieldVisualProfile.PulseFrequency);
        shieldShell.transform.localScale = shieldBaseScale *
            (1f + shellPulse * shieldVisualProfile.PulseAmount);

        float emissionStrength = 0.72f + shellPulse * 0.58f;
        SetEmissionStrength(
            shieldShellMaterial,
            shieldVisualProfile.ShellEmissionColor,
            emissionStrength);
        SetEmissionStrength(
            shieldRingMaterial,
            shieldVisualProfile.RingEmissionColor,
            emissionStrength);
        SetEmissionStrength(
            shieldNodeMaterial,
            shieldVisualProfile.NodeEmissionColor,
            0.82f + shellPulse * 0.7f);

        for (int i = 0; i < shieldRings.Count; i++)
        {
            ShieldRingState ring = shieldRings[i];
            float ringPulse = ShieldVisualProfileCatalog.EvaluatePulse(
                shieldVisualProfile.PulsePattern,
                time,
                shieldVisualProfile.PulseFrequency,
                ring.Phase * 0.42f);
            ring.Root.transform.localScale = Vector3.one *
                (1f + ringPulse * shieldVisualProfile.PulseAmount * 0.55f);
            ring.Root.transform.localRotation = EvaluateRingRotation(ring, time);
        }

        for (int i = 0; i < shieldEnergyNodes.Count; i++)
        {
            ShieldNodeState node = shieldEnergyNodes[i];
            float nodePulse = ShieldVisualProfileCatalog.EvaluatePulse(
                shieldVisualProfile.PulsePattern,
                time,
                shieldVisualProfile.PulseFrequency,
                node.Phase * 0.32f);
            node.Transform.localScale = Vector3.one * shieldNodeBaseScale *
                (0.78f + nodePulse * 0.48f);
        }
    }

    private Quaternion EvaluateRingRotation(ShieldRingState ring, float time)
    {
        float direction = ring.Index % 2 == 0 ? 1f : -1f;
        float speed = shieldVisualProfile.RotationSpeed * (1f + ring.Index * 0.13f);
        Quaternion spin;

        switch (shieldVisualProfile.RotationPattern)
        {
            case ShieldRotationPattern.SingleOrbit:
                spin = Quaternion.AngleAxis(time * speed, Vector3.up);
                return ring.BaseRotation * spin;
            case ShieldRotationPattern.CounterRotating:
                spin = Quaternion.AngleAxis(time * speed * direction, Vector3.up);
                return ring.BaseRotation * spin;
            case ShieldRotationPattern.Gyroscope:
                spin = Quaternion.AngleAxis(time * speed * direction, Vector3.up);
                Quaternion wobble = Quaternion.AngleAxis(
                    Mathf.Sin(time * 0.9f + ring.Phase * Mathf.PI * 2f) * 14f,
                    GetRingMotionAxis(ring.Index));
                return wobble * ring.BaseRotation * spin;
            case ShieldRotationPattern.LockingOscillation:
                float lockAngle = Mathf.Sin(
                    time * (0.85f + ring.Index * 0.12f) + ring.Phase * Mathf.PI * 2f) * speed;
                return ring.BaseRotation * Quaternion.AngleAxis(lockAngle, Vector3.up);
            case ShieldRotationPattern.PrecessingSpiral:
                spin = Quaternion.AngleAxis(time * speed * direction, Vector3.up);
                Quaternion precession = Quaternion.AngleAxis(
                    time * (18f + ring.Index * 4f) * direction,
                    GetRingMotionAxis(ring.Index));
                return precession * ring.BaseRotation * spin;
        }

        return ring.BaseRotation;
    }

    private Vector3 GetRingMotionAxis(int index)
    {
        switch (index % 3)
        {
            case 0:
                return Vector3.forward;
            case 1:
                return Vector3.right;
            default:
                return new Vector3(1f, 0f, 1f).normalized;
        }
    }

    private void SetEmissionStrength(Material material, Color emissionColor, float strength)
    {
        if (material == null)
            return;

        material.SetColor(
            "_EmissionColor",
            new Color(
                emissionColor.r * strength,
                emissionColor.g * strength,
                emissionColor.b * strength,
                emissionColor.a));
    }

    private void HideShieldView()
    {
        if (shieldRoot != null)
        {
            shieldRoot.SetActive(false);
        }
    }
}

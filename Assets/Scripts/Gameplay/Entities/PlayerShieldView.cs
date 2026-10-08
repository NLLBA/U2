using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerShieldView : MonoBehaviour
{
    private sealed class RingState
    {
        public GameObject Root;
        public Quaternion BaseRot;
        public float Radius;
        public float Phase;
        public int Index;
    }

    private sealed class NodeState
    {
        public Transform Transform;
        public int Ring;
        public float Angle;
        public float Phase;
    }

    private GameObject root;
    private GameObject shell;
    private Material shellMat;
    private Material ringMat;
    private Material nodeMat;
    private ShieldStyle style;
    private readonly List<RingState> rings = new List<RingState>();
    private readonly List<NodeState> nodes = new List<NodeState>();
    private Vector3 baseScale = Vector3.one;
    private float nodeScale = 1f;

    internal void Show()
    {
        Build();
        Fit();
        root.SetActive(true);
    }

    internal void Hide()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    internal void Build()
    {
        if (root != null)
            return;

        style = LoadStyle();
        root = new GameObject("ShieldViewRoot");
        root.transform.SetParent(transform, false);

        shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shell.name = "ShieldShell";
        shell.transform.SetParent(root.transform, false);
        RemoveHitbox(shell);

        shellMat = CreateMat(
            "RuntimeShieldShellMaterial",
            style.ShellColor,
            style.ShellGlow,
            0.78f,
            0.08f);
        shell.GetComponent<Renderer>().sharedMaterial = shellMat;

        ringMat = CreateMat(
            "RuntimeShieldRingMaterial",
            style.RingColor,
            style.RingGlow,
            0.9f,
            0.12f);
        nodeMat = CreateMat(
            "RuntimeShieldNodeMaterial",
            style.NodeColor,
            style.NodeGlow,
            0.95f,
            0.18f);

        for (int i = 0; i < style.RingCount; i++)
        {
            rings.Add(BuildRing(i));
        }

        BuildNodes();
        root.SetActive(false);
    }

    internal void Tick()
    {
        if (root == null || !root.activeSelf)
            return;

        float time = Time.time;
        float shellPulse = ShieldStyles.EvalPulse(
            style.Pulse,
            time,
            style.PulseRate);
        shell.transform.localScale = baseScale * (1f + shellPulse * style.PulseScale);

        float glow = 0.72f + shellPulse * 0.58f;
        SetGlow(shellMat, style.ShellGlow, glow);
        SetGlow(ringMat, style.RingGlow, glow);
        SetGlow(nodeMat, style.NodeGlow, 0.82f + shellPulse * 0.7f);

        for (int i = 0; i < rings.Count; i++)
        {
            RingState ring = rings[i];
            float ringPulse = ShieldStyles.EvalPulse(
                style.Pulse,
                time,
                style.PulseRate,
                ring.Phase * 0.42f);
            ring.Root.transform.localScale = Vector3.one *
                (1f + ringPulse * style.PulseScale * 0.55f);
            ring.Root.transform.localRotation = RingRot(ring, time);
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            NodeState node = nodes[i];
            float nodePulse = ShieldStyles.EvalPulse(
                style.Pulse,
                time,
                style.PulseRate,
                node.Phase * 0.32f);
            node.Transform.localScale = Vector3.one * nodeScale *
                (0.78f + nodePulse * 0.48f);
        }
    }

    private void OnDestroy()
    {
        DestroyNow(shellMat);
        DestroyNow(ringMat);
        DestroyNow(nodeMat);
    }

    private ShieldStyle LoadStyle()
    {
        RoleInfo role = GameDataMgr.Instance.GetHero();
        int fallback = Mathf.Clamp(GameDataMgr.Instance.heroIndex + 1, 1, 5);
        int styleId = role != null && role.shieldId > 0 ? role.shieldId : fallback;
        return ShieldStyles.Get(styleId);
    }

    private Material CreateMat(
        string name,
        Color color,
        Color glow,
        float smooth,
        float metallic)
    {
        Material material = new Material(Shader.Find("Standard"));
        material.name = name;
        material.color = color;
        material.SetColor("_EmissionColor", glow);
        material.SetFloat("_Glossiness", smooth);
        material.SetFloat("_Metallic", metallic);
        material.EnableKeyword("_EMISSION");
        SetFade(material);
        return material;
    }

    private RingState BuildRing(int index)
    {
        GameObject ringObj = new GameObject("ShieldRing" + (index + 1));
        ringObj.transform.SetParent(root.transform, false);

        RingState ring = new RingState
        {
            Root = ringObj,
            BaseRot = BaseRot(index),
            Phase = (float)index / Mathf.Max(1, style.RingCount),
            Index = index,
        };
        ringObj.transform.localRotation = ring.BaseRot;

        LineRenderer line = ringObj.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 96;
        line.widthMultiplier = 0.16f;
        line.sharedMaterial = ringMat;
        line.startColor = style.RingColor;
        line.endColor = style.RingColor;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return ring;
    }

    private void BuildNodes()
    {
        int perRing = style.NodeCount / rings.Count;
        int remainder = style.NodeCount % rings.Count;
        int created = 0;

        for (int ring = 0; ring < rings.Count; ring++)
        {
            int count = perRing + (ring < remainder ? 1 : 0);
            for (int node = 0; node < count; node++)
            {
                GameObject nodeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                nodeObj.name = "ShieldEnergyNode" + (created + 1);
                nodeObj.transform.SetParent(rings[ring].Root.transform, false);
                RemoveHitbox(nodeObj);

                Renderer renderer = nodeObj.GetComponent<Renderer>();
                renderer.sharedMaterial = nodeMat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                float angle = (float)node / count + ring * 0.07f;
                nodes.Add(new NodeState
                {
                    Transform = nodeObj.transform,
                    Ring = ring,
                    Angle = Mathf.Repeat(angle, 1f),
                    Phase = (float)created / style.NodeCount,
                });
                created++;
            }
        }
    }

    private Quaternion BaseRot(int index)
    {
        switch (style.Rotation)
        {
            case ShieldRotation.SingleOrbit:
                return Quaternion.Euler(12f, 0f, 0f);
            case ShieldRotation.CounterRotating:
                return index == 0
                    ? Quaternion.Euler(24f, 0f, 18f)
                    : Quaternion.Euler(72f, 0f, 92f);
            case ShieldRotation.Gyroscope:
                return Quaternion.Euler(18f + index * 52f, index * 37f, index * 24f);
            case ShieldRotation.LockingOscillation:
                return Quaternion.Euler(12f + index * 38f, index * 45f, index * 90f);
            case ShieldRotation.PrecessingSpiral:
                return Quaternion.Euler(index * 27f, index * 36f, 10f + index * 19f);
            default:
                return Quaternion.identity;
        }
    }

    private void Fit()
    {
        Bounds bounds;
        if (!TryBounds(out bounds))
        {
            bounds = new Bounds(Vector3.zero, new Vector3(12f, 8f, 18f));
        }

        Vector3 size = bounds.size;
        size.x = Mathf.Max(size.x, 4f);
        size.y = Mathf.Max(size.y, 4f);
        size.z = Mathf.Max(size.z, 4f);

        Vector3 padding = new Vector3(1.25f, 1.45f, 1.2f);
        baseScale = new Vector3(size.x * padding.x, size.y * padding.y, size.z * padding.z);

        root.transform.localPosition = bounds.center;
        root.transform.localRotation = Quaternion.identity;
        shell.transform.localPosition = Vector3.zero;
        shell.transform.localRotation = Quaternion.identity;
        shell.transform.localScale = baseScale;

        float radius = Mathf.Max(baseScale.x, baseScale.z) * 0.54f;
        float width = Mathf.Max(0.08f, radius * 0.025f);
        for (int i = 0; i < rings.Count; i++)
        {
            RingState ring = rings[i];
            ring.Radius = radius * (1f - i * 0.055f);
            ring.Root.transform.localPosition = Vector3.zero;
            ring.Root.transform.localRotation = ring.BaseRot;
            ring.Root.transform.localScale = Vector3.one;
            SetLine(ring, ring.Radius, width * (1f - i * 0.07f));
        }

        nodeScale = Mathf.Max(0.1f, radius * style.NodeSize);
        for (int i = 0; i < nodes.Count; i++)
        {
            NodeState node = nodes[i];
            float angle = node.Angle * Mathf.PI * 2f;
            float ringSize = rings[node.Ring].Radius;
            node.Transform.localPosition = new Vector3(
                Mathf.Cos(angle) * ringSize,
                0f,
                Mathf.Sin(angle) * ringSize);
            node.Transform.localScale = Vector3.one * nodeScale;
        }
    }

    private void SetLine(RingState ring, float radius, float width)
    {
        LineRenderer line = ring.Root.GetComponent<LineRenderer>();
        if (line == null)
            return;

        line.widthMultiplier = width;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = (float)i / line.positionCount * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    private bool TryBounds(out Bounds bounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasData = false;
        bounds = new Bounds(Vector3.zero, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.transform.IsChildOf(root.transform))
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                AddPoint(ref bounds, ref hasData, point);
            }
        }

        return hasData;
    }

    private void AddPoint(ref Bounds bounds, ref bool hasData, Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        if (!hasData)
        {
            bounds = new Bounds(local, Vector3.zero);
            hasData = true;
            return;
        }

        bounds.Encapsulate(local);
    }

    private Quaternion RingRot(RingState ring, float time)
    {
        float direction = ring.Index % 2 == 0 ? 1f : -1f;
        float speed = style.RotateSpeed * (1f + ring.Index * 0.13f);
        Quaternion spin;

        switch (style.Rotation)
        {
            case ShieldRotation.SingleOrbit:
                spin = Quaternion.AngleAxis(time * speed, Vector3.up);
                return ring.BaseRot * spin;
            case ShieldRotation.CounterRotating:
                spin = Quaternion.AngleAxis(time * speed * direction, Vector3.up);
                return ring.BaseRot * spin;
            case ShieldRotation.Gyroscope:
                spin = Quaternion.AngleAxis(time * speed * direction, Vector3.up);
                Quaternion wobble = Quaternion.AngleAxis(
                    Mathf.Sin(time * 0.9f + ring.Phase * Mathf.PI * 2f) * 14f,
                    MoveAxis(ring.Index));
                return wobble * ring.BaseRot * spin;
            case ShieldRotation.LockingOscillation:
                float lockAngle = Mathf.Sin(
                    time * (0.85f + ring.Index * 0.12f) + ring.Phase * Mathf.PI * 2f) * speed;
                return ring.BaseRot * Quaternion.AngleAxis(lockAngle, Vector3.up);
            case ShieldRotation.PrecessingSpiral:
                spin = Quaternion.AngleAxis(time * speed * direction, Vector3.up);
                Quaternion precession = Quaternion.AngleAxis(
                    time * (18f + ring.Index * 4f) * direction,
                    MoveAxis(ring.Index));
                return precession * ring.BaseRot * spin;
            default:
                return ring.BaseRot;
        }
    }

    private Vector3 MoveAxis(int index)
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

    private void RemoveHitbox(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider == null)
            return;

        collider.enabled = false;
        DestroyNow(collider);
    }

    private void DestroyNow(Object target)
    {
        if (target == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(target);
            return;
        }
#endif
        Destroy(target);
    }

    private void SetFade(Material material)
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

    private void SetGlow(Material material, Color glow, float strength)
    {
        if (material == null)
            return;

        material.SetColor(
            "_EmissionColor",
            new Color(
                glow.r * strength,
                glow.g * strength,
                glow.b * strength,
                glow.a));
    }
}

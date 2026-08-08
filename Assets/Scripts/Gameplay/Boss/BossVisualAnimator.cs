using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossVisualAnimator : MonoBehaviour, IPoolableObject
{
    public Transform[] clockwiseParts = new Transform[0];
    public Transform[] counterClockwiseParts = new Transform[0];
    public Transform[] pulseParts = new Transform[0];
    public float clockwiseSpeed = 35f;
    public float counterClockwiseSpeed = 52f;
    public float pulseFrequency = 1.4f;
    [Range(0f, 0.25f)] public float pulseAmount = 0.06f;

    private Quaternion[] clockwiseBaseRotations;
    private Quaternion[] counterClockwiseBaseRotations;
    private Vector3[] pulseBaseScales;
    private float phaseOffset;
    private bool poseCached;

    private void Awake()
    {
        CachePose();
    }

    private void OnEnable()
    {
        if (!poseCached)
        {
            CachePose();
        }
    }

    private void Update()
    {
        RotateParts(clockwiseParts, clockwiseSpeed * Time.deltaTime);
        RotateParts(counterClockwiseParts, -counterClockwiseSpeed * Time.deltaTime);

        float pulse = 1f + Mathf.Sin(
            (Time.time + phaseOffset) * pulseFrequency * Mathf.PI * 2f) * pulseAmount;
        int count = Mathf.Min(pulseParts.Length, pulseBaseScales.Length);
        for (int i = 0; i < count; i++)
        {
            if (pulseParts[i] != null)
            {
                pulseParts[i].localScale = pulseBaseScales[i] * pulse;
            }
        }
    }

    public void OnPoolAcquire()
    {
        RestorePose();
        phaseOffset = Random.Range(0f, 1f);
    }

    public void OnPoolRelease()
    {
        RestorePose();
    }

    private void CachePose()
    {
        clockwiseParts = clockwiseParts ?? new Transform[0];
        counterClockwiseParts = counterClockwiseParts ?? new Transform[0];
        pulseParts = pulseParts ?? new Transform[0];

        clockwiseBaseRotations = CaptureRotations(clockwiseParts);
        counterClockwiseBaseRotations = CaptureRotations(counterClockwiseParts);
        pulseBaseScales = new Vector3[pulseParts.Length];
        for (int i = 0; i < pulseParts.Length; i++)
        {
            pulseBaseScales[i] = pulseParts[i] != null
                ? pulseParts[i].localScale
                : Vector3.one;
        }

        poseCached = true;
    }

    private void RestorePose()
    {
        if (!poseCached)
        {
            CachePose();
        }

        RestoreRotations(clockwiseParts, clockwiseBaseRotations);
        RestoreRotations(counterClockwiseParts, counterClockwiseBaseRotations);

        int count = Mathf.Min(pulseParts.Length, pulseBaseScales.Length);
        for (int i = 0; i < count; i++)
        {
            if (pulseParts[i] != null)
            {
                pulseParts[i].localScale = pulseBaseScales[i];
            }
        }
    }

    private static Quaternion[] CaptureRotations(Transform[] parts)
    {
        Quaternion[] rotations = new Quaternion[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            rotations[i] = parts[i] != null
                ? parts[i].localRotation
                : Quaternion.identity;
        }

        return rotations;
    }

    private static void RestoreRotations(Transform[] parts, Quaternion[] rotations)
    {
        int count = Mathf.Min(parts.Length, rotations.Length);
        for (int i = 0; i < count; i++)
        {
            if (parts[i] != null)
            {
                parts[i].localRotation = rotations[i];
            }
        }
    }

    private static void RotateParts(Transform[] parts, float angle)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] != null)
            {
                parts[i].Rotate(Vector3.up, angle, Space.Self);
            }
        }
    }
}

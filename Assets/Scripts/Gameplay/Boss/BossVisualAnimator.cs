using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class BossVisualAnimator : MonoBehaviour, IPoolable
{
    [FormerlySerializedAs("clockwiseParts")]
    public Transform[] spinParts = new Transform[0];
    [FormerlySerializedAs("counterClockwiseParts")]
    public Transform[] reverseParts = new Transform[0];
    public Transform[] pulseParts = new Transform[0];
    [FormerlySerializedAs("clockwiseSpeed")]
    public float spinSpeed = 35f;
    [FormerlySerializedAs("counterClockwiseSpeed")]
    public float reverseSpeed = 52f;
    [FormerlySerializedAs("pulseFrequency")]
    public float pulseRate = 1.4f;
    [FormerlySerializedAs("pulseAmount")]
    [Range(0f, 0.25f)] public float pulseSize = 0.06f;

    private Quaternion[] spinPose;
    private Quaternion[] reversePose;
    private Vector3[] pulsePose;
    private float phase;
    private bool cached;

    private void Awake()
    {
        Cache();
    }

    private void OnEnable()
    {
        if (!cached)
        {
            Cache();
        }
    }

    private void Update()
    {
        Rotate(spinParts, spinSpeed * Time.deltaTime);
        Rotate(reverseParts, -reverseSpeed * Time.deltaTime);

        float pulse = 1f + Mathf.Sin(
            (Time.time + phase) * pulseRate * Mathf.PI * 2f) * pulseSize;
        int count = Mathf.Min(pulseParts.Length, pulsePose.Length);
        for (int i = 0; i < count; i++)
        {
            if (pulseParts[i] != null)
            {
                pulseParts[i].localScale = pulsePose[i] * pulse;
            }
        }
    }

    public void OnAcquire()
    {
        ResetPose();
        phase = Random.Range(0f, 1f);
    }

    public void OnRelease()
    {
        ResetPose();
    }

    private void Cache()
    {
        spinParts = spinParts ?? new Transform[0];
        reverseParts = reverseParts ?? new Transform[0];
        pulseParts = pulseParts ?? new Transform[0];

        spinPose = ReadPose(spinParts);
        reversePose = ReadPose(reverseParts);
        pulsePose = new Vector3[pulseParts.Length];
        for (int i = 0; i < pulseParts.Length; i++)
        {
            pulsePose[i] = pulseParts[i] != null
                ? pulseParts[i].localScale
                : Vector3.one;
        }

        cached = true;
    }

    private void ResetPose()
    {
        if (!cached)
        {
            Cache();
        }

        ApplyPose(spinParts, spinPose);
        ApplyPose(reverseParts, reversePose);

        int count = Mathf.Min(pulseParts.Length, pulsePose.Length);
        for (int i = 0; i < count; i++)
        {
            if (pulseParts[i] != null)
            {
                pulseParts[i].localScale = pulsePose[i];
            }
        }
    }

    private static Quaternion[] ReadPose(Transform[] parts)
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

    private static void ApplyPose(Transform[] parts, Quaternion[] rotations)
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

    private static void Rotate(Transform[] parts, float angle)
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

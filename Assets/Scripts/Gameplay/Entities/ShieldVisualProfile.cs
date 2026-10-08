using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ShieldRotation
{
    SingleOrbit,
    CounterRotating,
    Gyroscope,
    LockingOscillation,
    PrecessingSpiral,
}

public enum ShieldPulse
{
    CalmWave,
    DoubleBeat,
    TravelingRipple,
    HeavyBeat,
    RapidSurge,
}

public sealed class ShieldStyle
{
    public readonly int Id;
    public readonly Color ShellColor;
    public readonly Color ShellGlow;
    public readonly Color RingColor;
    public readonly Color RingGlow;
    public readonly Color NodeColor;
    public readonly Color NodeGlow;
    public readonly int RingCount;
    public readonly ShieldRotation Rotation;
    public readonly float RotateSpeed;
    public readonly ShieldPulse Pulse;
    public readonly float PulseRate;
    public readonly float PulseScale;
    public readonly int NodeCount;
    public readonly float NodeSize;

    public ShieldStyle(
        int id,
        Color shellColor,
        Color shellGlow,
        Color ringColor,
        Color ringGlow,
        Color nodeColor,
        Color nodeGlow,
        int ringCount,
        ShieldRotation rotation,
        float rotateSpeed,
        ShieldPulse pulse,
        float pulseRate,
        float pulseScale,
        int nodeCount,
        float nodeSize)
    {
        Id = id;
        ShellColor = shellColor;
        ShellGlow = shellGlow;
        RingColor = ringColor;
        RingGlow = ringGlow;
        NodeColor = nodeColor;
        NodeGlow = nodeGlow;
        RingCount = ringCount;
        Rotation = rotation;
        RotateSpeed = rotateSpeed;
        Pulse = pulse;
        PulseRate = pulseRate;
        PulseScale = pulseScale;
        NodeCount = nodeCount;
        NodeSize = nodeSize;
    }
}

public static class ShieldStyles
{
    private static readonly ShieldStyle[] Items =
    {
        new ShieldStyle(
            1,
            new Color(0.02f, 0.35f, 1f, 0.28f),
            new Color(0f, 0.55f, 1.6f, 1f),
            new Color(0.2f, 0.85f, 1f, 0.95f),
            new Color(0.1f, 0.9f, 2f, 1f),
            new Color(0.75f, 0.97f, 1f, 1f),
            new Color(0.35f, 1.2f, 2.5f, 1f),
            1,
            ShieldRotation.SingleOrbit,
            72f,
            ShieldPulse.CalmWave,
            0.8f,
            0.035f,
            3,
            0.065f),
        new ShieldStyle(
            2,
            new Color(0.02f, 0.72f, 0.3f, 0.27f),
            new Color(0f, 1.15f, 0.42f, 1f),
            new Color(0.2f, 1f, 0.58f, 0.96f),
            new Color(0.08f, 1.65f, 0.55f, 1f),
            new Color(0.82f, 1f, 0.68f, 1f),
            new Color(0.45f, 2.1f, 0.72f, 1f),
            2,
            ShieldRotation.CounterRotating,
            118f,
            ShieldPulse.DoubleBeat,
            1.05f,
            0.055f,
            4,
            0.058f),
        new ShieldStyle(
            3,
            new Color(0.55f, 0.08f, 1f, 0.26f),
            new Color(0.85f, 0.12f, 1.75f, 1f),
            new Color(0.92f, 0.3f, 1f, 0.96f),
            new Color(1.35f, 0.25f, 2.1f, 1f),
            new Color(1f, 0.78f, 1f, 1f),
            new Color(1.8f, 0.55f, 2.4f, 1f),
            3,
            ShieldRotation.Gyroscope,
            148f,
            ShieldPulse.TravelingRipple,
            1.45f,
            0.045f,
            6,
            0.052f),
        new ShieldStyle(
            4,
            new Color(1f, 0.42f, 0.02f, 0.3f),
            new Color(1.8f, 0.58f, 0.03f, 1f),
            new Color(1f, 0.78f, 0.18f, 0.98f),
            new Color(2.2f, 1.1f, 0.12f, 1f),
            new Color(1f, 0.94f, 0.62f, 1f),
            new Color(2.5f, 1.65f, 0.35f, 1f),
            4,
            ShieldRotation.LockingOscillation,
            54f,
            ShieldPulse.HeavyBeat,
            0.62f,
            0.075f,
            8,
            0.05f),
        new ShieldStyle(
            5,
            new Color(1f, 0.04f, 0.09f, 0.25f),
            new Color(1.9f, 0.06f, 0.12f, 1f),
            new Color(1f, 0.28f, 0.08f, 0.97f),
            new Color(2.4f, 0.32f, 0.08f, 1f),
            new Color(1f, 0.82f, 0.42f, 1f),
            new Color(2.8f, 0.8f, 0.16f, 1f),
            5,
            ShieldRotation.PrecessingSpiral,
            212f,
            ShieldPulse.RapidSurge,
            2.2f,
            0.04f,
            10,
            0.045f),
    };

    public static int Count => Items.Length;

    public static ShieldStyle Get(int id)
    {
        int index = Mathf.Clamp(id, 1, Items.Length) - 1;
        return Items[index];
    }

    public static float EvalPulse(
        ShieldPulse pulse,
        float time,
        float rate,
        float offset = 0f)
    {
        float cycle = Mathf.Repeat(time * rate + offset, 1f);
        float phase = cycle * Mathf.PI * 2f;

        switch (pulse)
        {
            case ShieldPulse.CalmWave:
                return 0.5f + Mathf.Sin(phase) * 0.5f;
            case ShieldPulse.DoubleBeat:
                return Mathf.Clamp01(
                    Peak(cycle, 0.16f, 0.13f) +
                    Peak(cycle, 0.4f, 0.1f) * 0.7f);
            case ShieldPulse.TravelingRipple:
                return 0.5f + Mathf.Sin(phase + Mathf.Sin(phase * 0.5f) * 0.8f) * 0.5f;
            case ShieldPulse.HeavyBeat:
                return Mathf.Pow(0.5f + Mathf.Sin(phase) * 0.5f, 4f);
            case ShieldPulse.RapidSurge:
                return Mathf.Clamp01(
                    0.5f + Mathf.Sin(phase) * 0.32f + Mathf.Sin(phase * 3f) * 0.18f);
            default:
                return 0f;
        }
    }

    private static float Peak(float cycle, float center, float halfWidth)
    {
        float distance = Mathf.Abs(cycle - center);
        distance = Mathf.Min(distance, 1f - distance);
        float strength = 1f - Mathf.Clamp01(distance / halfWidth);
        return strength * strength;
    }
}

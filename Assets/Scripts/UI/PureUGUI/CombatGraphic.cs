using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CombatGraphic : MaskableGraphic
{
    public enum Shape { Hud, Timer, Dialog, Stat, Button, DangerButton, Keycap }

    [SerializeField] private Shape shape;
    [SerializeField] private Color accent = new Color(0.20f, 0.86f, 0.94f);

    public void Configure(Shape kind, Color tint)
    {
        shape = kind;
        accent = tint;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f) return;

        float cut = shape == Shape.Dialog ? 18f : shape == Shape.Keycap ? 6f : 10f;
        Color fill;
        switch (shape)
        {
            case Shape.Dialog: fill = new Color(0.018f, 0.043f, 0.065f, 0.985f); break;
            case Shape.Button: fill = new Color(0.035f, 0.125f, 0.17f, 0.98f); break;
            case Shape.DangerButton: fill = new Color(0.18f, 0.055f, 0.055f, 0.98f); break;
            case Shape.Keycap: fill = new Color(0.035f, 0.15f, 0.19f, 0.98f); break;
            case Shape.Stat: fill = new Color(0.018f, 0.05f, 0.072f, 0.88f); break;
            case Shape.Timer: fill = new Color(0.018f, 0.052f, 0.075f, 0.84f); break;
            default: fill = new Color(0.018f, 0.052f, 0.075f, 0.86f); break;
        }
        bool isButton = shape == Shape.Button || shape == Shape.DangerButton;
        Color border = shape == Shape.DangerButton
            ? new Color(0.92f, 0.24f, 0.18f, 0.92f)
            : new Color(accent.r, accent.g, accent.b,
                shape == Shape.Dialog ? 0.58f : isButton ? 0.78f : 0.46f);
        UIChromeMesh.AddChamfer(vh, rect, cut, UIChromeMesh.Multiply(fill, color));
        UIChromeMesh.AddChamferOutline(vh, rect, cut, 2f,
            UIChromeMesh.Multiply(border, color));

        if (shape == Shape.Dialog || shape == Shape.Hud || shape == Shape.Timer)
        {
            float rail = Mathf.Min(rect.width * 0.28f, shape == Shape.Dialog ? 130f : 78f);
            UIChromeMesh.AddQuad(vh, new Rect(rect.xMin + cut + 12f, rect.yMax - 2f, rail, 2f), accent);
            UIChromeMesh.AddQuad(vh, new Rect(rect.xMin + cut + 17f + rail, rect.yMax - 2f, 14f, 2f),
                new Color(1f, 0.57f, 0.22f, 0.9f));
        }
        if (shape == Shape.Stat)
            UIChromeMesh.AddQuad(vh, new Rect(rect.xMin + 1f, rect.yMin + 10f, 3f, rect.height - 20f), accent);
        if (isButton)
        {
            Color markerBase = new Color(border.r, border.g, border.b, Mathf.Max(0.9f, border.a));
            Color marker = UIChromeMesh.Multiply(markerBase, color);
            float markerHeight = Mathf.Max(0f, rect.height - 20f);
            UIChromeMesh.AddQuad(vh, new Rect(rect.xMin + 8f, rect.yMin + 10f, 3f,
                markerHeight), marker);
            UIChromeMesh.AddQuad(vh, new Rect(rect.xMax - 11f, rect.yMin + 10f, 3f,
                markerHeight), marker);
        }
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CombatHealthSegmentFrame : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f)
            return;
        UIChromeMesh.AddChamferOutline(vh, rect, 2f, 1.5f,
            UIChromeMesh.Multiply(new Color(0.18f, 0.78f, 0.86f, 0.95f), color));
    }
}

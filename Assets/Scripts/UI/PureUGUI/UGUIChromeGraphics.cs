using UnityEngine;
using UnityEngine.UI;

public enum UGUIChromeRole
{
    Primary,
    Secondary,
    Danger,
    Selected,
    Input,
    Easy,
    Normal,
    Hard,
}

public enum UGUIChromeIcon
{
    None,
    Play,
    Back,
    Network,
    Trophy,
    Audio,
    Power,
    Pause,
    Info,
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class UGUIButtonChrome : MaskableGraphic
{
    [SerializeField] private UGUIChromeRole role = UGUIChromeRole.Secondary;
    [SerializeField] private UGUIChromeIcon icon;
    [SerializeField] private bool emphasized;

    public UGUIChromeIcon Icon => icon;

    public void SetRole(UGUIChromeRole value)
    {
        if (role == value)
            return;
        role = value;
        SetVerticesDirty();
    }

    public void SetIcon(UGUIChromeIcon value)
    {
        if (icon == value)
            return;
        icon = value;
        SetVerticesDirty();
    }

    public void SetEmphasis(bool value)
    {
        if (emphasized == value)
            return;
        emphasized = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        Color fill;
        Color edge;
        Color glow;
        switch (role)
        {
            case UGUIChromeRole.Primary:
                fill = new Color(0.025f, 0.20f, 0.27f, 0.98f);
                edge = new Color(0.16f, 0.88f, 0.94f, 1f);
                glow = new Color(0.25f, 0.95f, 1f, 0.32f);
                break;
            case UGUIChromeRole.Danger:
                fill = new Color(0.25f, 0.045f, 0.045f, 0.98f);
                edge = new Color(1f, 0.29f, 0.20f, 1f);
                glow = new Color(1f, 0.30f, 0.15f, 0.28f);
                break;
            case UGUIChromeRole.Selected:
                fill = new Color(0.055f, 0.27f, 0.34f, 0.99f);
                edge = new Color(0.44f, 0.96f, 1f, 1f);
                glow = new Color(0.33f, 0.95f, 1f, 0.38f);
                break;
            case UGUIChromeRole.Input:
                fill = new Color(0.012f, 0.047f, 0.070f, 0.98f);
                edge = new Color(0.19f, 0.38f, 0.48f, 1f);
                glow = new Color(0.17f, 0.72f, 0.82f, 0.18f);
                break;
            case UGUIChromeRole.Easy:
                fill = new Color(0.018f, 0.14f, 0.105f, 0.98f);
                edge = new Color(0.22f, 0.88f, 0.58f, 1f);
                glow = new Color(0.22f, 1f, 0.64f, 0.30f);
                break;
            case UGUIChromeRole.Normal:
                fill = new Color(0.025f, 0.12f, 0.21f, 0.98f);
                edge = new Color(0.22f, 0.70f, 1f, 1f);
                glow = new Color(0.18f, 0.75f, 1f, 0.30f);
                break;
            case UGUIChromeRole.Hard:
                fill = new Color(0.23f, 0.07f, 0.035f, 0.98f);
                edge = new Color(1f, 0.52f, 0.18f, 1f);
                glow = new Color(1f, 0.32f, 0.10f, 0.32f);
                break;
            default:
                fill = new Color(0.022f, 0.095f, 0.135f, 0.98f);
                edge = new Color(0.16f, 0.48f, 0.60f, 1f);
                glow = new Color(0.15f, 0.72f, 0.82f, 0.18f);
                break;
        }

        if (emphasized)
        {
            fill = Color.Lerp(fill, edge, 0.28f);
            glow.a = Mathf.Max(glow.a, 0.48f);
        }

        fill = UIChromeMesh.Multiply(fill, color);
        edge = UIChromeMesh.Multiply(edge, color);
        glow = UIChromeMesh.Multiply(glow, color);

        float cut = Mathf.Clamp(rect.height * 0.18f, 6f, 13f);
        UIChromeMesh.AddChamfer(vh, UIChromeMesh.Expand(rect, 2f, -2f), cut + 1f,
            new Color(0f, 0.01f, 0.018f, 0.65f));
        UIChromeMesh.AddChamfer(vh, rect, cut, edge);
        UIChromeMesh.AddChamfer(vh, UIChromeMesh.Inset(rect, 1.5f), Mathf.Max(1f, cut - 1.5f), fill);
        UIChromeMesh.AddChamferOutline(vh, rect, cut, 2f, edge);

        float left = rect.xMin + 10f;
        UIChromeMesh.AddQuad(vh, new Rect(left, rect.yMin + 8f, 3f, rect.height - 16f), glow);
        UIChromeMesh.AddQuad(vh, new Rect(left + 8f, rect.yMax - 3f, Mathf.Min(54f, rect.width * 0.22f), 1.5f), edge);
        UIChromeMesh.AddDiamond(vh, new Vector2(rect.xMax - 18f, rect.center.y), 3.5f, edge);
        UIChromeMesh.AddQuad(vh, new Rect(rect.xMin + cut + 4f, rect.yMin + 2f,
            Mathf.Max(0f, rect.width - (cut + 4f) * 2f), 2f), glow);
        if (emphasized)
        {
            UIChromeMesh.AddQuad(vh, new Rect(rect.xMin + 11f, rect.yMin + 12f, 5f,
                Mathf.Max(0f, rect.height - 24f)), edge);
            UIChromeMesh.AddQuad(vh, new Rect(rect.xMin + 24f, rect.yMax - 6f,
                Mathf.Max(0f, rect.width - 48f), 3f), edge);
        }
        if (rect.width >= 150f && icon != UGUIChromeIcon.None)
            DrawIcon(vh, new Vector2(rect.xMin + 30f, rect.center.y), edge);
    }

    private void DrawIcon(VertexHelper vh, Vector2 center, Color iconColor)
    {
        switch (icon)
        {
            case UGUIChromeIcon.Play:
                UIChromeMesh.AddTriangle(vh, center + new Vector2(-4f, -7f),
                    center + new Vector2(-4f, 7f), center + new Vector2(7f, 0f), iconColor);
                break;
            case UGUIChromeIcon.Back:
                UIChromeMesh.AddTriangle(vh, center + new Vector2(-7f, 0f),
                    center + new Vector2(3f, 7f), center + new Vector2(3f, -7f), iconColor);
                UIChromeMesh.AddQuad(vh, new Rect(center.x + 1f, center.y - 1.2f, 8f, 2.4f), iconColor);
                break;
            case UGUIChromeIcon.Network:
                UIChromeMesh.AddQuad(vh, new Rect(center.x - 7f, center.y - 1f, 14f, 2f), iconColor);
                UIChromeMesh.AddQuad(vh, new Rect(center.x - 1f, center.y - 7f, 2f, 14f), iconColor);
                UIChromeMesh.AddDiamond(vh, center, 3f, iconColor);
                UIChromeMesh.AddDiamond(vh, center + new Vector2(-8f, 0f), 2.5f, iconColor);
                UIChromeMesh.AddDiamond(vh, center + new Vector2(8f, 0f), 2.5f, iconColor);
                UIChromeMesh.AddDiamond(vh, center + new Vector2(0f, 8f), 2.5f, iconColor);
                break;
            case UGUIChromeIcon.Trophy:
                UIChromeMesh.AddQuad(vh, new Rect(center.x - 6f, center.y, 12f, 7f), iconColor);
                UIChromeMesh.AddQuad(vh, new Rect(center.x - 1f, center.y - 6f, 2f, 7f), iconColor);
                UIChromeMesh.AddQuad(vh, new Rect(center.x - 6f, center.y - 8f, 12f, 2f), iconColor);
                UIChromeMesh.AddQuad(vh, new Rect(center.x - 9f, center.y + 2f, 3f, 3f), iconColor);
                UIChromeMesh.AddQuad(vh, new Rect(center.x + 6f, center.y + 2f, 3f, 3f), iconColor);
                break;
            case UGUIChromeIcon.Audio:
                UIChromeMesh.AddQuad(vh, new Rect(center.x - 8f, center.y - 3f, 4f, 6f), iconColor);
                UIChromeMesh.AddTriangle(vh, center + new Vector2(-4f, -3f),
                    center + new Vector2(2f, -7f), center + new Vector2(2f, 7f), iconColor);
                UIChromeMesh.AddQuad(vh, new Rect(center.x + 5f, center.y - 5f, 2f, 10f), iconColor);
                break;
            case UGUIChromeIcon.Power:
                UIChromeMesh.AddRing(vh, center, 8f, 5.5f, iconColor, 14, 0, 14);
                UIChromeMesh.AddQuad(vh, new Rect(center.x - 1.25f, center.y + 1f, 2.5f, 9f), iconColor);
                break;
            case UGUIChromeIcon.Pause:
                UIChromeMesh.AddQuad(vh, new Rect(center.x - 6f, center.y - 7f, 4f, 14f), iconColor);
                UIChromeMesh.AddQuad(vh, new Rect(center.x + 2f, center.y - 7f, 4f, 14f), iconColor);
                break;
            case UGUIChromeIcon.Info:
                UIChromeMesh.AddRing(vh, center, 8f, 6.5f, iconColor, 16, 0, 16);
                UIChromeMesh.AddQuad(vh, new Rect(center.x - 1f, center.y - 5f, 2f, 7f), iconColor);
                UIChromeMesh.AddQuad(vh, new Rect(center.x - 1f, center.y + 4f, 2f, 2f), iconColor);
                break;
        }
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class UGUISliderRailChrome : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        Rect rail = new Rect(rect.xMin + 12f, rect.center.y - 6f,
            Mathf.Max(0f, rect.width - 24f), 12f);
        UIChromeMesh.AddCapsule(vh, rail, UIChromeMesh.Multiply(
            new Color(0.10f, 0.40f, 0.48f, 1f), color));
        UIChromeMesh.AddCapsule(vh, UIChromeMesh.Inset(rail, 1.5f), UIChromeMesh.Multiply(
            new Color(0.012f, 0.070f, 0.090f, 1f), color));
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class UGUISliderFillChrome : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0.5f || rect.height <= 0.5f)
            return;

        Rect fill = new Rect(rect.xMin, rect.center.y - 4.5f, rect.width, 9f);
        UIChromeMesh.AddCapsule(vh, fill, UIChromeMesh.Multiply(
            new Color(0.10f, 0.82f, 0.88f, 1f), color));
        Rect highlight = new Rect(fill.xMin + 2f, fill.yMax - 2.5f,
            Mathf.Max(0f, fill.width - 4f), 1.2f);
        UIChromeMesh.AddCapsule(vh, highlight, UIChromeMesh.Multiply(
            new Color(0.65f, 0.98f, 1f, 0.58f), color));
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class UGUISliderHandleChrome : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        Color edge = UIChromeMesh.Multiply(new Color(0.42f, 0.94f, 1f, 1f), color);
        Color fill = UIChromeMesh.Multiply(new Color(0.88f, 0.98f, 1f, 1f), color);
        float radius = Mathf.Max(1f, Mathf.Min(rect.width, rect.height) * 0.5f);
        UIChromeMesh.AddDisc(vh, rect.center, radius, edge, 24);
        UIChromeMesh.AddDisc(vh, rect.center, Mathf.Max(0f, radius - 2.5f), fill, 24);
        UIChromeMesh.AddDisc(vh, rect.center + new Vector2(-2.5f, 2.5f),
            Mathf.Max(0f, radius * 0.22f), new Color(1f, 1f, 1f, 0.62f), 16);
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class UGUIRankRowChrome : MaskableGraphic
{
    [SerializeField] private int rank;

    public void Configure(int currentRank)
    {
        rank = Mathf.Max(1, currentRank);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        Color accent = rank == 1
            ? new Color(1f, 0.72f, 0.20f, 0.95f)
            : rank == 2
                ? new Color(0.55f, 0.83f, 0.94f, 0.92f)
                : rank == 3
                    ? new Color(0.94f, 0.50f, 0.23f, 0.92f)
                    : new Color(0.18f, 0.60f, 0.68f, 0.55f);
        Color surface = rank <= 3
            ? new Color(0.025f, 0.095f, 0.125f, 0.97f)
            : (rank % 2 == 0
                ? new Color(0.014f, 0.058f, 0.078f, 0.92f)
                : new Color(0.020f, 0.072f, 0.092f, 0.92f));

        UIChromeMesh.AddChamfer(vh, rect, 5f, UIChromeMesh.Multiply(surface, color));
        UIChromeMesh.AddQuad(vh, new Rect(rect.xMin, rect.yMin + 7f, 3f, rect.height - 14f),
            UIChromeMesh.Multiply(accent, color));
        if (rank <= 3)
        {
            UIChromeMesh.AddQuad(vh, new Rect(rect.xMin + 12f, rect.yMax - 1.5f,
                Mathf.Min(120f, rect.width * 0.16f), 1.5f), UIChromeMesh.Multiply(accent, color));
        }
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class UGUIScrollbarTrackChrome : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        Rect track = new Rect(rect.center.x - 3f, rect.yMin, 6f, rect.height);
        UIChromeMesh.AddChamfer(vh, track, 3f,
            UIChromeMesh.Multiply(new Color(0.025f, 0.105f, 0.14f, 0.95f), color));
        UIChromeMesh.AddQuad(vh, new Rect(track.center.x - 0.5f, track.yMin + 5f, 1f,
            Mathf.Max(0f, track.height - 10f)),
            UIChromeMesh.Multiply(new Color(0.22f, 0.70f, 0.78f, 0.42f), color));
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class UGUIScrollbarHandleChrome : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        Color rim = UIChromeMesh.Multiply(new Color(0.22f, 0.86f, 0.94f, 0.95f), color);
        Color fill = UIChromeMesh.Multiply(new Color(0.055f, 0.34f, 0.41f, 0.98f), color);
        UIChromeMesh.AddChamfer(vh, rect, Mathf.Min(5f, rect.width * 0.3f), rim);
        UIChromeMesh.AddChamfer(vh, UIChromeMesh.Inset(rect, 1.5f),
            Mathf.Min(3.5f, rect.width * 0.22f), fill);
        UIChromeMesh.AddChamferOutline(vh, rect, Mathf.Min(5f, rect.width * 0.3f), 1.5f, rim);
        for (int i = -1; i <= 1; i++)
            UIChromeMesh.AddQuad(vh, new Rect(rect.center.x - 3f, rect.center.y + i * 5f, 6f, 1f),
                new Color(0.72f, 0.96f, 1f, 0.72f));
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class UGUIPanelChrome : MaskableGraphic
{
    [SerializeField] private bool compact;

    public void Configure(bool compactPanel)
    {
        if (compact == compactPanel)
            return;
        compact = compactPanel;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        float cut = compact ? 9f : Mathf.Clamp(Mathf.Min(rect.width, rect.height) * 0.035f, 12f, 24f);
        Color shadow = new Color(0f, 0.012f, 0.020f, 0.28f);
        Color rim = new Color(0.14f, 0.48f, 0.58f, 0.72f);
        Color innerRim = new Color(0.30f, 0.88f, 0.94f, 0.12f);
        Color fill = compact
            ? new Color(0.020f, 0.070f, 0.095f, 0.90f)
            : new Color(0.014f, 0.055f, 0.080f, 0.95f);

        fill = UIChromeMesh.Multiply(fill, color);
        rim = UIChromeMesh.Multiply(rim, color);
        innerRim = UIChromeMesh.Multiply(innerRim, color);
        UIChromeMesh.AddChamfer(vh, UIChromeMesh.Expand(rect, 2f, -2f), cut + 2f, shadow);
        UIChromeMesh.AddChamfer(vh, rect, cut, rim);
        UIChromeMesh.AddChamfer(vh, UIChromeMesh.Inset(rect, 1.5f), Mathf.Max(1f, cut - 1.5f), fill);
        UIChromeMesh.AddChamferOutline(vh, rect, cut, 2f, rim);
        UIChromeMesh.AddChamferOutline(vh, UIChromeMesh.Inset(rect, 5f),
            Mathf.Max(1f, cut - 5f), 1.25f, innerRim);

        float markerWidth = Mathf.Min(compact ? 48f : 86f, rect.width * 0.22f);
        UIChromeMesh.AddQuad(vh, new Rect(rect.xMin + cut + 10f, rect.yMax - 3f, markerWidth, 3f),
            new Color(0.20f, 0.92f, 1f, 0.95f));
        UIChromeMesh.AddQuad(vh, new Rect(rect.xMin + cut + 14f + markerWidth, rect.yMax - 3f, 18f, 3f),
            new Color(1f, 0.57f, 0.18f, 0.85f));
        UIChromeMesh.AddDiamond(vh, new Vector2(rect.xMax - cut - 14f, rect.yMax - 12f), 3f,
            new Color(0.30f, 0.91f, 1f, 0.8f));

        float spacing = compact ? 24f : 32f;
        for (float y = rect.yMin + spacing; y < rect.yMax - 8f; y += spacing)
            UIChromeMesh.AddQuad(vh, new Rect(rect.xMin + cut, y, rect.width - cut * 2f, 0.7f),
                new Color(0.25f, 0.78f, 0.86f, compact ? 0.025f : 0.035f));

        AddCornerBrackets(vh, rect, cut);
    }

    private static void AddCornerBrackets(VertexHelper vh, Rect rect, float cut)
    {
        Color color = new Color(0.28f, 0.90f, 0.98f, 0.62f);
        float length = Mathf.Min(22f, rect.width * 0.08f);
        UIChromeMesh.AddQuad(vh, new Rect(rect.xMin + cut, rect.yMin + 5f, length, 1.5f), color);
        UIChromeMesh.AddQuad(vh, new Rect(rect.xMax - cut - length, rect.yMin + 5f, length, 1.5f), color);
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class UGUITacticalBackdrop : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        Color grid = new Color(0.22f, 0.72f, 0.78f, 0.035f);
        Color major = new Color(0.24f, 0.84f, 0.90f, 0.065f);
        const float step = 96f;
        int column = 0;
        for (float x = rect.xMin; x <= rect.xMax; x += step, column++)
            UIChromeMesh.AddQuad(vh, new Rect(x, rect.yMin, column % 4 == 0 ? 1f : 0.6f, rect.height),
                column % 4 == 0 ? major : grid);
        int row = 0;
        for (float y = rect.yMin; y <= rect.yMax; y += step, row++)
            UIChromeMesh.AddQuad(vh, new Rect(rect.xMin, y, rect.width, row % 4 == 0 ? 1f : 0.6f),
                row % 4 == 0 ? major : grid);

        Color rail = new Color(0.25f, 0.88f, 0.96f, 0.16f);
        UIChromeMesh.AddQuad(vh, new Rect(rect.xMin + 34f, rect.yMax - 72f, 130f, 2f), rail);
        UIChromeMesh.AddQuad(vh, new Rect(rect.xMax - 164f, rect.yMin + 72f, 130f, 2f), rail);
        UIChromeMesh.AddDiamond(vh, new Vector2(rect.xMin + 26f, rect.yMax - 71f), 4f,
            new Color(1f, 0.57f, 0.18f, 0.42f));
    }
}

internal static class UIChromeMesh
{
    public static Rect Inset(Rect rect, float amount)
    {
        return new Rect(rect.xMin + amount, rect.yMin + amount,
            Mathf.Max(0f, rect.width - amount * 2f), Mathf.Max(0f, rect.height - amount * 2f));
    }

    public static Rect Expand(Rect rect, float x, float y)
    {
        return new Rect(rect.xMin - x, rect.yMin - y, rect.width + x * 2f, rect.height + y * 2f);
    }

    public static Color Multiply(Color left, Color right)
    {
        return new Color(left.r * right.r, left.g * right.g, left.b * right.b, left.a * right.a);
    }

    public static void AddQuad(VertexHelper vh, Rect rect, Color color)
    {
        if (rect.width <= 0f || rect.height <= 0f)
            return;
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.up);
        vh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.one);
        vh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.right);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    public static void AddDiamond(VertexHelper vh, Vector2 center, float radius, Color color)
    {
        int start = vh.currentVertCount;
        vh.AddVert(center + Vector2.down * radius, color, Vector2.zero);
        vh.AddVert(center + Vector2.left * radius, color, Vector2.zero);
        vh.AddVert(center + Vector2.up * radius, color, Vector2.zero);
        vh.AddVert(center + Vector2.right * radius, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    public static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int start = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero);
        vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
    }

    public static void AddDisc(VertexHelper vh, Vector2 center, float radius, Color color, int segments)
    {
        if (radius <= 0f || segments < 3)
            return;
        int start = vh.currentVertCount;
        vh.AddVert(center, color, new Vector2(0.5f, 0.5f));
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                color, Vector2.zero);
        }
        for (int i = 0; i < segments; i++)
            vh.AddTriangle(start, start + i + 1, start + i + 2);
    }

    public static void AddCapsule(VertexHelper vh, Rect rect, Color color)
    {
        if (rect.width <= 0f || rect.height <= 0f)
            return;
        float radius = Mathf.Min(rect.height * 0.5f, rect.width * 0.5f);
        if (rect.width > radius * 2f)
            AddQuad(vh, new Rect(rect.xMin + radius, rect.yMin,
                rect.width - radius * 2f, rect.height), color);
        AddDisc(vh, new Vector2(rect.xMin + radius, rect.center.y), radius, color, 20);
        if (rect.width > radius * 2f)
            AddDisc(vh, new Vector2(rect.xMax - radius, rect.center.y), radius, color, 20);
    }

    public static void AddRing(VertexHelper vh, Vector2 center, float outerRadius, float innerRadius,
        Color color, int segments, int firstSegment, int lastSegment)
    {
        for (int i = firstSegment; i < lastSegment; i++)
        {
            float a0 = Mathf.PI * 2f * i / segments;
            float a1 = Mathf.PI * 2f * (i + 1) / segments;
            Vector2 outer0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * outerRadius;
            Vector2 outer1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * outerRadius;
            Vector2 inner0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * innerRadius;
            Vector2 inner1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * innerRadius;
            int start = vh.currentVertCount;
            vh.AddVert(outer0, color, Vector2.zero);
            vh.AddVert(outer1, color, Vector2.zero);
            vh.AddVert(inner1, color, Vector2.zero);
            vh.AddVert(inner0, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    public static void AddChamfer(VertexHelper vh, Rect rect, float cut, Color color)
    {
        Vector2[] points = Points(rect, cut);
        int center = vh.currentVertCount;
        vh.AddVert(rect.center, color, new Vector2(0.5f, 0.5f));
        for (int i = 0; i < points.Length; i++)
            vh.AddVert(points[i], color, Vector2.zero);
        for (int i = 0; i < points.Length; i++)
            vh.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % points.Length);
    }

    public static void AddChamferRing(VertexHelper vh, Rect outer, Rect inner, float cut, Color color)
    {
        if (outer.width <= 0f || outer.height <= 0f || inner.width <= 0f || inner.height <= 0f)
            return;

        float inset = Mathf.Max(0f, Mathf.Min(
            Mathf.Min(inner.xMin - outer.xMin, outer.xMax - inner.xMax),
            Mathf.Min(inner.yMin - outer.yMin, outer.yMax - inner.yMax)));
        Vector2[] outside = Points(outer, cut);
        Vector2[] inside = Points(inner, Mathf.Max(0f, cut - inset));
        for (int i = 0; i < outside.Length; i++)
        {
            int next = (i + 1) % outside.Length;
            int start = vh.currentVertCount;
            vh.AddVert(outside[i], color, Vector2.zero);
            vh.AddVert(outside[next], color, Vector2.zero);
            vh.AddVert(inside[next], color, Vector2.zero);
            vh.AddVert(inside[i], color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    public static void AddChamferOutline(VertexHelper vh, Rect rect, float cut, float thickness, Color color)
    {
        float maxThickness = Mathf.Min(rect.width, rect.height) * 0.25f;
        thickness = Mathf.Clamp(thickness, 0f, maxThickness);
        if (rect.width <= 0f || rect.height <= 0f || thickness <= 0f)
            return;

        // Keep the complete stroke on integer-aligned pixels inside the graphic rect.
        // Half-pixel outer edges can lose one triangle under the UI rasterizer's edge rule.
        const float pixelInset = 1f;
        Rect outer = Inset(rect, pixelInset);
        Rect inner = Inset(rect, pixelInset + thickness);
        AddChamferRing(vh, outer, inner, Mathf.Max(0f, cut - pixelInset), color);
    }

    private static Vector2[] Points(Rect rect, float cut)
    {
        cut = Mathf.Clamp(cut, 0f, Mathf.Min(rect.width, rect.height) * 0.45f);
        return new[]
        {
            new Vector2(rect.xMin + cut, rect.yMin),
            new Vector2(rect.xMax - cut, rect.yMin),
            new Vector2(rect.xMax, rect.yMin + cut),
            new Vector2(rect.xMax, rect.yMax - cut),
            new Vector2(rect.xMax - cut, rect.yMax),
            new Vector2(rect.xMin + cut, rect.yMax),
            new Vector2(rect.xMin, rect.yMax - cut),
            new Vector2(rect.xMin, rect.yMin + cut),
        };
    }
}

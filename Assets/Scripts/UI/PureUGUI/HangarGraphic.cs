using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class HangarGraphic : MaskableGraphic
{
    public enum Shape { Surface, Stage, Meter, Slot, Button }
    [SerializeField] private Shape shape;
    [SerializeField] private Color accent = new Color(0.23f, 0.85f, 0.92f);
    [SerializeField] private float value = 0.4f;
    [SerializeField] private bool selected;

    public void Configure(Shape kind, Color tint) { shape = kind; accent = tint; SetVerticesDirty(); }
    public void SetValue(float amount) { value = Mathf.Clamp01(amount); SetVerticesDirty(); }
    public void SetSelected(bool state) { if (selected != state) { selected = state; SetVerticesDirty(); } }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        if (shape == Shape.Meter)
        {
            Quad(vh, r, new Color(0.08f, 0.15f, 0.19f));
            Quad(vh, new Rect(r.xMin, r.yMin, r.width * value, r.height), accent);
            for (int i = 1; i < 10; i++)
                Quad(vh, new Rect(r.xMin + r.width * i / 10f, r.yMin, 2f, r.height), new Color(0.02f, 0.055f, 0.08f));
            return;
        }
        if (shape == Shape.Stage)
        {
            Color grid = new Color(accent.r, accent.g, accent.b, 0.06f);
            Vector2 center = r.center + new Vector2(0f, -r.height * 0.25f);
            for (int i = 0; i < 3; i++) Ellipse(vh, center, r.width * (0.27f + i * 0.06f), r.height * (0.10f + i * 0.03f), grid);
            Color edge = new Color(accent.r, accent.g, accent.b, 0.45f);
            foreach (float x in new[] { r.xMin, r.xMax - 24f })
            foreach (float y in new[] { r.yMin, r.yMax - 1f }) Quad(vh, new Rect(x, y, 24f, 1f), edge);
            foreach (float x in new[] { r.xMin, r.xMax - 1f })
            foreach (float y in new[] { r.yMin, r.yMax - 24f }) Quad(vh, new Rect(x, y, 1f, 24f), edge);
            return;
        }
        Color fill = shape == Shape.Surface ? new Color(0.025f, 0.047f, 0.075f, 0.97f) :
            selected ? new Color(0.04f, 0.19f, 0.25f) : new Color(0.035f, 0.075f, 0.11f);
        Color border = selected ? accent : new Color(0.14f, 0.24f, 0.30f, 0.85f);
        UIChromeMesh.AddChamfer(vh, r, shape == Shape.Surface ? 18f : 8f, UIChromeMesh.Multiply(fill, color));
        UIChromeMesh.AddChamferOutline(vh, r, shape == Shape.Surface ? 18f : 8f, 2f,
            UIChromeMesh.Multiply(border, color));
        if (shape == Shape.Surface)
        {
            Quad(vh, new Rect(r.xMin + 30f, r.yMax - 1f, 104f, 2f), accent);
            Quad(vh, new Rect(r.xMin + 140f, r.yMax - 1f, 20f, 2f), new Color(1f, 0.61f, 0.28f));
        }
        if (shape == Shape.Slot && selected)
            Quad(vh, new Rect(r.xMin + 1f, r.yMin + 10f, 3f, r.height - 20f), accent);
    }

    private void Quad(VertexHelper vh, Rect rect, Color tint) { UIChromeMesh.AddQuad(vh, rect, UIChromeMesh.Multiply(tint, color)); }

    private void Ellipse(VertexHelper vh, Vector2 center, float xRadius, float yRadius, Color tint)
    {
        for (int i = 0; i < 96; i++)
        {
            float a = i * Mathf.PI * 2f / 96f;
            float b = (i + 1) * Mathf.PI * 2f / 96f;
            Vector2 p = center + new Vector2(Mathf.Cos(a) * xRadius, Mathf.Sin(a) * yRadius);
            Vector2 q = center + new Vector2(Mathf.Cos(b) * xRadius, Mathf.Sin(b) * yRadius);
            Vector2 normal = (q - p).normalized;
            normal = new Vector2(-normal.y, normal.x) * 0.5f;
            UIChromeMesh.AddTriangle(vh, p - normal, p + normal, q + normal, tint);
            UIChromeMesh.AddTriangle(vh, p - normal, q + normal, q - normal, tint);
        }
    }
}

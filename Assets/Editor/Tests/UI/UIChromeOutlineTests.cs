using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class UIChromeOutlineTests
{
    [Test]
    public void ChamferOutlineCoversEveryStraightEdgeAndCorner()
    {
        using (VertexHelper helper = new VertexHelper())
        {
            MethodInfo method = typeof(UGUIButtonChrome).Assembly
                .GetType("UIChromeMesh")
                .GetMethod("AddChamferOutline", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[]
            {
                helper,
                new Rect(0f, 0f, 320f, 52f),
                10f,
                2f,
                Color.cyan,
            });

            Mesh mesh = new Mesh();
            helper.FillMesh(mesh);
            try
            {
                AssertCovered(mesh, new Vector2(80f, 1.25f), "bottom-left straight edge");
                AssertCovered(mesh, new Vector2(160f, 1.25f), "bottom-center straight edge");
                AssertCovered(mesh, new Vector2(240f, 1.25f), "bottom-right straight edge");
                AssertCovered(mesh, new Vector2(80f, 50.75f), "top-left straight edge");
                AssertCovered(mesh, new Vector2(160f, 50.75f), "top-center straight edge");
                AssertCovered(mesh, new Vector2(240f, 50.75f), "top-right straight edge");
                AssertCovered(mesh, new Vector2(1f, 26f), "left edge");
                AssertCovered(mesh, new Vector2(319f, 26f), "right edge");
                AssertCovered(mesh, new Vector2(5.5f, 5.5f), "bottom-left chamfer");
                AssertCovered(mesh, new Vector2(314.5f, 5.5f), "bottom-right chamfer");
                AssertCovered(mesh, new Vector2(5.5f, 46.5f), "top-left chamfer");
                AssertCovered(mesh, new Vector2(314.5f, 46.5f), "top-right chamfer");

                foreach (Vector3 vertex in mesh.vertices)
                {
                    Assert.That(vertex.x, Is.InRange(0f, 320f));
                    Assert.That(vertex.y, Is.InRange(0f, 52f));
                }
                Assert.AreEqual(1f, mesh.vertices.Min(vertex => vertex.y), 0.001f,
                    "The outer stroke must stay on an integer-aligned pixel inside the rect.");
                Assert.AreEqual(51f, mesh.vertices.Max(vertex => vertex.y), 0.001f,
                    "The outer stroke must stay on an integer-aligned pixel inside the rect.");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }

    [Test]
    public void CombatButtonsHaveMatchingLeftAndRightInnerRails()
    {
        GameObject buttonObject = new GameObject("CombatButtonMeshTest", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(CombatGraphic));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(640f, 56f);
        CombatGraphic graphic = buttonObject.GetComponent<CombatGraphic>();
        graphic.Configure(CombatGraphic.Shape.Button, Color.cyan);

        using (VertexHelper helper = new VertexHelper())
        {
            MethodInfo method = typeof(CombatGraphic).GetMethod("OnPopulateMesh",
                BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(VertexHelper) }, null);
            Assert.IsNotNull(method);
            method.Invoke(graphic, new object[] { helper });
            Mesh mesh = new Mesh();
            helper.FillMesh(mesh);
            try
            {
                Rect graphicRect = rect.rect;
                AssertHasVerticalRail(mesh, graphicRect.xMin + 8f, graphicRect.xMin + 11f,
                    "left inner rail");
                AssertHasVerticalRail(mesh, graphicRect.xMax - 11f, graphicRect.xMax - 8f,
                    "right inner rail");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(buttonObject);
            }
        }
    }

    private static void AssertCovered(Mesh mesh, Vector2 point, string edge)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            if (Contains(point, vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]))
                return;
        }
        Assert.Fail("The closed outline does not cover its " + edge + ".");
    }

    private static void AssertHasVerticalRail(Mesh mesh, float xMin, float xMax, string rail)
    {
        Vector3[] vertices = mesh.vertices;
        int minMatches = 0;
        int maxMatches = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (Mathf.Abs(vertices[i].x - xMin) < 0.01f) minMatches++;
            if (Mathf.Abs(vertices[i].x - xMax) < 0.01f) maxMatches++;
        }
        Assert.GreaterOrEqual(minMatches, 2, rail + " is missing its inner edge.");
        Assert.GreaterOrEqual(maxMatches, 2, rail + " is missing its outer edge.");
    }

    private static bool Contains(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(point, a, b);
        float d2 = Cross(point, b, c);
        float d3 = Cross(point, c, a);
        const float epsilon = 0.0001f;
        bool hasNegative = d1 < -epsilon || d2 < -epsilon || d3 < -epsilon;
        bool hasPositive = d1 > epsilon || d2 > epsilon || d3 > epsilon;
        return !(hasNegative && hasPositive);
    }

    private static float Cross(Vector2 point, Vector2 a, Vector2 b)
    {
        return (point.x - b.x) * (a.y - b.y) - (a.x - b.x) * (point.y - b.y);
    }
}

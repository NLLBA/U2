using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EntryUIPolishTests
{
    private const string BeginScenePath = "Assets/Scenes/BeginScene.unity";
    private const string PrimaryButtonPath = "Assets/ArtRes/UI/Polished/ui_button_primary.png";
    private const string SecondaryButtonPath = "Assets/ArtRes/UI/Polished/ui_button_secondary.png";
    private const string DangerButtonPath = "Assets/ArtRes/UI/Polished/ui_button_danger.png";

    [Test]
    public void SavedEntrySceneUsesPolishedMainMenuWithoutChangingBindings()
    {
        Scene scene = EditorSceneManager.OpenScene(BeginScenePath, OpenSceneMode.Single);
        GameObject menuObject = FindSceneObject(scene, "BeginPanel");
        BeginPanel menu = menuObject.GetComponent<BeginPanel>();

        Assert.IsNotNull(menu);
        Assert.IsTrue(menuObject.activeSelf);
        Assert.AreEqual("btnBegin", menu.btnBegin.name);
        Assert.AreEqual("btnRank", menu.btnRank.name);
        Assert.AreEqual("btnSet", menu.btnSetting.name);
        Assert.AreEqual("btnQuit", menu.btnQuit.name);

        AssertButton(menuObject.transform, menu.btnBegin, "开始任务", new Vector2(625f, 190f), PrimaryButtonPath);
        AssertButton(menuObject.transform, menu.btnRank, "排行榜", new Vector2(625f, 78f), SecondaryButtonPath);
        AssertButton(menuObject.transform, menu.btnSetting, "音频设置", new Vector2(625f, -34f), SecondaryButtonPath);
        AssertButton(menuObject.transform, menu.btnQuit, "退出游戏", new Vector2(625f, -146f), DangerButtonPath);

        AssertLabel(menuObject.transform, "labTitle", "星际突围");
        AssertLabel(menuObject.transform, "Polish_Subtitle", "深空防线");
        AssertLabel(menuObject.transform, "Polish_Mission", "守住航线，突破弹幕封锁");
        AssertLabel(menuObject.transform, "Polish_MenuHeader", "任务控制台");
        AssertLabel(menuObject.transform, "Polish_Status", "SYSTEM READY");

        AssertSingleDirectChild(menuObject.transform, "Polish_Scrim");
        AssertSingleDirectChild(menuObject.transform, "Polish_MenuPanel");
        AssertSingleDirectChild(menuObject.transform, "Polish_TitleAccent");
        AssertSingleDirectChild(menuObject.transform, "Polish_MenuAccent");
        AssertSingleDirectChild(menuObject.transform, "Polish_FooterLine");
        AssertSingleDirectChild(menuObject.transform, "Polish_StatusMark");
    }

    [Test]
    public void EntrySceneKeepsAllNguiPanelsAndRuntimeControllers()
    {
        Scene scene = EditorSceneManager.OpenScene(BeginScenePath, OpenSceneMode.Single);
        UIRoot[] roots = FindSceneComponents<UIRoot>(scene);

        Assert.AreEqual(1, roots.Length);
        Assert.AreEqual(UIRoot.Scaling.Constrained, roots[0].scalingStyle);
        Assert.AreEqual(NGUIAspectSafeLayout.DefaultReferenceWidth, roots[0].manualWidth);
        Assert.AreEqual(NGUIAspectSafeLayout.DefaultReferenceHeight, roots[0].manualHeight);
        Assert.IsNotNull(roots[0].GetComponent<NGUIAspectSafeLayout>());

        AssertPanelController<BeginPanel>(scene, "BeginPanel");
        AssertPanelController<ChoosePanel>(scene, "ChoosePanel");
        AssertPanelController<DifficultyPanel>(scene, "DifficultyPanel");
        AssertPanelController<RankPanel>(scene, "RankPanel");
        AssertPanelController<SettingPanel>(scene, "SetingPanel");
    }

    [Test]
    public void SafeAreaIsClampedAndConvertedToCameraViewport()
    {
        Rect viewport = NGUIAspectSafeLayout.CalculateViewport(
            2400, 1080, new Rect(120f, 24f, 2160f, 1032f));
        Assert.That(viewport.x, Is.EqualTo(0.05f).Within(0.0001f));
        Assert.That(viewport.y, Is.EqualTo(24f / 1080f).Within(0.0001f));
        Assert.That(viewport.width, Is.EqualTo(0.9f).Within(0.0001f));
        Assert.That(viewport.height, Is.EqualTo(1032f / 1080f).Within(0.0001f));

        Rect clamped = NGUIAspectSafeLayout.ClampSafeArea(
            2400, 1080, new Rect(-100f, -50f, 2700f, 1200f));
        Assert.AreEqual(new Rect(0f, 0f, 2400f, 1080f), clamped);

        Rect emptyFallback = NGUIAspectSafeLayout.ClampSafeArea(
            2400, 1080, new Rect(100f, 100f, 0f, 500f));
        Assert.AreEqual(new Rect(0f, 0f, 2400f, 1080f), emptyFallback);

        Rect invalidFallback = NGUIAspectSafeLayout.ClampSafeArea(
            2400, 1080, new Rect(float.NaN, 0f, 100f, 100f));
        Assert.AreEqual(new Rect(0f, 0f, 2400f, 1080f), invalidFallback);
    }

    [Test]
    public void PerspectiveCameraFitsTheViewportAspectIndependentlyFromRootScale()
    {
        const float referenceFieldOfView = 60f;
        const float narrowAspect = 4f / 3f;

        float narrowFieldOfView = NGUIAspectSafeLayout.CalculateVerticalFieldOfView(
            referenceFieldOfView, 1920f / 1080f, narrowAspect);
        Assert.Greater(narrowFieldOfView, referenceFieldOfView);

        float wideFieldOfView = NGUIAspectSafeLayout.CalculateVerticalFieldOfView(
            referenceFieldOfView, 1920f / 1080f, 21f / 9f);
        Assert.That(wideFieldOfView, Is.EqualTo(referenceFieldOfView).Within(0.0001f));
    }

    private static void AssertButton(Transform panel, UIButton button, string text, Vector2 expectedPosition,
        string expectedSpritePath)
    {
        Assert.IsNotNull(button);
        Assert.AreSame(panel, FindPanelRoot(button.transform));
        Assert.That(button.transform.localPosition.x, Is.EqualTo(expectedPosition.x).Within(0.01f));
        Assert.That(button.transform.localPosition.y, Is.EqualTo(expectedPosition.y).Within(0.01f));

        BoxCollider collider = button.GetComponent<BoxCollider>();
        Assert.IsNotNull(collider);
        Assert.IsTrue(collider.enabled);
        Assert.That(collider.size.x, Is.GreaterThanOrEqualTo(360f));
        Assert.That(collider.size.y, Is.GreaterThanOrEqualTo(82f));

        Assert.IsNull(FindDirectChild(button.transform, "Polish_ButtonVisual"));
        UI2DSprite visual = button.GetComponent<UI2DSprite>();
        Assert.IsNotNull(visual);
        Assert.IsTrue(visual.enabled);
        Assert.AreSame(visual.gameObject, button.tweenTarget);
        Assert.AreEqual(expectedSpritePath, AssetDatabase.GetAssetPath(visual.sprite2D));

        UILabel[] labels = button.GetComponentsInChildren<UILabel>(true);
        List<UILabel> enabledLabels = new List<UILabel>();
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i].enabled && labels[i].gameObject.activeSelf)
                enabledLabels.Add(labels[i]);
        }
        Assert.AreEqual(1, enabledLabels.Count);
        Assert.AreEqual(text, enabledLabels[0].text);
    }

    private static void AssertLabel(Transform root, string name, string expectedText)
    {
        Transform child = FindChild(root, name);
        Assert.IsNotNull(child, name + " is missing.");
        UILabel label = child.GetComponent<UILabel>();
        Assert.IsNotNull(label, name + " has no UILabel.");
        Assert.AreEqual(expectedText, label.text);
    }

    private static void AssertSingleDirectChild(Transform root, string name)
    {
        int count = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            if (root.GetChild(i).name == name)
                count++;
        }
        Assert.AreEqual(1, count, name + " must exist exactly once under BeginPanel.");
    }

    private static void AssertPanelController<T>(Scene scene, string name) where T : Component
    {
        GameObject panel = FindSceneObject(scene, name);
        Assert.IsNotNull(panel.GetComponent<UIPanel>(), name + " has no UIPanel.");
        Assert.IsNotNull(panel.GetComponent<T>(), name + " has no " + typeof(T).Name + ".");
    }

    private static Transform FindPanelRoot(Transform child)
    {
        Transform current = child;
        while (current != null && !current.name.EndsWith("Panel", System.StringComparison.Ordinal))
            current = current.parent;
        return current;
    }

    private static Transform FindDirectChild(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            if (root.GetChild(i).name == name)
                return root.GetChild(i);
        }
        return null;
    }

    private static Transform FindChild(Transform root, string name)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == name)
                return children[i];
        }
        return null;
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform child = FindChild(roots[i].transform, name);
            if (child != null)
                return child.gameObject;
        }
        Assert.Fail(name + " was not found in " + scene.path + ".");
        return null;
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        List<T> components = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            components.AddRange(roots[i].GetComponentsInChildren<T>(true));
        return components.ToArray();
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class EntryUIPolishTests
{
    [Test]
    public void EntrySceneUsesResponsiveNativeCanvas()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BeginScene.unity", OpenSceneMode.Single);
        Canvas canvas = UGUIRuntime.Canvas;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>());
        Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution);
        Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode);
    }

    [Test]
    public void EntryButtonsHaveNativeLabelsAndStableRects()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BeginScene.unity", OpenSceneMode.Single);
        BeginPanel panel = Object.FindObjectOfType<BeginPanel>(true);
        Button[] buttons = { panel.btnBegin, panel.btnRank, panel.btnSetting, panel.btnQuit };
        foreach (Button button in buttons)
        {
            Assert.IsNotNull(button);
            Assert.IsNotNull(UGUIRuntime.Label(button));
            Assert.Greater(button.GetComponent<RectTransform>().rect.width, 0f);
        }
    }

    [Test]
    public void AudioSettingsButtonHasExplicitAudioIcon()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BeginScene.unity", OpenSceneMode.Single);
        BeginPanel panel = Object.FindObjectOfType<BeginPanel>(true);
        Assert.AreEqual(UGUIChromeIcon.Audio, UGUIVisualSystem.ResolveIcon(panel.btnSetting));
    }

    [TestCase("Assets/Scenes/BeginScene.unity")]
    [TestCase("Assets/Scenes/GameScene.unity")]
    public void ProductionCanvasHasGlobalSafeAspectLayout(string scenePath)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Canvas canvas = UGUIRuntime.Canvas;
        UGUIAspectSafeLayout layout = canvas.GetComponent<UGUIAspectSafeLayout>();
        Assert.IsNotNull(layout);
        Assert.IsTrue(layout.UseSafeArea);
        Assert.AreEqual(UGUIAspectSafeLayout.DefaultWidth, layout.RefWidth);
        Assert.AreEqual(UGUIAspectSafeLayout.DefaultHeight, layout.RefHeight);
    }

    [Test]
    public void SafeLayoutMathFitsNarrowAndWideDisplays()
    {
        Assert.AreEqual(1440, UGUIAspectSafeLayout.FitHeight(1920, 1080, 4f / 3f));
        Assert.AreEqual(1080, UGUIAspectSafeLayout.FitHeight(1920, 1080, 21f / 9f));
        Assert.Greater(UGUIAspectSafeLayout.VerticalFov(60f, 16f / 9f, 4f / 3f), 60f);
        Assert.AreEqual(1f, UGUIAspectSafeLayout.SafeContentScale(2560, 1080,
            new Rect(0f, 0f, 2560f, 1080f)), 0.0001f);
        Assert.AreEqual(new Vector2(1920f, 1440f),
            UGUIAspectSafeLayout.ExpandedCanvasSize(1200, 900, 1920, 1080));
        Assert.AreEqual(new Vector2(2520f, 1080f),
            UGUIAspectSafeLayout.ExpandedCanvasSize(2100, 900, 1920, 1080));
        Assert.Less(UGUIAspectSafeLayout.SafeContentScale(1920, 1080,
            new Rect(0f, 80f, 1920f, 1000f)), 1f);
        Assert.AreEqual(new Vector2(-50f, 0f), UGUIAspectSafeLayout.SafeContentOffset(
            1920, 1080, new Rect(0f, 0f, 1820f, 1080f)));
    }

    [UnityTest]
    public IEnumerator RuntimeMovesEverySceneViewUnderAdaptiveRoot()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BeginScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return null;

        Canvas canvas = UGUIRuntime.Canvas;
        UGUIAspectSafeLayout layout = canvas.GetComponent<UGUIAspectSafeLayout>();
        RectTransform root = layout.ContentRoot;
        Assert.IsNotNull(root);
        Assert.AreEqual("UGUI_AdaptiveRoot", root.name);
        Assert.AreEqual(1, canvas.transform.childCount,
            "Every static and runtime-created UI view must share the adaptive root.");
        Assert.AreSame(root, canvas.transform.GetChild(0));
        Assert.IsTrue(UGUIRuntime.View(BeginPanel.Instance).IsChildOf(root));
        RectTransform beginView = UGUIRuntime.View(BeginPanel.Instance) as RectTransform;
        Assert.AreEqual(Vector2.zero, beginView.anchorMin);
        Assert.AreEqual(Vector2.one, beginView.anchorMax);
        Assert.AreEqual(Vector2.zero, beginView.sizeDelta);
        RectTransform scrim = beginView.Find("Polish_Scrim") as RectTransform;
        Assert.IsNotNull(scrim);
        Assert.AreEqual(Vector2.zero, scrim.anchorMin);
        Assert.AreEqual(Vector2.one, scrim.anchorMax);
        Assert.AreEqual(Vector2.zero, scrim.sizeDelta);

        yield return new ExitPlayMode();
    }
}

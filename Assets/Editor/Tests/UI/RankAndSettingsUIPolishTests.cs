using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class RankAndSettingsUIPolishTests
{
    private const string BeginScenePath = "Assets/Scenes/BeginScene.unity";
    private const string RankItemPrefabPath = "Assets/Resources/UI/RankItem.prefab";
    private const float Tolerance = 0.01f;

    [Test]
    public void RankPanelUsesAlignedColumnsAndABoundedVerticalScrollView()
    {
        Scene scene = EditorSceneManager.OpenScene(BeginScenePath, OpenSceneMode.Single);
        GameObject panel = FindSceneObject(scene, "RankPanel");
        RankPanel controller = panel.GetComponent<RankPanel>();

        Assert.IsNotNull(controller);
        Assert.IsTrue(panel.activeSelf,
            "RankPanel must initialize active so BasePanel.Start can bind events before hiding it.");
        Assert.AreEqual("btnClose", controller.btnClose.name);
        Assert.AreEqual("svList", controller.scrollView.name);
        Assert.AreEqual(UIScrollView.Movement.Vertical, controller.scrollView.movement);

        UILabel title = FindChildComponent<UILabel>(panel.transform, "lblTitle");
        Assert.AreEqual("飞行员排行榜", title.text);
        Assert.That(title.transform.localPosition.y, Is.EqualTo(350f).Within(Tolerance));

        UIPanel clipPanel = controller.scrollView.GetComponent<UIPanel>();
        Assert.IsNotNull(clipPanel);
        Assert.AreEqual(UIDrawCall.Clipping.SoftClip, clipPanel.clipping);
        AssertHasNoAnchors(clipPanel);
        AssertVector4(new Vector4(35f, 0f, 930f, 400f), clipPanel.baseClipRegion);
        AssertVector2(Vector2.zero, clipPanel.clipOffset);
        Assert.That(controller.scrollView.transform.localPosition.x, Is.EqualTo(-35f).Within(Tolerance));
        Assert.That(controller.scrollView.transform.localPosition.y, Is.EqualTo(-60f).Within(Tolerance));

        AssertDirectPolishObject(panel.transform, "Polish_Dialog", 1120, 860);
        AssertDirectPolishObject(panel.transform, "Polish_ListFrame", 1000, 500);
        AssertDirectPolishObject(panel.transform, "Polish_RankHeader", 930, 52);

        UILabel rankHeader = FindChildComponent<UILabel>(panel.transform, "Polish_RankColumnRank");
        UILabel playerHeader = FindChildComponent<UILabel>(panel.transform, "Polish_RankColumnPlayer");
        UILabel recordHeader = FindChildComponent<UILabel>(panel.transform, "Polish_RankColumnRecord");
        UIProgressBar scrollBar = FindChildComponent<UIProgressBar>(panel.transform, "polLeft");
        Assert.AreEqual(typeof(UIProgressBar), scrollBar.GetType());
        Assert.AreSame(scrollBar, controller.scrollView.verticalScrollBar);
        Assert.That(scrollBar.transform.localPosition.x, Is.EqualTo(475f).Within(Tolerance));
        Assert.That(scrollBar.transform.localPosition.y, Is.EqualTo(-60f).Within(Tolerance));
        Assert.AreEqual(UIProgressBar.FillDirection.TopToBottom, scrollBar.fillDirection);
        Assert.That(scrollBar.value, Is.EqualTo(0f).Within(Tolerance));
        Assert.AreSame(scrollBar.GetComponent<UIWidget>(), scrollBar.backgroundWidget);
        AssertHasNoAnchors(scrollBar.backgroundWidget);
        AssertHasNoAnchors(scrollBar.foregroundWidget);
        Assert.AreEqual(UIWidget.Pivot.Center, scrollBar.backgroundWidget.pivot);
        Assert.AreEqual(UIWidget.Pivot.Center, scrollBar.foregroundWidget.pivot);
        Assert.AreEqual(18, scrollBar.backgroundWidget.width);
        Assert.AreEqual(400, scrollBar.backgroundWidget.height);
        Assert.AreEqual(14, scrollBar.foregroundWidget.width);
        Assert.AreEqual(400, scrollBar.foregroundWidget.height);
        AssertVector3(Vector3.zero, scrollBar.foregroundWidget.transform.localPosition);
        AssertScrollBarCoversClip(scrollBar, controller.scrollView.transform, clipPanel);
        scrollBar.ForceUpdate();
        AssertVector4(new Vector4(0f, 1f - scrollBar.value, 1f, 1f),
            scrollBar.foregroundWidget.drawRegion);
        AssertVector3(new Vector3(34f, 400f, 0f), scrollBar.GetComponent<BoxCollider>().size);
        AssertColumn(rankHeader, "排名", -410f, 70, NGUIText.Alignment.Center, UIWidget.Pivot.Center);
        AssertColumn(playerHeader, "玩家", -350f, 220, NGUIText.Alignment.Left, UIWidget.Pivot.Left);
        AssertColumn(recordHeader, "作战记录", -95f, 560, NGUIText.Alignment.Left, UIWidget.Pivot.Left);

        AssertButton(controller.btnClose, "返回", Vector2.zero + new Vector2(0f, -365f), 240, 66);
        AssertAllLabelsUseNormalStyle(panel.transform);
    }

    [Test]
    public void RankItemPrefabKeepsBindingsAndFitsTheRuntimeRowSpacing()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RankItemPrefabPath);
        Assert.IsNotNull(prefab);

        RankItem item = prefab.GetComponent<RankItem>();
        UISprite background = prefab.GetComponent<UISprite>();
        BoxCollider collider = prefab.GetComponent<BoxCollider>();
        Assert.IsNotNull(item);
        Assert.IsNotNull(prefab.GetComponent<UIDragScrollView>());
        Assert.IsNotNull(prefab.GetComponent<RankItemDetailsPresenter>());
        Assert.IsNotNull(background);
        AssertHasNoAnchors(background);
        Assert.IsNotNull(background.atlas, "RankItem must keep using its existing NGUI atlas.");
        Assert.AreEqual("Dark", background.spriteName);
        Assert.AreEqual(930, background.width);
        Assert.AreEqual(54, background.height);
        Assert.IsNotNull(collider);
        AssertVector3(new Vector3(930f, 54f, 0f), collider.size);
        Assert.Less(background.height, 63, "RankItem must leave spacing between the existing 63-unit rows.");

        Assert.AreEqual("labRank", item.labRank.name);
        Assert.AreEqual("labName", item.labName.name);
        Assert.AreEqual("labTime", item.labTime.name);
        AssertItemColumn(item.labRank, -410f, 70, 50, NGUIText.Alignment.Center, UIWidget.Pivot.Center);
        AssertItemColumn(item.labName, -350f, 220, 50, NGUIText.Alignment.Left, UIWidget.Pivot.Left);
        AssertItemColumn(item.labTime, -95f, 560, 54, NGUIText.Alignment.Left, UIWidget.Pivot.Left);

        float rankRight = -410f + 35f;
        float playerLeft = -350f;
        float playerRight = playerLeft + 220f;
        float recordLeft = -95f;
        Assert.Greater(playerLeft - rankRight, 20f, "Rank and player columns overlap.");
        Assert.Greater(recordLeft - playerRight, 20f, "Player and record columns overlap.");
        Assert.That(recordLeft + 560f, Is.LessThanOrEqualTo(background.width * 0.5f + Tolerance));
    }

    [Test]
    public void SettingsPanelUsesTwoConsistentRowsWithoutChangingControlBindings()
    {
        Scene scene = EditorSceneManager.OpenScene(BeginScenePath, OpenSceneMode.Single);
        GameObject panel = FindSceneObject(scene, "SetingPanel");
        SettingPanel controller = panel.GetComponent<SettingPanel>();

        Assert.IsNotNull(controller);
        Assert.IsTrue(panel.activeSelf,
            "SetingPanel must initialize active so BasePanel.Start can bind events before hiding it.");
        Assert.AreEqual("btnClose", controller.btnClose.name);
        Assert.AreEqual("sildeMusic", controller.sliderMusic.name);
        Assert.AreEqual("sildeRound", controller.sliderSound.name);
        Assert.AreEqual("togMusic", controller.toggleMusic.name);
        Assert.AreEqual("togRound", controller.toggleSound.name);

        UILabel title = FindChildComponent<UILabel>(panel.transform, "lblTitle");
        Assert.AreEqual("音频设置", title.text);
        AssertDirectPolishObject(panel.transform, "Polish_Dialog", 960, 720);
        AssertDirectPolishObject(panel.transform, "Polish_SettingsRowMusic", 800, 132);
        AssertDirectPolishObject(panel.transform, "Polish_SettingsRowSound", 800, 132);

        AssertSettingLabel(panel.transform, "labMusic", "背景音乐", -350f, 65f);
        AssertSettingLabel(panel.transform, "labSound", "战斗音效", -350f, -95f);
        AssertSlider(controller.sliderMusic, -55f, 65f);
        AssertSlider(controller.sliderSound, -55f, -95f);
        AssertToggle(controller.toggleMusic, 315f, 65f);
        AssertToggle(controller.toggleSound, 315f, -95f);
        Assert.That(controller.sliderMusic.transform.localPosition.y - controller.sliderSound.transform.localPosition.y,
            Is.EqualTo(160f).Within(Tolerance));
        Assert.That(controller.toggleMusic.transform.localPosition.y - controller.toggleSound.transform.localPosition.y,
            Is.EqualTo(160f).Within(Tolerance));

        AssertButton(controller.btnClose, "返回", new Vector2(0f, -280f), 240, 66);
        AssertAllLabelsUseNormalStyle(panel.transform);
    }

    [UnityTest]
    public IEnumerator MainMenuKeepsRankAndSettingsTransitionsAndCloseEvents()
    {
        EditorSceneManager.OpenScene(BeginScenePath, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        SceneManager.LoadScene(BeginScenePath);
        yield return null;
        yield return null;

        Scene scene = SceneManager.GetActiveScene();
        BeginPanel menu = FindSceneObject(scene, "BeginPanel").GetComponent<BeginPanel>();
        RankPanel rank = FindSceneObject(scene, "RankPanel").GetComponent<RankPanel>();
        SettingPanel settings = FindSceneObject(scene, "SetingPanel").GetComponent<SettingPanel>();

        Assert.IsNotNull(RankPanel.Instance);
        Assert.IsNotNull(SettingPanel.Instance);
        Assert.IsFalse(rank.gameObject.activeSelf);
        Assert.IsFalse(settings.gameObject.activeSelf);

        EventDelegate.Execute(menu.btnRank.onClick);
        yield return null;
        Assert.IsTrue(rank.gameObject.activeSelf);
        UIPanel runtimeClipPanel = rank.scrollView.GetComponent<UIPanel>();
        AssertHasNoAnchors(runtimeClipPanel);
        AssertVector4(new Vector4(35f, 0f, 930f, 400f), runtimeClipPanel.baseClipRegion);
        UIProgressBar runtimeScrollBar = FindChildComponent<UIProgressBar>(rank.transform, "polLeft");
        Assert.That(runtimeScrollBar.transform.localPosition.x, Is.EqualTo(475f).Within(Tolerance));
        Assert.That(runtimeScrollBar.transform.localPosition.y, Is.EqualTo(-60f).Within(Tolerance));
        Assert.AreEqual(UIProgressBar.FillDirection.TopToBottom, runtimeScrollBar.fillDirection);
        Assert.AreEqual(400, runtimeScrollBar.backgroundWidget.height);
        Assert.AreEqual(400, runtimeScrollBar.foregroundWidget.height);
        Assert.AreEqual(UIWidget.Pivot.Center, runtimeScrollBar.foregroundWidget.pivot);
        AssertVector3(Vector3.zero, runtimeScrollBar.foregroundWidget.transform.localPosition);
        AssertScrollBarCoversClip(runtimeScrollBar, rank.scrollView.transform, runtimeClipPanel);
        runtimeScrollBar.ForceUpdate();
        AssertVector4(new Vector4(0f, 1f - runtimeScrollBar.value, 1f, 1f),
            runtimeScrollBar.foregroundWidget.drawRegion);
        EventDelegate.Execute(rank.btnClose.onClick);
        yield return null;
        Assert.IsFalse(rank.gameObject.activeSelf);

        EventDelegate.Execute(menu.btnSetting.onClick);
        yield return null;
        Assert.IsTrue(settings.gameObject.activeSelf);
        EventDelegate.Execute(settings.btnClose.onClick);
        yield return null;
        Assert.IsFalse(settings.gameObject.activeSelf);
        Assert.IsTrue(menu.gameObject.activeSelf);

        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator ExitPlayModeAfterEachTest()
    {
        if (Application.isPlaying)
            yield return new ExitPlayMode();
    }

    private static void AssertSlider(UISlider slider, float x, float y)
    {
        Assert.That(slider.transform.localPosition.x, Is.EqualTo(x).Within(Tolerance));
        Assert.That(slider.transform.localPosition.y, Is.EqualTo(y).Within(Tolerance));
        Assert.AreEqual(420, slider.backgroundWidget.width);
        Assert.AreEqual(18, slider.backgroundWidget.height);
        Assert.AreEqual(412, slider.foregroundWidget.width);
        Assert.AreEqual(12, slider.foregroundWidget.height);
        Assert.IsNotNull(slider.thumb);
        Assert.AreEqual(30, slider.thumb.GetComponent<UIWidget>().width);
        AssertVector3(new Vector3(450f, 54f, 0f), slider.GetComponent<BoxCollider>().size);
    }

    private static void AssertToggle(UIToggle toggle, float x, float y)
    {
        Assert.That(toggle.transform.localPosition.x, Is.EqualTo(x).Within(Tolerance));
        Assert.That(toggle.transform.localPosition.y, Is.EqualTo(y).Within(Tolerance));
        Assert.AreEqual(48, toggle.GetComponent<UIWidget>().width);
        Assert.AreEqual(48, toggle.GetComponent<UIWidget>().height);
        Assert.AreEqual(32, toggle.activeSprite.width);
        Assert.AreEqual(32, toggle.activeSprite.height);
        AssertVector3(new Vector3(60f, 60f, 0f), toggle.GetComponent<BoxCollider>().size);
    }

    private static void AssertSettingLabel(Transform panel, string name, string text, float x, float y)
    {
        UILabel label = FindChildComponent<UILabel>(panel, name);
        Assert.AreEqual(text, label.text);
        Assert.That(label.transform.localPosition.x, Is.EqualTo(x).Within(Tolerance));
        Assert.That(label.transform.localPosition.y, Is.EqualTo(y).Within(Tolerance));
        Assert.AreEqual(190, label.width);
        Assert.AreEqual(48, label.height);
    }

    private static void AssertColumn(UILabel label, string text, float x, int width,
        NGUIText.Alignment alignment, UIWidget.Pivot pivot)
    {
        Assert.IsNotNull(label);
        Assert.AreEqual(text, label.text);
        Assert.That(label.transform.localPosition.x, Is.EqualTo(x).Within(Tolerance));
        Assert.AreEqual(width, label.width);
        Assert.AreEqual(alignment, label.alignment);
        Assert.AreEqual(pivot, label.pivot);
    }

    private static void AssertItemColumn(UILabel label, float x, int width, int height,
        NGUIText.Alignment alignment, UIWidget.Pivot pivot)
    {
        Assert.IsNotNull(label);
        Assert.That(label.transform.localPosition.x, Is.EqualTo(x).Within(Tolerance));
        Assert.AreEqual(width, label.width);
        Assert.AreEqual(height, label.height);
        Assert.AreEqual(alignment, label.alignment);
        Assert.AreEqual(pivot, label.pivot);
        Assert.AreEqual(UILabel.Overflow.ShrinkContent, label.overflowMethod);
        Assert.AreEqual(FontStyle.Normal, label.fontStyle);
    }

    private static void AssertScrollBarCoversClip(UIProgressBar scrollBar, Transform scrollView, UIPanel clipPanel)
    {
        float clipTop = scrollView.localPosition.y + clipPanel.baseClipRegion.y + clipPanel.baseClipRegion.w * 0.5f;
        float clipBottom = scrollView.localPosition.y + clipPanel.baseClipRegion.y - clipPanel.baseClipRegion.w * 0.5f;
        float foregroundTop = scrollBar.transform.localPosition.y + scrollBar.foregroundWidget.transform.localPosition.y +
                              scrollBar.foregroundWidget.height * 0.5f;
        float foregroundBottom = scrollBar.transform.localPosition.y + scrollBar.foregroundWidget.transform.localPosition.y -
                                 scrollBar.foregroundWidget.height * 0.5f;

        Assert.That(foregroundTop, Is.EqualTo(clipTop).Within(Tolerance));
        Assert.That(foregroundBottom, Is.EqualTo(clipBottom).Within(Tolerance));
    }

    private static void AssertAllLabelsUseNormalStyle(Transform root)
    {
        UILabel[] labels = root.GetComponentsInChildren<UILabel>(true);
        Assert.IsNotEmpty(labels);
        for (int i = 0; i < labels.Length; i++)
            Assert.AreEqual(FontStyle.Normal, labels[i].fontStyle, labels[i].name + " must use Normal font style.");
    }

    private static void AssertDirectPolishObject(Transform panel, string name, int width, int height)
    {
        Transform child = panel.Find(name);
        Assert.IsNotNull(child, name + " is missing or is not a direct panel child.");
        UIWidget widget = child.GetComponent<UIWidget>();
        Assert.IsNotNull(widget);
        Assert.AreEqual(width, widget.width);
        Assert.AreEqual(height, widget.height);
    }

    private static void AssertButton(UIButton button, string text, Vector2 position, int width, int height)
    {
        Assert.That(button.transform.localPosition.x, Is.EqualTo(position.x).Within(Tolerance));
        Assert.That(button.transform.localPosition.y, Is.EqualTo(position.y).Within(Tolerance));
        UIWidget widget = button.GetComponent<UIWidget>();
        Assert.IsNotNull(widget);
        Assert.AreEqual(width, widget.width);
        Assert.AreEqual(height, widget.height);
        AssertVector3(new Vector3(width, height, 0f), button.GetComponent<BoxCollider>().size);

        UILabel[] labels = button.GetComponentsInChildren<UILabel>(true);
        int visibleCount = 0;
        UILabel visibleLabel = null;
        for (int i = 0; i < labels.Length; i++)
        {
            if (!labels[i].enabled || !labels[i].gameObject.activeSelf)
                continue;
            visibleCount++;
            visibleLabel = labels[i];
        }
        Assert.AreEqual(1, visibleCount);
        Assert.AreEqual(text, visibleLabel.text);
    }

    private static T FindChildComponent<T>(Transform root, string name) where T : Component
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == name)
                return children[i].GetComponent<T>();
        }
        Assert.Fail(name + " was not found under " + root.name + ".");
        return null;
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] children = roots[i].GetComponentsInChildren<Transform>(true);
            for (int childIndex = 0; childIndex < children.Length; childIndex++)
            {
                if (children[childIndex].name == name)
                    return children[childIndex].gameObject;
            }
        }
        Assert.Fail(name + " was not found in " + scene.path + ".");
        return null;
    }

    private static void AssertVector2(Vector2 expected, Vector2 actual)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
    }

    private static void AssertVector3(Vector3 expected, Vector3 actual)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
    }

    private static void AssertVector4(Vector4 expected, Vector4 actual)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        Assert.That(actual.w, Is.EqualTo(expected.w).Within(Tolerance));
    }

    private static void AssertHasNoAnchors(UIRect rect)
    {
        Assert.IsNotNull(rect);
        Assert.IsNull(rect.leftAnchor.target);
        Assert.IsNull(rect.rightAnchor.target);
        Assert.IsNull(rect.bottomAnchor.target);
        Assert.IsNull(rect.topAnchor.target);
    }
}

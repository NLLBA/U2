using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class RankAndSettingsUIPolishTests
{
    [Test]
    public void SettingsUseNativeContinuousAndBinaryControls()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BeginScene.unity", OpenSceneMode.Single);
        SettingPanel panel = Object.FindObjectOfType<SettingPanel>(true);
        Assert.IsNotNull(panel.sliderMusic);
        Assert.IsNotNull(panel.sliderSound);
        Assert.IsNotNull(panel.toggleMusic);
        Assert.IsNotNull(panel.toggleSound);
        Assert.AreEqual(0f, panel.sliderMusic.minValue);
        Assert.AreEqual(1f, panel.sliderMusic.maxValue);
    }

    [Test]
    public void RankPanelUsesNativeScrollContent()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BeginScene.unity", OpenSceneMode.Single);
        RankPanel panel = Object.FindObjectOfType<RankPanel>(true);
        Assert.IsNotNull(panel.scrollView);
        Assert.IsNotNull(panel.scrollView.viewport);
        Assert.IsNotNull(panel.scrollView.content);
        Assert.IsFalse(panel.scrollView.horizontal);
        Assert.IsTrue(panel.scrollView.vertical);
    }

    [Test]
    public void RankPrefabHasDirectNativeTextReferences()
    {
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/RankItem.prefab");
        RankItem item = prefab.GetComponentInChildren<RankItem>(true);
        Assert.IsNotNull(item.labRank);
        Assert.IsNotNull(item.labName);
        Assert.IsNotNull(item.labTime);
        Assert.That(item.labRank, Is.TypeOf<Text>());
    }
}

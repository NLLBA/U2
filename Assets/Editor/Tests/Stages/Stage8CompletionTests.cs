using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class XmlRecoveryTestData
{
    public int value;
}

public class Stage8CompletionTests
{
    private string temporaryDirectory;

    [SetUp]
    public void SetUp()
    {
        temporaryDirectory = Path.Combine(Path.GetTempPath(), "U2Stage8-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }

    [Test]
    public void CorruptPersistentXmlIsBackedUpAndRecoveredFromFallback()
    {
        string dataPath = Path.Combine(temporaryDirectory, "Data.xml");
        string defaultPath = Path.Combine(temporaryDirectory, "Default.xml");
        XmlDataMgr.Instance.SavePath(new XmlRecoveryTestData { value = 42 }, defaultPath);
        File.WriteAllText(dataPath, "<broken>");

        LogAssert.Expect(LogType.Warning, new Regex("XML data was corrupt and has been backed up"));
        XmlRecoveryTestData result = XmlDataMgr.Instance.LoadPaths(
            typeof(XmlRecoveryTestData), dataPath, defaultPath) as XmlRecoveryTestData;

        Assert.IsNotNull(result);
        Assert.AreEqual(42, result.value);
        Assert.IsTrue(File.Exists(dataPath));
        Assert.AreEqual(1, Directory.GetFiles(temporaryDirectory, "Data.corrupt-*.xml").Length);

        XmlRecoveryTestData persisted = XmlDataMgr.Instance.LoadPaths(
            typeof(XmlRecoveryTestData), dataPath, defaultPath) as XmlRecoveryTestData;
        Assert.IsNotNull(persisted);
        Assert.AreEqual(42, persisted.value);
    }

    [Test]
    public void CorruptPersistentXmlWithoutFallbackRestoresDefaultObject()
    {
        string dataPath = Path.Combine(temporaryDirectory, "Data.xml");
        string missingFallbackPath = Path.Combine(temporaryDirectory, "Missing.xml");
        File.WriteAllText(dataPath, "not xml");

        LogAssert.Expect(LogType.Warning, new Regex("XML data was corrupt and has been backed up"));
        XmlRecoveryTestData result = XmlDataMgr.Instance.LoadPaths(
            typeof(XmlRecoveryTestData), dataPath, missingFallbackPath) as XmlRecoveryTestData;

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.value);
        Assert.IsTrue(File.Exists(dataPath));
        Assert.AreEqual(1, Directory.GetFiles(temporaryDirectory, "Data.corrupt-*.xml").Length);
    }

    [Test]
    public void BeginSceneDoesNotContainOperationHelp()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/BeginScene.unity", OpenSceneMode.Single);
        GameObject help = FindSceneObject(scene, "labOperationHelp");

        Assert.IsNull(help);
    }

    [Test]
    public void GameSceneContainsStage8Controls()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        GamePanel gamePanel = FindSceneComponent<GamePanel>(scene, "GamePanel");
        QuitPanel quitPanel = FindSceneComponent<QuitPanel>(scene, "QuitPanel");
        GameOverPanel gameOverPanel = FindSceneComponent<GameOverPanel>(scene, "GameOverPanel");

        Assert.IsNotNull(gamePanel);
        Assert.IsNotNull(gamePanel.labBossWarning);
        Assert.IsNotNull(quitPanel);
        Assert.IsNotNull(quitPanel.btnRestart);
        Assert.IsNotNull(gameOverPanel);
        Assert.IsNotNull(gameOverPanel.btnRestart);
    }

    private static T FindSceneComponent<T>(Scene scene, string objectName) where T : Component
    {
        GameObject obj = FindSceneObject(scene, objectName);
        return obj != null ? obj.GetComponent<T>() : null;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j].name == objectName)
                {
                    return transforms[j].gameObject;
                }
            }
        }

        return null;
    }
}

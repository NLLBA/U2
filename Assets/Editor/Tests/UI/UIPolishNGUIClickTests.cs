using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class UIPolishNGUIClickTests
{
    private const string BeginScenePath = "Assets/Scenes/BeginScene.unity";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string TestSceneRoot = "Assets/__U2UIPolishTests/UIPolishNGUIClickTests";
    private const string TestBeginScenePath = TestSceneRoot + "/BeginScene.unity";
    private const string TestGameScenePath = TestSceneRoot + "/GameScene.unity";
    private const string BeginSceneHashKey = "U2.UIPolishTests.BeginSceneHash";
    private const string GameSceneHashKey = "U2.UIPolishTests.GameSceneHash";
    private int validatedRaycasts;
    private RenderTexture raycastTarget;

    private static readonly PanelButtons[] BeginPanels =
    {
        new PanelButtons("BeginPanel", "btnBegin", "btnRank", "btnSet", "btnQuit"),
        new PanelButtons("ChoosePanel", "btnLeft", "btnRight", "btnStart", "btnClose"),
        new PanelButtons("DifficultyPanel", "btnEasy", "btnNormal", "btnHard", "btnStart", "btnClose"),
        new PanelButtons("RankPanel", "btnClose"),
        new PanelButtons("SetingPanel", "btnClose")
    };

    private static readonly PanelButtons[] GamePanels =
    {
        new PanelButtons("GamePanel", "btnClose"),
        new PanelButtons("QuitPanel", "btnSure", "btnRestart", "btnClose"),
        new PanelButtons("GameOverPanel", "btnOK", "btnRestart")
    };

    private static readonly Vector2Int[] SupportedResolutions =
    {
        new Vector2Int(1366, 768),
        new Vector2Int(1920, 1080),
        new Vector2Int(1920, 1200),
        new Vector2Int(2560, 1440),
        new Vector2Int(1440, 1080),
        new Vector2Int(2560, 1080),
        new Vector2Int(3440, 1440),
        new Vector2Int(2340, 1080),
        new Vector2Int(1080, 1920)
    };

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        UIPolishTestArtifacts.Cleanup();
        UIPolishTestArtifacts.AssertProductionStateIsClean();
        SessionState.SetString(BeginSceneHashKey, ComputeFileHash(BeginScenePath));
        SessionState.SetString(GameSceneHashKey, ComputeFileHash(GameScenePath));
        UIPolishTestArtifacts.BackupBuildSettings();
        UIPolishTestArtifacts.CreateSceneCopies(TestSceneRoot, TestBeginScenePath, TestGameScenePath);
        UIPolishTestArtifacts.InstallTemporaryBuildSettings(TestBeginScenePath, TestGameScenePath);
        yield return null;
    }

    [Test]
    public void RestoredScenesUseVisibleButtonWidgets()
    {
        AssertSceneButtonVisuals(TestBeginScenePath, BeginPanels);
        AssertSceneButtonVisuals(TestGameScenePath, GamePanels);
    }

    [Test]
    public void AllUiRootsUseAspectSafeReferenceScaling()
    {
        AssertAspectSafeUiRoots(TestBeginScenePath);
        AssertAspectSafeUiRoots(TestGameScenePath);
    }

    [Test]
    public void PanelsUseExpectedRootPanelAndCamera()
    {
        AssertPanelOwnership(TestBeginScenePath, BeginPanels, false);
        AssertPanelOwnership(TestGameScenePath, GamePanels, true);
    }

    [Test]
    public void SupportedAspectRatiosKeepTheReferenceFrameVisible()
    {
        const float referenceAspect = 1920f / 1080f;
        const float referenceFieldOfView = 60f;
        float referenceHalfHeight = Mathf.Tan(referenceFieldOfView * 0.5f * Mathf.Deg2Rad);

        for (int i = 0; i < SupportedResolutions.Length; i++)
        {
            Vector2Int resolution = SupportedResolutions[i];
            float aspect = (float)resolution.x / resolution.y;
            int activeRootHeight = NGUIAspectSafeLayout.CalculateFitHeight(1920, 1080, aspect);
            float rootScaleRatio = 1080f / activeRootHeight;
            float fieldOfView = NGUIAspectSafeLayout.CalculateVerticalFieldOfView(
                referenceFieldOfView, referenceAspect, aspect);
            float fittedHalfHeight = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float verticalExtent = referenceHalfHeight / fittedHalfHeight;
            float horizontalExtent = referenceHalfHeight * referenceAspect / (fittedHalfHeight * aspect);

            Assert.LessOrEqual(verticalExtent, 1.0001f, resolution + " crops the reference height.");
            Assert.LessOrEqual(horizontalExtent, 1.0001f, resolution + " crops the reference width.");
            Assert.IsTrue(Mathf.Abs(verticalExtent - 1f) < 0.0001f ||
                Mathf.Abs(horizontalExtent - 1f) < 0.0001f,
                resolution + " must fit exactly one reference dimension.");

            float orthographicSize = NGUIAspectSafeLayout.CalculateOrthographicSize(
                1920, 1080, activeRootHeight, aspect, 1f);
            float orthographicVerticalExtent = rootScaleRatio / orthographicSize;
            float orthographicHorizontalExtent = referenceAspect * rootScaleRatio /
                (orthographicSize * aspect);
            Assert.LessOrEqual(orthographicVerticalExtent, 1.0001f,
                resolution + " crops the orthographic reference height.");
            Assert.LessOrEqual(orthographicHorizontalExtent, 1.0001f,
                resolution + " crops the orthographic reference width.");
        }
    }

    [Test]
    public void InteractiveControlsStayInsideTheReferenceFrameWithoutOverlap()
    {
        AssertPanelControlBounds(TestBeginScenePath, BeginPanels);
        AssertPanelControlBounds(TestGameScenePath, GamePanels);
    }

    [Test]
    public void ChoosePanelGenerationIsRepeatableAndMeetsVisualContract()
    {
        U2UIPolishThemeBuilder.BuildChoosePanelSceneCopyForTests(TestBeginScenePath);
        Scene firstScene = EditorSceneManager.OpenScene(TestBeginScenePath, OpenSceneMode.Single);
        GameObject firstPanel = FindSceneObject(firstScene, "ChoosePanel");
        string firstSignature = CapturePolishSignature(firstPanel.transform);

        U2UIPolishThemeBuilder.BuildChoosePanelSceneCopyForTests(TestBeginScenePath);
        Scene secondScene = EditorSceneManager.OpenScene(TestBeginScenePath, OpenSceneMode.Single);
        GameObject panel = FindSceneObject(secondScene, "ChoosePanel");
        Assert.AreEqual(firstSignature, CapturePolishSignature(panel.transform),
            "Repeated ChoosePanel generation must not add duplicate Polish objects or labels.");

        ChoosePanel controller = panel.GetComponent<ChoosePanel>();
        Assert.IsNotNull(controller);
        Assert.IsNotNull(controller.heroNameLabel, "The generated fighter-name label must be serialized on ChoosePanel.");
        Assert.AreEqual(ChoosePanel.GetHeroDisplayName(GameDataMgr.Instance.GetNowSelHeroInfo()),
            controller.heroNameLabel.text);
        Assert.AreNotEqual("当前战机", controller.heroNameLabel.text);

        AssertSingleNormalLabels(panel.transform);
        AssertChooseButtonTheme(panel.transform);
        AssertChooseLayout(panel.transform, controller);
    }

    [Test]
    public void SavedChoosePanelSceneMatchesGeneratedVisualContract()
    {
        Scene scene = EditorSceneManager.OpenScene(TestBeginScenePath, OpenSceneMode.Single);
        GameObject panel = FindSceneObject(scene, "ChoosePanel");
        ChoosePanel controller = panel.GetComponent<ChoosePanel>();

        Assert.IsNotNull(controller.btnClose);
        Assert.IsNotNull(controller.btnLeft);
        Assert.IsNotNull(controller.btnRight);
        Assert.IsNotNull(controller.btnStart);
        Assert.IsNotNull(controller.heroPos);
        Assert.IsNotNull(controller.uis);
        Assert.IsNotNull(controller.heroNameLabel);
        Assert.AreEqual(10, controller.hpObjs.Count);
        Assert.AreEqual(10, controller.speedObjs.Count);
        Assert.AreEqual(10, controller.volumeObjs.Count);

        AssertSingleNormalLabels(panel.transform);
        AssertChooseButtonTheme(panel.transform);
        AssertChooseLayout(panel.transform, controller);
    }

    [UnityTest]
    public IEnumerator StyledButtonsUseNguiRaycastsAndInjectedTouchClick()
    {
        EditorSceneManager.OpenScene(TestBeginScenePath, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        SceneManager.LoadScene(TestBeginScenePath);
        yield return null;
        yield return null;

        Assert.AreEqual(TestBeginScenePath, SceneManager.GetActiveScene().path,
            "Play Mode must load the temporary BeginScene copy.");

        validatedRaycasts = 0;
        yield return AssertPanelRaycasts(BeginPanels, 0);

        Scene beginScene = SceneManager.GetActiveScene();
        GameObject beginPanel = FindSceneObject(beginScene, "BeginPanel");
        GameObject choosePanel = FindSceneObject(beginScene, "ChoosePanel");
        UIButton beginButton = FindChildComponent<UIButton>(beginPanel.transform, "btnBegin");
        Assert.IsFalse(choosePanel.activeSelf, "ChoosePanel must start hidden so the click transition is meaningful.");

        InjectNguiTouchClick(beginButton);
        yield return null;

        Assert.IsTrue(choosePanel.activeSelf, "The NGUI click should open ChoosePanel.");
        Assert.IsFalse(beginPanel.activeSelf, "The NGUI click should hide BeginPanel.");

        UIButton leftButton = FindChildComponent<UIButton>(choosePanel.transform, "btnLeft");
        UIButton rightButton = FindChildComponent<UIButton>(choosePanel.transform, "btnRight");
        UIButton startButton = FindChildComponent<UIButton>(choosePanel.transform, "btnStart");
        UIButton closeButton = FindChildComponent<UIButton>(choosePanel.transform, "btnClose");
        int roleCount = GameDataMgr.Instance.roleData.roleDatas.Count;
        Assert.Greater(roleCount, 1, "ChoosePanel click validation needs at least two roles.");
        Assert.AreEqual(0, GameDataMgr.Instance.nowSelHeroIndex, "ChoosePanel must retain its default role.");
        AssertSelectedHeroName(choosePanel);

        InjectNguiTouchClick(leftButton);
        yield return null;
        Assert.AreEqual(roleCount - 1, GameDataMgr.Instance.nowSelHeroIndex,
            "The real NGUI left-button click must preserve the original wrap order.");
        AssertSelectedHeroName(choosePanel);

        InjectNguiTouchClick(rightButton);
        yield return null;
        Assert.AreEqual(0, GameDataMgr.Instance.nowSelHeroIndex,
            "The real NGUI right-button click must restore the default role.");
        AssertSelectedHeroName(choosePanel);

        InjectNguiTouchClick(closeButton);
        yield return null;
        Assert.IsFalse(choosePanel.activeSelf, "The real NGUI back-button click must close ChoosePanel.");
        Assert.IsTrue(beginPanel.activeSelf, "The real NGUI back-button click must restore BeginPanel.");

        InjectNguiTouchClick(beginButton);
        yield return null;
        Assert.IsTrue(choosePanel.activeSelf, "ChoosePanel must reopen through the real main-menu click.");
        Assert.AreEqual(0, GameDataMgr.Instance.nowSelHeroIndex, "Reopening ChoosePanel must retain the original default role.");

        InjectNguiTouchClick(rightButton);
        yield return null;
        int selectedHeroIndex = GameDataMgr.Instance.nowSelHeroIndex;
        Assert.AreEqual(1, selectedHeroIndex, "The workflow test must select a non-default role before choosing difficulty.");

        InjectNguiTouchClick(startButton);
        yield return null;
        GameObject difficultyPanel = FindSceneObject(beginScene, "DifficultyPanel");
        Assert.IsFalse(choosePanel.activeSelf, "The real NGUI confirm click must close ChoosePanel.");
        Assert.IsTrue(difficultyPanel.activeSelf, "The real NGUI confirm click must open the existing DifficultyPanel.");

        DifficultyPanel difficultyController = difficultyPanel.GetComponent<DifficultyPanel>();
        UIButton hardButton = FindChildComponent<UIButton>(difficultyPanel.transform, "btnHard");
        UIButton difficultyStartButton = FindChildComponent<UIButton>(difficultyPanel.transform, "btnStart");
        UIButton difficultyCloseButton = FindChildComponent<UIButton>(difficultyPanel.transform, "btnClose");

        InjectNguiTouchClick(hardButton);
        yield return null;
        Assert.AreEqual(TestBeginScenePath, SceneManager.GetActiveScene().path,
            "Choosing a difficulty must wait for explicit start confirmation.");
        Assert.AreEqual(3, GameDataMgr.Instance.nowDifficultyIndex);
        Assert.AreEqual(3, difficultyController.SelectedDifficultyId);
        StringAssert.Contains("困难", difficultyController.labSelected.text);
        StringAssert.Contains("125%", difficultyController.labDetail.text);

        InjectNguiTouchClick(difficultyCloseButton);
        yield return null;
        Assert.IsTrue(choosePanel.activeSelf, "Difficulty back must restore ChoosePanel.");
        Assert.IsFalse(difficultyPanel.activeSelf, "Difficulty back must close DifficultyPanel.");
        Assert.AreEqual(selectedHeroIndex, GameDataMgr.Instance.nowSelHeroIndex,
            "Returning from difficulty selection must preserve the selected role.");

        InjectNguiTouchClick(startButton);
        yield return null;
        Assert.IsTrue(difficultyPanel.activeSelf, "ChoosePanel confirmation must reopen DifficultyPanel.");
        Assert.AreEqual(3, difficultyController.SelectedDifficultyId,
            "Reopening DifficultyPanel must retain the selected difficulty.");

        for (int i = 1; i < BeginPanels.Length; i++)
            yield return AssertPanelRaycasts(BeginPanels, i);
        Assert.AreEqual(15, validatedRaycasts, "Every styled BeginScene button must pass the NGUI raycast path.");

        for (int i = 0; i < BeginPanels.Length; i++)
        {
            FindSceneObject(beginScene, BeginPanels[i].PanelName).SetActive(i == 2);
        }
        yield return null;

        InjectNguiTouchClick(difficultyStartButton);
        yield return null;
        yield return null;

        Assert.AreEqual(TestGameScenePath, SceneManager.GetActiveScene().path,
            "Play Mode must load the temporary GameScene copy.");

        validatedRaycasts = 0;
        for (int i = 0; i < GamePanels.Length; i++)
            yield return AssertPanelRaycasts(GamePanels, i);
        Assert.AreEqual(6, validatedRaycasts, "Every styled GameScene button must pass the NGUI raycast path.");

        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        UICamera.GetInputTouchCount = null;
        UICamera.GetInputTouch = null;
        ReleaseRaycastTarget();
        Time.timeScale = 1f;
        if (Application.isPlaying)
            yield return new ExitPlayMode();

        UIPolishTestArtifacts.Cleanup();
        UIPolishTestArtifacts.AssertProductionStateIsClean();
        Assert.AreEqual(SessionState.GetString(BeginSceneHashKey, string.Empty), ComputeFileHash(BeginScenePath),
            "The production BeginScene changed while UI tests were running.");
        Assert.AreEqual(SessionState.GetString(GameSceneHashKey, string.Empty), ComputeFileHash(GameScenePath),
            "The production GameScene changed while UI tests were running.");
        SessionState.EraseString(BeginSceneHashKey);
        SessionState.EraseString(GameSceneHashKey);
    }

    private static string ComputeFileHash(string assetPath)
    {
        using (SHA256 hash = SHA256.Create())
            return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(assetPath)));
    }

    private static string CapturePolishSignature(Transform root)
    {
        StringBuilder signature = new StringBuilder();
        HashSet<string> names = new HashSet<string>();
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform descendant = descendants[i];
            if (!descendant.name.StartsWith("Polish_", StringComparison.Ordinal))
                continue;
            Assert.IsTrue(names.Add(descendant.name),
                "Duplicate generated ChoosePanel node found: " + descendant.name);
            signature.Append(descendant.name).Append('|')
                .Append(descendant.GetComponents<UILabel>().Length).Append('|')
                .Append(descendant.GetComponents<UI2DSprite>().Length).Append('\n');
        }
        return signature.ToString();
    }

    private static void AssertSingleNormalLabels(Transform panel)
    {
        Dictionary<string, UILabel> visibleText = new Dictionary<string, UILabel>();
        UILabel[] labels = panel.GetComponentsInChildren<UILabel>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            UILabel label = labels[i];
            Assert.AreEqual(FontStyle.Normal, label.fontStyle,
                label.name + " must use the Normal font style in ChoosePanel.");
            if (!label.enabled || !label.gameObject.activeSelf || string.IsNullOrEmpty(label.text))
                continue;

            Assert.AreEqual(UILabel.Effect.Shadow, label.effectStyle,
                label.name + " must use one UILabel shadow pass rather than stacked labels or an outline.");
            Assert.LessOrEqual(Mathf.Abs(label.effectDistance.x), 2f, label.name + " shadow X is too large.");
            Assert.LessOrEqual(Mathf.Abs(label.effectDistance.y), 2f, label.name + " shadow Y is too large.");
            Assert.IsFalse(visibleText.ContainsKey(label.text),
                "Duplicate visible UILabel text found: " + label.text);
            visibleText.Add(label.text, label);
        }

        Assert.IsTrue(visibleText.ContainsKey("选择战机"));
        Assert.IsTrue(visibleText.ContainsKey("耐久"));
        Assert.IsTrue(visibleText.ContainsKey("机动"));
        Assert.IsTrue(visibleText.ContainsKey("火力"));
        Assert.IsTrue(visibleText.ContainsKey("确认出击"));
    }

    private static void AssertChooseButtonTheme(Transform panel)
    {
        string[] buttonNames = { "btnClose", "btnLeft", "btnRight", "btnStart" };
        Sprite sharedSprite = null;
        for (int i = 0; i < buttonNames.Length; i++)
        {
            UIButton button = FindChildComponent<UIButton>(panel, buttonNames[i]);
            Assert.IsNotNull(button);
            UIWidget[] widgets = button.GetComponents<UIWidget>();
            Assert.AreEqual(1, widgets.Length, button.name + " must have exactly one visible widget on its collider object.");
            UI2DSprite visual = widgets[0] as UI2DSprite;
            Assert.IsNotNull(visual, button.name + " must replace the old atlas/gradient widget with the ChoosePanel sprite.");
            Assert.AreEqual("Assets/ArtRes/UI/Polished/ui_button_choose.png", AssetDatabase.GetAssetPath(visual.sprite2D));
            Assert.IsTrue(visual.enabled);
            Assert.AreSame(button.gameObject, button.tweenTarget);
            Assert.AreNotEqual(button.defaultColor, button.hover, button.name + " needs a distinct hover state.");
            Assert.AreNotEqual(button.defaultColor, button.pressed, button.name + " needs a distinct pressed state.");
            Assert.AreNotEqual(button.hover, button.pressed, button.name + " hover and pressed states must differ.");

            UILabel[] labels = button.GetComponentsInChildren<UILabel>(true);
            int activeLabels = 0;
            for (int labelIndex = 0; labelIndex < labels.Length; labelIndex++)
            {
                if (labels[labelIndex].enabled && labels[labelIndex].gameObject.activeSelf)
                    activeLabels++;
            }
            Assert.AreEqual(1, activeLabels, button.name + " must display exactly one UILabel.");

            if (sharedSprite == null)
                sharedSprite = visual.sprite2D;
            else
                Assert.AreSame(sharedSprite, visual.sprite2D, "ChoosePanel buttons must share one visual theme.");
        }
    }

    private static void AssertChooseLayout(Transform panel, ChoosePanel controller)
    {
        UIButton close = FindChildComponent<UIButton>(panel, "btnClose");
        UIButton left = FindChildComponent<UIButton>(panel, "btnLeft");
        UIButton right = FindChildComponent<UIButton>(panel, "btnRight");
        UIWidget closeWidget = close.GetComponent<UIWidget>();
        UIWidget leftWidget = left.GetComponent<UIWidget>();
        UIWidget rightWidget = right.GetComponent<UIWidget>();
        Assert.AreEqual(56, closeWidget.width);
        Assert.AreEqual(56, closeWidget.height);
        Assert.AreEqual(leftWidget.width, rightWidget.width);
        Assert.AreEqual(leftWidget.height, rightWidget.height);

        Vector3 leftPosition = panel.InverseTransformPoint(left.transform.position);
        Vector3 rightPosition = panel.InverseTransformPoint(right.transform.position);
        Transform stage = panel.Find("Polish_Stage");
        Assert.IsNotNull(stage);
        float stageCenter = panel.InverseTransformPoint(stage.position).x;
        Assert.AreEqual(leftPosition.y, rightPosition.y, 0.01f);
        Assert.AreEqual(-210f, stageCenter, 0.01f);
        Assert.AreEqual(stageCenter, (leftPosition.x + rightPosition.x) * 0.5f, 0.01f,
            "Navigation buttons must be symmetric around the stage center.");

        Assert.AreEqual(0.87f, controller.heroPos.localScale.x, 0.001f);
        Assert.AreEqual(0.87f, controller.heroPos.localScale.y, 0.001f);
        Assert.AreEqual(0.87f, controller.heroPos.localScale.z, 0.001f);
        Assert.AreEqual(-124f, controller.heroPos.localPosition.x, 0.01f,
            "The fighter anchor must center the aircraft through the perspective UI camera.");
        AssertAircraftVisualBounds(controller, stage, left, right, leftWidget.width, rightWidget.width);

        AssertAlignedSegments(controller.hpObjs, controller.speedObjs, controller.volumeObjs, panel);
        Assert.IsNull(panel.Find("Polish_HeroTitle"), "The obsolete generic fighter-title label must be removed.");
        Assert.IsNotNull(panel.Find("Polish_HeroName"));
    }

    private static void AssertAircraftVisualBounds(ChoosePanel controller, Transform stage, UIButton left, UIButton right,
        int leftWidth, int rightWidth)
    {
        const float minimumLeftClearance = 0.05f;
        const float minimumRightClearance = 8f / NGUIAspectSafeLayout.DefaultReferenceWidth;
        Camera camera = controller.uis;
        Assert.IsNotNull(camera);
        float stageCenter = camera.WorldToViewportPoint(stage.position).x;
        float leftInnerEdge = camera.WorldToViewportPoint(
            left.transform.TransformPoint(new Vector3(leftWidth * 0.5f, 0f, 0f))).x;
        float rightInnerEdge = camera.WorldToViewportPoint(
            right.transform.TransformPoint(new Vector3(-rightWidth * 0.5f, 0f, 0f))).x;
        float centerSum = 0f;
        int aircraftCount = GameDataMgr.Instance.roleData.roleDatas.Count;

        for (int i = 0; i < aircraftCount; i++)
        {
            RoleInfo info = GameDataMgr.Instance.roleData.roleDatas[i];
            GameObject prefab = Resources.Load<GameObject>(info.resName);
            Assert.IsNotNull(prefab, info.resName + " must exist for the ChoosePanel visual-bounds check.");

            GameObject aircraft = UnityEngine.Object.Instantiate(prefab, controller.heroPos, false);
            try
            {
                aircraft.transform.localPosition = Vector3.zero;
                aircraft.transform.localRotation = Quaternion.identity;
                aircraft.transform.localScale = Vector3.one * info.scale;

                float minX;
                float maxX;
                GetMeshViewportBoundsX(camera, aircraft, out minX, out maxX);
                float visualCenter = (minX + maxX) * 0.5f;
                centerSum += visualCenter;

                Assert.AreEqual(stageCenter, visualCenter, 0.055f,
                    info.resName + " is visually off-center through the perspective UI camera.");
                Assert.GreaterOrEqual(minX - leftInnerEdge, minimumLeftClearance,
                    info.resName + " presses against the left navigation button.");
                Assert.GreaterOrEqual(rightInnerEdge - maxX, minimumRightClearance,
                    info.resName + " presses against the right navigation button.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(aircraft);
            }
        }

        Assert.Greater(aircraftCount, 0);
        Assert.AreEqual(stageCenter, centerSum / aircraftCount, 0.002f,
            "The aircraft set must be centered on average through the perspective UI camera.");
    }

    private static void GetMeshViewportBoundsX(Camera camera, GameObject aircraft, out float minX, out float maxX)
    {
        minX = float.PositiveInfinity;
        maxX = float.NegativeInfinity;
        MeshFilter[] filters = aircraft.GetComponentsInChildren<MeshFilter>(true);
        Assert.Greater(filters.Length, 0, aircraft.name + " has no mesh to validate.");

        for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
        {
            Mesh mesh = filters[filterIndex].sharedMesh;
            Assert.IsNotNull(mesh, filters[filterIndex].name + " has no shared mesh.");
            Vector3[] vertices = mesh.vertices;
            for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
            {
                float x = camera.WorldToViewportPoint(
                    filters[filterIndex].transform.TransformPoint(vertices[vertexIndex])).x;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
            }
        }
    }

    private static void AssertAlignedSegments(List<GameObject> hp, List<GameObject> speed, List<GameObject> volume,
        Transform panel)
    {
        Assert.AreEqual(hp.Count, speed.Count);
        Assert.AreEqual(hp.Count, volume.Count);
        for (int i = 0; i < hp.Count; i++)
        {
            Vector3 hpPosition = panel.InverseTransformPoint(hp[i].transform.position);
            Vector3 speedPosition = panel.InverseTransformPoint(speed[i].transform.position);
            Vector3 volumePosition = panel.InverseTransformPoint(volume[i].transform.position);
            Assert.AreEqual(hpPosition.x, speedPosition.x, 0.01f, "Stat segment column " + i + " is misaligned.");
            Assert.AreEqual(hpPosition.x, volumePosition.x, 0.01f, "Stat segment column " + i + " is misaligned.");
            Assert.AreEqual(110f, hpPosition.y - speedPosition.y, 0.01f);
            Assert.AreEqual(110f, speedPosition.y - volumePosition.y, 0.01f);
        }
    }

    private static void AssertSelectedHeroName(GameObject choosePanel)
    {
        ChoosePanel controller = choosePanel.GetComponent<ChoosePanel>();
        Assert.IsNotNull(controller.heroNameLabel);
        Assert.AreEqual(ChoosePanel.GetHeroDisplayName(GameDataMgr.Instance.GetNowSelHeroInfo()),
            controller.heroNameLabel.text, "The fighter name must follow the selected role after a real NGUI click.");
    }

    private static void AssertSceneButtonVisuals(string scenePath, PanelButtons[] panels)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        for (int panelIndex = 0; panelIndex < panels.Length; panelIndex++)
        {
            GameObject panel = FindSceneObject(scene, panels[panelIndex].PanelName);
            Assert.IsNotNull(panel);
            string[] buttonNames = panels[panelIndex].ButtonNames;
            for (int buttonIndex = 0; buttonIndex < buttonNames.Length; buttonIndex++)
            {
                UIButton button = FindChildComponent<UIButton>(panel.transform, buttonNames[buttonIndex]);
                Assert.IsNotNull(button);
                Assert.IsNull(button.transform.Find("Polish_ButtonVisual"), button.name + " must not own a nested visual layer.");
                AssertButtonVisual(button);
            }
            AssertDecorationsAreNonInteractive(panel.transform);
        }
    }

    private IEnumerator AssertPanelRaycasts(PanelButtons[] panels, int activePanelIndex)
    {
        Scene scene = SceneManager.GetActiveScene();
        for (int i = 0; i < panels.Length; i++)
        {
            GameObject panel = FindSceneObject(scene, panels[i].PanelName);
            Assert.IsNotNull(panel, panels[i].PanelName + " was not found.");
            panel.SetActive(i == activePanelIndex);
        }

        GameObject activePanel = FindSceneObject(scene, panels[activePanelIndex].PanelName);
        activePanel.SetActive(true);
        yield return null;

        // Several BasePanel.Init implementations hide themselves on their first frame.
        activePanel.SetActive(true);
        yield return null;

        RefreshNgui(scene);
        Physics.SyncTransforms();

        string[] buttonNames = panels[activePanelIndex].ButtonNames;
        for (int i = 0; i < buttonNames.Length; i++)
        {
            UIButton button = FindChildComponent<UIButton>(activePanel.transform, buttonNames[i]);
            Assert.IsNotNull(button, activePanel.name + "/" + buttonNames[i] + " was not found.");

            UIWidget hitWidget = button.GetComponent<UIWidget>();
            BoxCollider collider = button.GetComponent<BoxCollider>();
            Assert.IsNotNull(hitWidget, button.name + " has no parent UIWidget for NGUI raycast filtering.");
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " is not active in the loaded panel.");
            Assert.IsTrue(hitWidget.enabled, button.name + " has a disabled parent UIWidget.");
            Assert.IsTrue(hitWidget.isVisible, button.name + " has a parent UIWidget rejected by NGUI visibility filtering.");
            Assert.IsNotNull(collider, button.name + " has no BoxCollider.");
            Assert.IsTrue(collider.enabled, button.name + " has a disabled BoxCollider.");

            Assert.IsNull(button.transform.Find("Polish_ButtonVisual"), button.name + " must not own a nested visual layer.");
            AssertButtonVisual(button, true);

            UICamera camera = FindEventCamera(button.gameObject.layer);
            Vector2 screenPosition = GetColliderScreenPosition(camera, collider);
            UICamera.MouseOrTouch ray = new UICamera.MouseOrTouch { pos = screenPosition };
            UICamera.Raycast(ray);

            Ray physicsRay = camera.cachedCamera.ScreenPointToRay(screenPosition);
            float rayDistance = camera.rangeDistance > 0f
                ? camera.rangeDistance
                : camera.cachedCamera.farClipPlane - camera.cachedCamera.nearClipPlane;
            int rayMask = camera.cachedCamera.cullingMask & (int)camera.eventReceiverMask;
            RaycastHit[] physicsHits = Physics.RaycastAll(
                physicsRay,
                rayDistance,
                rayMask,
                QueryTriggerInteraction.Collide);
            string hitSummary = string.Empty;
            for (int hitIndex = 0; hitIndex < physicsHits.Length && hitIndex < 10; hitIndex++)
            {
                if (hitIndex > 0)
                    hitSummary += ", ";
                hitSummary += physicsHits[hitIndex].collider.name + "@" + physicsHits[hitIndex].distance.ToString("F3");
            }

            Assert.AreSame(
                button.gameObject,
                ray.current,
                activePanel.name + "/" + button.name + " was filtered or another object intercepted its NGUI raycast. " +
                "camera=" + camera.name +
                " screen=" + screenPosition +
                " screenSize=" + Screen.width + "x" + Screen.height +
                " world=" + collider.transform.TransformPoint(collider.center) +
                " physicsHits=[" + hitSummary + "]");
            InjectNguiTouchPress(button, camera, screenPosition);
            validatedRaycasts++;
        }
    }

    private static void InjectNguiTouchPress(UIButton button, UICamera camera, Vector2 screenPosition)
    {
        bool receivedPress = false;
        UIEventListener listener = UIEventListener.Get(button.gameObject);
        UIEventListener.BoolDelegate onPress = (ignored, pressed) => receivedPress |= pressed;
        listener.onPress += onPress;

        int touchCount = 1;
        UICamera.Touch touch = new UICamera.Touch
        {
            fingerId = 91,
            phase = TouchPhase.Began,
            position = screenPosition,
            tapCount = 1
        };

        UICamera.GetInputTouchCount = () => touchCount;
        UICamera.GetInputTouch = ignored => touch;
        try
        {
            camera.ProcessTouches();
            Assert.IsTrue(receivedPress, button.name + " did not receive a real NGUI touch press.");

            touch.phase = TouchPhase.Canceled;
            touch.position = new Vector2(-1000f, -1000f);
            camera.ProcessTouches();
            touchCount = 0;
            camera.ProcessTouches();
        }
        finally
        {
            listener.onPress -= onPress;
            UICamera.GetInputTouchCount = null;
            UICamera.GetInputTouch = null;
        }
    }

    private void InjectNguiTouchClick(UIButton button)
    {
        Assert.IsNotNull(button);
        BoxCollider collider = button.GetComponent<BoxCollider>();
        Assert.IsNotNull(collider);

        RefreshNgui(SceneManager.GetActiveScene());
        Physics.SyncTransforms();
        UIWidget hitWidget = button.GetComponent<UIWidget>();
        Assert.IsNotNull(hitWidget, button.name + " must keep its visible UIWidget on the collider object.");
        Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " is inactive before the real click.");
        Assert.IsTrue(hitWidget.enabled, button.name + " has a disabled UIWidget before the real click.");
        Assert.IsTrue(hitWidget.isVisible, button.name + " is outside NGUI visibility before the real click.");
        Assert.Greater(hitWidget.finalAlpha, 0.9f, button.name + " is transparent before the real click.");
        Assert.IsTrue(collider.enabled, button.name + " has a disabled collider before the real click.");

        UICamera camera = FindEventCamera(button.gameObject.layer);
        Vector2 screenPosition = GetColliderScreenPosition(camera, collider);
        UICamera.MouseOrTouch ray = new UICamera.MouseOrTouch { pos = screenPosition };
        UICamera.Raycast(ray);
        Ray physicsRay = camera.cachedCamera.ScreenPointToRay(screenPosition);
        float rayDistance = camera.rangeDistance > 0f
            ? camera.rangeDistance
            : camera.cachedCamera.farClipPlane - camera.cachedCamera.nearClipPlane;
        int rayMask = camera.cachedCamera.cullingMask & (int)camera.eventReceiverMask;
        RaycastHit[] physicsHits = Physics.RaycastAll(
            physicsRay, rayDistance, rayMask, QueryTriggerInteraction.Collide);
        string hitSummary = string.Empty;
        for (int i = 0; i < physicsHits.Length && i < 10; i++)
        {
            if (i > 0)
                hitSummary += ", ";
            hitSummary += physicsHits[i].collider.name + "@" + physicsHits[i].distance.ToString("F3");
        }
        Assert.AreSame(button.gameObject, ray.current,
            "Injected touch must begin on the real button collider. button=" + button.name +
            " screen=" + screenPosition + " world=" + collider.transform.TransformPoint(collider.center) +
            " physicsHits=[" + hitSummary + "]");

        int touchCount = 1;
        UICamera.Touch touch = new UICamera.Touch
        {
            fingerId = 77,
            phase = TouchPhase.Began,
            position = screenPosition,
            tapCount = 1
        };

        UICamera.GetInputTouchCount = () => touchCount;
        UICamera.GetInputTouch = ignored => touch;
        try
        {
            camera.ProcessTouches();
            touch.phase = TouchPhase.Ended;
            camera.ProcessTouches();
            touchCount = 0;
            camera.ProcessTouches();
        }
        finally
        {
            UICamera.GetInputTouchCount = null;
            UICamera.GetInputTouch = null;
        }
    }

    private UICamera FindEventCamera(int layer)
    {
        UICamera[] cameras = Resources.FindObjectsOfTypeAll<UICamera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            UICamera camera = cameras[i];
            if (!camera.gameObject.scene.IsValid() || !camera.gameObject.scene.isLoaded || !camera.isActiveAndEnabled)
                continue;

            int layerMask = 1 << layer;
            if ((camera.cachedCamera.cullingMask & layerMask) != 0 &&
                ((int)camera.eventReceiverMask & layerMask) != 0)
            {
                if (raycastTarget == null)
                {
                    raycastTarget = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
                    raycastTarget.Create();
                }

                // Headless EditMode uses a 4:3 GameView. A 16:9 target makes camera
                // projection and UICamera's viewport check match the captured UI.
                camera.cachedCamera.targetTexture = raycastTarget;
                const float targetAspect = 16f / 9f;
                camera.cachedCamera.aspect = targetAspect;
                if (camera.cachedCamera.orthographic)
                {
                    float halfHeight = camera.cachedCamera.orthographicSize;
                    float halfWidth = halfHeight * targetAspect;
                    camera.cachedCamera.projectionMatrix = Matrix4x4.Ortho(
                        -halfWidth,
                        halfWidth,
                        -halfHeight,
                        halfHeight,
                        camera.cachedCamera.nearClipPlane,
                        camera.cachedCamera.farClipPlane);
                }
                else
                {
                    camera.cachedCamera.projectionMatrix = Matrix4x4.Perspective(
                        camera.cachedCamera.fieldOfView,
                        targetAspect,
                        camera.cachedCamera.nearClipPlane,
                        camera.cachedCamera.farClipPlane);
                }
                return camera;
            }
        }

        Assert.Fail("No active NGUI event camera can see layer " + layer + ".");
        return null;
    }

    private void ReleaseRaycastTarget()
    {
        if (raycastTarget == null)
            return;

        UICamera[] cameras = Resources.FindObjectsOfTypeAll<UICamera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i].cachedCamera.targetTexture == raycastTarget)
            {
                cameras[i].cachedCamera.targetTexture = null;
                cameras[i].cachedCamera.ResetProjectionMatrix();
                cameras[i].cachedCamera.ResetAspect();
            }
        }

        raycastTarget.Release();
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(raycastTarget);
        else
            UnityEngine.Object.DestroyImmediate(raycastTarget);
        raycastTarget = null;
    }

    private static Vector2 GetColliderScreenPosition(UICamera camera, BoxCollider collider)
    {
        Vector3 worldPosition = collider.transform.TransformPoint(collider.center);
        Vector3 screenPosition = camera.cachedCamera.WorldToScreenPoint(worldPosition);
        return new Vector2(screenPosition.x, screenPosition.y);
    }

    private static void AssertAspectSafeUiRoots(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        List<UIRoot> uiRoots = new List<UIRoot>();
        List<UIWidget> widgets = new List<UIWidget>();
        GameObject[] sceneRoots = scene.GetRootGameObjects();

        for (int i = 0; i < sceneRoots.Length; i++)
        {
            uiRoots.AddRange(sceneRoots[i].GetComponentsInChildren<UIRoot>(true));
            widgets.AddRange(sceneRoots[i].GetComponentsInChildren<UIWidget>(true));
        }

        Assert.IsNotEmpty(uiRoots, scenePath + " must contain an NGUI UIRoot.");
        for (int i = 0; i < uiRoots.Count; i++)
        {
            UIRoot root = uiRoots[i];
            Assert.AreEqual(UIRoot.Scaling.Constrained, root.scalingStyle,
                root.name + " must use the reference resolution on every build platform.");
            Assert.AreEqual(1920, root.manualWidth, root.name + " has an unexpected reference width.");
            Assert.AreEqual(1080, root.manualHeight, root.name + " has an unexpected reference height.");
            Assert.IsTrue(root.fitWidth && root.fitHeight,
                root.name + " must fit the complete UI inside both narrow and wide screens.");

            NGUIAspectSafeLayout layout = root.GetComponent<NGUIAspectSafeLayout>();
            Assert.IsNotNull(layout, root.name + " must update its NGUI Camera for aspect and safe-area changes.");
            Assert.AreEqual(1920, layout.ReferenceWidth);
            Assert.AreEqual(1080, layout.ReferenceHeight);
            Assert.IsTrue(layout.RespectScreenSafeArea);
            Assert.IsNotNull(layout.UiCamera);
            Assert.IsTrue(layout.UiCamera.transform.IsChildOf(root.transform));
        }

        Assert.IsNotEmpty(widgets, scenePath + " must contain NGUI widgets.");
        for (int i = 0; i < widgets.Count; i++)
        {
            Assert.IsNotNull(NGUITools.FindInParents<UIRoot>(widgets[i].gameObject),
                widgets[i].name + " is outside an adaptive UIRoot in " + scenePath + ".");
        }
    }

    private static void AssertPanelOwnership(string scenePath, PanelButtons[] panels, bool expectOrthographicCamera)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        UIRoot[] roots = FindSceneComponents<UIRoot>(scene);
        Assert.AreEqual(1, roots.Length, scenePath + " must have one authoritative NGUI UIRoot.");

        UIRoot root = roots[0];
        NGUIAspectSafeLayout layout = root.GetComponent<NGUIAspectSafeLayout>();
        Assert.IsNotNull(layout);
        Assert.AreEqual(expectOrthographicCamera, layout.UiCamera.orthographic,
            scenePath + " has an unexpected NGUI Camera projection.");
        Assert.IsEmpty(root.GetComponentsInChildren<UIAnchor>(true),
            scenePath + " must not have legacy UIAnchor components competing with the reference layout.");
        Assert.IsEmpty(root.GetComponentsInChildren<UIStretch>(true),
            scenePath + " must not have legacy UIStretch components competing with the reference layout.");

        for (int i = 0; i < panels.Length; i++)
        {
            GameObject panel = FindSceneObject(scene, panels[i].PanelName);
            Assert.IsNotNull(panel, panels[i].PanelName + " was not found in " + scenePath + ".");
            Assert.AreSame(root, NGUITools.FindInParents<UIRoot>(panel),
                panels[i].PanelName + " belongs to a different UIRoot.");
            Assert.IsNotNull(panel.GetComponent<UIPanel>(),
                panels[i].PanelName + " must own its UIPanel.");
            Assert.AreNotEqual(0, layout.UiCamera.cullingMask & (1 << panel.layer),
                layout.UiCamera.name + " cannot render " + panels[i].PanelName + ".");
        }

        if (!expectOrthographicCamera)
        {
            ChoosePanel choosePanel = FindSceneObject(scene, "ChoosePanel").GetComponent<ChoosePanel>();
            Assert.AreSame(layout.UiCamera, choosePanel.uis,
                "ChoosePanel must use the same perspective Camera as its UIRoot and UIPanel.");
        }
    }

    private static void AssertPanelControlBounds(string scenePath, PanelButtons[] panels)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        UIRoot root = FindSceneComponents<UIRoot>(scene)[0];
        const float halfWidth = 960f;
        const float halfHeight = 540f;
        const float tolerance = 0.1f;

        for (int panelIndex = 0; panelIndex < panels.Length; panelIndex++)
        {
            GameObject panel = FindSceneObject(scene, panels[panelIndex].PanelName);
            List<Rect> buttonRects = new List<Rect>();
            List<string> buttonRectNames = new List<string>();
            string[] buttonNames = panels[panelIndex].ButtonNames;
            for (int buttonIndex = 0; buttonIndex < buttonNames.Length; buttonIndex++)
            {
                UIButton button = FindChildComponent<UIButton>(panel.transform, buttonNames[buttonIndex]);
                Assert.IsNotNull(button);
                UIWidget widget = button.GetComponent<UIWidget>();
                Assert.IsNotNull(widget, panel.name + "/" + button.name + " has no UIWidget.");
                Rect rect = GetReferenceRect(root.transform, widget.worldCorners);

                Assert.GreaterOrEqual(rect.xMin, -halfWidth - tolerance,
                    panel.name + "/" + button.name + " exceeds the left safe edge.");
                Assert.LessOrEqual(rect.xMax, halfWidth + tolerance,
                    panel.name + "/" + button.name + " exceeds the right safe edge.");
                Assert.GreaterOrEqual(rect.yMin, -halfHeight - tolerance,
                    panel.name + "/" + button.name + " exceeds the bottom safe edge.");
                Assert.LessOrEqual(rect.yMax, halfHeight + tolerance,
                    panel.name + "/" + button.name + " exceeds the top safe edge.");

                for (int previous = 0; previous < buttonRects.Count; previous++)
                {
                    Assert.IsFalse(rect.Overlaps(buttonRects[previous]),
                        panel.name + "/" + button.name + " overlaps " + buttonRectNames[previous] + ".");
                }
                buttonRects.Add(rect);
                buttonRectNames.Add(button.name);
            }
        }
    }

    private static Rect GetReferenceRect(Transform root, Vector3[] worldCorners)
    {
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector3 position = root.InverseTransformPoint(worldCorners[i]);
            minX = Mathf.Min(minX, position.x);
            maxX = Mathf.Max(maxX, position.x);
            minY = Mathf.Min(minY, position.y);
            maxY = Mathf.Max(maxY, position.y);
        }
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        List<T> components = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            components.AddRange(roots[i].GetComponentsInChildren<T>(true));
        return components.ToArray();
    }

    private static void RefreshNgui(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            UIRoot[] rootsInScene = roots[rootIndex].GetComponentsInChildren<UIRoot>(true);
            for (int i = 0; i < rootsInScene.Length; i++)
                rootsInScene[i].UpdateScale(false);

            UIPanel[] panels = roots[rootIndex].GetComponentsInChildren<UIPanel>(true);
            for (int i = 0; i < panels.Length; i++)
            {
                if (!panels[i].gameObject.activeInHierarchy)
                    continue;
                panels[i].Refresh();
                panels[i].RebuildAllDrawCalls();
                panels[i].Refresh();
            }
        }
    }

    private static void AssertButtonVisual(UIButton button, bool requireVisible = false)
    {
        GameObject visualObject = button.tweenTarget != null ? button.tweenTarget : button.gameObject;
        UIWidget visual = visualObject.GetComponent<UIWidget>();
        Assert.IsNotNull(visual, button.name + " has no UIWidget visual target.");
        Assert.IsTrue(visual.enabled, visual.name + " is disabled.");
        Assert.Greater(visual.alpha, 0.9f, visual.name + " has transparent local alpha.");
        if (requireVisible)
        {
            Assert.IsTrue(visual.isVisible, visual.name + " is outside NGUI visibility.");
            Assert.Greater(visual.finalAlpha, 0.9f, visual.name + " has transparent inherited alpha.");
        }

        if (visual.name.StartsWith("Polish_"))
        {
            Assert.AreNotSame(button.transform, visual.transform.parent, "Fallback visuals must not be children of buttons.");
            Assert.AreSame(button.transform.parent, visual.transform.parent, "Fallback visuals must be siblings of buttons.");
        }
    }

    private static void AssertDecorationsAreNonInteractive(Transform root)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (!descendants[i].name.StartsWith("Polish_", StringComparison.Ordinal))
                continue;

            Assert.IsNull(descendants[i].GetComponent<Collider>(),
                descendants[i].name + " is decorative and must not intercept input.");
        }
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i].name == name && objects[i].scene == scene)
                return objects[i];
        }
        return null;
    }

    private static T FindChildComponent<T>(Transform root, string name) where T : Component
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == name)
                return children[i].GetComponent<T>();
        }
        return null;
    }

    private sealed class PanelButtons
    {
        public readonly string PanelName;
        public readonly string[] ButtonNames;

        public PanelButtons(string panelName, params string[] buttonNames)
        {
            PanelName = panelName;
            ButtonNames = buttonNames;
        }
    }
}

internal static class UIPolishTestArtifacts
{
    private const string TemporaryRoot = "Assets/__U2UIPolishTests";
    private const string BeginScenePath = "Assets/Scenes/BeginScene.unity";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string BuildSettingsAssetPath = "ProjectSettings/EditorBuildSettings.asset";
    private const string BuildSettingsBackupPath = "Library/UIPolishNGUIClickTests.EditorBuildSettings.asset.backup";

    internal static void BackupBuildSettings()
    {
        File.Copy(BuildSettingsAssetPath, BuildSettingsBackupPath, true);
    }

    internal static void CreateSceneCopies(string testRoot, string beginCopyPath, string gameCopyPath)
    {
        EnsureAssetFolder(testRoot);
        AssetDatabase.DeleteAsset(beginCopyPath);
        AssetDatabase.DeleteAsset(gameCopyPath);
        Assert.IsTrue(AssetDatabase.CopyAsset(BeginScenePath, beginCopyPath), "Failed to copy BeginScene for UI tests.");
        Assert.IsTrue(AssetDatabase.CopyAsset(GameScenePath, gameCopyPath), "Failed to copy GameScene for UI tests.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    internal static void InstallTemporaryBuildSettings(string beginCopyPath, string gameCopyPath)
    {
        RestoreProductionBuildSettings();
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        int replacements = 0;
        for (int i = 0; i < scenes.Length; i++)
        {
            string path = Normalize(scenes[i].path);
            if (path == BeginScenePath)
            {
                scenes[i] = new EditorBuildSettingsScene(beginCopyPath, scenes[i].enabled);
                replacements++;
            }
            else if (path == GameScenePath)
            {
                scenes[i] = new EditorBuildSettingsScene(gameCopyPath, scenes[i].enabled);
                replacements++;
            }
        }

        Assert.AreEqual(2, replacements, "The two production scenes must each have one temporary test replacement.");
        EditorBuildSettings.scenes = scenes;
        AssetDatabase.SaveAssets();
    }

    internal static void Cleanup()
    {
        if (File.Exists(BuildSettingsBackupPath))
        {
            File.Copy(BuildSettingsBackupPath, BuildSettingsAssetPath, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        RestoreProductionBuildSettings();
        AssetDatabase.SaveAssets();

        if (File.Exists(BuildSettingsBackupPath))
            File.Delete(BuildSettingsBackupPath);

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && IsTemporaryPath(activeScene.path) && !EditorApplication.isPlayingOrWillChangePlaymode)
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        if (AssetDatabase.IsValidFolder(TemporaryRoot))
        {
            AssetDatabase.DeleteAsset(TemporaryRoot);
            AssetDatabase.Refresh();
        }
    }

    internal static void RestoreProductionBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        List<EditorBuildSettingsScene> restored = new List<EditorBuildSettingsScene>();
        List<bool> usesProductionEntry = new List<bool>();
        Dictionary<string, int> pathIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < scenes.Length; i++)
        {
            string originalPath = Normalize(scenes[i].path);
            string restoredPath = GetProductionPath(originalPath);
            if (restoredPath == null)
                continue;

            bool isProductionEntry = originalPath == BeginScenePath || originalPath == GameScenePath;
            int existingIndex;
            if (!pathIndices.TryGetValue(restoredPath, out existingIndex))
            {
                pathIndices.Add(restoredPath, restored.Count);
                restored.Add(new EditorBuildSettingsScene(restoredPath, scenes[i].enabled));
                usesProductionEntry.Add(isProductionEntry);
            }
            else if (isProductionEntry && !usesProductionEntry[existingIndex])
            {
                restored[existingIndex] = new EditorBuildSettingsScene(restoredPath, scenes[i].enabled);
                usesProductionEntry[existingIndex] = true;
            }
        }

        EnsureProductionScene(restored, usesProductionEntry, pathIndices, BeginScenePath);
        EnsureProductionScene(restored, usesProductionEntry, pathIndices, GameScenePath);
        EditorBuildSettings.scenes = restored.ToArray();
    }

    internal static void AssertProductionStateIsClean()
    {
        Assert.IsTrue(File.Exists(BeginScenePath), "Production BeginScene is missing.");
        Assert.IsTrue(File.Exists(GameScenePath), "Production GameScene is missing.");

        HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int beginCount = 0;
        int gameCount = 0;
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            string path = Normalize(scenes[i].path);
            Assert.IsFalse(IsTemporaryPath(path), "Build Settings still contains a temporary test scene: " + path);
            Assert.IsTrue(paths.Add(path), "Build Settings contains a duplicate scene path: " + path);
            if (path == BeginScenePath)
                beginCount++;
            if (path == GameScenePath)
                gameCount++;
        }

        Assert.AreEqual(1, beginCount, "Build Settings must contain one production BeginScene.");
        Assert.AreEqual(1, gameCount, "Build Settings must contain one production GameScene.");
        string serializedSettings = File.ReadAllText(BuildSettingsAssetPath).Replace('\\', '/');
        Assert.IsFalse(serializedSettings.Contains(TemporaryRoot),
            "EditorBuildSettings.asset still contains a temporary test path.");
        Assert.IsFalse(AssetDatabase.IsValidFolder(TemporaryRoot), "Temporary UI test assets were not deleted.");
        Assert.IsFalse(File.Exists(BuildSettingsBackupPath), "The Build Settings recovery backup was not deleted.");
    }

    internal static bool HasArtifacts()
    {
        if (AssetDatabase.IsValidFolder(TemporaryRoot) || File.Exists(BuildSettingsBackupPath))
            return true;

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (IsTemporaryPath(scenes[i].path))
                return true;
        }
        return false;
    }

    private static void EnsureProductionScene(List<EditorBuildSettingsScene> scenes, List<bool> productionEntries,
        Dictionary<string, int> pathIndices, string scenePath)
    {
        if (pathIndices.ContainsKey(scenePath))
            return;

        pathIndices.Add(scenePath, scenes.Count);
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        productionEntries.Add(true);
    }

    private static string GetProductionPath(string path)
    {
        if (!IsTemporaryPath(path))
            return path;
        if (path.EndsWith("/BeginScene.unity", StringComparison.OrdinalIgnoreCase))
            return BeginScenePath;
        if (path.EndsWith("/GameScene.unity", StringComparison.OrdinalIgnoreCase))
            return GameScenePath;
        return null;
    }

    private static bool IsTemporaryPath(string path)
    {
        return Normalize(path).StartsWith(TemporaryRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }

    private static void EnsureAssetFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        int separator = assetPath.LastIndexOf('/');
        string parent = assetPath.Substring(0, separator);
        EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, assetPath.Substring(separator + 1));
    }
}

[InitializeOnLoad]
internal static class UIPolishTestArtifactRecovery
{
    static UIPolishTestArtifactRecovery()
    {
        AssemblyReloadEvents.beforeAssemblyReload += RestoreBeforeIdleDomainReload;
        EditorApplication.playModeStateChanged += RestoreAfterPlayMode;
        EditorApplication.delayCall += CleanupWhenEditorIsIdle;
    }

    private static void RestoreBeforeIdleDomainReload()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && UIPolishTestArtifacts.HasArtifacts())
            UIPolishTestArtifacts.RestoreProductionBuildSettings();
    }

    private static void RestoreAfterPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode && UIPolishTestArtifacts.HasArtifacts())
            UIPolishTestArtifacts.RestoreProductionBuildSettings();
        else if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += CleanupWhenEditorIsIdle;
    }

    private static void CleanupWhenEditorIsIdle()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && UIPolishTestArtifacts.HasArtifacts())
            UIPolishTestArtifacts.Cleanup();
    }
}

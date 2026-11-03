using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;

public class UGUIMigrationTests
{
    private static readonly HashSet<string> LegacyComponentNames = new HashSet<string>
    {
        "UIRoot", "UIPanel", "UILabel", "UISprite", "UI2DSprite", "UIWidget", "UIRect",
        "UIButton", "UIButtonColor", "UIButtonScale", "UICamera", "UIInput", "UISlider",
        "UIProgressBar", "UIToggle", "UIScrollView", "UIDragScrollView", "UIEventListener",
        "UIAnchor", "UIStretch", "UIDrawCall", "UIFont", "UGUIBridgeRoot", "UGUIPointerRelay",
        "UGUIViewLink",
    };

    [TestCase("Assets/Scenes/BeginScene.unity")]
    [TestCase("Assets/Scenes/GameScene.unity")]
    public void ProductionSceneUsesOneNativeCanvasAndNoLegacyComponents(string scenePath)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
        Assert.AreEqual(1, canvases.Count(item => item.name == "UICanvas"));

        Canvas canvas = canvases.Single(item => item.name == "UICanvas");
        Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
        Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>());
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        Assert.IsNotNull(scaler);
        Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);

        foreach (MonoBehaviour behaviour in Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            Assert.IsFalse(LegacyComponentNames.Contains(behaviour.GetType().Name),
                scenePath + " still contains " + behaviour.GetType().Name + " on " + behaviour.name);
        }

        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject),
                scenePath + " contains a missing script on " + child.name);
        Assert.IsFalse(EditorSceneManager.GetActiveScene().GetRootGameObjects().Any(r => r.name.StartsWith("UI Root")));
        HangarSceneUpgrade.ValidateReferences(EditorSceneManager.GetActiveScene());
    }

    [Test]
    public void BeginSceneBindingsResolveToNativeControls()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BeginScene.unity", OpenSceneMode.Single);
        BeginPanel begin = Object.FindObjectOfType<BeginPanel>(true);
        ChoosePanel choose = Object.FindObjectOfType<ChoosePanel>(true);
        DifficultyPanel difficulty = Object.FindObjectOfType<DifficultyPanel>(true);
        SettingPanel settings = Object.FindObjectOfType<SettingPanel>(true);
        RankPanel rank = Object.FindObjectOfType<RankPanel>(true);

        Assert.IsNotNull(begin.btnBegin);
        Assert.IsNotNull(begin.btnRank);
        Assert.IsNotNull(choose.labHero);
        Assert.IsNotNull(difficulty.btnStart);
        Assert.IsNotNull(difficulty.labDetail);
        Assert.IsNotNull(settings.sliderMusic);
        Assert.IsNotNull(settings.toggleSound);
        Assert.IsNotNull(rank.scrollView);
        Assert.IsNotNull(rank.scrollView.content);
        Assert.IsTrue(begin.btnBegin.transform.IsChildOf(UGUIRuntime.Canvas.transform));
    }

    [Test]
    public void GameSceneBindingsResolveToNativeControls()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        GamePanel game = Object.FindObjectOfType<GamePanel>(true);
        GameOverPanel gameOver = Object.FindObjectOfType<GameOverPanel>(true);
        QuitPanel quit = Object.FindObjectOfType<QuitPanel>(true);

        Assert.IsNotNull(game.btnBack);
        Assert.IsNotNull(game.labTime);
        Assert.IsNotNull(game.labBossWarning);
        Assert.IsNotNull(gameOver.inputName);
        Assert.IsNotNull(gameOver.btnRestart);
        Assert.IsNotNull(quit.btnClose);
        Assert.IsTrue(game.btnBack.transform.IsChildOf(UGUIRuntime.Canvas.transform));
    }

    [Test]
    public void RankItemPrefabContainsOnlyNativeUi()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/RankItem.prefab");
        Assert.IsNotNull(prefab);
        Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab));
        RankItem item = prefab.GetComponentInChildren<RankItem>(true);
        Assert.IsNotNull(item);
        Assert.IsNotNull(item.labRank);
        Assert.IsNotNull(item.labName);
        Assert.IsNotNull(item.labTime);
        Assert.GreaterOrEqual(prefab.GetComponentsInChildren<Text>(true).Length, 3);
    }

    [Test]
    public void LegacyColorMarkupConvertsToUnityRichText()
    {
        Assert.AreEqual("<color=#72E6A1>OK</color> <color=#6F7D86>pending</color>",
            UGUIRuntime.ConvertLegacyRichText("[72E6A1]OK[-] [6F7D86]pending[-]"));
    }

    [UnityTest]
    public IEnumerator NativeMenuButtonsOpenAndClosePanels()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BeginScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return null;
        yield return null;

        BeginPanel begin = Object.FindObjectOfType<BeginPanel>(true);
        SettingPanel settings = Object.FindObjectOfType<SettingPanel>(true);
        RankPanel rank = Object.FindObjectOfType<RankPanel>(true);
        Assert.IsNotNull(begin.btnSetting);
        Assert.IsNotNull(begin.btnRank);
        Assert.IsInstanceOf<UGUIButtonChrome>(begin.btnSetting.targetGraphic,
            "Menu buttons should use the redesigned chrome renderer.");

        begin.btnSetting.onClick.Invoke();
        yield return null;
        Assert.IsTrue(settings.gameObject.activeSelf);
        Assert.IsTrue(UGUIRuntime.View(settings).gameObject.activeSelf);
        GameObject settingsDialog = UGUIRuntime.FindObject(UGUIRuntime.View(settings), "Polish_Dialog");
        Assert.IsNotNull(settingsDialog);
        Assert.IsNotNull(settingsDialog.GetComponentInChildren<UGUIPanelChrome>(true),
            "Settings should use the redesigned panel renderer.");
        Assert.AreEqual(1f, UGUIRuntime.View(settings).GetComponent<CanvasGroup>().alpha, 0.001f,
            "Opening a modal must not expose a black transition frame.");
        Assert.IsNull(UGUIRuntime.View(settings).GetComponent<UGUIScreenTransition>(),
            "Modal screens should be presented immediately without a full-screen alpha fade.");
        Assert.IsNotNull(settings.sliderMusic.fillRect);
        Assert.IsNotNull(settings.sliderMusic.handleRect);
        Assert.IsNull(settings.toggleMusic.GetComponent<UGUIInteractionFeedback>(),
            "Audio toggles must not animate their settings dialog parent.");
        Assert.IsNotNull(settings.toggleMusic.GetComponent<UGUIToggleSwitchVisual>());
        float settingsScaleBeforeToggle = UGUIRuntime.View(settings).localScale.x;
        settings.toggleMusic.SetIsOnWithoutNotify(!settings.toggleMusic.isOn);
        settings.toggleMusic.GetComponent<UGUIToggleSwitchVisual>().Refresh();
        yield return null;
        Assert.AreEqual(settingsScaleBeforeToggle, UGUIRuntime.View(settings).localScale.x, 0.0001f,
            "Changing audio must not shake or resize the settings dialog.");
        Assert.LessOrEqual(settings.sliderMusic.fillRect.parent.GetComponent<RectTransform>().rect.height, 20f);
        Assert.IsInstanceOf<UGUISliderRailChrome>(settings.sliderMusic.targetGraphic);
        Assert.IsInstanceOf<UGUISliderFillChrome>(settings.sliderMusic.fillRect.GetComponent<Graphic>());
        Assert.IsNotNull(settings.sliderMusic.targetGraphic.GetComponent<CanvasRenderer>(),
            "The custom slider rail must have a CanvasRenderer for GraphicRaycaster.");
        Assert.IsNotNull(settings.sliderMusic.handleRect.GetComponent<CanvasRenderer>(),
            "The custom slider handle must have a CanvasRenderer.");
        Assert.GreaterOrEqual(settings.sliderMusic.targetGraphic.rectTransform.rect.height, 48f,
            "The full slider row should accept pointer drags, not only the thin rail.");
        Assert.LessOrEqual(settings.sliderMusic.handleRect.rect.width, 26f);
        Assert.LessOrEqual(settings.sliderMusic.handleRect.rect.height,
            ((RectTransform)settings.sliderMusic.transform).rect.height);
        RectTransform musicToggleRect = settings.toggleMusic.transform as RectTransform;
        RectTransform soundToggleRect = settings.toggleSound.transform as RectTransform;
        Assert.That(musicToggleRect.rect.width, Is.EqualTo(60f).Within(0.1f));
        Assert.That(musicToggleRect.rect.height, Is.EqualTo(30f).Within(0.1f));
        Assert.That(soundToggleRect.rect.width, Is.EqualTo(60f).Within(0.1f));
        Assert.That(musicToggleRect.anchoredPosition.y,
            Is.EqualTo(((RectTransform)settings.sliderMusic.transform).anchoredPosition.y).Within(0.1f));
        Assert.That(soundToggleRect.anchoredPosition.y,
            Is.EqualTo(((RectTransform)settings.sliderSound.transform).anchoredPosition.y).Within(0.1f));
        Assert.IsNull(UGUIRuntime.FindObject(UGUIRuntime.View(settings), "Polish_SettingsVolumeHeader"));
        Assert.IsNull(UGUIRuntime.FindObject(UGUIRuntime.View(settings), "Polish_SettingsToggleHeader"));
        Assert.IsNull(UGUIRuntime.FindObject(UGUIRuntime.View(settings), "labMusic"));
        Assert.IsNull(UGUIRuntime.FindObject(UGUIRuntime.View(settings), "labSound"));
        Text musicLabel = UGUIRuntime.Find<Text>(UGUIRuntime.View(settings), "UGUI_MusicLabel");
        Text soundLabel = UGUIRuntime.Find<Text>(UGUIRuntime.View(settings), "UGUI_SoundLabel");
        Assert.IsNotNull(musicLabel);
        Assert.IsNotNull(soundLabel);
        Assert.That(musicLabel.rectTransform.anchoredPosition.y,
            Is.EqualTo(((RectTransform)settings.sliderMusic.transform).anchoredPosition.y).Within(0.1f));
        Assert.That(soundLabel.rectTransform.anchoredPosition.y,
            Is.EqualTo(((RectTransform)settings.sliderSound.transform).anchoredPosition.y).Within(0.1f));
        Assert.IsNotNull(UGUIRuntime.Find<Text>(UGUIRuntime.View(settings), "UGUI_MusicValue"));
        GameObject obsoleteState = UGUIRuntime.FindObject(UGUIRuntime.View(settings), "UGUI_SoundState");
        Assert.IsTrue(obsoleteState == null || !obsoleteState.activeSelf);
        settings.btnClose.onClick.Invoke();
        yield return null;
        Assert.IsFalse(settings.gameObject.activeSelf);

        begin.btnRank.onClick.Invoke();
        yield return null;
        Assert.IsTrue(rank.gameObject.activeSelf);
        Assert.IsTrue(UGUIRuntime.View(rank).gameObject.activeSelf);
        Assert.IsTrue(UGUIRuntime.View(rank).GetComponentsInChildren<Text>(true)
            .All(label => !label.resizeTextForBestFit),
            "Leaderboard typography must be final before the panel is rendered.");
        Assert.GreaterOrEqual(rank.scrollView.viewport.rect.width, 900f);
        RankItem firstRow = rank.scrollView.content.GetComponentInChildren<RankItem>(true);
        Assert.IsNotNull(firstRow);
        RankItem[] visibleRows = rank.scrollView.content.GetComponentsInChildren<RankItem>(false);
        Assert.AreEqual(20, visibleRows.Length,
            "The leaderboard should expose twenty rows so scrolling can be reviewed.");
        Assert.GreaterOrEqual(rank.scrollView.content.rect.height, 20f * 58f);
        Image scrollInputSurface = rank.scrollView.viewport.GetComponent<Image>();
        Assert.IsNotNull(scrollInputSurface);
        Assert.IsTrue(scrollInputSurface.raycastTarget,
            "The complete leaderboard viewport must accept wheel and drag input.");
        Assert.IsNotNull(rank.scrollView.verticalScrollbar,
            "The leaderboard should expose a functional vertical scrollbar.");
        Assert.IsInstanceOf<UGUIScrollbarHandleChrome>(rank.scrollView.verticalScrollbar.targetGraphic);
        Assert.LessOrEqual(rank.scrollView.verticalScrollbar.handleRect.rect.width, 20f,
            "The scrollbar handle must remain a narrow rail instead of the default 100-pixel block.");
        Assert.AreEqual(ScrollRect.ScrollbarVisibility.Permanent,
            rank.scrollView.verticalScrollbarVisibility);
        GameObject legacyRankRail = UGUIRuntime.FindObject(UGUIRuntime.View(rank), "polLeft");
        Assert.IsTrue(legacyRankRail == null || !legacyRankRail.activeSelf,
            "The leaderboard must not display its obsolete second scrollbar.");
        Assert.AreEqual(1, UGUIRuntime.View(rank).GetComponentsInChildren<Scrollbar>(false).Length,
            "Only one interactive vertical scrollbar should remain active.");
        GameObject rankSummary = UGUIRuntime.FindObject(UGUIRuntime.View(rank), "UGUI_RankSummary");
        Assert.IsTrue(rankSummary == null || !rankSummary.activeSelf,
            "The redundant record-count summary should not be visible.");
        Text rankHeader = UGUIRuntime.Find<Text>(UGUIRuntime.View(rank), "UGUI_RankHeaderRank");
        Assert.IsNotNull(rankHeader);
        Image headerSurface = UGUIRuntime.FindObject(UGUIRuntime.View(rank), "Polish_RankHeader")
            .GetComponentInChildren<Image>(true);
        Image rankDialog = UGUIRuntime.FindObject(UGUIRuntime.View(rank), "Polish_Dialog")
            .GetComponentInChildren<Image>(true);
        Assert.That(rankHeader.rectTransform.position.y,
            Is.EqualTo(headerSurface.rectTransform.position.y).Within(0.1f),
            "Column headings should sit in the vertical center of their header surface.");
        Assert.That(rankHeader.rectTransform.position.x,
            Is.EqualTo(firstRow.labRank.rectTransform.position.x).Within(0.1f),
            "Header and row ranks must use the same column alignment.");
        RectTransform rankTitle = UGUIRuntime.Find<Text>(UGUIRuntime.View(rank), "lblTitle").rectTransform;
        AssertInsideWithPadding(rankDialog.rectTransform, rankTitle, 20f);
        AssertInsideWithPadding(rankDialog.rectTransform, rank.btnClose.targetGraphic.rectTransform, 20f);
        AssertInsideWithPadding(headerSurface.rectTransform, rankHeader.rectTransform, 4f);
        Assert.That(rank.scrollView.verticalScrollbar.GetComponent<RectTransform>().anchoredPosition.y,
            Is.EqualTo(rank.scrollView.viewport.anchoredPosition.y).Within(0.1f));
        Assert.IsNotNull(firstRow.GetComponent<UGUIRankRowChrome>());
        Assert.IsFalse(firstRow.labTime.gameObject.activeSelf,
            "The dense legacy record block should be replaced by clear data columns.");
        Assert.IsNotNull(UGUIRuntime.Find<Text>(firstRow.transform, "UGUI_RankScore"));
        Assert.IsNotNull(UGUIRuntime.Find<Text>(firstRow.transform, "UGUI_RankSurvival"));
        Assert.IsNotNull(UGUIRuntime.Find<Text>(firstRow.transform, "UGUI_RankLoadout"));
        RectTransform firstRowRect = firstRow.transform as RectTransform;
        Assert.LessOrEqual(firstRowRect.rect.width, rank.scrollView.viewport.rect.width);
        Assert.That(firstRowRect.anchoredPosition.x, Is.EqualTo(-8f).Within(0.1f));
        Assert.That(firstRowRect.anchoredPosition.y, Is.EqualTo(-4f).Within(0.1f));
        float rankPositionBeforeScroll = rank.scrollView.verticalNormalizedPosition;
        rank.scrollView.OnScroll(new PointerEventData(EventSystem.current)
        {
            scrollDelta = new Vector2(0f, -3f)
        });
        Canvas.ForceUpdateCanvases();
        Assert.Less(rank.scrollView.verticalNormalizedPosition, rankPositionBeforeScroll,
            "Mouse-wheel input should move the leaderboard away from its top position.");
        yield return null;
        Canvas.ForceUpdateCanvases();
        Assert.That(rank.scrollView.verticalScrollbar.value,
            Is.EqualTo(rank.scrollView.verticalNormalizedPosition).Within(0.02f));
        rank.btnClose.onClick.Invoke();
        yield return null;
        Assert.IsFalse(rank.gameObject.activeSelf);

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DynamicScreensAreStyledBeforeTheyBecomeVisible()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BeginScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return null;
        yield return null;

        Canvas canvas = UGUIRuntime.Canvas;
        GameObject networkEntryObject = UGUIRuntime.FindObject(canvas.transform, "NetworkBattleEntryButton");
        Assert.IsNotNull(networkEntryObject);
        Button networkEntry = networkEntryObject.GetComponentInChildren<Button>(true);
        networkEntry.onClick.Invoke();

        GameObject lobby = UGUIRuntime.FindObject(canvas.transform, "NetworkBattleLobby");
        GameObject lobbyPanel = UGUIRuntime.FindObject(canvas.transform, "LobbyPanel");
        Assert.IsNotNull(lobby);
        Assert.IsTrue(lobby.activeSelf, "The lobby should be presented in the same button callback.");
        Assert.IsNotNull(lobbyPanel.GetComponentInChildren<UGUIPanelChrome>(true),
            "The lobby must receive its final chrome before it is made visible.");

        Button closeLobby = UGUIRuntime.FindObject(lobby.transform, "CloseButton")
            .GetComponentInChildren<Button>(true);
        closeLobby.onClick.Invoke();
        yield return null;

        BeginPanel begin = Object.FindObjectOfType<BeginPanel>(true);
        ChoosePanel choose = Object.FindObjectOfType<ChoosePanel>(true);
        DifficultyPanel difficulty = Object.FindObjectOfType<DifficultyPanel>(true);
        begin.btnBegin.onClick.Invoke();
        yield return null;
        Assert.IsTrue(choose.gameObject.activeSelf);
        Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode,
            "The aircraft preview must not reconfigure the menu canvas or cause a camera transition.");
        Assert.IsNotNull(choose.aircraftPreview.GetComponent<RawImage>().texture);
        GameObject obsoleteScrim = UGUIRuntime.FindObject(UGUIRuntime.View(choose), "UGUI_ModalScrim");
        Assert.IsTrue(obsoleteScrim == null || !obsoleteScrim.activeSelf,
            "The aircraft hangar must not retain the obsolete square modal scrim.");
        Assert.IsNotNull(UGUIRuntime.Find<HangarGraphic>(UGUIRuntime.View(choose), "DurabilityMeter"));
        Assert.GreaterOrEqual(choose.aircraftPreview.Pivot.GetComponentsInChildren<Renderer>(true).Length, 1,
            "Aircraft selection must instantiate a visible preview model.");
        GameObject stageStatus = UGUIRuntime.FindObject(UGUIRuntime.View(choose), "Polish_StageStatus");
        Assert.IsTrue(stageStatus == null || !stageStatus.activeSelf,
            "The old HANGAR status label should not compete with aircraft details.");

        int aircraftCount = GameDataMgr.Instance.roleData.roles.Count;
        Assert.AreEqual(5, aircraftCount);
        for (int aircraftIndex = 0; aircraftIndex < aircraftCount; aircraftIndex++)
        {
            if (aircraftIndex > 0)
            {
                choose.btnRight.onClick.Invoke();
                yield return null;
            }
            Renderer[] aircraftRenderers = choose.aircraftPreview.Pivot.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(aircraftRenderers.Length, 0, "Aircraft " + (aircraftIndex + 1) + " has no renderer.");
            foreach (Renderer aircraftRenderer in aircraftRenderers)
            {
                Assert.IsTrue(aircraftRenderer.enabled);
                foreach (Material material in aircraftRenderer.sharedMaterials)
                {
                    Assert.IsNotNull(material.shader);
                    StringAssert.StartsWith("Unlit/", material.shader.name,
                        "Aircraft " + (aircraftIndex + 1) + " should not depend on scene lighting.");
                }
            }
        }

        choose.btnStart.onClick.Invoke();
        yield return null;
        Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode,
            "Leaving aircraft selection must restore the normal menu canvas mode.");
        Assert.IsNull(choose.aircraftPreview.PreviewCamera, "Closing the hangar must release its preview resources.");
        difficulty.btnStart.onClick.Invoke();

        GameObject levelRoot = UGUIRuntime.FindObject(canvas.transform, "CampaignLevelSelect");
        GameObject levelPanel = UGUIRuntime.FindObject(canvas.transform, "LevelSelectPanel");
        Assert.IsNotNull(levelRoot);
        Assert.IsTrue(levelRoot.activeSelf, "The level selector should be visible immediately.");
        Assert.IsNotNull(levelPanel.GetComponentInChildren<UGUIPanelChrome>(true),
            "The level selector must receive its final chrome before it is made visible.");

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator GameHudAndSettlementStayVisible()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();

        GamePanel game = Object.FindObjectOfType<GamePanel>(true);
        Canvas canvas = UGUIRuntime.Canvas;
        RectTransform canvasRect = canvas.transform as RectTransform;
        RectTransform healthPanel = UGUIRuntime.FindObject(UGUIRuntime.View(game), "CombatHealth")
            .transform as RectTransform;
        RectTransform skillPanel = UGUIRuntime.FindObject(UGUIRuntime.View(game), "CombatSkills")
            .transform as RectTransform;
        Assert.LessOrEqual(healthPanel.anchoredPosition.y, -400f);
        Assert.That(skillPanel.anchoredPosition.y, Is.EqualTo(healthPanel.anchoredPosition.y).Within(0.1f),
            "Health and skills should share one bottom HUD baseline.");
        for (int i = 0; i < game.hpObjs.Count; i++)
        {
            Assert.IsTrue(game.hpObjs[i].transform.IsChildOf(canvas.transform));
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                canvasRect, game.hpObjs[i].transform as RectTransform);
            Assert.GreaterOrEqual(bounds.min.y, canvasRect.rect.yMin - 1f,
                "Health block " + i + " is below the canvas.");
            Assert.IsTrue(game.hpObjs[i].activeSelf,
                "Empty health slots should remain visible instead of collapsing the gauge.");
        }
        GameObject emptyHealth = game.hpObjs[game.hpObjs.Count - 1];
        Image emptyHealthImage = emptyHealth.GetComponent<Image>();
        if (emptyHealthImage == null)
            emptyHealthImage = emptyHealth.GetComponentInChildren<Image>(true);
        Assert.IsNotNull(emptyHealthImage);
        Assert.Less(Mathf.Max(emptyHealthImage.color.r, emptyHealthImage.color.g, emptyHealthImage.color.b), 0.05f,
            "Empty health slots should use the requested near-black fill.");

        GameOverPanel gameOver = Object.FindObjectOfType<GameOverPanel>(true);
        gameOver.Showme();
        yield return null;
        Assert.IsNull(UGUIRuntime.FindObject(UGUIRuntime.View(gameOver), "PilotNameDisplay"),
            "The result screen must use the native input text instead of a blinking duplicate layer.");
        Text pilotName = gameOver.inputName.textComponent;
        Assert.IsNotNull(pilotName);
        Assert.AreEqual("Player", pilotName.text);
        Assert.Greater(pilotName.color.a, 0.9f);

        yield return new ExitPlayMode();
    }

    private static void AssertInsideWithPadding(RectTransform container, RectTransform child, float padding)
    {
        Vector3[] corners = new Vector3[4];
        child.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector3 local = container.InverseTransformPoint(corner);
            Assert.That(local.x, Is.InRange(container.rect.xMin + padding, container.rect.xMax - padding),
                child.name + " must stay inside the panel horizontally.");
            Assert.That(local.y, Is.InRange(container.rect.yMin + padding, container.rect.yMax - padding),
                child.name + " must stay inside the panel vertically.");
        }
    }

    [TestCase(1920, 1080, 16f / 9f, 1080)]
    [TestCase(1920, 1080, 4f / 3f, 1440)]
    [TestCase(1920, 1080, 21f / 9f, 1080)]
    public void AspectLayoutExpandsWithoutCropping(int width, int height, float aspect, int expectedHeight)
    {
        Assert.AreEqual(expectedHeight, UGUIAspectSafeLayout.FitHeight(width, height, aspect));
    }

}

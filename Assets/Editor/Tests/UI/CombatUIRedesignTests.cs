using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class CombatUIRedesignTests
{
    [Test]
    public void CombatSceneUsesEdgeHudAndCleanNativeModals()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        GamePanel game = Object.FindObjectOfType<GamePanel>(true);
        QuitPanel pause = Object.FindObjectOfType<QuitPanel>(true);
        GameOverPanel over = Object.FindObjectOfType<GameOverPanel>(true);

        Assert.IsNotNull(game.GetComponent<CombatHudVisibility>());
        Assert.AreEqual(7, game.GetComponent<CombatHudVisibility>().Regions.Length);
        Assert.AreEqual(10, game.hpObjs.Count);
        Assert.IsTrue(game.hpObjs.All(item => item != null && item.GetComponent<Image>() != null));
        Assert.IsNotNull(UGUIRuntime.FindObject(game.transform, "CombatMission"));
        Assert.IsNotNull(UGUIRuntime.FindObject(game.transform, "CombatTimer"));
        Assert.IsNotNull(UGUIRuntime.FindObject(game.transform, "CombatHealth"));
        Assert.IsNotNull(UGUIRuntime.FindObject(game.transform, "CombatSkills"));
        Assert.IsNull(UGUIRuntime.FindObject(game.transform, "Polish_HealthPanel"));
        Assert.IsNull(UGUIRuntime.FindObject(game.transform, "Polish_SkillPanel"));

        RectTransform mission = UGUIRuntime.FindObject(game.transform, "CombatMission").transform as RectTransform;
        RectTransform health = UGUIRuntime.FindObject(game.transform, "CombatHealth").transform as RectTransform;
        Assert.Greater(mission.anchoredPosition.y - mission.rect.height * 0.5f, 380f);
        Assert.Less(health.anchoredPosition.y + health.rect.height * 0.5f, -440f);

        Assert.AreEqual(1, pause.GetComponentsInChildren<Transform>(true).Count(t => t.name == "UGUI_ModalScrim"));
        Assert.IsNotNull(UGUIRuntime.FindObject(pause.transform, "PauseDialog").GetComponent<CombatGraphic>());
        Assert.IsNotNull(pause.MusicButton);
        Assert.IsNotNull(pause.LevelSelectButton);
        Assert.AreEqual(1, over.GetComponentsInChildren<Transform>(true).Count(t => t.name == "UGUI_ModalScrim"));
        Assert.IsNotNull(UGUIRuntime.FindObject(over.transform, "GameOverDialog").GetComponent<CombatGraphic>());
        Assert.IsNotNull(over.inputName);
        Assert.IsInstanceOf<CombatGraphic>(over.inputName.targetGraphic);

        RectTransform pauseDialog = UGUIRuntime.FindObject(pause.transform, "PauseDialog").transform as RectTransform;
        Assert.That(pauseDialog.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(pauseDialog.sizeDelta, Is.EqualTo(new Vector2(760f, 460f)));
        Assert.IsNull(UGUIRuntime.FindObject(pause.transform, "PauseStatus"));
        Assert.That(UGUIRuntime.Find<Text>(pause.transform, "PauseTitle").rectTransform.anchoredPosition.x,
            Is.EqualTo(0f).Within(0.1f));
    }

    [UnityTest]
    public IEnumerator PauseMenuOpensWithoutChangingAuthoredLayout()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return null;
        yield return null;

        GamePanel game = GamePanel.Instance;
        QuitPanel pause = QuitPanel.Instance;
        Assert.IsNotNull(game);
        Assert.IsNotNull(pause);
        game.btnBack.onClick.Invoke();
        yield return null;
        Assert.IsTrue(pause.gameObject.activeSelf);
        Assert.AreEqual(1f, pause.GetComponent<CanvasGroup>().alpha, 0.001f);
        Assert.IsNull(pause.GetComponent<UGUIScreenTransition>());
        Assert.IsInstanceOf<CombatGraphic>(pause.btnClose.targetGraphic);
        Assert.AreEqual("继续任务", UGUIRuntime.Label(pause.btnClose).text);

        bool musicBefore = GameDataMgr.Instance.musicData.musicOn;
        pause.MusicButton.onClick.Invoke();
        Assert.AreNotEqual(musicBefore, GameDataMgr.Instance.musicData.musicOn);
        StringAssert.Contains(GameDataMgr.Instance.musicData.musicOn ? "已开启" : "已关闭",
            UGUIRuntime.Label(pause.MusicButton).text);
        GameDataMgr.Instance.SetMusic(musicBefore);
        GameDataMgr.Instance.SaveMusic();

        pause.btnClose.onClick.Invoke();
        yield return null;
        Assert.IsFalse(pause.gameObject.activeSelf);

        pause.Showme();
        pause.LevelSelectButton.onClick.Invoke();
        Assert.AreEqual(MenuNavigationTarget.LevelSelect, MenuNavigationRequest.Pending);
        Assert.IsFalse(pause.LevelSelectButton.interactable);
        StringAssert.Contains("正在返回选关",
            UGUIRuntime.Find<Text>(pause.transform, "PauseNavigationStatus").text);
        pause.StopAllCoroutines();
        MenuNavigationRequest.Pending = MenuNavigationTarget.None;

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator HealthFramesStayClosedAndPilotNameCanBeCleared()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return null;
        yield return null;

        GamePanel game = GamePanel.Instance;
        game.ChangeHP(3);
        foreach (GameObject segment in game.hpObjs)
        {
            CombatHealthSegmentFrame frame = segment.GetComponentInChildren<CombatHealthSegmentFrame>(true);
            Assert.IsNotNull(frame, segment.name + " must use a closed mesh frame.");
            Outline legacy = segment.GetComponent<Outline>();
            Assert.IsTrue(legacy == null || !legacy.enabled);
        }

        GameOverPanel over = GameOverPanel.Instance;
        over.Showme();
        yield return null;
        Assert.IsNull(UGUIRuntime.FindObject(over.transform, "PilotNameDisplay"));
        Assert.Greater(over.inputName.textComponent.color.a, 0.9f);
        over.inputName.SetTextWithoutNotify(string.Empty);
        yield return null;
        Assert.AreEqual(string.Empty, over.inputName.text,
            "An empty pilot name must remain editable instead of being replaced every frame.");
        over.inputName.SetTextWithoutNotify("Ace");
        yield return null;
        yield return null;
        Assert.AreEqual("Ace", over.inputName.text);

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator CampaignResultAppearsImmediatelyAndAllActionsRespond()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return null;
        yield return null;

        CampaignMissionController mission = CampaignMissionController.Instance;
        Assert.IsNotNull(mission);
        Assert.AreEqual(CampaignMissionState.Running, mission.State);
        mission.FailMission();

        GameObject result = UGUIRuntime.FindObject(UGUIRuntime.Canvas.transform, "CampaignMissionResult");
        Assert.IsNotNull(result, "The final panel must be built in the failure call, not one frame later.");
        Assert.IsTrue(result.activeSelf);
        Assert.IsFalse(GameOverPanel.Instance.gameObject.activeSelf);

        Button details = UGUIRuntime.Find<Button>(result.transform, "MissionChallengeButton");
        details.onClick.Invoke();
        Assert.AreEqual("收起详情", UGUIRuntime.Label(details).text);
        StringAssert.Contains("挑战详情", UGUIRuntime.Find<Text>(result.transform, "MissionResultChallenges").text);

        Button levelSelect = UGUIRuntime.Find<Button>(result.transform, "MissionMenuButton");
        levelSelect.onClick.Invoke();
        Assert.AreEqual(MenuNavigationTarget.LevelSelect, MenuNavigationRequest.Pending);
        Assert.IsFalse(levelSelect.interactable);
        StringAssert.Contains("正在返回选关",
            UGUIRuntime.Find<Text>(result.transform, "MissionNavigationStatus").text);
        CampaignMissionUI.Ensure().StopAllCoroutines();
        MenuNavigationRequest.Pending = MenuNavigationTarget.None;

        yield return new ExitPlayMode();
    }
}

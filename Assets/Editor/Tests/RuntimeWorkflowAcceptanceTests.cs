using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class RuntimeWorkflowAcceptanceTests
{
    [Test]
    public void MainMenuExposesTheCompleteNativeWorkflow()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BeginScene.unity", OpenSceneMode.Single);
        BeginPanel begin = Object.FindObjectOfType<BeginPanel>(true);
        ChoosePanel choose = Object.FindObjectOfType<ChoosePanel>(true);
        DifficultyPanel difficulty = Object.FindObjectOfType<DifficultyPanel>(true);

        Assert.IsNotNull(begin.btnBegin);
        Assert.IsNotNull(begin.btnRank);
        Assert.IsNotNull(begin.btnSetting);
        Assert.IsNotNull(choose.btnLeft);
        Assert.IsNotNull(choose.btnRight);
        Assert.IsNotNull(choose.btnStart);
        Assert.IsNotNull(difficulty.btnEasy);
        Assert.IsNotNull(difficulty.btnNormal);
        Assert.IsNotNull(difficulty.btnHard);
        Assert.IsNotNull(difficulty.btnStart);
        Assert.That(begin.btnBegin, Is.TypeOf<Button>());
    }

    [Test]
    public void GameSceneExposesPauseAndSettlementWorkflow()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        GamePanel game = Object.FindObjectOfType<GamePanel>(true);
        QuitPanel quit = Object.FindObjectOfType<QuitPanel>(true);
        GameOverPanel gameOver = Object.FindObjectOfType<GameOverPanel>(true);

        Assert.IsNotNull(game.btnBack);
        Assert.IsNotNull(quit.btnClose);
        Assert.IsNotNull(quit.btnRestart);
        Assert.IsNotNull(quit.btnSure);
        Assert.IsNotNull(gameOver.inputName);
        Assert.IsNotNull(gameOver.btnRestart);
        Assert.IsNotNull(gameOver.btnSure);
        Assert.That(gameOver.inputName, Is.TypeOf<InputField>());
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class QuitPanel : BasePanel<QuitPanel>
{
    public UIButton btnSure;
    public UIButton btnClose;
    public UIButton btnRestart;

    public override void Init()
    {
        if (btnSure != null)
        {
            btnSure.onClick.Add(new EventDelegate(ReturnToMainMenu));
        }

        if (btnRestart != null)
        {
            btnRestart.onClick.Add(new EventDelegate(RestartGame));
        }

        if (btnClose != null)
        {
            btnClose.onClick.Add(new EventDelegate(ResumeGame));
        }

        Hideme();
    }

    public void ReturnToMainMenu()
    {
        StartCoroutine(LoadSceneAfterPoolCleanup("BeginScene"));
    }

    public void RestartGame()
    {
        StartCoroutine(LoadSceneAfterPoolCleanup("GameScene"));
    }

    private IEnumerator LoadSceneAfterPoolCleanup(string sceneName)
    {
        Time.timeScale = 0f;

        ObjectPoolManager pool = ObjectPoolManager.Instance;
        if (pool != null)
        {
            pool.poolingEnabled = false;
            pool.ClearInactive();
        }

        yield return null;

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public void ResumeGame()
    {
        Hideme();

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.ResumeGame();
        }
        else
        {
            Time.timeScale = 1f;
        }
    }
}

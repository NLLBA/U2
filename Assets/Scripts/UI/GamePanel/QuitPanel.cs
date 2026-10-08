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
            btnSure.onClick.Add(new EventDelegate(MainMenu));
        }

        if (btnRestart != null)
        {
            btnRestart.onClick.Add(new EventDelegate(Restart));
        }

        if (btnClose != null)
        {
            btnClose.onClick.Add(new EventDelegate(Resume));
        }

        Hideme();
    }

    public void MainMenu()
    {
        StartCoroutine(LoadScene("BeginScene"));
    }

    public void Restart()
    {
        StartCoroutine(LoadScene("GameScene"));
    }

    private IEnumerator LoadScene(string sceneName)
    {
        Time.timeScale = 0f;

        ObjectPoolManager pool = ObjectPoolManager.Instance;
        if (pool != null)
        {
            pool.usePool = false;
            pool.ClearPool();
        }

        yield return null;

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public void Resume()
    {
        Hideme();

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.Resume();
        }
        else
        {
            Time.timeScale = 1f;
        }
    }
}

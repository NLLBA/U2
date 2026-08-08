using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverPanel : BasePanel<GameOverPanel>
{
    public UILabel labTime;
    public UIInput inputName;
    public UIButton btnSure;
    public UIButton btnRestart;

    private int endTime;
    private bool hasSubmitted;

    public override void Init()
    {
        if (btnSure != null)
        {
            btnSure.onClick.Add(new EventDelegate(() => SubmitAndLoad("BeginScene")));
        }

        if (btnRestart != null)
        {
            btnRestart.onClick.Add(new EventDelegate(() => SubmitAndLoad("GameScene")));
        }

        Hideme();
    }


    public override void Showme()
    {
        base.Showme();
        hasSubmitted = false;
        endTime = (int)GamePanel.Instance.nowTime;
        labTime.text = GamePanel.Instance.labTime.text;

    }

    private void SubmitAndLoad(string sceneName)
    {
        if (hasSubmitted)
            return;

        hasSubmitted = true;
        string playerName = inputName != null ? inputName.value : string.Empty;
        GameDataMgr.Instance.AddRankData(playerName, endTime);
        StartCoroutine(LoadSceneAfterPoolCleanup(sceneName));
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
}


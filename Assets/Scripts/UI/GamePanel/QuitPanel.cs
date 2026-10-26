using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class QuitPanel : BasePanel<QuitPanel>
{
    public UIButton btnSure;
    public UIButton btnClose;
    public UIButton btnRestart;

    private UIButton btnLevelSelect;
    private UIButton btnMusic;

    public override void Init()
    {
        BuildCompletePauseMenu();
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

    public override void Showme()
    {
        base.Showme();
        RefreshMusicButton();
    }

    private void BuildCompletePauseMenu()
    {
        ResizePauseDialog();
        SetButtonText(btnClose, "继续");
        SetButtonText(btnRestart, "重新开始");
        SetButtonText(btnSure, "返回主菜单");
        if (btnClose != null)
            btnClose.transform.localPosition = new Vector3(-280f, 30f, 0f);
        if (btnRestart != null)
            btnRestart.transform.localPosition = new Vector3(0f, 30f, 0f);
        if (btnSure != null)
            btnSure.transform.localPosition = new Vector3(280f, 30f, 0f);

        btnLevelSelect = CloneButton(btnRestart, "PauseLevelSelect", new Vector3(-145f, -90f, 0f), "返回选关");
        if (btnLevelSelect != null)
            btnLevelSelect.onClick.Add(new EventDelegate(ReturnToLevelSelect));
        btnMusic = CloneButton(btnRestart, "PauseMusic", new Vector3(145f, -90f, 0f), "音乐设置");
        if (btnMusic != null)
            btnMusic.onClick.Add(new EventDelegate(ToggleMusic));
        RefreshMusicButton();
    }

    private void ResizePauseDialog()
    {
        Transform dialog = transform.Find("Polish_Dialog");
        UI2DSprite dialogSprite = dialog != null ? dialog.GetComponent<UI2DSprite>() : null;
        if (dialogSprite != null)
            dialogSprite.height = 460;

        Transform subtitle = transform.Find("Polish_PauseSubtitle");
        if (subtitle != null)
            subtitle.gameObject.SetActive(false);

        Transform headerLine = transform.Find("Polish_HeaderLine");
        if (headerLine != null)
            headerLine.localPosition = new Vector3(0f, 92f, 0f);

        Transform actionLine = transform.Find("Polish_ActionLine");
        if (actionLine != null)
            actionLine.localPosition = new Vector3(0f, -30f, 0f);
    }

    private UIButton CloneButton(UIButton source, string objectName, Vector3 position, string text)
    {
        if (source == null)
            return null;
        GameObject clone = Instantiate(source.gameObject, transform, false);
        clone.name = objectName;
        clone.transform.localPosition = position;
        UIButton button = clone.GetComponent<UIButton>();
        button.onClick.Clear();
        SetButtonText(button, text);
        return button;
    }

    private static void SetButtonText(UIButton button, string text)
    {
        if (button == null)
            return;
        UILabel label = button.GetComponentInChildren<UILabel>(true);
        if (label != null)
            label.text = text;
    }

    private void ToggleMusic()
    {
        bool enabled = !GameDataMgr.Instance.musicData.musicOn;
        GameDataMgr.Instance.SetMusic(enabled);
        GameDataMgr.Instance.SaveMusic();
        RefreshMusicButton();
    }

    private void RefreshMusicButton()
    {
        SetButtonText(btnMusic, GameDataMgr.Instance.musicData.musicOn ? "音乐：[开]" : "音乐：[关]");
    }

    private void ReturnToLevelSelect()
    {
        MenuNavigationRequest.Pending = MenuNavigationTarget.LevelSelect;
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

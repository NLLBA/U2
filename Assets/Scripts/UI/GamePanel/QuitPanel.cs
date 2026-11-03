using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class QuitPanel : BasePanel<QuitPanel>
{
    public Button btnSure;
    public Button btnClose;
    public Button btnRestart;

    [SerializeField] private Button btnLevelSelect;
    [SerializeField] private Button btnMusic;
    [SerializeField] private Text navigationStatus;
    private bool navigating;

    public Button LevelSelectButton => btnLevelSelect;
    public Button MusicButton => btnMusic;

    public void ConfigureAdditionalButtons(Button levelSelect, Button music, Text status)
    {
        btnLevelSelect = levelSelect;
        btnMusic = music;
        navigationStatus = status;
    }

    public override void Init()
    {
        if (btnSure != null)
        {
            btnSure.onClick.AddListener(MainMenu);
        }

        if (btnRestart != null)
        {
            btnRestart.onClick.AddListener(Restart);
        }

        if (btnClose != null)
        {
            btnClose.onClick.AddListener(Resume);
        }

        if (btnLevelSelect != null)
            btnLevelSelect.onClick.AddListener(ReturnToLevelSelect);
        if (btnMusic != null)
            btnMusic.onClick.AddListener(ToggleMusic);

        Hideme();
    }

    public void MainMenu()
    {
        BeginNavigation("BeginScene", "正在返回主菜单...");
    }

    public override void Showme()
    {
        base.Showme();
        navigating = false;
        SetNavigationEnabled(true);
        if (navigationStatus != null)
            navigationStatus.text = "按 Esc 可继续任务";
        RefreshMusicButton();
    }

    private static void SetButtonText(Button button, string text)
    {
        if (button == null)
            return;
        Text label = UGUIRuntime.Label(button);
        if (label != null)
            label.text = text;
    }

    private void ToggleMusic()
    {
        if (navigating)
            return;
        bool enabled = !GameDataMgr.Instance.musicData.musicOn;
        GameDataMgr.Instance.SetMusic(enabled);
        GameDataMgr.Instance.SaveMusic();
        RefreshMusicButton();
    }

    private void RefreshMusicButton()
    {
        SetButtonText(btnMusic, GameDataMgr.Instance.musicData.musicOn ? "音乐  已开启" : "音乐  已关闭");
    }

    private void ReturnToLevelSelect()
    {
        MenuNavigationRequest.Pending = MenuNavigationTarget.LevelSelect;
        BeginNavigation("BeginScene", "正在返回选关...");
    }

    public void Restart()
    {
        BeginNavigation("GameScene", "正在重新部署...");
    }

    private void BeginNavigation(string sceneName, string message)
    {
        if (navigating)
            return;
        navigating = true;
        SetNavigationEnabled(false);
        if (navigationStatus != null)
            navigationStatus.text = message;
        StartCoroutine(LoadScene(sceneName));
    }

    private void SetNavigationEnabled(bool enabled)
    {
        foreach (Button button in new[] { btnSure, btnClose, btnRestart, btnLevelSelect, btnMusic })
            if (button != null) button.interactable = enabled;
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
        if (navigating)
            return;
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

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverPanel : BasePanel<GameOverPanel>
{
    private const string DefaultPilotName = "Player";

    public UILabel labTime;
    public UIInput inputName;
    public UIButton btnSure;
    public UIButton btnRestart;

    private int endTime;
    private bool submitted;

    public override void Init()
    {
        ConfigurePilotInput();

        if (btnSure != null)
        {
            btnSure.onClick.Add(new EventDelegate(() => Submit("BeginScene")));
        }

        if (btnRestart != null)
        {
            btnRestart.onClick.Add(new EventDelegate(() => Submit("GameScene")));
        }

        Hideme();
    }


    public override void Showme()
    {
        base.Showme();
        submitted = false;
        endTime = (int)GamePanel.Instance.nowTime;
        labTime.text = GamePanel.Instance.labTime.text;

        if (inputName != null)
        {
            inputName.RemoveFocus();
            inputName.value = DefaultPilotName;
            inputName.selectAllTextOnFocus = true;
        }
    }

    private void Submit(string sceneName)
    {
        if (submitted)
            return;

        submitted = true;
        string name = inputName != null ? NormalizePilotName(inputName.value) : DefaultPilotName;
        if (inputName != null)
        {
            inputName.value = name;
            inputName.RemoveFocus();
        }

        GameDataMgr.Instance.AddRank(name, endTime);
        StartCoroutine(LoadScene(sceneName));
    }

    private void ConfigurePilotInput()
    {
        if (inputName == null)
            return;

        inputName.selectAllTextOnFocus = true;
        if (string.IsNullOrEmpty(inputName.value))
            inputName.value = DefaultPilotName;

        UIEventListener listener = UIEventListener.Get(inputName.gameObject);
        listener.onPress -= BeginPilotEditing;
        listener.onPress += BeginPilotEditing;
    }

    private void BeginPilotEditing(GameObject ignored, bool isPressed)
    {
        if (!isPressed || inputName == null || !inputName.gameObject.activeInHierarchy)
            return;

        // Normally UICamera has already sent OnSelect during the press. If
        // its global selection changed without that notification, restore
        // UIInput's internal selection explicitly before editing.
        if (!inputName.isSelected)
        {
            UICamera.selectedObject = inputName.gameObject;
            if (!inputName.isSelected)
                UICamera.Notify(inputName.gameObject, "OnSelect", true);
        }

        inputName.isSelected = true;
        inputName.selectionStart = 0;
        inputName.selectionEnd = inputName.value.Length;
    }

    private static string NormalizePilotName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DefaultPilotName;

        return value.Trim();
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
}


using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverPanel : BasePanel<GameOverPanel>
{
    private const string DefaultPilotName = "Player";

    public Text labTime;
    public InputField inputName;
    public Button btnSure;
    public Button btnRestart;

    private int endTime;
    private bool submitted;

    public override void Init()
    {
        ConfigurePilotInput();

        if (btnSure != null)
        {
            btnSure.onClick.AddListener(() => Submit("BeginScene"));
        }

        if (btnRestart != null)
        {
            btnRestart.onClick.AddListener(() => Submit("GameScene"));
        }

        Hideme();
    }


    public override void Showme()
    {
        if (NetworkBattleSession.Instance != null && NetworkBattleSession.Instance.IsBattle)
        {
            Hideme();
            return;
        }
        base.Showme();
        submitted = false;
        endTime = (int)GamePanel.Instance.nowTime;
        labTime.text = GamePanel.Instance.labTime.text;

        if (inputName != null)
        {
            inputName.DeactivateInputField();
            inputName.SetTextWithoutNotify(DefaultPilotName);
            inputName.caretPosition = inputName.text.Length;
            inputName.selectionAnchorPosition = 0;
            inputName.selectionFocusPosition = inputName.text.Length;
            StartCoroutine(FocusPilotInput());
        }
    }

    private void Submit(string sceneName)
    {
        if (NetworkBattleSession.Instance != null && NetworkBattleSession.Instance.IsBattle)
        {
            Hideme();
            return;
        }
        if (submitted)
            return;

        submitted = true;
        string name = inputName != null ? NormalizePilotName(inputName.text) : DefaultPilotName;
        if (inputName != null)
        {
            inputName.SetTextWithoutNotify(name);
            inputName.DeactivateInputField();
        }

        GameDataMgr.Instance.AddRank(name, endTime);
        StartCoroutine(LoadScene(sceneName));
    }

    private void ConfigurePilotInput()
    {
        if (inputName == null)
            return;

        Text previousText = inputName.textComponent;
        Text inputText = UGUIRuntime.Find<Text>(inputName.transform, "PilotNameText");
        if (inputText == null)
        {
            inputText = UGUIRuntime.CreateText(inputName.transform, "PilotNameText", Vector3.zero,
                352, 42, string.Empty, 24, new Color(0.92f, 0.97f, 1f, 1f),
                TextAnchor.MiddleCenter, previousText);
        }
        RectTransform textRect = inputText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 6f);
        textRect.offsetMax = new Vector2(-16f, -6f);
        textRect.SetAsLastSibling();
        inputText.resizeTextForBestFit = false;
        inputName.textComponent = inputText;
        if (previousText != null && previousText != inputText)
            previousText.gameObject.SetActive(false);

        Text placeholder = inputName.placeholder as Text;
        if (placeholder == null)
            placeholder = UGUIRuntime.Find<Text>(inputName.transform, "PilotNamePlaceholder");
        if (placeholder == null)
        {
            placeholder = UGUIRuntime.CreateText(inputName.transform, "PilotNamePlaceholder", Vector3.zero,
                352, 42, "输入飞行员代号", 20, new Color(0.48f, 0.64f, 0.72f, 0.8f),
                TextAnchor.MiddleCenter, inputText);
        }
        RectTransform placeholderRect = placeholder.rectTransform;
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(16f, 6f);
        placeholderRect.offsetMax = new Vector2(-16f, -6f);
        placeholder.text = "输入飞行员代号";
        placeholder.alignment = TextAnchor.MiddleCenter;
        placeholder.resizeTextForBestFit = false;
        placeholder.fontSize = 20;
        placeholder.color = new Color(0.48f, 0.64f, 0.72f, 0.8f);
        placeholder.raycastTarget = false;
        placeholder.transform.SetAsFirstSibling();
        inputName.placeholder = placeholder;

        inputText.transform.SetAsLastSibling();
        inputText.color = new Color(0.92f, 0.97f, 1f, 1f);
        inputText.resizeTextForBestFit = false;
        inputText.fontSize = 22;
        inputText.alignment = TextAnchor.MiddleCenter;
        inputText.raycastTarget = false;
        inputName.caretBlinkRate = 0.85f;
        inputName.characterLimit = 16;

        if (string.IsNullOrEmpty(inputName.text))
            inputName.SetTextWithoutNotify(DefaultPilotName);
    }

    private IEnumerator FocusPilotInput()
    {
        yield return null;
        if (inputName == null || !inputName.gameObject.activeInHierarchy)
            yield break;
        inputName.Select();
        inputName.ActivateInputField();
        inputName.selectionAnchorPosition = 0;
        inputName.selectionFocusPosition = inputName.text.Length;
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


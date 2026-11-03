using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DifficultySelectButton : MonoBehaviour
{
    public int difficultyId = 2;
    public string difficultyName = "普通";
    public Text statusLabel;

    private Button button;

    private void SelectDifficulty()
    {
        GameDataMgr.Instance.SetDifficulty(difficultyId);
        RefreshStatus();
        GameLog.Debug(GameLogCategory.General, "选择难度：" + difficultyName);
    }

    private void Start()
    {
        Transform view = UGUIRuntime.View(this);
        button = view != null ? view.GetComponentInChildren<Button>(true) : null;
        if (button != null)
            button.onClick.AddListener(SelectDifficulty);
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (statusLabel != null)
        {
            statusLabel.text = "当前难度：" + GameDataMgr.Instance.DifficultyName();
        }
    }
}

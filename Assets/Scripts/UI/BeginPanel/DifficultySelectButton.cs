using UnityEngine;

public class DifficultySelectButton : MonoBehaviour
{
    public int difficultyId = 2;
    public string difficultyName = "普通";
    public UILabel statusLabel;

    private void OnClick()
    {
        GameDataMgr.Instance.SetDifficulty(difficultyId);
        RefreshStatus();
        Debug.Log("选择难度：" + difficultyName);
    }

    private void Start()
    {
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (statusLabel != null)
        {
            statusLabel.text = "当前难度：" + GameDataMgr.Instance.GetDifficultyName();
        }
    }
}

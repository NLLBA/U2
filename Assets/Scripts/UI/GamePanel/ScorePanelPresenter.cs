public static class ScorePanelPresenter
{
    public static void ShowFinalScore(GameOverPanel panel)
    {
        if (panel == null || panel.labTime == null || ScoreManager.Instance == null)
            return;

        ScoreManager.Instance.FinishScore();
        string survivalText = GamePanel.Instance != null && GamePanel.Instance.labTime != null
            ? GamePanel.Instance.labTime.text
            : ((int)ScoreManager.Instance.RunTime) + "s";

        panel.labTime.fontSize = 26;
        panel.labTime.fontStyle = UnityEngine.FontStyle.Normal;
        panel.labTime.overflowMethod = UILabel.Overflow.ShrinkContent;
        panel.labTime.text = "最终分数：" + ScoreManager.Instance.TotalScore +
            "\n生存：" + survivalText + "  难度：" + GameDataMgr.Instance.DifficultyName() +
            "\nBoss：" + ScoreManager.Instance.BossRounds +
            "  清除：" + ScoreManager.Instance.ClearCount +
            "  威胁：" + ScoreManager.Instance.Threat;
    }
}

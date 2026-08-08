public static class ScorePanelPresenter
{
    public static void ShowFinalScore(GameOverPanel panel)
    {
        if (panel == null || panel.labTime == null || ScoreManager.Instance == null)
            return;

        ScoreManager.Instance.FinalizeScore();
        string survivalText = GamePanel.Instance != null && GamePanel.Instance.labTime != null
            ? GamePanel.Instance.labTime.text
            : ((int)ScoreManager.Instance.SurvivalTime) + "s";

        panel.labTime.fontSize = 26;
        panel.labTime.fontStyle = UnityEngine.FontStyle.Normal;
        panel.labTime.overflowMethod = UILabel.Overflow.ShrinkContent;
        panel.labTime.text = "最终分数：" + ScoreManager.Instance.FinalScore +
            "\n生存：" + survivalText + "  难度：" + GameDataMgr.Instance.GetDifficultyName() +
            "\nBoss：" + ScoreManager.Instance.CompletedBossRounds +
            "  清除：" + ScoreManager.Instance.ManualClearedBullets +
            "  威胁：" + ScoreManager.Instance.CurrentThreatLevel;
    }
}

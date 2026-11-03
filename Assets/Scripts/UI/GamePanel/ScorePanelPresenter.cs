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

        panel.labTime.fontSize = 23;
        panel.labTime.fontStyle = UnityEngine.FontStyle.Normal;
        panel.labTime.resizeTextForBestFit = true;
        panel.labTime.resizeTextMinSize = 12;
        panel.labTime.resizeTextMaxSize = 23;
        panel.labTime.text = "最终分数：" + ScoreManager.Instance.TotalScore +
            "（原始 " + ScoreManager.Instance.RawScore + " × " + ScoreManager.Instance.ScoreRate.ToString("0.00") + "）" +
            "\n生存：" + survivalText + "  难度：" + GameDataMgr.Instance.DifficultyName() +
            "\n模式：" + GameDataMgr.Instance.LevelName() +
            "\nBoss：" + ScoreManager.Instance.BossRounds +
            "  清除：" + ScoreManager.Instance.ClearCount +
            "  威胁：" + ScoreManager.Instance.Threat +
            "\n受击：" + ScoreManager.Instance.PlayerHits +
            "  失误：" + ScoreManager.Instance.Mistakes +
            "  最高连击：" + ScoreManager.Instance.MaxMechanicCombo +
            (EndlessRunController.Instance != null
                ? "\n最高波：" + EndlessRunController.Instance.CurrentWave +
                    "  Boss轮次：" + ScoreManager.Instance.BossRounds +
                    "  失败原因：" + EndlessRunController.Instance.FailureReason
                : string.Empty);
    }
}

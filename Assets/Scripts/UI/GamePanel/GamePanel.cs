using System.Collections.Generic;
using UnityEngine;

public class GamePanel : BasePanel<GamePanel>
{
    public UIButton btnBack;
    public UILabel labTime;
    public UILabel labLevel;
    public UILabel labDifficulty;
    public UILabel labSkill;
    public UILabel labShieldIcon;
    public UILabel labHeroSkillIcon;
    public UILabel labBossWarning;

    public List<GameObject> hpObjs;

    public float nowTime = 0;

    public override void Init()
    {
        if (btnBack != null)
        {
            btnBack.onClick.Add(new EventDelegate(() =>
            {
                ShowPauseMenu();
            }));
        }

        if (labDifficulty != null)
        {
            labDifficulty.overflowMethod = UILabel.Overflow.ShrinkContent;
        }

        if (labBossWarning != null)
        {
            labBossWarning.gameObject.SetActive(false);
        }

        ChangeHP(5);
    }

    public void ChangeHP(int hp)
    {
        for (int i = 0; i < hpObjs.Count; i++)
        {
            hpObjs[i].SetActive(i < hp);
        }
    }

    private void Update()
    {
        HandlePauseInput();

        if (GameFlowManager.Instance != null)
        {
            nowTime = GameFlowManager.Instance.SurvivalTime;
        }
        else
        {
            nowTime += Time.deltaTime;
        }

        UpdateTimeText();
        UpdateFeatureText();
        UpdateBossWarning();
    }

    private void HandlePauseInput()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || QuitPanel.Instance == null)
            return;

        if (GameFlowManager.Instance != null && GameFlowManager.Instance.State == GameFlowState.GameOver)
            return;

        if (QuitPanel.Instance.gameObject.activeSelf)
        {
            QuitPanel.Instance.ResumeGame();
        }
        else
        {
            ShowPauseMenu();
        }
    }

    private void ShowPauseMenu()
    {
        if (QuitPanel.Instance == null)
            return;

        QuitPanel.Instance.Showme();

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.PauseGame();
        }
        else
        {
            Time.timeScale = 0f;
        }
    }

    private void UpdateTimeText()
    {
        labTime.text = "";

        if ((int)nowTime / 3600 > 0)
        {
            labTime.text += (int)nowTime / 3600 + "h";
        }
        if ((int)nowTime % 3600 / 60 > 0 || labTime.text != "")
        {
            labTime.text += (int)nowTime % 3600 / 60 + "m";
        }
        labTime.text += (int)nowTime % 60 + "s";
    }

    private void UpdateFeatureText()
    {
        if (labLevel != null)
        {
            labLevel.text = GameDataMgr.Instance.GetLevelName();
            if (GameFlowManager.Instance != null)
            {
                labLevel.text += "  |  " + GameFlowManager.Instance.GetStatusText();
                if (GameFlowManager.Instance.State == GameFlowState.BossSurvival &&
                    BossController.Instance != null && BossController.Instance.IsBossActive)
                {
                    labLevel.text += "  |  弹幕：" + BossController.Instance.GetPatternName();
                }
            }
        }

        if (labDifficulty != null)
        {
            labDifficulty.text = "难度：" + GameDataMgr.Instance.GetDifficultyName();
            if (ScoreManager.Instance != null)
            {
                labDifficulty.text += "  |  威胁：" + ScoreManager.Instance.CurrentThreatLevel +
                    "  |  分数：" + ScoreManager.Instance.FinalScore;
            }
            else if (DifficultyDirector.Instance != null)
            {
                labDifficulty.text += "  |  威胁等级：" + DifficultyDirector.Instance.ThreatLevel;
            }
        }

        if (PlayerObject.Instance == null)
            return;

        if (labSkill != null)
        {
            labSkill.text = PlayerObject.Instance.GetSkillText();
        }

        if (labShieldIcon != null)
        {
            labShieldIcon.text = PlayerObject.Instance.GetShieldIconText();
        }

        if (labHeroSkillIcon != null)
        {
            labHeroSkillIcon.text = PlayerObject.Instance.GetHeroSkillIconText();
        }
    }

    private void UpdateBossWarning()
    {
        if (labBossWarning == null)
            return;

        GameFlowManager flow = GameFlowManager.Instance;
        bool shouldShow = flow != null &&
            (flow.State == GameFlowState.BossWarning || flow.State == GameFlowState.BossSurvival);

        if (labBossWarning.gameObject.activeSelf != shouldShow)
        {
            labBossWarning.gameObject.SetActive(shouldShow);
        }

        if (!shouldShow)
            return;

        int seconds = Mathf.CeilToInt(flow.GetCurrentCountdown());
        if (flow.State == GameFlowState.BossWarning)
        {
            labBossWarning.text = "BOSS 接近  第" + (flow.CompletedBossRounds + 1) + "轮  " + seconds + "秒";
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.08f;
            labBossWarning.transform.localScale = Vector3.one * pulse;
            labBossWarning.color = Color.Lerp(
                new Color(1f, 0.25f, 0.12f, 1f),
                new Color(1f, 0.85f, 0.2f, 1f),
                (Mathf.Sin(Time.unscaledTime * 8f) + 1f) * 0.5f);
        }
        else
        {
            labBossWarning.text = "第" + flow.CurrentBossRound + "轮 BOSS  坚持 " + seconds + "秒";
            labBossWarning.transform.localScale = Vector3.one;
            labBossWarning.color = new Color(1f, 0.72f, 0.16f, 1f);
        }
    }
}

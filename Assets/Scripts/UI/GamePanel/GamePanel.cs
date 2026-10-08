using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class GamePanel : BasePanel<GamePanel>
{
    public UIButton btnBack;
    public UILabel labTime;
    public UILabel labLevel;
    public UILabel labDifficulty;
    public UILabel labSkill;
    public UILabel labShieldIcon;
    [FormerlySerializedAs("labHeroSkillIcon")]
    public UILabel labSkillIcon;
    public UILabel labBossWarning;

    public List<GameObject> hpObjs;

    public float nowTime = 0;

    public override void Init()
    {
        if (btnBack != null)
        {
            btnBack.onClick.Add(new EventDelegate(() =>
            {
                ShowPause();
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
        CheckPause();

        if (GameFlowManager.Instance != null)
        {
            nowTime = GameFlowManager.Instance.RunTime;
        }
        else
        {
            nowTime += Time.deltaTime;
        }

        UpdateTime();
        UpdateInfo();
        UpdateBoss();
    }

    private void CheckPause()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || QuitPanel.Instance == null)
            return;

        if (GameFlowManager.Instance != null && GameFlowManager.Instance.State == GameFlowState.GameOver)
            return;

        if (QuitPanel.Instance.gameObject.activeSelf)
        {
            QuitPanel.Instance.Resume();
        }
        else
        {
            ShowPause();
        }
    }

    private void ShowPause()
    {
        if (QuitPanel.Instance == null)
            return;

        QuitPanel.Instance.Showme();

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.Pause();
        }
        else
        {
            Time.timeScale = 0f;
        }
    }

    private void UpdateTime()
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

    private void UpdateInfo()
    {
        if (labLevel != null)
        {
            labLevel.text = GameDataMgr.Instance.LevelName();
            if (GameFlowManager.Instance != null)
            {
                labLevel.text += "  |  " + GameFlowManager.Instance.StatusText();
                if (GameFlowManager.Instance.State == GameFlowState.BossSurvival &&
                    BossController.Instance != null && BossController.Instance.BossActive)
                {
                    labLevel.text += "  |  弹幕：" + BossController.Instance.PatternName();
                }
            }
        }

        if (labDifficulty != null)
        {
            labDifficulty.text = "难度：" + GameDataMgr.Instance.DifficultyName();
            if (ScoreManager.Instance != null)
            {
                labDifficulty.text += "  |  威胁：" + ScoreManager.Instance.Threat +
                    "  |  分数：" + ScoreManager.Instance.TotalScore;
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
            labSkill.text = PlayerObject.Instance.SkillText();
        }

        if (labShieldIcon != null)
        {
            labShieldIcon.text = PlayerObject.Instance.ShieldIcon();
        }

        if (labSkillIcon != null)
        {
            labSkillIcon.text = PlayerObject.Instance.SkillIconText();
        }
    }

    private void UpdateBoss()
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

        int seconds = Mathf.CeilToInt(flow.Countdown());
        if (flow.State == GameFlowState.BossWarning)
        {
            labBossWarning.text = "BOSS 接近  第" + (flow.BossRounds + 1) + "轮  " + seconds + "秒";
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.08f;
            labBossWarning.transform.localScale = Vector3.one * pulse;
            labBossWarning.color = Color.Lerp(
                new Color(1f, 0.25f, 0.12f, 1f),
                new Color(1f, 0.85f, 0.2f, 1f),
                (Mathf.Sin(Time.unscaledTime * 8f) + 1f) * 0.5f);
        }
        else
        {
            labBossWarning.text = "第" + flow.BossRound + "轮 BOSS  坚持 " + seconds + "秒";
            labBossWarning.transform.localScale = Vector3.one;
            labBossWarning.color = new Color(1f, 0.72f, 0.16f, 1f);
        }
    }
}

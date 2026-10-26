using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DifficultyPanel : BasePanel<DifficultyPanel>
{
    public UIButton btnEasy;
    public UIButton btnNormal;
    public UIButton btnHard;
    public UIButton btnStart;
    public UIButton btnClose;
    public UILabel labTitle;
    public UILabel labSelected;
    public UILabel labDetail;

    private static readonly Color SelectedTint = new Color(0.55f, 0.95f, 1f, 1f);

    private Color easyColor;
    private Color normalColor;
    private Color hardColor;
    private bool hasColors;

    public int DifficultyId { get; private set; }

    public override void Init()
    {
        CacheColors();

        if (btnEasy != null)
        {
            btnEasy.onClick.Add(new EventDelegate(() =>
            {
                Select(1);
            }));
        }

        if (btnNormal != null)
        {
            btnNormal.onClick.Add(new EventDelegate(() =>
            {
                Select(2);
            }));
        }

        if (btnHard != null)
        {
            btnHard.onClick.Add(new EventDelegate(() =>
            {
                Select(3);
            }));
        }

        if (btnStart != null)
        {
            btnStart.onClick.Add(new EventDelegate(StartGame));
        }

        if (btnClose != null)
        {
            btnClose.onClick.Add(new EventDelegate(() =>
            {
                Hideme();
                if (ChoosePanel.Instance != null)
                {
                    ChoosePanel.Instance.ShowCurrent();
                }
            }));
        }

        Hideme();
    }

    public void Select(int difficultyId)
    {
        GameDataMgr.Instance.SetDifficulty(difficultyId);
        DifficultyInfo selected = GameDataMgr.Instance.GetDifficulty();
        DifficultyId = selected != null ? selected.id : difficultyId;
        Refresh();
    }

    private void StartGame()
    {
        if (DifficultyId <= 0)
        {
            Select(GameDataMgr.Instance.difficultyIndex);
        }

        GameDataMgr.Instance.SetDifficulty(DifficultyId);
        CampaignLevelSelectUI.Ensure().Open();
    }

    public override void Showme()
    {
        base.Showme();
        if (labTitle != null)
        {
            labTitle.text = "选择难度";
        }

        CacheColors();
        Select(GameDataMgr.Instance.difficultyIndex);
    }

    private void Refresh()
    {
        DifficultyInfo selected = GameDataMgr.Instance.GetDifficulty();
        if (selected == null)
        {
            return;
        }

        if (labSelected != null)
        {
            string attackStyle = selected.id == 1 ? "单一招式 · 高亮预警" :
                (selected.id == 3 ? "假预警 · 反向横扫 · 双核心" : "中后段叠加组合攻击");
            labSelected.text = "当前选择：" + selected.name + " · " + attackStyle;
        }

        if (labDetail != null)
        {
            labDetail.text = DetailText(selected);
            labDetail.maxLineCount = 3;
            labDetail.height = 88;
            labDetail.overflowMethod = UILabel.Overflow.ShrinkContent;
        }

        SetButton(btnEasy, easyColor, selected.id == 1);
        SetButton(btnNormal, normalColor, selected.id == 2);
        SetButton(btnHard, hardColor, selected.id == 3);
    }

    private void CacheColors()
    {
        if (hasColors)
        {
            return;
        }

        easyColor = GetButtonColor(btnEasy);
        normalColor = GetButtonColor(btnNormal);
        hardColor = GetButtonColor(btnHard);
        hasColors = true;
    }

    private static Color GetButtonColor(UIButton button)
    {
        UIWidget widget = GetButtonWidget(button);
        return widget != null ? widget.color : Color.white;
    }

    private static void SetButton(UIButton button, Color defaultColor, bool selected)
    {
        if (button == null)
        {
            return;
        }

        Color color = selected ? Color.Lerp(defaultColor, SelectedTint, 0.7f) : defaultColor;
        button.defaultColor = color;

        UIWidget widget = GetButtonWidget(button);
        if (widget != null)
        {
            widget.color = color;
        }

        button.SetState(UIButtonColor.State.Normal, true);
    }

    private static UIWidget GetButtonWidget(UIButton button)
    {
        if (button == null)
        {
            return null;
        }

        GameObject target = button.tweenTarget != null ? button.tweenTarget : button.gameObject;
        return target.GetComponent<UIWidget>();
    }

    private static string DetailText(DifficultyInfo info)
    {
        string hpText = info.hpBonus > 0
            ? "+" + info.hpBonus.ToString(CultureInfo.InvariantCulture)
            : info.hpBonus.ToString(CultureInfo.InvariantCulture);
        if (info.id == 3)
            hpText += "（最低2）";
        string bulletSpeed = Mathf.RoundToInt(info.bulletRate * 100f).ToString(CultureInfo.InvariantCulture);
        string shieldTime = info.shieldTime.ToString("0.#", CultureInfo.InvariantCulture);
        string skillCd = info.skillCd.ToString("0.#", CultureInfo.InvariantCulture);
        int density = GameDataMgr.Instance.EncounterDensityPercent();
        int warning = GameDataMgr.Instance.EncounterWarningPercent();
        string scoreRate = ScoreManager.DefaultRateFor(info.id).ToString("0.00", CultureInfo.InvariantCulture);

        return "生命修正 " + hpText +
            "  |  敌弹速度 " + bulletSpeed + "%" +
            "  |  弹幕密度 " + density + "%" +
            "\n预警时间 " + warning + "%" +
            "  |  护盾 " + shieldTime + " 秒" +
            "  |  技能冷却 " + skillCd + " 秒" +
            "  |  分数倍率 " + scoreRate + "x";
    }
}

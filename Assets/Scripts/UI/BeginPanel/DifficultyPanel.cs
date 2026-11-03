using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DifficultyPanel : BasePanel<DifficultyPanel>
{
    public Button btnEasy;
    public Button btnNormal;
    public Button btnHard;
    public Button btnStart;
    public Button btnClose;
    public Text labTitle;
    public Text labSelected;
    public Text labDetail;

    private static readonly Color SelectedTint = new Color(0.55f, 0.95f, 1f, 1f);
    private static readonly Color EasyTint = new Color(0.25f, 0.92f, 0.62f, 1f);
    private static readonly Color NormalTint = new Color(0.24f, 0.72f, 1f, 1f);
    private static readonly Color HardTint = new Color(1f, 0.55f, 0.20f, 1f);

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
            btnEasy.onClick.AddListener(() =>
            {
                Select(1);
            });
        }

        if (btnNormal != null)
        {
            btnNormal.onClick.AddListener(() =>
            {
                Select(2);
            });
        }

        if (btnHard != null)
        {
            btnHard.onClick.AddListener(() =>
            {
                Select(3);
            });
        }

        if (btnStart != null)
        {
            btnStart.onClick.AddListener(StartGame);
        }

        if (btnClose != null)
        {
            btnClose.onClick.AddListener(() =>
            {
                Hideme();
                if (ChoosePanel.Instance != null)
                {
                    ChoosePanel.Instance.ShowCurrent();
                }
            });
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
            labSelected.color = selected.id == 1 ? EasyTint : (selected.id == 3 ? HardTint : NormalTint);
        }

        if (labDetail != null)
        {
            labDetail.text = DetailText(selected);
            UGUIRuntime.SetSize(labDetail, labDetail.rectTransform.rect.width, 88f);
            labDetail.resizeTextForBestFit = true;
            labDetail.resizeTextMinSize = 11;
            labDetail.resizeTextMaxSize = labDetail.fontSize;
        }

        SetButton(btnEasy, easyColor, selected.id == 1, UGUIChromeRole.Easy, EasyTint);
        SetButton(btnNormal, normalColor, selected.id == 2, UGUIChromeRole.Normal, NormalTint);
        SetButton(btnHard, hardColor, selected.id == 3, UGUIChromeRole.Hard, HardTint);
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

    private static Color GetButtonColor(Button button)
    {
        return button != null ? button.colors.normalColor : Color.white;
    }

    private static void SetButton(Button button, Color defaultColor, bool selected,
        UGUIChromeRole role, Color accent)
    {
        if (button == null)
        {
            return;
        }

        UGUIButtonChrome chrome = button.targetGraphic as UGUIButtonChrome;
        if (chrome != null)
        {
            chrome.SetRole(role);
            chrome.SetEmphasis(selected);
            chrome.color = Color.white;
            ColorBlock chromeColors = button.colors;
            chromeColors.normalColor = Color.white;
            chromeColors.selectedColor = Color.white;
            button.colors = chromeColors;
            Text label = UGUIRuntime.Label(button);
            if (label != null)
                label.color = selected ? Color.white : Color.Lerp(Color.white, accent, 0.32f);
            return;
        }

        Color color = selected ? Color.Lerp(defaultColor, accent, 0.72f) : Color.Lerp(defaultColor, accent, 0.28f);
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.selectedColor = color;
        button.colors = colors;
        if (button.targetGraphic != null)
            button.targetGraphic.color = color;
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

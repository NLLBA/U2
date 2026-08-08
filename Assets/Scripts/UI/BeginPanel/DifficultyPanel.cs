using System.Globalization;
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

    private Color easyDefaultColor;
    private Color normalDefaultColor;
    private Color hardDefaultColor;
    private bool buttonColorsCached;

    public int SelectedDifficultyId { get; private set; }

    public override void Init()
    {
        CacheButtonColors();

        if (btnEasy != null)
        {
            btnEasy.onClick.Add(new EventDelegate(() =>
            {
                SelectDifficulty(1);
            }));
        }

        if (btnNormal != null)
        {
            btnNormal.onClick.Add(new EventDelegate(() =>
            {
                SelectDifficulty(2);
            }));
        }

        if (btnHard != null)
        {
            btnHard.onClick.Add(new EventDelegate(() =>
            {
                SelectDifficulty(3);
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
                    ChoosePanel.Instance.ShowmeKeepingSelection();
                }
            }));
        }

        Hideme();
    }

    public void SelectDifficulty(int difficultyId)
    {
        GameDataMgr.Instance.SetDifficulty(difficultyId);
        DifficultyInfo selected = GameDataMgr.Instance.GetNowDifficultyInfo();
        SelectedDifficultyId = selected != null ? selected.id : difficultyId;
        RefreshSelection();
    }

    private void StartGame()
    {
        if (SelectedDifficultyId <= 0)
        {
            SelectDifficulty(GameDataMgr.Instance.nowDifficultyIndex);
        }

        GameDataMgr.Instance.SetDifficulty(SelectedDifficultyId);
        Time.timeScale = 1;
        SceneManager.LoadScene("GameScene");
    }

    public override void Showme()
    {
        base.Showme();
        if (labTitle != null)
        {
            labTitle.text = "选择难度";
        }

        CacheButtonColors();
        SelectDifficulty(GameDataMgr.Instance.nowDifficultyIndex);
    }

    private void RefreshSelection()
    {
        DifficultyInfo selected = GameDataMgr.Instance.GetNowDifficultyInfo();
        if (selected == null)
        {
            return;
        }

        if (labSelected != null)
        {
            labSelected.text = "当前选择：" + selected.name;
        }

        if (labDetail != null)
        {
            labDetail.text = BuildDetailText(selected);
        }

        ApplyButtonState(btnEasy, easyDefaultColor, selected.id == 1);
        ApplyButtonState(btnNormal, normalDefaultColor, selected.id == 2);
        ApplyButtonState(btnHard, hardDefaultColor, selected.id == 3);
    }

    private void CacheButtonColors()
    {
        if (buttonColorsCached)
        {
            return;
        }

        easyDefaultColor = GetButtonColor(btnEasy);
        normalDefaultColor = GetButtonColor(btnNormal);
        hardDefaultColor = GetButtonColor(btnHard);
        buttonColorsCached = true;
    }

    private static Color GetButtonColor(UIButton button)
    {
        UIWidget widget = GetButtonWidget(button);
        return widget != null ? widget.color : Color.white;
    }

    private static void ApplyButtonState(UIButton button, Color defaultColor, bool selected)
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

    private static string BuildDetailText(DifficultyInfo info)
    {
        string hpText = info.hpAdd > 0
            ? "+" + info.hpAdd.ToString(CultureInfo.InvariantCulture)
            : info.hpAdd.ToString(CultureInfo.InvariantCulture);
        string bulletSpeed = Mathf.RoundToInt(info.bulletSpeedMul * 100f).ToString(CultureInfo.InvariantCulture);
        string shieldTime = info.shieldTime.ToString("0.#", CultureInfo.InvariantCulture);
        string skillCooldown = info.skillCD.ToString("0.#", CultureInfo.InvariantCulture);

        return "生命修正 " + hpText +
            "  |  敌弹速度 " + bulletSpeed + "%" +
            "  |  护盾 " + shieldTime + " 秒" +
            "  |  技能冷却 " + skillCooldown + " 秒";
    }
}

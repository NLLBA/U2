using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ChoosePanel : BasePanel<ChoosePanel>
{
    public Button btnClose;
    public Button btnLeft;
    public Button btnRight;
    public Button btnStart;
    [FormerlySerializedAs("heroNameLabel")] public Text labHero;
    public AircraftPreview aircraftPreview;

    public static readonly string[] AircraftNames = { "苍穹", "流光", "赤隼", "重锤", "凌云" };
    public static readonly string[] AircraftClasses = { "均衡突击机", "支援护航机", "高速截击机", "重装歼灭机", "巡航多用途机" };
    private static readonly string[] Abilities = { "爆破清屏", "紧急维修", "电磁干扰", "能量脉冲", "疾航推进" };
    private static readonly string[] AbilityDescriptions = {
        "瞬间清除场上弹幕，打开突围通道。", "修复机体耐久，为下一轮交战做准备。",
        "干扰敌方弹幕，创造安全的闪避窗口。", "连续释放清弹脉冲，压制密集火力。", "短时提升航速，快速穿越危险区域。"
    };
    private readonly Button[] aircraftButtons = new Button[5];

    public override void Init()
    {
        btnClose.onClick.AddListener(() => {
            if (IsNetworkSelection()) NetworkBattleSession.Instance.Leave();
            Hideme();
            BeginPanel.Instance.Showme();
        });
        btnLeft.onClick.AddListener(() => SelectAircraft(GameDataMgr.Instance.heroIndex - 1));
        btnRight.onClick.AddListener(() => SelectAircraft(GameDataMgr.Instance.heroIndex + 1));
        btnStart.onClick.AddListener(() => {
            if (IsNetworkSelection()) { NetworkBattleSession.Instance.ConfirmCharacter(); return; }
            DifficultyPanel.Instance.Showme();
            Hideme();
        });
        Transform view = UGUIRuntime.View(this);
        for (int i = 0; i < aircraftButtons.Length; i++)
        {
            int index = i;
            aircraftButtons[i] = UGUIRuntime.Find<Button>(view, "AircraftSlot" + i);
            aircraftButtons[i].onClick.AddListener(() => SelectAircraft(index));
        }
        UGUIRuntime.Find<Button>(view, "ResetAircraftView").onClick.AddListener(aircraftPreview.ResetView);
        Hideme();
    }

    public void SelectAircraft(int index)
    {
        if (IsNetworkSelection() && NetworkBattleSession.Instance.LocalReady) return;
        int count = GameDataMgr.Instance.roleData.roles.Count;
        GameDataMgr.Instance.heroIndex = (index % count + count) % count;
        UpdateHero();
    }

    private void UpdateHero()
    {
        RoleInfo info = GameDataMgr.Instance.GetHero();
        int index = GameDataMgr.Instance.heroIndex;
        Transform view = UGUIRuntime.View(this);
        labHero.text = AircraftNames[index];
        SetText(view, "AircraftDesignation", "STRIKE CRAFT / 0" + (index + 1));
        SetText(view, "AircraftClass", AircraftClasses[index]);
        SetText(view, "AircraftCounter", "0" + (index + 1) + "  /  05");
        SetText(view, "SelectedAircraft", AircraftNames[index] + "  /  " + AircraftClasses[index]);
        SetText(view, "AbilityName", Abilities[index]);
        SetText(view, "AbilityDescription", AbilityDescriptions[index]);
        SetText(view, "AbilityCooldown", "Q  战机技能   ·   冷却 " + PlayerObject.HeroSkillCooldown(index,
            GameDataMgr.Instance.SkillCd()).ToString("0") + " 秒");
        SetText(view, "AircraftAffinity", PlayerObject.StageAffinityText(index).Replace("：", "\n"));
        SetText(view, "AircraftRecommendation", PlayerObject.RecommendedLevelText(index) + "   ·   " +
            PlayerObject.OperationDifficultyText(index));
        SetMeter(view, "Durability", info.hp);
        SetMeter(view, "Mobility", info.speed);
        SetMeter(view, "Firepower", info.volume);
        for (int i = 0; i < aircraftButtons.Length; i++)
        {
            HangarGraphic graphic = aircraftButtons[i].GetComponent<HangarGraphic>();
            graphic.SetSelected(i == index);
        }
        aircraftPreview.SetAircraft(Resources.Load<GameObject>(info.asset));
        RefreshNetworkControls();
    }

    private static void SetText(Transform view, string name, string value)
    {
        Text text = UGUIRuntime.Find<Text>(view, name);
        if (text != null) text.text = value;
    }

    private static void SetMeter(Transform view, string name, int value)
    {
        UGUIRuntime.Find<HangarGraphic>(view, name + "Meter").SetValue(value / 10f);
        SetText(view, name + "Value", value.ToString("00") + " <color=#7995A7>/ 10</color>");
    }

    public static string HeroName(RoleInfo info)
    {
        if (info == null || string.IsNullOrEmpty(info.asset)) return "未知战机";
        int slash = info.asset.LastIndexOf('/');
        return info.asset.Substring(slash + 1);
    }

    public override void Showme()
    {
        GameDataMgr.Instance.heroIndex = 0;
        ShowCurrent();
    }

    public void ShowCurrent()
    {
        base.Showme();
        UpdateHero();
    }

    private void Update() { RefreshNetworkControls(); }

    private static bool IsNetworkSelection()
    {
        return NetworkBattleSession.Instance != null &&
            NetworkBattleSession.Instance.State == NetworkBattleSession.BattleState.SelectingCharacter;
    }

    private void RefreshNetworkControls()
    {
        bool selecting = IsNetworkSelection();
        bool confirmed = selecting && NetworkBattleSession.Instance.LocalReady;
        btnLeft.interactable = btnRight.interactable = btnStart.interactable = !confirmed;
        foreach (Button button in aircraftButtons) if (button != null) button.interactable = !confirmed;
        Text label = UGUIRuntime.Label(btnStart);
        if (label != null) label.text = selecting ? (confirmed ? "已确认，等待对手" : "确认战机") : "确认战机  →";
    }
}

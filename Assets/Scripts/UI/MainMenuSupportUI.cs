using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum MenuNavigationTarget
{
    None,
    LevelSelect,
    Leaderboard,
}

public static class MenuNavigationRequest
{
    public static MenuNavigationTarget Pending { get; set; }
}

public class MainMenuSupportUI : MonoBehaviour
{
    private static MainMenuSupportUI instance;
    private GameObject helpEntry;
    private GameObject viewRoot;
    private Text templateLabel;

    private static readonly Color PanelColor = new Color(0.015f, 0.055f, 0.09f, 0.99f);
    private static readonly Color TextColor = new Color(0.91f, 0.96f, 1f, 1f);
    private static readonly Color MutedColor = new Color(0.52f, 0.69f, 0.76f, 1f);
    private static readonly Color AccentColor = new Color(0.18f, 0.88f, 1f, 1f);

    public static MainMenuSupportUI Ensure()
    {
        if (instance != null)
            return instance;
        GameObject host = new GameObject("MainMenuSupportUI");
        return host.AddComponent<MainMenuSupportUI>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Ensure();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
            instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DestroyView();
        helpEntry = null;
        if (scene.name == "BeginScene")
            StartCoroutine(BuildAfterSceneLoad());
    }

    private IEnumerator BuildAfterSceneLoad()
    {
        yield return null;
        BuildEntries();
        MenuNavigationTarget target = MenuNavigationRequest.Pending;
        MenuNavigationRequest.Pending = MenuNavigationTarget.None;
        if (target == MenuNavigationTarget.LevelSelect && BeginPanel.Instance != null)
        {
            BeginPanel.Instance.Hideme();
            CampaignLevelSelectUI.Ensure().Open();
        }
        else if (target == MenuNavigationTarget.Leaderboard && RankPanel.Instance != null)
        {
            RankPanel.Instance.Showme();
        }
    }

    private void Update()
    {
        if (helpEntry != null)
            LayoutMenuButtons();
    }

    public void OpenHelp()
    {
        DestroyView();
        BuildModal("玩法说明", "五关机制、难度、挑战与联网流程", 1320, 850);
        if (viewRoot == null)
            return;
        CreateHelpSection("HelpSectionStages", new Vector3(-292f, 105f, 0f), "关卡机制", "01  边境巡航  ·  跟随移动缺口\n02  弹幕清扫  ·  识别青色核心\n03  追踪压制  ·  锁定后再换位\n04  信标接力  ·  取能并送往信标\n05  星环风暴  ·  破盾后反击");
        CreateHelpSection("HelpSectionControl", new Vector3(292f, 105f, 0f), "飞行控制", "WASD / 左摇杆  移动\nE / 手柄 A  启动护盾\nQ / 手柄 B  释放技能\n鼠标  点击战术核心\nEsc  暂停任务");
        CreateHelpSection("HelpSectionChallenge", new Vector3(-292f, -132f, 0f), "难度与挑战", "简单  明确预警与单一招式\n普通  后段加入组合攻击\n困难  假预警、反向横扫、双核心\n挑战  零受击 / 零失误 / 不断连击\n无尽  每 12 秒升级一波");
        CreateHelpSection("HelpSectionScore", new Vector3(292f, -132f, 0f), "计分与联网", "基础分  生存、清除与机制奖励\n难度倍率  1.00 / 1.25 / 1.60\n受击或误点会中断连击\n公网房间使用 6 位房间码\n双方准备后进入战机选择");
        CreateCloseButton("关闭说明", -360f);
        UGUIVisualSystem.RefreshNow();
    }

    private void BuildEntries()
    {
        BeginPanel menu = BeginPanel.Instance;
        if (menu == null || helpEntry != null)
            return;
        templateLabel = UGUIRuntime.Label(menu.btnBegin);
        helpEntry = CloneMenuButton(menu, "HelpEntryButton", new Vector3(625f, 43f, 0f), "玩法说明", OpenHelp);
        LayoutMenuButtons();
    }

    private void LayoutMenuButtons()
    {
        BeginPanel menu = BeginPanel.Instance;
        if (menu == null)
            return;
        SetPosition(menu.btnBegin, 215f);
        GameObject network = GameObject.Find("NetworkBattleEntryButton");
        if (network != null)
            UGUIRuntime.SetPosition(network.transform, new Vector3(625f, 129f, 0f));
        if (helpEntry != null)
            UGUIRuntime.SetPosition(helpEntry.transform, new Vector3(625f, 43f, 0f));
        SetPosition(menu.btnRank, -43f);
        SetPosition(menu.btnSetting, -129f);
        SetPosition(menu.btnQuit, -215f);
    }

    private static void SetPosition(Button button, float y)
    {
        if (button != null)
            UGUIRuntime.SetPosition(UGUIRuntime.ControlRoot(button).transform, new Vector3(625f, y, 0f));
    }

    private GameObject CloneMenuButton(BeginPanel menu, string name, Vector3 position, string text, UnityEngine.Events.UnityAction callback)
    {
        GameObject go = Instantiate(UGUIRuntime.ControlRoot(menu.btnBegin), UGUIRuntime.View(menu), false);
        go.name = name;
        UGUIRuntime.SetPosition(go.transform, position);
        Button button = go.GetComponentInChildren<Button>(true);
        SetButtonText(button, text);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(callback);
        return go;
    }

    private void BuildModal(string title, string subtitle, int width, int height)
    {
        Transform parent = UGUIRuntime.Root;
        if (parent == null)
            return;
        UGUIRuntime.EnsureEventSystem();
        viewRoot = UGUIRuntime.CreateRect(parent, "SupportModal", Vector3.zero, 1920, 1080).gameObject;
        CreateSprite(viewRoot.transform, "Scrim", Vector3.zero, 4000, 2400, FindSprite("ui_scrim"), new Color(0f, 0.012f, 0.022f, 0.50f), true);
        CreateSprite(viewRoot.transform, "SupportPanel", Vector3.zero, width, height, FindSprite("ui_panel"), PanelColor, true);
        CreateSprite(viewRoot.transform, "Accent", new Vector3(0f, height * 0.5f - 72f, 0f), width - 180, 4, FindSprite("ui_accent"), AccentColor, false);
        CreateLabel(viewRoot.transform, "Title", new Vector3(0f, height * 0.5f - 125f, 0f), width - 160, 54,
            title, 34, TextColor, TextAnchor.MiddleCenter);
        CreateLabel(viewRoot.transform, "Subtitle", new Vector3(0f, height * 0.5f - 170f, 0f), width - 160, 32,
            subtitle, 16, MutedColor, TextAnchor.MiddleCenter);
    }

    private void CreateHelpSection(string name, Vector3 position, string title, string body)
    {
        Image panel = CreateSprite(viewRoot.transform, name, position, 540, 202, FindSprite("ui_panel_soft"),
            new Color(0.012f, 0.055f, 0.075f, 0.94f), false);
        CreateLabel(panel.transform, "SectionTitle", new Vector3(0f, 70f, 0f), 460, 34,
            title, 22, AccentColor, TextAnchor.MiddleLeft);
        CreateLabel(panel.transform, "SectionBody", new Vector3(0f, -18f, 0f), 460, 132,
            body, 16, TextColor, TextAnchor.MiddleLeft);
    }

    private void CreateCloseButton(string text, float y)
    {
        Button close = CreateButton("Close", new Vector3(0f, y, 0f), 320, 60, text, AccentColor);
        close.onClick.AddListener(DestroyView);
    }

    private Button CreateButton(string name, Vector3 position, int width, int height, string text, Color color)
    {
        GameObject source = BeginPanel.Instance != null ? UGUIRuntime.ControlRoot(BeginPanel.Instance.btnBegin) : null;
        if (source == null)
            return null;
        GameObject go = Instantiate(source, viewRoot.transform, false);
        go.name = name;
        UGUIRuntime.SetPosition(go.transform, position);
        go.transform.localScale = Vector3.one;
        UGUIRuntime.SetSize(go.transform, width, height);
        Button button = go.GetComponentInChildren<Button>(true);
        button.onClick.RemoveAllListeners();
        Text label = UGUIRuntime.Label(button);
        if (label != null)
        {
            label.text = text;
            label.color = color;
            label.rectTransform.sizeDelta = new Vector2(width - 24, height - 8);
            label.resizeTextForBestFit = true;
        }
        return button;
    }

    private Text CreateLabel(Transform parent, string name, Vector3 position, int width, int height,
        string text, int size, Color color, TextAnchor alignment)
    {
        return UGUIRuntime.CreateText(parent, name, position, width, height, text, size, color, alignment, templateLabel);
    }

    private static Image CreateSprite(Transform parent, string name, Vector3 position, int width, int height,
        Sprite spriteAsset, Color color, bool raycastTarget)
    {
        return UGUIRuntime.CreateImage(parent, name, position, width, height, null, spriteAsset, color, raycastTarget);
    }

    private void DestroyView()
    {
        if (viewRoot != null)
        {
            viewRoot.SetActive(false);
            Destroy(viewRoot);
        }
        viewRoot = null;
    }

    private static void SetButtonText(Button button, string text)
    {
        if (button == null)
            return;
        Text label = UGUIRuntime.Label(button);
        if (label != null)
            label.text = text;
    }

    private static Sprite FindSprite(string name)
    {
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < sprites.Length; i++)
            if (sprites[i].name == name)
                return sprites[i];
        return null;
    }
}

public static class ChineseFontFallback
{
    private static Font cached;
    public static Font Font
    {
        get
        {
            if (cached == null)
                cached = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial Unicode MS", "Arial" }, 24);
            return cached;
        }
    }
}

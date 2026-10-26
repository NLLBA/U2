using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    private UILabel templateLabel;

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
        CreateLabel(viewRoot.transform, "HelpBody", new Vector3(0f, 30f, 0f), 1140, 590,
            "[第一关 · 边境巡航] 跟随移动缺口；普通难度前段只教学单一规则。\n" +
            "[第二关 · 弹幕清扫] 只点击青色发光核心；误点诱饵会触发分裂。\n" +
            "[第三关 · 追踪压制] 等锁定线固定后换位；提前移动不算完美闪避。\n" +
            "[第四关 · 信标接力] 中央取能后送往橙色信标；携能受击会损失能量。\n" +
            "[第五关 · 星环风暴] 点击金色核心反击；破盾后进入狂暴并获得1.5秒保护。\n\n" +
            "难度：简单提示更明显；普通后段加入组合攻击；困难加入假预警、反向横扫、双核心等机制。\n" +
            "挑战：零受击、关卡专属零失误、全程不断连击。无尽每12秒一波，后期组合升级并检测固定安全点。\n" +
            "操作：WASD/左摇杆移动，E/手柄A护盾，Q/手柄B技能，鼠标点击核心，Esc暂停。\n" +
            "联网：选择公网房间，房主创建6位房间码，对方可在不同网络输入同一码；双方准备后先选角色。\n\n" +
            "计分：原始分 = 生存秒数×10 + 手动清除×25 + Boss奖励 + 机制奖励。\n" +
            "Boss奖励 = 1000×已完成轮数×(轮数+1)÷2；机制奖励：穿越100、连锁120、闪避150、信标250、反弹200。\n" +
            "最终分 = 原始分×难度倍率（简单1.00、普通1.25、困难1.60，四舍五入）。受击/误点会中断连击，但不直接扣除已得分。",
            18, TextColor, NGUIText.Alignment.Left);
        CreateCloseButton("关闭说明", -360f);
    }

    private void BuildEntries()
    {
        BeginPanel menu = BeginPanel.Instance;
        if (menu == null || helpEntry != null)
            return;
        templateLabel = menu.btnBegin.GetComponentInChildren<UILabel>(true);
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
        if (network != null && network.transform.parent == menu.transform)
            network.transform.localPosition = new Vector3(625f, 129f, 0f);
        if (helpEntry != null)
            helpEntry.transform.localPosition = new Vector3(625f, 43f, 0f);
        SetPosition(menu.btnRank, -43f);
        SetPosition(menu.btnSetting, -129f);
        SetPosition(menu.btnQuit, -215f);
    }

    private static void SetPosition(UIButton button, float y)
    {
        if (button != null)
            button.transform.localPosition = new Vector3(625f, y, 0f);
    }

    private GameObject CloneMenuButton(BeginPanel menu, string name, Vector3 position, string text, EventDelegate.Callback callback)
    {
        GameObject go = Instantiate(menu.btnBegin.gameObject, menu.transform, false);
        go.name = name;
        go.transform.localPosition = position;
        UIButton button = go.GetComponent<UIButton>();
        SetButtonText(button, text);
        button.onClick.Clear();
        button.onClick.Add(new EventDelegate(callback));
        return go;
    }

    private void BuildModal(string title, string subtitle, int width, int height)
    {
        UIRoot root = FindObjectOfType<UIRoot>();
        if (root == null)
            return;
        viewRoot = new GameObject("SupportModal");
        viewRoot.transform.SetParent(root.transform, false);
        viewRoot.layer = root.gameObject.layer;
        viewRoot.AddComponent<UIPanel>().depth = 95;
        CreateSprite(viewRoot.transform, "Scrim", Vector3.zero, 4000, 2400, FindSprite("ui_scrim"), new Color(0f, 0f, 0f, 0.82f), -100, true);
        CreateSprite(viewRoot.transform, "Panel", Vector3.zero, width, height, FindSprite("ui_panel"), PanelColor, -50, true);
        CreateSprite(viewRoot.transform, "Accent", new Vector3(0f, height * 0.5f - 72f, 0f), width - 180, 4, FindSprite("ui_accent"), AccentColor, -10, false);
        CreateLabel(viewRoot.transform, "Title", new Vector3(0f, height * 0.5f - 125f, 0f), width - 160, 54,
            title, 34, TextColor, NGUIText.Alignment.Center);
        CreateLabel(viewRoot.transform, "Subtitle", new Vector3(0f, height * 0.5f - 170f, 0f), width - 160, 32,
            subtitle, 16, MutedColor, NGUIText.Alignment.Center);
    }

    private void CreateCloseButton(string text, float y)
    {
        UIButton close = CreateButton("Close", new Vector3(0f, y, 0f), 320, 60, text, AccentColor);
        close.onClick.Add(new EventDelegate(DestroyView));
    }

    private UIButton CreateButton(string name, Vector3 position, int width, int height, string text, Color color)
    {
        GameObject source = BeginPanel.Instance != null ? BeginPanel.Instance.btnBegin.gameObject : null;
        if (source == null)
            return null;
        GameObject go = Instantiate(source, viewRoot.transform, false);
        go.name = name;
        go.transform.localPosition = position;
        go.transform.localScale = Vector3.one;
        UI2DSprite background = go.GetComponent<UI2DSprite>();
        if (background != null)
        {
            background.width = width;
            background.height = height;
        }
        BoxCollider collider = go.GetComponent<BoxCollider>();
        if (collider != null)
            collider.size = new Vector3(width, height, 0.1f);
        UIButton button = go.GetComponent<UIButton>();
        button.onClick.Clear();
        UILabel label = go.GetComponentInChildren<UILabel>(true);
        if (label != null)
        {
            label.text = text;
            label.color = color;
            label.width = width - 24;
            label.height = height - 8;
            label.overflowMethod = UILabel.Overflow.ShrinkContent;
        }
        return button;
    }

    private UILabel CreateLabel(Transform parent, string name, Vector3 position, int width, int height,
        string text, int size, Color color, NGUIText.Alignment alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.layer = parent.gameObject.layer;
        UILabel label = go.AddComponent<UILabel>();
        if (templateLabel != null)
        {
            label.font = templateLabel.font;
            label.trueTypeFont = templateLabel.trueTypeFont;
            label.fontStyle = templateLabel.fontStyle;
            label.effectStyle = templateLabel.effectStyle;
            label.effectColor = templateLabel.effectColor;
        }
        if (label.font == null && label.trueTypeFont == null)
            label.trueTypeFont = ChineseFontFallback.Font;
        label.text = text;
        label.width = width;
        label.height = height;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.overflowMethod = UILabel.Overflow.ShrinkContent;
        label.depth = 40;
        return label;
    }

    private static UI2DSprite CreateSprite(Transform parent, string name, Vector3 position, int width, int height,
        Sprite spriteAsset, Color color, int depth, bool collider)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.layer = parent.gameObject.layer;
        UI2DSprite sprite = go.AddComponent<UI2DSprite>();
        sprite.sprite2D = spriteAsset;
        sprite.type = UIBasicSprite.Type.Sliced;
        sprite.width = width;
        sprite.height = height;
        sprite.color = color;
        sprite.depth = depth;
        if (collider)
            go.AddComponent<BoxCollider>().size = new Vector3(width, height, 0.1f);
        return sprite;
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

    private static void SetButtonText(UIButton button, string text)
    {
        if (button == null)
            return;
        UILabel label = button.GetComponentInChildren<UILabel>(true);
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

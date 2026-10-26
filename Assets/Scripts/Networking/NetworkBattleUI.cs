using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// 联网双人生存竞技 UI。
/// 复用项目现有的独立 UI 图片、字体和层级。
/// </summary>
public class NetworkBattleUI : MonoBehaviour
{
    [Serializable]
    private class RelayHealth
    {
        public int protocol = -1;
        public string clientVersion = string.Empty;
        public string status = string.Empty;
    }

    private static NetworkBattleUI instance;

    private NetworkBattleSession session;
    private GameObject viewRoot;
    private GameObject menuEntry;
    private GameObject recoveryNoticeObject;
    private UI2DSprite scrimTemplate;
    private UI2DSprite panelTemplate;
    private UI2DSprite inputTemplate;
    private UI2DSprite buttonTemplate;
    private UI2DSprite accentTemplate;
    private UILabel templateLabel;

    private UIInput nameInput;
    private UIInput addressInput;
    private UIInput portInput;
    private UIInput roomCodeInput;
    private UILabel lobbyStateLabel;
    private UILabel lobbyHintLabel;
    private UILabel lobbyPlayersLabel;
    private UILabel lobbyResultLabel;
    private UILabel localHudLabel;
    private UILabel remoteHudLabel;
    private UILabel hudStateLabel;
    private UILabel hudRuleLabel;
    private UILabel hudResultLabel;
    private UILabel lobbyLevelLabel;
    private UILabel rematchStatusLabel;
    private UIButton lobbyReadyButton;
    private UIButton hostConnectButton;
    private UIButton joinConnectButton;

    private bool showLobby;
    private string playerName = "Player";
    private string address = "";
    private string localAddress = "";
    private float nextAddressRefresh;
    private string portText = "7777";
    private bool publicMode = true;
    private string roomCode = "";
    private NetworkRelaySettings relaySettings;
    private string builtScene = "";
    private NetworkBattleSession.BattleState builtState = (NetworkBattleSession.BattleState)(-1);
    private bool builtLobby;
    private bool builtLocalAlive;
    private bool relayHealthChecking;
    private Coroutine relayHealthRoutine;
    private bool lastAttemptHost;
    private string startupNotice;

    private static readonly Color PanelColor = new Color(0.015f, 0.055f, 0.09f, 0.97f);
    private static readonly Color TextColor = new Color(0.91f, 0.96f, 1f, 1f);
    private static readonly Color MutedTextColor = new Color(0.55f, 0.72f, 0.79f, 1f);
    private static readonly Color AccentColor = new Color(0.18f, 0.88f, 1f, 1f);
    private static readonly Color ErrorColor = new Color(1f, 0.42f, 0.35f, 1f);

    public static NetworkBattleUI Ensure()
    {
        if (instance != null)
            return instance;

        GameObject host = new GameObject("NetworkBattleUI");
        return host.AddComponent<NetworkBattleUI>();
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
        session = NetworkBattleSession.Ensure();
        relaySettings = NetworkRelaySettings.Load();
        startupNotice = XmlDataMgr.ConsumeRecoveryNotice();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (relayHealthRoutine != null)
            StopCoroutine(relayHealthRoutine);
        if (instance == this)
            instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DestroyView();
        builtScene = "";
        builtState = (NetworkBattleSession.BattleState)(-1);
    }

    private void Update()
    {
        if (session == null)
            session = NetworkBattleSession.Instance;
        if (session == null)
            return;

        CaptureInputValues();

        string scene = SceneManager.GetActiveScene().name;
        if (Input.GetKeyDown(KeyCode.F2) && scene == "BeginScene" && session.State == NetworkBattleSession.BattleState.Idle)
        {
            if (showLobby) CloseLobby();
            else if (BeginPanel.Instance != null && BeginPanel.Instance.gameObject.activeInHierarchy) OpenLobby();
        }

        NetworkBattleSession.BattleState state = session.State;
        // An inactive scene panel runs Init() on its first activation and may hide itself once.
        if (scene == "BeginScene" && state == NetworkBattleSession.BattleState.SelectingCharacter &&
            ChoosePanel.Instance != null && !ChoosePanel.Instance.gameObject.activeSelf)
            ChoosePanel.Instance.ShowCurrent();
        if (scene != builtScene || state != builtState || showLobby != builtLobby ||
            session.LocalAlive != builtLocalAlive)
        {
            RebuildView(scene, state);
        }
        else
        {
            RefreshDynamicText();
        }
    }

    private void CaptureInputValues()
    {
        if (nameInput != null && !string.IsNullOrEmpty(nameInput.value))
            playerName = nameInput.value;
        if (addressInput != null)
            address = addressInput.value;
        if (portInput != null && !string.IsNullOrEmpty(portInput.value))
            portText = portInput.value;
        if (roomCodeInput != null)
            roomCode = roomCodeInput.value;
    }

    private void RebuildView(string scene, NetworkBattleSession.BattleState state)
    {
        CaptureInputValues();
        DestroyView();
        builtScene = scene;
        builtState = state;
        builtLobby = showLobby;
        builtLocalAlive = session.LocalAlive;

        if (scene == "BeginScene")
        {
            if (state == NetworkBattleSession.BattleState.SelectingCharacter)
            {
                if (BeginPanel.Instance != null) BeginPanel.Instance.Hideme();
                if (ChoosePanel.Instance != null) ChoosePanel.Instance.ShowCurrent();
            }
            else if (showLobby || state != NetworkBattleSession.BattleState.Idle)
            {
                if (ChoosePanel.Instance != null) ChoosePanel.Instance.Hideme();
                BuildLobby();
            }
            else
                BuildBeginEntry();
        }
        else if (scene == "GameScene" && session.IsBattle)
        {
            BuildBattleHud();
        }
        else if (scene == "GameScene" && state == NetworkBattleSession.BattleState.Error)
        {
            BuildConnectionLost();
        }
    }

    private void DestroyView()
    {
        if (viewRoot != null)
        {
            viewRoot.SetActive(false);
            Destroy(viewRoot);
        }

        viewRoot = null;
        nameInput = null;
        addressInput = null;
        portInput = null;
        roomCodeInput = null;
        lobbyStateLabel = null;
        lobbyHintLabel = null;
        lobbyPlayersLabel = null;
        lobbyResultLabel = null;
        localHudLabel = null;
        remoteHudLabel = null;
        hudStateLabel = null;
        hudRuleLabel = null;
        hudResultLabel = null;
        lobbyLevelLabel = null;
        rematchStatusLabel = null;
        lobbyReadyButton = null;
        hostConnectButton = null;
        joinConnectButton = null;
    }

    private Transform GetUiParent()
    {
        UIRoot root = FindObjectOfType<UIRoot>();
        return root != null ? root.transform : null;
    }

    private void CacheTemplates()
    {
        Transform parent = GetUiParent();
        if (parent == null)
            return;

        scrimTemplate = panelTemplate = inputTemplate = buttonTemplate = accentTemplate = null;
        UI2DSprite fallbackButtonTemplate = null;
        // Hidden scene panels own the authored assets. Exclude previous dynamic views,
        // which may still exist until the end of this frame after DestroyView().
        foreach (UI2DSprite sprite in parent.GetComponentsInChildren<UI2DSprite>(true))
        {
            UIPanel owner = NGUITools.FindInParents<UIPanel>(sprite.gameObject);
            if (sprite.sprite2D == null || (owner != null && owner.name.StartsWith("NetworkBattle")))
                continue;
            if (fallbackButtonTemplate == null && sprite.sprite2D.name.StartsWith("ui_button_"))
                fallbackButtonTemplate = sprite;
            switch (sprite.sprite2D.name)
            {
                case "ui_scrim": scrimTemplate = sprite; break;
                case "ui_panel": panelTemplate = sprite; break;
                case "ui_input": inputTemplate = sprite; break;
                case "ui_button_primary": buttonTemplate = sprite; break;
                case "ui_accent": accentTemplate = sprite; break;
            }
        }

        if (buttonTemplate == null)
            buttonTemplate = fallbackButtonTemplate;

        if (BeginPanel.Instance != null)
            templateLabel = BeginPanel.Instance.btnBegin.GetComponentInChildren<UILabel>(true);
        else if (GamePanel.Instance != null)
            templateLabel = GamePanel.Instance.labTime;
    }

    private void CreateViewRoot(string name, int panelDepth)
    {
        Transform parent = GetUiParent();
        viewRoot = new GameObject(name);
        if (parent != null)
        {
            viewRoot.transform.SetParent(parent, false);
            viewRoot.layer = parent.gameObject.layer;
        }

        UIPanel panel = viewRoot.AddComponent<UIPanel>();
        panel.depth = panelDepth;
        CacheTemplates();
    }

    private void BuildBeginEntry()
    {
        BeginPanel menu = BeginPanel.Instance;
        if (menu == null || menuEntry != null)
            return;

        // Clone the authored start button to retain its texture, typography and hover animation.
        menuEntry = Instantiate(menu.btnBegin.gameObject, menu.transform, false);
        menuEntry.name = "NetworkBattleEntryButton";
        menuEntry.transform.localPosition = new Vector3(625f, 86f, 0f);
        menuEntry.GetComponentInChildren<UILabel>().text = "联网模式";
        UIButton button = menuEntry.GetComponent<UIButton>();
        button.onClick.Clear();
        button.onClick.Add(new EventDelegate(OpenLobby));
        menu.btnRank.transform.localPosition = new Vector3(625f, -18f, 0f);
        menu.btnSetting.transform.localPosition = new Vector3(625f, -122f, 0f);
        menu.btnQuit.transform.localPosition = new Vector3(625f, -226f, 0f);

        if (!string.IsNullOrEmpty(startupNotice))
        {
            UILabel template = menu.btnBegin.GetComponentInChildren<UILabel>(true);
            if (template != null)
            {
                recoveryNoticeObject = Instantiate(template.gameObject, menu.transform, false);
                recoveryNoticeObject.name = "SaveRecoveryNotice";
                recoveryNoticeObject.transform.localPosition = new Vector3(0f, -330f, 0f);
                UILabel notice = recoveryNoticeObject.GetComponent<UILabel>();
                notice.width = 980;
                notice.height = 62;
                notice.fontSize = 16;
                notice.overflowMethod = UILabel.Overflow.ShrinkContent;
                notice.color = new Color(1f, 0.78f, 0.3f, 1f);
                notice.text = startupNotice;
            }
        }
    }

    private void BuildLobby()
    {
        if (BeginPanel.Instance != null)
            BeginPanel.Instance.Hideme();
        CreateViewRoot("NetworkBattleLobby", 80);
        UI2DSprite scrim = CreateSprite(viewRoot.transform, "LobbyScrim", Vector3.zero, 4000, 2400, "Dark", new Color(1f, 1f, 1f, 0.75f), true);
        CopySpriteStyle(scrim, scrimTemplate);
        scrim.depth = -100;
        CreateSprite(viewRoot.transform, "LobbyPanel", Vector3.zero, 1180, 760, "Dark", PanelColor, true);
        CreateSprite(viewRoot.transform, "LobbyAccent", new Vector3(0f, 245f, 0f), 1000, 4, "Bright", AccentColor, false);

        CreateLabel(viewRoot.transform, "LobbyTitle", new Vector3(0f, 310f, 0f), 900, 58, "联网双人对战", 34, TextColor, NGUIText.Alignment.Center);
        CreateLabel(viewRoot.transform, "LobbySubtitle", new Vector3(0f, 270f, 0f), 900, 32, "房主选关  ·  独立计分  ·  无尽生存竞技", 17, MutedTextColor, NGUIText.Alignment.Center);

        if (session.State == NetworkBattleSession.BattleState.Idle || session.State == NetworkBattleSession.BattleState.Error)
        {
            BuildJoinForm();
        }
        else if (session.State == NetworkBattleSession.BattleState.Finished)
        {
            BuildLobbyFinished();
        }
        else
        {
            BuildWaitingRoom();
        }
    }

    private void BuildJoinForm()
    {
        UIButton publicButton = CreateButton(viewRoot.transform, "PublicModeButton", new Vector3(-235f, 200f, 0f), 420, 48,
            "互联网房间" + (publicMode ? " · 已选" : ""), publicMode ? AccentColor : MutedTextColor);
        UIButton lanButton = CreateButton(viewRoot.transform, "LanModeButton", new Vector3(235f, 200f, 0f), 420, 48,
            "局域网直连" + (!publicMode ? " · 已选" : ""), !publicMode ? AccentColor : MutedTextColor);
        publicButton.onClick.Add(new EventDelegate(UsePublicMode));
        lanButton.onClick.Add(new EventDelegate(UseLanMode));
        CreateLabel(viewRoot.transform, "FormHint", new Vector3(0f, 135f, 0f), 1000, 55,
            publicMode ? "双方可以使用不同 Wi-Fi 或手机热点。\n房主创建房间后，把 6 位房间码发给另一方即可。" :
            "局域网直连需要连接同一网络。\n另一方输入房主 IP 和相同端口加入；127.0.0.1 仅限本机双开。",
            16, MutedTextColor, NGUIText.Alignment.Center);

        nameInput = CreateInput(viewRoot.transform, "NameInput", new Vector3(80f, 55f, 0f), 760, 64, "飞行员代号", playerName, 16);
        if (publicMode)
        {
            roomCodeInput = CreateInput(viewRoot.transform, "RoomCodeInput", new Vector3(80f, -30f, 0f), 760, 64, "6 位房间码", roomCode, 6);
            roomCodeInput.validation = UIInput.Validation.Integer;
            CreateLabel(viewRoot.transform, "RelayNotice", new Vector3(0f, -105f, 0f), 1000, 50,
                RelayNoticeText("创建房间时无需填写房间码。"),
                16, MutedTextColor, NGUIText.Alignment.Center);
        }
        else
        {
            addressInput = CreateInput(viewRoot.transform, "AddressInput", new Vector3(80f, -30f, 0f), 760, 64, "房主 IP 地址", address, 32);
            portInput = CreateInput(viewRoot.transform, "PortInput", new Vector3(80f, -115f, 0f), 760, 64, "端口（默认 7777）", portText, 6);
        }

        hostConnectButton = CreateButton(viewRoot.transform, "HostButton", new Vector3(-200f, -185f, 0f), 300, 70, "创建房间", AccentColor);
        joinConnectButton = CreateButton(viewRoot.transform, "JoinButton", new Vector3(200f, -185f, 0f), 300, 70, "加入房间", TextColor);
        hostConnectButton.onClick.Add(new EventDelegate(StartHost));
        joinConnectButton.onClick.Add(new EventDelegate(StartJoin));

        UIButton closeButton = CreateButton(viewRoot.transform, "CloseButton", new Vector3(0f, -260f, 0f), 240, 54, "返回", MutedTextColor);
        closeButton.onClick.Add(new EventDelegate(CloseLobby));

        if (session.State == NetworkBattleSession.BattleState.Error)
        {
            CreateLabel(viewRoot.transform, "ErrorLabel", new Vector3(0f, -330f, 0f), 1080, 80, "连接失败：" + session.LastError, 17, ErrorColor, NGUIText.Alignment.Center);
        }
    }

    private void BuildWaitingRoom()
    {
        lobbyStateLabel = CreateLabel(viewRoot.transform, "LobbyState", new Vector3(0f, 200f, 0f), 900, 48,
            StateText(session.State), 24, AccentColor, NGUIText.Alignment.Center);
        lobbyHintLabel = CreateLabel(viewRoot.transform, "LobbyHint", new Vector3(0f, 145f, 0f), 1080, 72,
            "", 18, MutedTextColor, NGUIText.Alignment.Center);
        lobbyPlayersLabel = CreateLabel(viewRoot.transform, "LobbyPlayers", new Vector3(0f, 60f, 0f), 900, 70, "", 20, TextColor, NGUIText.Alignment.Center);
        lobbyLevelLabel = CreateLabel(viewRoot.transform, "LobbyLevel", new Vector3(0f, -5f, 0f), 650, 44,
            "", 20, AccentColor, NGUIText.Alignment.Center);
        if (session.IsHost)
        {
            UIButton previous = CreateButton(viewRoot.transform, "PreviousLevelButton", new Vector3(-390f, -5f, 0f), 58, 52, "<", TextColor);
            previous.onClick.Add(new EventDelegate(PreviousNetworkLevel));
            UIButton next = CreateButton(viewRoot.transform, "NextLevelButton", new Vector3(390f, -5f, 0f), 58, 52, ">", TextColor);
            next.onClick.Add(new EventDelegate(NextNetworkLevel));
        }
        CreateLabel(viewRoot.transform, "WaitingRules", new Vector3(0f, -80f, 0f), 1000, 50,
            MatchRulesText(), 16, MutedTextColor, NGUIText.Alignment.Center);

        if (session.State == NetworkBattleSession.BattleState.Waiting || session.State == NetworkBattleSession.BattleState.Ready)
        {
            lobbyReadyButton = CreateButton(viewRoot.transform, "ReadyButton", new Vector3(0f, -165f, 0f), 360, 70, "准备开始", AccentColor);
            lobbyReadyButton.onClick.Add(new EventDelegate(SetReady));
        }

        UIButton leaveButton = CreateButton(viewRoot.transform, "LeaveButton", new Vector3(0f, -270f, 0f), 280, 56, "退出房间", MutedTextColor);
        leaveButton.onClick.Add(new EventDelegate(LeaveRoom));
        if (session.IsHost)
        {
            UIButton copyButton = CreateButton(viewRoot.transform, "CopyAddressButton", new Vector3(0f, -345f, 0f), 300, 44,
                session.IsRelayRoom ? "复制房间码" : "复制房主 IP", TextColor);
            copyButton.onClick.Add(new EventDelegate(CopyHostAddress));
        }
        RefreshDynamicText();
    }

    private void BuildLobbyFinished()
    {
        lobbyResultLabel = CreateLabel(viewRoot.transform, "LobbyResult", new Vector3(0f, 145f, 0f), 900, 58,
            session.ResultText, 28, AccentColor, NGUIText.Alignment.Center);
        CreateLabel(viewRoot.transform, "LobbyScore", new Vector3(0f, 55f, 0f), 900, 70,
            session.LocalPlayerName + "  " + session.LocalScore + " 分    ·    " + session.RemotePlayerName + "  " + session.RemoteScore + " 分",
            22, TextColor, NGUIText.Alignment.Center);
        CreateLabel(viewRoot.transform, "LobbyTime", new Vector3(0f, -20f, 0f), 900, 40,
            FormatTime(session.LocalTime) + "    ·    " + FormatTime(session.RemoteTime), 18, MutedTextColor, NGUIText.Alignment.Center);

        UIButton leaveButton = CreateButton(viewRoot.transform, "FinishedLeaveButton", new Vector3(0f, -180f, 0f), 320, 64, "退出房间", AccentColor);
        leaveButton.onClick.Add(new EventDelegate(LeaveRoom));
    }

    private void BuildBattleHud()
    {
        if (session.State == NetworkBattleSession.BattleState.Finished)
        {
            BuildBattleResult();
            return;
        }

        if (!session.LocalAlive)
        {
            BuildWaitingForResult();
            return;
        }

        CreateViewRoot("NetworkBattleHud", 60);
        const int panelHeight = 250;
        CreateSprite(viewRoot.transform, "HudPanel", new Vector3(625f, 500f - panelHeight * 0.5f, 0f), 500, panelHeight, "Dark", PanelColor, true);
        CreateSprite(viewRoot.transform, "HudAccent", new Vector3(625f, 490f, 0f), 380, 4, "Bright", AccentColor, false);
        CreateLabel(viewRoot.transform, "HudTitle", new Vector3(625f, 452f, 0f), 460, 34, "双人存活竞技", 20, TextColor, NGUIText.Alignment.Center);

        localHudLabel = CreateLabel(viewRoot.transform, "LocalHud", new Vector3(625f, 405f, 0f), 460, 32, "", 16, TextColor, NGUIText.Alignment.Center);
        remoteHudLabel = CreateLabel(viewRoot.transform, "RemoteHud", new Vector3(625f, 365f, 0f), 460, 32, "", 16, TextColor, NGUIText.Alignment.Center);
        hudStateLabel = CreateLabel(viewRoot.transform, "HudState", new Vector3(625f, 325f, 0f), 460, 30, "", 15, MutedTextColor, NGUIText.Alignment.Center);
        hudRuleLabel = CreateLabel(viewRoot.transform, "HudRules", new Vector3(625f, 285f, 0f), 460, 30, "", 15, AccentColor, NGUIText.Alignment.Center);

        RefreshDynamicText();
    }

    private void HideSoloResults()
    {
        if (GameOverPanel.Instance != null) GameOverPanel.Instance.Hideme();
        if (QuitPanel.Instance != null) QuitPanel.Instance.Hideme();
    }

    private void BuildWaitingForResult()
    {
        HideSoloResults();
        CreateViewRoot("NetworkBattleWaitingResult", 90);
        UI2DSprite scrim = CreateSprite(viewRoot.transform, "WaitingResultScrim", Vector3.zero, 4000, 2400,
            "Dark", new Color(1f, 1f, 1f, 0.84f), true);
        CopySpriteStyle(scrim, scrimTemplate);
        scrim.depth = -100;
        CreateSprite(viewRoot.transform, "WaitingResultPanel", Vector3.zero, 1080, 580, "Dark", PanelColor, true);
        CreateLabel(viewRoot.transform, "WaitingResultTitle", new Vector3(0f, 180f, 0f), 940, 64,
            "你已阵亡，等待对局结束", 36, AccentColor, NGUIText.Alignment.Center);
        CreateLabel(viewRoot.transform, "WaitingResultHint", new Vector3(0f, 100f, 0f), 940, 70,
            "本局成绩已保留，请保持连接\n对手结束后将自动显示最终胜负", 21, TextColor, NGUIText.Alignment.Center);
        localHudLabel = CreateLabel(viewRoot.transform, "WaitingLocalScore", new Vector3(0f, 5f, 0f), 940, 42,
            "", 24, TextColor, NGUIText.Alignment.Center);
        remoteHudLabel = CreateLabel(viewRoot.transform, "WaitingRemoteScore", new Vector3(0f, -65f, 0f), 940, 42,
            "", 24, AccentColor, NGUIText.Alignment.Center);
        hudStateLabel = CreateLabel(viewRoot.transform, "WaitingPlayerStatus", new Vector3(0f, -145f, 0f), 940, 38,
            "", 20, MutedTextColor, NGUIText.Alignment.Center);
        CreateLabel(viewRoot.transform, "WaitingResultRule", new Vector3(0f, -220f, 0f), 940, 34,
            "双方阵亡后统一结算 · 分数优先，同分比较生存时间", 17, MutedTextColor, NGUIText.Alignment.Center);
        RefreshDynamicText();
    }

    private void BuildConnectionLost()
    {
        HideSoloResults();
        Time.timeScale = 0f;
        CreateViewRoot("NetworkBattleResult", 90);
        UI2DSprite scrim = CreateSprite(viewRoot.transform, "DisconnectScrim", Vector3.zero, 4000, 2400,
            "Dark", new Color(1f, 1f, 1f, 0.84f), true);
        CopySpriteStyle(scrim, scrimTemplate);
        scrim.depth = -100;
        CreateSprite(viewRoot.transform, "DisconnectPanel", Vector3.zero, 1080, 580, "Dark", PanelColor, true);
        CreateLabel(viewRoot.transform, "DisconnectTitle", new Vector3(0f, 180f, 0f), 900, 64,
            "联机连接已断开", 36, ErrorColor, NGUIText.Alignment.Center);
        CreateLabel(viewRoot.transform, "DisconnectReason", new Vector3(0f, 20f, 0f), 900, 160,
            session.LastError + "\n可直接重试，或返回主菜单重新设置。", 22, TextColor, NGUIText.Alignment.Center);
        CreateButton(viewRoot.transform, "DisconnectRetryButton", new Vector3(-185f, -180f, 0f), 320, 64,
            "重新连接", AccentColor).onClick.Add(new EventDelegate(RetryLastConnection));
        CreateButton(viewRoot.transform, "DisconnectMenuButton", new Vector3(185f, -180f, 0f), 320, 60,
            "返回主菜单", MutedTextColor).onClick.Add(new EventDelegate(ReturnToMenu));
    }

    private void BuildBattleResult()
    {
        HideSoloResults();
        CreateViewRoot("NetworkBattleResult", 90);

        string resultText = session.ResultText ?? string.Empty;
        bool isDraw = resultText == "平局";
        bool localWon = !isDraw && resultText.StartsWith("你获胜");
        Color resultColor = isDraw ? MutedTextColor : (localWon ? AccentColor : ErrorColor);
        string resultTitle = isDraw ? "势均力敌" : (localWon ? "任务胜利" : "任务失败");
        string resultReason = isDraw
            ? "双方最终成绩完全相同"
            : (resultText.Contains("生存时间") ? "同分后按生存时间判定" : "按最终积分判定");

        UI2DSprite scrim = CreateSprite(viewRoot.transform, "ResultScrim", Vector3.zero, 4000, 2400,
            "Dark", new Color(1f, 1f, 1f, 0.84f), true);
        CopySpriteStyle(scrim, scrimTemplate);
        scrim.depth = -100;
        CreateSprite(viewRoot.transform, "ResultPanel", Vector3.zero, 1080, 720, "Dark", PanelColor, true);
        CreateSprite(viewRoot.transform, "ResultAccent", new Vector3(0f, 320f, 0f), 900, 5,
            "Bright", resultColor, false);

        CreateLabel(viewRoot.transform, "ResultEyebrow", new Vector3(0f, 275f, 0f), 900, 32,
            "联机生存竞技 · 对局结算", 17, MutedTextColor, NGUIText.Alignment.Center);
        CreateLabel(viewRoot.transform, "ResultTitle", new Vector3(0f, 215f, 0f), 900, 70,
            resultTitle, 48, resultColor, NGUIText.Alignment.Center);
        CreateLabel(viewRoot.transform, "ResultReason", new Vector3(0f, 155f, 0f), 900, 34,
            resultReason, 17, MutedTextColor, NGUIText.Alignment.Center);
        CreateSprite(viewRoot.transform, "ResultDivider", new Vector3(0f, 112f, 0f), 900, 2,
            "Bright", new Color(MutedTextColor.r, MutedTextColor.g, MutedTextColor.b, 0.42f), false);
        CreateSprite(viewRoot.transform, "ResultColumnDivider", new Vector3(0f, -15f, 0f), 2, 220,
            "Bright", new Color(MutedTextColor.r, MutedTextColor.g, MutedTextColor.b, 0.32f), false);

        string localStatus = isDraw ? "平局" : (localWon ? "胜方" : "败方");
        string remoteStatus = isDraw ? "平局" : (localWon ? "败方" : "胜方");
        Color localColor = isDraw ? MutedTextColor : (localWon ? AccentColor : ErrorColor);
        Color remoteColor = isDraw ? MutedTextColor : (localWon ? ErrorColor : AccentColor);
        BuildResultColumn(-255f, "本机", session.LocalPlayerName, session.LocalScore, session.LocalTime,
            localStatus, localColor, "Local");
        BuildResultColumn(255f, "对手", session.RemotePlayerName, session.RemoteScore, session.RemoteTime,
            remoteStatus, remoteColor, "Remote");

        CreateLabel(viewRoot.transform, "ResultRule", new Vector3(0f, -185f, 0f), 900, 30,
            "分数优先 · 同分比较生存时间", 15, MutedTextColor, NGUIText.Alignment.Center);
        rematchStatusLabel = CreateLabel(viewRoot.transform, "RematchStatus", new Vector3(0f, -215f, 0f), 900, 30,
            "可再次进行同一关卡", 15, MutedTextColor, NGUIText.Alignment.Center);
        UIButton backButton = CreateButton(viewRoot.transform, "ResultBackButton", new Vector3(-190f, -290f, 0f),
            320, 60, "返回任务控制台", MutedTextColor);
        backButton.onClick.Add(new EventDelegate(ReturnToMenu));
        UIButton rematchButton = CreateButton(viewRoot.transform, "ResultRematchButton", new Vector3(190f, -290f, 0f),
            320, 64, "再来一局", AccentColor);
        rematchButton.onClick.Add(new EventDelegate(RequestRematch));
    }

    private void BuildResultColumn(float x, string role, string player, int score, float time,
        string status, Color statusColor, string namePrefix)
    {
        CreateLabel(viewRoot.transform, namePrefix + "ResultRole", new Vector3(x, 75f, 0f), 420, 28,
            role, 15, MutedTextColor, NGUIText.Alignment.Center);
        CreateLabel(viewRoot.transform, namePrefix + "ResultName", new Vector3(x, 38f, 0f), 420, 38,
            string.IsNullOrEmpty(player) ? "Player" : player, 22, TextColor, NGUIText.Alignment.Center);
        CreateLabel(viewRoot.transform, namePrefix + "ResultScore", new Vector3(x, -20f, 0f), 420, 60,
            score + " 分", 34, statusColor, NGUIText.Alignment.Center);
        CreateLabel(viewRoot.transform, namePrefix + "ResultTime", new Vector3(x, -75f, 0f), 420, 32,
            "生存 " + FormatTime(time), 17, MutedTextColor, NGUIText.Alignment.Center);
        CreateLabel(viewRoot.transform, namePrefix + "ResultStatus", new Vector3(x, -125f, 0f), 420, 36,
            status, 20, statusColor, NGUIText.Alignment.Center);
    }

    private UI2DSprite CreateSprite(Transform parent, string name, Vector3 position, int width, int height, string spriteName, Color color, bool addCollider)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.layer = parent.gameObject.layer;

        UI2DSprite sprite = go.AddComponent<UI2DSprite>();
        CopySpriteStyle(sprite, spriteName == "Bright" ? accentTemplate : panelTemplate);
        sprite.type = UIBasicSprite.Type.Sliced;
        sprite.width = width;
        sprite.height = height;
        sprite.color = spriteName == "Bright" ? color : new Color(1f, 1f, 1f, color.a);
        sprite.depth = spriteName == "Bright" ? -10 : -50;

        if (addCollider)
        {
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, height, 0.1f);
        }
        return sprite;
    }

    private static void CopySpriteStyle(UI2DSprite sprite, UI2DSprite source)
    {
        if (source == null)
            return;
        sprite.sprite2D = source.sprite2D;
        sprite.shader = source.shader;
        sprite.border = source.border;
    }

    private UILabel CreateLabel(Transform parent, string name, Vector3 position, int width, int height, string text,
        int fontSize, Color color, NGUIText.Alignment alignment)
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
            label.spacingX = templateLabel.spacingX;
            label.spacingY = templateLabel.spacingY;
            label.fontStyle = templateLabel.fontStyle;
            label.effectStyle = templateLabel.effectStyle;
            label.effectColor = templateLabel.effectColor;
            label.effectDistance = templateLabel.effectDistance;
        }
        label.text = text;
        label.width = width;
        label.height = height;
        label.fontSize = fontSize;
        label.depth = 40;
        label.alignment = alignment;
        label.overflowMethod = UILabel.Overflow.ShrinkContent;
        label.color = color;
        return label;
    }

    private UIInput CreateInput(Transform parent, string name, Vector3 position, int width, int height,
        string hint, string initialValue, int limit)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.layer = parent.gameObject.layer;

        UI2DSprite background = go.AddComponent<UI2DSprite>();
        CopySpriteStyle(background, inputTemplate);
        background.type = UIBasicSprite.Type.Sliced;
        background.width = width;
        background.height = height;
        background.color = Color.white;
        background.depth = 10;

        CreateLabel(parent, name + "Caption", position + new Vector3(-width * 0.5f - 130f, 0f, 0f),
            200, height, hint, 20, MutedTextColor, NGUIText.Alignment.Right);

        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.size = new Vector3(width, height, 0.1f);

        GameObject labelObject = new GameObject(name + "Label");
        labelObject.transform.SetParent(go.transform, false);
        labelObject.layer = go.layer;
        UILabel textLabel = labelObject.AddComponent<UILabel>();
        if (templateLabel != null)
        {
            textLabel.font = templateLabel.font;
            textLabel.trueTypeFont = templateLabel.trueTypeFont;
        }
        textLabel.width = width - 52;
        textLabel.height = height - 8;
        textLabel.fontSize = 20;
        textLabel.pivot = UIWidget.Pivot.Left;
        // NGUI moves a widget when its pivot changes. Position it after setting the pivot.
        labelObject.transform.localPosition = new Vector3(-width * 0.5f + 26f, 0f, -1f);
        textLabel.depth = 20;
        textLabel.alignment = NGUIText.Alignment.Left;
        textLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
        textLabel.color = TextColor;

        UIInput input = go.AddComponent<UIInput>();
        input.label = textLabel;
        input.characterLimit = limit;
        input.defaultText = hint;
        input.defaultColor = MutedTextColor;
        input.activeTextColor = TextColor;
        input.selectAllTextOnFocus = true;
        input.value = initialValue;
        return input;
    }

    private UIButton CreateButton(Transform parent, string name, Vector3 position, int width, int height,
        string text, Color textColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.layer = parent.gameObject.layer;

        UI2DSprite sprite = go.AddComponent<UI2DSprite>();
        if (buttonTemplate != null)
        {
            CopySpriteStyle(sprite, buttonTemplate);
        }
        else
        {
            sprite.sprite2D = FindLoadedSprite("ui_button_primary", "ui_button_secondary", "ui_button_choose");
        }
        if (sprite.sprite2D == null && GameOverPanel.Instance != null && GameOverPanel.Instance.btnRestart != null)
        {
            UI2DSprite sceneButton = GameOverPanel.Instance.btnRestart.GetComponent<UI2DSprite>();
            if (sceneButton != null)
                CopySpriteStyle(sprite, sceneButton);
        }
        if (sprite.sprite2D == null)
            sprite.sprite2D = FindLoadedSprite("ui_button_primary", "ui_button_secondary", "ui_button_choose");
        if (sprite.sprite2D == null && inputTemplate != null)
            CopySpriteStyle(sprite, inputTemplate);
        if (sprite.sprite2D == null && panelTemplate != null)
            CopySpriteStyle(sprite, panelTemplate);
        sprite.type = UIBasicSprite.Type.Sliced;
        sprite.width = width;
        sprite.height = height;
        sprite.color = Color.white;
        sprite.depth = 20;

        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.size = new Vector3(width, height, 0.1f);

        UIButton button = go.AddComponent<UIButton>();
        button.tweenTarget = go;
        button.hover = new Color(0.78f, 1f, 1f, 1f);
        button.pressed = new Color(0.72f, 0.86f, 0.9f, 1f);
        button.pixelSnap = true;

        UILabel label = CreateLabel(go.transform, name + "Label", new Vector3(0f, 0f, -1f), width - 20, height - 10,
            text, 22, textColor, NGUIText.Alignment.Center);
        return button;
    }

    private static Sprite FindLoadedSprite(params string[] preferredNames)
    {
        Sprite[] loaded = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int nameIndex = 0; nameIndex < preferredNames.Length; nameIndex++)
        {
            for (int spriteIndex = 0; spriteIndex < loaded.Length; spriteIndex++)
            {
                if (loaded[spriteIndex].name == preferredNames[nameIndex])
                    return loaded[spriteIndex];
            }
        }

        return null;
    }

    private string RelayNoticeText(string fallback)
    {
        if (relaySettings == null)
            return fallback;
        if (relaySettings.IsTemporaryServer())
            return "当前为临时联网服务；服务重启后需更新 U2-Network.json。";
        return string.IsNullOrWhiteSpace(relaySettings.notice) ? fallback : relaySettings.notice.Trim();
    }

    private void RefreshDynamicText()
    {
        if (session == null)
            return;

        bool connectionBusy = relayHealthChecking ||
            session.State == NetworkBattleSession.BattleState.Hosting ||
            session.State == NetworkBattleSession.BattleState.Connecting;
        if (hostConnectButton != null)
        {
            hostConnectButton.isEnabled = !connectionBusy;
            UILabel label = hostConnectButton.GetComponentInChildren<UILabel>();
            if (label != null)
                label.text = connectionBusy && lastAttemptHost ? "处理中…" : "创建房间";
        }
        if (joinConnectButton != null)
        {
            joinConnectButton.isEnabled = !connectionBusy;
            UILabel label = joinConnectButton.GetComponentInChildren<UILabel>();
            if (label != null)
                label.text = connectionBusy && !lastAttemptHost ? "处理中…" : "加入房间";
        }

        if (lobbyReadyButton != null)
        {
            bool peerJoined = !string.IsNullOrEmpty(session.RemotePlayerName);
            lobbyReadyButton.isEnabled = peerJoined && !session.LocalReady;
            lobbyReadyButton.GetComponentInChildren<UILabel>().text = !peerJoined ? "等待对手加入" :
                (session.LocalReady ? "已准备，等待对手" : "准备选角");
        }

        if (lobbyStateLabel != null)
            lobbyStateLabel.text = session.State == NetworkBattleSession.BattleState.Waiting && string.IsNullOrEmpty(session.RemotePlayerName)
                ? "等待对手加入" : StateText(session.State);
        if (lobbyHintLabel != null)
        {
            if (relayHealthChecking)
                lobbyHintLabel.text = "正在检查中转服务器，请稍候…";
            else if (session.IsRelayRoom)
                lobbyHintLabel.text = string.IsNullOrEmpty(session.RoomCode) ? "正在连接互联网房间服务…" :
                    "房间码：" + session.RoomCode + "\n" + (session.IsHost ? "分享此房间码；对方可从不同网络加入。" : "已加入互联网房间，双方准备后开始。") +
                    "\n" + RelayNoticeText("联网服务已连接。");
            else if (session.IsHost)
            {
                RefreshLocalAddress();
                lobbyHintLabel.text = (string.IsNullOrEmpty(localAddress) ? "未检测到局域网 IP，请检查网络连接。" :
                    "房主 IP：" + localAddress + "    端口：" + session.Port) +
                    "\n把此 IP 发给对方；换网络后地址会更新，防火墙需允许 U2 通信。";
            }
            else
                lobbyHintLabel.text = "房主地址：" + session.ServerAddress + ":" + session.Port +
                    "\n双方准备后先选角色，分别确认后开始对战。";
        }
        if (lobbyPlayersLabel != null)
        {
            string remoteName = string.IsNullOrEmpty(session.RemotePlayerName) ? "等待对手加入" : session.RemotePlayerName;
            lobbyPlayersLabel.text = "本机   " + session.LocalPlayerName + "\n对手   " + remoteName;
        }
        if (lobbyLevelLabel != null)
        {
            LevelInfo level = GameDataMgr.Instance.GetLevelById(session.SelectedLevelId);
            lobbyLevelLabel.text = (session.IsHost ? "比赛关卡：" : "房主选择：") + level.name;
        }
        if (lobbyResultLabel != null)
            lobbyResultLabel.text = session.ResultText;
        if (localHudLabel != null)
            localHudLabel.text = session.LocalPlayerName + "   " + session.LocalScore + " 分   " + FormatTime(session.LocalTime) + "   生命 " + session.LocalHp;
        if (remoteHudLabel != null)
            remoteHudLabel.text = session.RemotePlayerName + "   " + session.RemoteScore + " 分   " + FormatTime(session.RemoteTime) + "   生命 " + session.RemoteHp;
        if (hudStateLabel != null)
        {
            if (session.WaitingForResult)
                hudStateLabel.text = "本机已阵亡 · 等待最终结算 " + Mathf.CeilToInt(session.ResultWaitRemaining) + " 秒";
            else
                hudStateLabel.text = (session.LocalAlive ? "本机存活" : "本机已阵亡") + "    ·    " +
                    (session.RemoteAlive ? "对手存活" : "对手已阵亡");
        }
        if (hudRuleLabel != null)
        {
            hudRuleLabel.text = session.State == NetworkBattleSession.BattleState.Finished
                ? "分数优先 · 同分比较生存时间"
                : "无限时竞技 · 双方阵亡后结算";
        }
        if (hudResultLabel != null)
            hudResultLabel.text = session.ResultText;
        if (rematchStatusLabel != null)
        {
            rematchStatusLabel.text = session.LocalReady
                ? (session.RemoteReady ? "双方已准备，正在重新开始" : "已准备，等待对手")
                : "双方点击再来一局后重新选择角色";
        }
    }

    private void OpenLobby()
    {
        showLobby = true;
    }

    private void CloseLobby()
    {
        if (session.State == NetworkBattleSession.BattleState.Error)
            session.Leave();
        showLobby = false;
        if (BeginPanel.Instance != null)
            BeginPanel.Instance.Showme();
    }

    private void StartHost()
    {
        if (relayHealthChecking || session.State == NetworkBattleSession.BattleState.Hosting ||
            session.State == NetworkBattleSession.BattleState.Connecting)
            return;
        CaptureInputValues();
        lastAttemptHost = true;
        if (publicMode) StartPublicConnect(true);
        else session.Host(ParsePort(), SafePlayerName());
    }

    private void StartJoin()
    {
        if (relayHealthChecking || session.State == NetworkBattleSession.BattleState.Hosting ||
            session.State == NetworkBattleSession.BattleState.Connecting)
            return;
        CaptureInputValues();
        lastAttemptHost = false;
        if (publicMode) StartPublicConnect(false);
        else session.Join(address, ParsePort(), SafePlayerName());
    }

    private void StartPublicConnect(bool host)
    {
        if (relayHealthChecking)
            return;

        NetworkRelaySettings settings = NetworkRelaySettings.Load();
        string validationError;
        if (!settings.TryValidate(out validationError))
        {
            session.ReportClientError(validationError);
            return;
        }
        relaySettings = settings;
        Uri healthEndpoint;
        if (!NetworkRelaySettings.TryHealthEndpoint(settings.serverUrl, out healthEndpoint))
        {
            session.ReportClientError("中转服务器地址无效，请检查 U2-Network.json");
            return;
        }

        relayHealthRoutine = StartCoroutine(CheckRelayAndConnect(healthEndpoint, settings.serverUrl, host));
    }

    private IEnumerator CheckRelayAndConnect(Uri healthEndpoint, string serverUrl, bool host)
    {
        relayHealthChecking = true;
        UnityWebRequest request = UnityWebRequest.Get(healthEndpoint.AbsoluteUri);
        request.timeout = 5;
        yield return request.SendWebRequest();
        relayHealthChecking = false;
        relayHealthRoutine = null;

        if (request.result != UnityWebRequest.Result.Success || request.responseCode < 200 || request.responseCode >= 300)
        {
            session.ReportClientError("中转服务器不可用，请检查服务器地址或网络连接");
            yield break;
        }

        RelayHealth health = null;
        try { health = JsonUtility.FromJson<RelayHealth>(request.downloadHandler.text); }
        catch (Exception) { }
        if (health == null || health.status != "ok" || health.protocol != NetworkBattleSession.ProtocolVersion)
        {
            session.ReportClientError("中转服务器协议不兼容，请更新服务器或游戏程序");
            yield break;
        }
        if (string.IsNullOrWhiteSpace(health.clientVersion) ||
            !string.Equals(health.clientVersion.Trim(), Application.version, StringComparison.OrdinalIgnoreCase))
        {
            session.ReportClientError("服务器要求游戏版本 " + health.clientVersion.Trim() +
                "，当前版本为 " + Application.version);
            yield break;
        }

        if (host)
            session.CreatePublicRoom(serverUrl, SafePlayerName());
        else
            session.JoinPublicRoom(serverUrl, roomCode, SafePlayerName());
    }

    private void UsePublicMode() { ChangeMode(true); }
    private void UseLanMode() { ChangeMode(false); }
    private void ChangeMode(bool usePublic)
    {
        CaptureInputValues();
        publicMode = usePublic;
        if (session.State == NetworkBattleSession.BattleState.Error) session.Leave();
        builtState = (NetworkBattleSession.BattleState)(-1);
    }

    private void RefreshLocalAddress()
    {
        if (Time.unscaledTime < nextAddressRefresh)
            return;
        nextAddressRefresh = Time.unscaledTime + 2f;
        localAddress = NetworkBattleSession.GetPreferredLocalAddress();
    }

    private void CopyHostAddress()
    {
        if (session.IsRelayRoom)
        {
            GUIUtility.systemCopyBuffer = session.RoomCode;
            return;
        }
        nextAddressRefresh = 0f;
        RefreshLocalAddress();
        if (!string.IsNullOrEmpty(localAddress))
            GUIUtility.systemCopyBuffer = localAddress;
    }

    private void SetReady()
    {
        session.SetReady();
    }

    private void PreviousNetworkLevel()
    {
        int next = session.SelectedLevelId - 1;
        if (next < 1)
            next = CampaignProgress.MaxUnlocked;
        session.SelectLevel(next);
    }

    private void NextNetworkLevel()
    {
        int next = session.SelectedLevelId + 1;
        if (next > CampaignProgress.MaxUnlocked)
            next = 1;
        session.SelectLevel(next);
    }

    private void RequestRematch()
    {
        session.RequestRematch();
    }

    private void RetryLastConnection()
    {
        bool host = lastAttemptHost || session.IsHost;
        session.Leave();
        Time.timeScale = 1f;
        if (host)
            StartHost();
        else
            StartJoin();
    }

    private void LeaveRoom()
    {
        session.Leave();
        showLobby = false;
        Time.timeScale = 1f;
        if (BeginPanel.Instance != null)
            BeginPanel.Instance.Showme();
    }

    private void ReturnToMenu()
    {
        session.Leave();
        showLobby = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene("BeginScene");
    }

    private string SafePlayerName()
    {
        if (string.IsNullOrEmpty(playerName))
            playerName = "Player";
        return playerName.Trim().Length == 0 ? "Player" : playerName.Trim();
    }

    private int ParsePort()
    {
        int value;
        return int.TryParse(portText, out value) ? Mathf.Clamp(value, 1024, 65535) : 7777;
    }

    private static string FormatTime(float time)
    {
        int seconds = Mathf.Max(0, Mathf.FloorToInt(time));
        return (seconds / 60) + "m" + (seconds % 60).ToString("00") + "s";
    }

    private static string MatchRulesText()
    {
        return "对局不限时 · 双方阵亡后结算 · 分数优先，同分比生存时间";
    }

    private static string StateText(NetworkBattleSession.BattleState state)
    {
        switch (state)
        {
            case NetworkBattleSession.BattleState.Hosting: return "正在创建房间";
            case NetworkBattleSession.BattleState.Connecting: return "正在连接房间";
            case NetworkBattleSession.BattleState.Waiting: return "等待双方准备";
            case NetworkBattleSession.BattleState.Ready: return "已准备，等待对手";
            case NetworkBattleSession.BattleState.SelectingCharacter: return "选择角色，双方确认后开战";
            case NetworkBattleSession.BattleState.Playing: return "对战进行中";
            case NetworkBattleSession.BattleState.Finished: return "本局对战结束";
            case NetworkBattleSession.BattleState.Error: return "连接失败";
            default: return "尚未连接";
        }
    }
}

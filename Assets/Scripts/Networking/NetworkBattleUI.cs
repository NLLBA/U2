using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    private Image scrimTemplate;
    private Image panelTemplate;
    private Image inputTemplate;
    private Button buttonTemplate;
    private Image accentTemplate;

    private Text templateLabel;
    private InputField nameInput;
    private InputField addressInput;
    private InputField portInput;
    private InputField roomCodeInput;
    private Text lobbyStateLabel;
    private Text lobbyHintLabel;
    private Text lobbyPlayersLabel;
    private Text lobbyResultLabel;
    private Text localHudLabel;
    private Text remoteHudLabel;
    private Text hudStateLabel;
    private Text hudRuleLabel;
    private Text hudResultLabel;
    private Text lobbyLevelLabel;
    private Text rematchStatusLabel;
    private Button lobbyReadyButton;
    private Button hostConnectButton;
    private Button joinConnectButton;

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
        if (nameInput != null && !string.IsNullOrEmpty(nameInput.text))
            playerName = nameInput.text;
        if (addressInput != null)
            address = addressInput.text;
        if (portInput != null && !string.IsNullOrEmpty(portInput.text))
            portText = portInput.text;
        if (roomCodeInput != null)
            roomCode = roomCodeInput.text;
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

        PresentView();
    }

    private void PresentView()
    {
        if (viewRoot == null)
            return;

        // Dynamic screens are built off-screen so their unstyled Image components can
        // never produce a one-frame black flash before the visual system upgrades them.
        UGUIVisualSystem.RefreshNow();
        Canvas.ForceUpdateCanvases();
        viewRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
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
        return UGUIRuntime.Root;
    }

    private void CacheTemplates()
    {
        Transform parent = GetUiParent();
        if (parent == null)
            return;

        scrimTemplate = panelTemplate = inputTemplate = accentTemplate = null;
        buttonTemplate = null;
        Button fallbackButtonTemplate = null;
        // Hidden scene panels own the authored assets. Exclude previous dynamic views,
        // which may still exist until the end of this frame after DestroyView().
        foreach (Image image in parent.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null || image.transform.root.name.StartsWith("NetworkBattle"))
                continue;
            Button candidate = image.GetComponent<Button>();
            if (fallbackButtonTemplate == null && candidate != null && image.sprite.name.StartsWith("ui_button_"))
                fallbackButtonTemplate = candidate;
            switch (image.sprite.name)
            {
                case "ui_scrim": scrimTemplate = image; break;
                case "ui_panel": panelTemplate = image; break;
                case "ui_input": inputTemplate = image; break;
                case "ui_button_primary": buttonTemplate = candidate; break;
                case "ui_accent": accentTemplate = image; break;
            }
        }

        if (buttonTemplate == null)
            buttonTemplate = fallbackButtonTemplate;

        if (BeginPanel.Instance != null)
            templateLabel = UGUIRuntime.Label(BeginPanel.Instance.btnBegin);
        else if (GamePanel.Instance != null)
            templateLabel = GamePanel.Instance.labTime;
    }

    private void CreateViewRoot(string name, int panelDepth)
    {
        Transform parent = GetUiParent();
        if (parent == null)
            return;
        UGUIRuntime.EnsureEventSystem();
        viewRoot = UGUIRuntime.CreateRect(parent, name, Vector3.zero, 1920, 1080).gameObject;
        viewRoot.SetActive(false);
        CacheTemplates();
    }

    private void BuildBeginEntry()
    {
        BeginPanel menu = BeginPanel.Instance;
        if (menu == null || menuEntry != null)
            return;

        // Clone the authored start button to retain its texture, typography and hover animation.
        menuEntry = Instantiate(UGUIRuntime.ControlRoot(menu.btnBegin), UGUIRuntime.View(menu), false);
        menuEntry.name = "NetworkBattleEntryButton";
        UGUIRuntime.SetPosition(menuEntry.transform, new Vector3(625f, 86f, 0f));
        Button button = menuEntry.GetComponentInChildren<Button>(true);
        UGUIRuntime.Label(button).text = "联网模式";
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OpenLobby);
        UGUIRuntime.SetPosition(UGUIRuntime.ControlRoot(menu.btnRank).transform, new Vector3(625f, -18f, 0f));
        UGUIRuntime.SetPosition(UGUIRuntime.ControlRoot(menu.btnSetting).transform, new Vector3(625f, -122f, 0f));
        UGUIRuntime.SetPosition(UGUIRuntime.ControlRoot(menu.btnQuit).transform, new Vector3(625f, -226f, 0f));

        if (!string.IsNullOrEmpty(startupNotice))
        {
            Text template = UGUIRuntime.Label(menu.btnBegin);
            if (template != null)
            {
                recoveryNoticeObject = Instantiate(template.gameObject, UGUIRuntime.View(menu), false);
                recoveryNoticeObject.name = "SaveRecoveryNotice";
                UGUIRuntime.SetPosition(recoveryNoticeObject.transform, new Vector3(0f, -330f, 0f));
                Text notice = recoveryNoticeObject.GetComponent<Text>();
                notice.rectTransform.sizeDelta = new Vector2(980f, 62f);
                notice.fontSize = 16;
                notice.resizeTextForBestFit = true;
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
        CreateSprite(viewRoot.transform, "LobbyScrim", Vector3.zero, 4000, 2400, "Dark", new Color(0f, 0.015f, 0.03f, 0.52f), true);
        CreateSprite(viewRoot.transform, "LobbyPanel", Vector3.zero, 1160, 720, "Dark", PanelColor, true);
        CreateSprite(viewRoot.transform, "LobbyAccent", new Vector3(0f, 215f, 0f), 1000, 4, "Bright", AccentColor, false);

        CreateLabel(viewRoot.transform, "LobbyTitle", new Vector3(0f, 286f, 0f), 900, 52, "联网双人对战", 30, TextColor, TextAnchor.MiddleCenter);
        CreateLabel(viewRoot.transform, "LobbySubtitle", new Vector3(0f, 248f, 0f), 900, 30, "房主选关  ·  独立计分  ·  无尽生存竞技", 16, MutedTextColor, TextAnchor.MiddleCenter);

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
        Button publicButton = CreateButton(viewRoot.transform, "PublicModeButton", new Vector3(-220f, 180f, 0f), 360, 46,
            "互联网房间" + (publicMode ? " · 已选" : ""), publicMode ? AccentColor : MutedTextColor);
        Button lanButton = CreateButton(viewRoot.transform, "LanModeButton", new Vector3(220f, 180f, 0f), 360, 46,
            "局域网直连" + (!publicMode ? " · 已选" : ""), !publicMode ? AccentColor : MutedTextColor);
        ApplySelectionStyle(publicButton, publicMode);
        ApplySelectionStyle(lanButton, !publicMode);
        publicButton.onClick.AddListener(UsePublicMode);
        lanButton.onClick.AddListener(UseLanMode);
        CreateLabel(viewRoot.transform, "FormHint", new Vector3(0f, 120f, 0f), 1000, 52,
            publicMode ? "双方可以使用不同 Wi-Fi 或手机热点。\n房主创建房间后，把 6 位房间码发给另一方即可。" :
            "局域网直连需要连接同一网络。\n另一方输入房主 IP 和相同端口加入；127.0.0.1 仅限本机双开。",
            16, MutedTextColor, TextAnchor.MiddleCenter);

        nameInput = CreateInput(viewRoot.transform, "NameInput", new Vector3(60f, 48f, 0f), 700, 58, "飞行员代号", playerName, 16);
        if (publicMode)
        {
            roomCodeInput = CreateInput(viewRoot.transform, "RoomCodeInput", new Vector3(60f, -28f, 0f), 700, 58, "6 位房间码", roomCode, 6);
            roomCodeInput.contentType = InputField.ContentType.IntegerNumber;
            CreateLabel(viewRoot.transform, "RelayNotice", new Vector3(0f, -105f, 0f), 1000, 50,
                RelayNoticeText("创建房间时无需填写房间码。"),
                16, MutedTextColor, TextAnchor.MiddleCenter);
        }
        else
        {
            addressInput = CreateInput(viewRoot.transform, "AddressInput", new Vector3(60f, -28f, 0f), 700, 58, "房主 IP 地址", address, 32);
            portInput = CreateInput(viewRoot.transform, "PortInput", new Vector3(60f, -104f, 0f), 700, 58, "端口（默认 7777）", portText, 6);
        }

        hostConnectButton = CreateButton(viewRoot.transform, "HostButton", new Vector3(-180f, -176f, 0f), 270, 58, "创建房间", AccentColor);
        joinConnectButton = CreateButton(viewRoot.transform, "JoinButton", new Vector3(180f, -176f, 0f), 270, 58, "加入房间", TextColor);
        hostConnectButton.onClick.AddListener(StartHost);
        joinConnectButton.onClick.AddListener(StartJoin);

        Button closeButton = CreateButton(viewRoot.transform, "CloseButton", new Vector3(0f, -254f, 0f), 220, 50, "返回", MutedTextColor);
        closeButton.onClick.AddListener(CloseLobby);

        if (session.State == NetworkBattleSession.BattleState.Error)
        {
            CreateLabel(viewRoot.transform, "ErrorLabel", new Vector3(0f, -330f, 0f), 1080, 80, "连接失败：" + session.LastError, 17, ErrorColor, TextAnchor.MiddleCenter);
        }
    }

    private void BuildWaitingRoom()
    {
        lobbyStateLabel = CreateLabel(viewRoot.transform, "LobbyState", new Vector3(0f, 200f, 0f), 900, 48,
            StateText(session.State), 24, AccentColor, TextAnchor.MiddleCenter);
        lobbyHintLabel = CreateLabel(viewRoot.transform, "LobbyHint", new Vector3(0f, 145f, 0f), 1080, 72,
            "", 18, MutedTextColor, TextAnchor.MiddleCenter);
        lobbyPlayersLabel = CreateLabel(viewRoot.transform, "LobbyPlayers", new Vector3(0f, 60f, 0f), 900, 70, "", 20, TextColor, TextAnchor.MiddleCenter);
        lobbyLevelLabel = CreateLabel(viewRoot.transform, "LobbyLevel", new Vector3(0f, -5f, 0f), 650, 44,
            "", 20, AccentColor, TextAnchor.MiddleCenter);
        if (session.IsHost)
        {
            Button previous = CreateButton(viewRoot.transform, "PreviousLevelButton", new Vector3(-390f, -5f, 0f), 58, 52, "<", TextColor);
            previous.onClick.AddListener(PreviousNetworkLevel);
            Button next = CreateButton(viewRoot.transform, "NextLevelButton", new Vector3(390f, -5f, 0f), 58, 52, ">", TextColor);
            next.onClick.AddListener(NextNetworkLevel);
        }
        CreateLabel(viewRoot.transform, "WaitingRules", new Vector3(0f, -80f, 0f), 1000, 50,
            MatchRulesText(), 16, MutedTextColor, TextAnchor.MiddleCenter);

        if (session.State == NetworkBattleSession.BattleState.Waiting || session.State == NetworkBattleSession.BattleState.Ready)
        {
            lobbyReadyButton = CreateButton(viewRoot.transform, "ReadyButton", new Vector3(0f, -165f, 0f), 360, 70, "准备开始", AccentColor);
            lobbyReadyButton.onClick.AddListener(SetReady);
        }

        Button leaveButton = CreateButton(viewRoot.transform, "LeaveButton", new Vector3(0f, -270f, 0f), 280, 56, "退出房间", MutedTextColor);
        leaveButton.onClick.AddListener(LeaveRoom);
        if (session.IsHost)
        {
            Button copyButton = CreateButton(viewRoot.transform, "CopyAddressButton", new Vector3(0f, -345f, 0f), 300, 44,
                session.IsRelayRoom ? "复制房间码" : "复制房主 IP", TextColor);
            copyButton.onClick.AddListener(CopyHostAddress);
        }
        RefreshDynamicText();
    }

    private void BuildLobbyFinished()
    {
        lobbyResultLabel = CreateLabel(viewRoot.transform, "LobbyResult", new Vector3(0f, 145f, 0f), 900, 58,
            session.ResultText, 28, AccentColor, TextAnchor.MiddleCenter);
        CreateLabel(viewRoot.transform, "LobbyScore", new Vector3(0f, 55f, 0f), 900, 70,
            session.LocalPlayerName + "  " + session.LocalScore + " 分    ·    " + session.RemotePlayerName + "  " + session.RemoteScore + " 分",
            22, TextColor, TextAnchor.MiddleCenter);
        CreateLabel(viewRoot.transform, "LobbyTime", new Vector3(0f, -20f, 0f), 900, 40,
            FormatTime(session.LocalTime) + "    ·    " + FormatTime(session.RemoteTime), 18, MutedTextColor, TextAnchor.MiddleCenter);

        Button leaveButton = CreateButton(viewRoot.transform, "FinishedLeaveButton", new Vector3(0f, -180f, 0f), 320, 64, "退出房间", AccentColor);
        leaveButton.onClick.AddListener(LeaveRoom);
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
        CreateLabel(viewRoot.transform, "HudTitle", new Vector3(625f, 452f, 0f), 460, 34, "双人存活竞技", 20, TextColor, TextAnchor.MiddleCenter);

        localHudLabel = CreateLabel(viewRoot.transform, "LocalHud", new Vector3(625f, 405f, 0f), 460, 32, "", 16, TextColor, TextAnchor.MiddleCenter);
        remoteHudLabel = CreateLabel(viewRoot.transform, "RemoteHud", new Vector3(625f, 365f, 0f), 460, 32, "", 16, TextColor, TextAnchor.MiddleCenter);
        hudStateLabel = CreateLabel(viewRoot.transform, "HudState", new Vector3(625f, 325f, 0f), 460, 30, "", 15, MutedTextColor, TextAnchor.MiddleCenter);
        hudRuleLabel = CreateLabel(viewRoot.transform, "HudRules", new Vector3(625f, 285f, 0f), 460, 30, "", 15, AccentColor, TextAnchor.MiddleCenter);

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
        CreateSprite(viewRoot.transform, "WaitingResultScrim", Vector3.zero, 4000, 2400,
            "Dark", new Color(0f, 0.015f, 0.03f, 0.78f), true);
        CreateSprite(viewRoot.transform, "WaitingResultPanel", Vector3.zero, 1080, 580, "Dark", PanelColor, true);
        CreateLabel(viewRoot.transform, "WaitingResultTitle", new Vector3(0f, 180f, 0f), 940, 64,
            "你已阵亡，等待对局结束", 36, AccentColor, TextAnchor.MiddleCenter);
        CreateLabel(viewRoot.transform, "WaitingResultHint", new Vector3(0f, 100f, 0f), 940, 70,
            "本局成绩已保留，请保持连接\n对手结束后将自动显示最终胜负", 21, TextColor, TextAnchor.MiddleCenter);
        localHudLabel = CreateLabel(viewRoot.transform, "WaitingLocalScore", new Vector3(0f, 5f, 0f), 940, 42,
            "", 24, TextColor, TextAnchor.MiddleCenter);
        remoteHudLabel = CreateLabel(viewRoot.transform, "WaitingRemoteScore", new Vector3(0f, -65f, 0f), 940, 42,
            "", 24, AccentColor, TextAnchor.MiddleCenter);
        hudStateLabel = CreateLabel(viewRoot.transform, "WaitingPlayerStatus", new Vector3(0f, -145f, 0f), 940, 38,
            "", 20, MutedTextColor, TextAnchor.MiddleCenter);
        CreateLabel(viewRoot.transform, "WaitingResultRule", new Vector3(0f, -220f, 0f), 940, 34,
            "双方阵亡后统一结算 · 分数优先，同分比较生存时间", 17, MutedTextColor, TextAnchor.MiddleCenter);
        RefreshDynamicText();
    }

    private void BuildConnectionLost()
    {
        HideSoloResults();
        Time.timeScale = 0f;
        CreateViewRoot("NetworkBattleResult", 90);
        CreateSprite(viewRoot.transform, "DisconnectScrim", Vector3.zero, 4000, 2400,
            "Dark", new Color(0f, 0.015f, 0.03f, 0.78f), true);
        CreateSprite(viewRoot.transform, "DisconnectPanel", Vector3.zero, 1080, 580, "Dark", PanelColor, true);
        CreateLabel(viewRoot.transform, "DisconnectTitle", new Vector3(0f, 180f, 0f), 900, 64,
            "联机连接已断开", 36, ErrorColor, TextAnchor.MiddleCenter);
        CreateLabel(viewRoot.transform, "DisconnectReason", new Vector3(0f, 20f, 0f), 900, 160,
            session.LastError + "\n可直接重试，或返回主菜单重新设置。", 22, TextColor, TextAnchor.MiddleCenter);
        CreateButton(viewRoot.transform, "DisconnectRetryButton", new Vector3(-185f, -180f, 0f), 320, 64,
            "重新连接", AccentColor).onClick.AddListener(RetryLastConnection);
        CreateButton(viewRoot.transform, "DisconnectMenuButton", new Vector3(185f, -180f, 0f), 320, 60,
            "返回主菜单", MutedTextColor).onClick.AddListener(ReturnToMenu);
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

        CreateSprite(viewRoot.transform, "ResultScrim", Vector3.zero, 4000, 2400,
            "Dark", new Color(0f, 0.015f, 0.03f, 0.78f), true);
        CreateSprite(viewRoot.transform, "ResultPanel", Vector3.zero, 1080, 720, "Dark", PanelColor, true);
        CreateSprite(viewRoot.transform, "ResultAccent", new Vector3(0f, 320f, 0f), 900, 5,
            "Bright", resultColor, false);

        CreateLabel(viewRoot.transform, "ResultEyebrow", new Vector3(0f, 275f, 0f), 900, 32,
            "联机生存竞技 · 对局结算", 17, MutedTextColor, TextAnchor.MiddleCenter);
        CreateLabel(viewRoot.transform, "ResultTitle", new Vector3(0f, 215f, 0f), 900, 70,
            resultTitle, 48, resultColor, TextAnchor.MiddleCenter);
        CreateLabel(viewRoot.transform, "ResultReason", new Vector3(0f, 155f, 0f), 900, 34,
            resultReason, 17, MutedTextColor, TextAnchor.MiddleCenter);
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
            "分数优先 · 同分比较生存时间", 15, MutedTextColor, TextAnchor.MiddleCenter);
        rematchStatusLabel = CreateLabel(viewRoot.transform, "RematchStatus", new Vector3(0f, -215f, 0f), 900, 30,
            "可再次进行同一关卡", 15, MutedTextColor, TextAnchor.MiddleCenter);
        Button backButton = CreateButton(viewRoot.transform, "ResultBackButton", new Vector3(-190f, -290f, 0f),
            320, 60, "返回任务控制台", MutedTextColor);
        backButton.onClick.AddListener(ReturnToMenu);
        Button rematchButton = CreateButton(viewRoot.transform, "ResultRematchButton", new Vector3(190f, -290f, 0f),
            320, 64, "再来一局", AccentColor);
        rematchButton.onClick.AddListener(RequestRematch);
    }

    private void BuildResultColumn(float x, string role, string player, int score, float time,
        string status, Color statusColor, string namePrefix)
    {
        CreateLabel(viewRoot.transform, namePrefix + "ResultRole", new Vector3(x, 75f, 0f), 420, 28,
            role, 15, MutedTextColor, TextAnchor.MiddleCenter);
        CreateLabel(viewRoot.transform, namePrefix + "ResultName", new Vector3(x, 38f, 0f), 420, 38,
            string.IsNullOrEmpty(player) ? "Player" : player, 22, TextColor, TextAnchor.MiddleCenter);
        CreateLabel(viewRoot.transform, namePrefix + "ResultScore", new Vector3(x, -20f, 0f), 420, 60,
            score + " 分", 34, statusColor, TextAnchor.MiddleCenter);
        CreateLabel(viewRoot.transform, namePrefix + "ResultTime", new Vector3(x, -75f, 0f), 420, 32,
            "生存 " + FormatTime(time), 17, MutedTextColor, TextAnchor.MiddleCenter);
        CreateLabel(viewRoot.transform, namePrefix + "ResultStatus", new Vector3(x, -125f, 0f), 420, 36,
            status, 20, statusColor, TextAnchor.MiddleCenter);
    }

    private Image CreateSprite(Transform parent, string name, Vector3 position, int width, int height,
        string spriteName, Color color, bool raycastTarget)
    {
        Image template = spriteName == "Bright" ? accentTemplate :
            (name.IndexOf("Scrim", StringComparison.OrdinalIgnoreCase) >= 0 ? scrimTemplate : panelTemplate);
        return UGUIRuntime.CreateImage(parent, name, position, width, height,
            template, null, color, raycastTarget);
    }

    private Text CreateLabel(Transform parent, string name, Vector3 position, int width, int height, string text,
        int fontSize, Color color, TextAnchor alignment)
    {
        return UGUIRuntime.CreateText(parent, name, position, width, height,
            text, fontSize, color, alignment, templateLabel);
    }

    private InputField CreateInput(Transform parent, string name, Vector3 position, int width, int height,
        string hint, string initialValue, int limit)
    {
        CreateLabel(parent, name + "Caption", position + new Vector3(-width * 0.5f - 130f, 0f, 0f),
            200, height, hint, 20, MutedTextColor, TextAnchor.MiddleRight);
        InputField input = UGUIRuntime.CreateInput(parent, name, position, width, height,
            hint, initialValue, limit, inputTemplate, templateLabel);
        input.textComponent.color = TextColor;
        return input;
    }

    private Button CreateButton(Transform parent, string name, Vector3 position, int width, int height,
        string text, Color textColor)
    {
        return UGUIRuntime.CreateButton(parent, name, position, width, height,
            text, textColor, buttonTemplate, templateLabel);
    }

    private static void ApplySelectionStyle(Button button, bool selected)
    {
        if (button == null || button.targetGraphic == null)
            return;

        UGUIButtonChrome chrome = button.targetGraphic as UGUIButtonChrome;
        if (chrome != null)
        {
            chrome.SetRole(selected ? UGUIChromeRole.Selected : UGUIChromeRole.Secondary);
            button.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
            return;
        }

        Color normal = selected
            ? new Color(0.10f, 0.52f, 0.62f, 1f)
            : new Color(0.12f, 0.25f, 0.31f, 1f);
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.selectedColor = new Color(0.16f, 0.78f, 0.86f, 1f);
        colors.highlightedColor = new Color(0.12f, 0.62f, 0.72f, 1f);
        colors.pressedColor = new Color(0.06f, 0.38f, 0.46f, 1f);
        button.colors = colors;
        button.targetGraphic.CrossFadeColor(normal, 0f, true, true);
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
            hostConnectButton.interactable = !connectionBusy;
            Text label = hostConnectButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = connectionBusy && lastAttemptHost ? "处理中…" : "创建房间";
        }
        if (joinConnectButton != null)
        {
            joinConnectButton.interactable = !connectionBusy;
            Text label = joinConnectButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = connectionBusy && !lastAttemptHost ? "处理中…" : "加入房间";
        }

        if (lobbyReadyButton != null)
        {
            bool peerJoined = !string.IsNullOrEmpty(session.RemotePlayerName);
            lobbyReadyButton.interactable = peerJoined && !session.LocalReady;
            lobbyReadyButton.GetComponentInChildren<Text>().text = !peerJoined ? "等待对手加入" :
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
        RebuildView(SceneManager.GetActiveScene().name, session.State);
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

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 两人联网生存积分对战会话。
/// 公网房间使用加密 WebSocket 中转；局域网直连保留 TCP JSON 协议。
/// </summary>
public class NetworkBattleSession : MonoBehaviour
{
    public const int ProtocolVersion = 1;

    public enum BattleState
    {
        Idle,
        Hosting,
        Connecting,
        Waiting,
        Ready,
        SelectingCharacter,
        Playing,
        Finished,
        Error
    }

    [Serializable]
    private class BattleMessage
    {
        public string type;
        public string playerName;
        public int level;
        public int score;
        public float time;
        public int hp;
        public bool alive;
        public bool ready;
        public string result;
        public string roomCode;
        public int protocol;
        public string clientVersion;
        public string phase;
    }

    private static NetworkBattleSession instance;
    private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<string> outgoing = new ConcurrentQueue<string>();
    private readonly object clientLock = new object();

    private TcpListener listener;
    private TcpClient client;
    private NetworkStream stream;
    private StreamWriter writer;
    private Thread networkThread;
    private volatile bool workerRunning;
    private float nextStateSend;
    private bool localReady;
    private bool remoteReady;
    private bool localReportedDead;
    private bool startRequested;
    private bool sceneLoadRequested;
    private bool resultRecorded;
    private float resultWaitDeadline;
    private string remoteAddress;
    private int port;
    private NetworkRelayClient relay;
    private int connectionGeneration;
    private const float ResultWaitTimeout = 15f;

    public static NetworkBattleSession Instance => instance;
    public BattleState State { get; private set; } = BattleState.Idle;
    public bool IsBattle => State == BattleState.Waiting || State == BattleState.Ready ||
        State == BattleState.SelectingCharacter || State == BattleState.Playing || State == BattleState.Finished;
    public bool IsPlaying => State == BattleState.Playing;
    public bool IsHost { get; private set; }
    public string LocalPlayerName { get; private set; } = "Player";
    public string RemotePlayerName { get; private set; } = "对手";
    public int LocalScore { get; private set; }
    public int RemoteScore { get; private set; }
    public float LocalTime { get; private set; }
    public float RemoteTime { get; private set; }
    public int LocalHp { get; private set; }
    public int RemoteHp { get; private set; }
    public bool LocalAlive { get; private set; } = true;
    public bool RemoteAlive { get; private set; } = true;
    public string LastError { get; private set; }
    public string ResultText { get; private set; }
    public int Port => port;
    public string ServerAddress => remoteAddress;
    public int SelectedLevelId { get; private set; } = 1;
    public bool LocalReady => localReady;
    public bool RemoteReady => remoteReady;
    public bool IsRelayRoom { get; private set; }
    public string RoomCode { get; private set; } = "";
    public bool WaitingForResult => State == BattleState.Playing && !LocalAlive && string.IsNullOrEmpty(ResultText);
    public float ResultWaitRemaining => resultWaitDeadline <= 0f ? 0f : Mathf.Max(0f, resultWaitDeadline - Time.unscaledTime);
    public string ClientVersion => Application.version;
    public static NetworkBattleSession Ensure()
    {
        if (instance != null)
            return instance;

        GameObject host = new GameObject("NetworkBattleSession");
        return host.AddComponent<NetworkBattleSession>();
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

    private void Update()
    {
        DrainIncoming();

        // Also cover gameplay paths that mark the player dead without calling Dead().
        if (State == BattleState.Playing && PlayerObject.Instance != null &&
            PlayerObject.Instance.isDead && !localReportedDead)
        {
            ReportLocalDeath();
        }

        if (State == BattleState.Playing)
        {
            if (!LocalAlive && resultWaitDeadline <= 0f)
                resultWaitDeadline = Time.unscaledTime + ResultWaitTimeout;

            if (IsHost && !LocalAlive && RemoteAlive && resultWaitDeadline > 0f &&
                Time.unscaledTime >= resultWaitDeadline)
            {
                RemoteAlive = false;
                FinishMatch("你获胜（对手状态超时）");
            }

            if (Time.unscaledTime >= nextStateSend)
            {
                nextStateSend = Time.unscaledTime + 0.1f;
                SendLocalState("state");
            }

        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Leave();
        if (instance == this)
            instance = null;
    }

    public void Host(int listenPort, string playerName)
    {
        Leave();
        port = Mathf.Clamp(listenPort, 1024, 65535);
        LocalPlayerName = NormalizeName(playerName);
        IsHost = true;
        LastError = string.Empty;
        ResultText = string.Empty;
        SelectedLevelId = Mathf.Clamp(GameDataMgr.Instance.selectedLevelId, 1, CampaignProgress.MaxUnlocked);
        GameDataMgr.Instance.SetLevel(SelectedLevelId);
        State = BattleState.Hosting;
        workerRunning = true;
        listener = new TcpListener(IPAddress.Any, port);
        try
        {
            listener.Start();
            StartWorker();
            State = BattleState.Waiting;
        }
        catch (Exception ex)
        {
            Fail(ConnectionErrorText(ex, "本机", port));
        }
    }

    public void Join(string address, int targetPort, string playerName)
    {
        Leave();
        remoteAddress = address == null ? string.Empty : address.Trim();
        port = Mathf.Clamp(targetPort, 1024, 65535);
        LocalPlayerName = NormalizeName(playerName);
        IsHost = false;
        LastError = string.Empty;
        ResultText = string.Empty;
        SelectedLevelId = 1;
        IPAddress parsedAddress;
        if (!IPAddress.TryParse(remoteAddress, out parsedAddress) ||
            parsedAddress.AddressFamily != AddressFamily.InterNetwork ||
            parsedAddress.Equals(IPAddress.Any) || parsedAddress.Equals(IPAddress.Broadcast))
        {
            Fail("请输入房主界面显示的 IPv4 地址。\n只有同一台电脑开两个程序时才使用 127.0.0.1。");
            return;
        }
        State = BattleState.Connecting;
        workerRunning = true;
        StartWorker();
    }

    public void SetReady()
    {
        if ((State != BattleState.Waiting && State != BattleState.Ready) || string.IsNullOrEmpty(RemotePlayerName))
            return;

        localReady = true;
        State = remoteReady ? BattleState.Ready : BattleState.Waiting;
        Send(new BattleMessage { type = "ready", playerName = LocalPlayerName, ready = true });
        TryStartMatch();
    }

    public void SelectLevel(int levelId)
    {
        if (!IsHost || (State != BattleState.Waiting && State != BattleState.Ready) || localReady)
            return;

        SelectedLevelId = Mathf.Clamp(levelId, 1, CampaignProgress.MaxUnlocked);
        GameDataMgr.Instance.SetLevel(SelectedLevelId);
        Send(new BattleMessage { type = "level", level = SelectedLevelId, playerName = LocalPlayerName });
    }

    public void ConfirmCharacter()
    {
        if (State != BattleState.SelectingCharacter || localReady)
            return;

        localReady = true;
        Send(new BattleMessage { type = "ready", phase = "character", ready = true });
        TryStartMatch();
    }

    public void RequestRematch()
    {
        if (State != BattleState.Finished || localReady || !workerRunning)
            return;

        localReady = true;
        Send(new BattleMessage { type = "rematch", ready = true, level = SelectedLevelId });
        TryStartMatch();
    }

    public void ReportLocalDeath()
    {
        if (State != BattleState.Playing || localReportedDead)
            return;

        localReportedDead = true;
        LocalAlive = false;
        resultWaitDeadline = Time.unscaledTime + ResultWaitTimeout;
        SendLocalState("dead");
        if (IsHost && !RemoteAlive)
            FinishMatch();
    }

    public void Leave()
    {
        workerRunning = false;
        lock (clientLock)
        {
            connectionGeneration++;
            if (relay != null) relay.Dispose();
            relay = null;
            string stale;
            while (incoming.TryDequeue(out stale)) { }
            while (outgoing.TryDequeue(out stale)) { }
        }
        try
        {
            if (listener != null)
                listener.Stop();
        }
        catch { }

        lock (clientLock)
        {
            try { if (stream != null) stream.Close(); } catch { }
            try { if (client != null) client.Close(); } catch { }
            stream = null;
            client = null;
            writer = null;
        }

        if (networkThread != null && networkThread.IsAlive &&
            Thread.CurrentThread != networkThread)
        {
            if (!networkThread.Join(1000))
                GameLog.Warning(GameLogCategory.Network, "WORKER_TIMEOUT", "old network worker did not stop within 1 second");
        }

        networkThread = null;
        listener = null;
        localReady = false;
        remoteReady = false;
        localReportedDead = false;
        startRequested = false;
        sceneLoadRequested = false;
        resultRecorded = false;
        resultWaitDeadline = 0f;
        LocalAlive = true;
        RemoteAlive = true;
        LocalScore = RemoteScore = 0;
        LocalTime = RemoteTime = 0f;
        RemotePlayerName = string.Empty;
        ResultText = string.Empty;
        SelectedLevelId = 1;
        IsRelayRoom = false;
        RoomCode = "";
        State = BattleState.Idle;
        GameFlowManager.ResetGlobalTimeScale();
    }

    public void CreatePublicRoom(string serverUrl, string playerName)
    {
        ConnectRelay(serverUrl, "", playerName, true);
    }

    public void JoinPublicRoom(string serverUrl, string roomCode, string playerName)
    {
        ConnectRelay(serverUrl, roomCode, playerName, false);
    }

    private void ConnectRelay(string serverUrl, string roomCode, string playerName, bool host)
    {
        Leave();
        IsRelayRoom = true;
        IsHost = host;
        LocalPlayerName = NormalizeName(playerName);
        LastError = ResultText = "";
        remoteAddress = serverUrl;
        RoomCode = roomCode == null ? "" : roomCode.Trim();
        Uri endpoint;
        if (!NetworkRelaySettings.TryEndpoint(serverUrl, out endpoint))
        {
            Fail("公网中转服务尚未配置。请使用包含 U2-Network.json 的完整联网测试包。");
            return;
        }
        if (!host && (RoomCode.Length != 6 || !System.Text.RegularExpressions.Regex.IsMatch(RoomCode, "^[0-9]{6}$")))
        {
            Fail("请输入房主分享的 6 位数字房间码。");
            return;
        }
        SelectedLevelId = host ? Mathf.Clamp(GameDataMgr.Instance.selectedLevelId, 1, CampaignProgress.MaxUnlocked) : 1;
        GameDataMgr.Instance.SetLevel(SelectedLevelId);
        State = host ? BattleState.Hosting : BattleState.Connecting;
        workerRunning = true;
        int generation = connectionGeneration;
        string request = JsonUtility.ToJson(new BattleMessage {
            type = host ? "create" : "join", protocol = ProtocolVersion,
            clientVersion = Application.version, playerName = LocalPlayerName, roomCode = RoomCode
        });
        relay = new NetworkRelayClient(endpoint, request,
            message => EnqueueRelay(generation, message),
            error => EnqueueRelay(generation, JsonUtility.ToJson(new BattleMessage { type = "relay_error", result = error })));
    }

    private void EnqueueRelay(int generation, string message)
    {
        lock (clientLock)
        {
            if (generation == connectionGeneration && workerRunning)
                incoming.Enqueue(message);
        }
    }

    private void StartWorker()
    {
        int generation = connectionGeneration;
        networkThread = new Thread(() => NetworkLoop(generation))
        {
            IsBackground = true,
            Name = "NetworkBattle"
        };
        networkThread.Start();
    }

    private bool IsCurrentConnection(int generation)
    {
        lock (clientLock)
        {
            return workerRunning && generation == connectionGeneration;
        }
    }

    private void NetworkLoop(int generation)
    {
        try
        {
            if (IsHost)
            {
                TcpClient accepted = listener.AcceptTcpClient();
                if (!IsCurrentConnection(generation))
                {
                    accepted.Close();
                    return;
                }
                client = accepted;
                SetupStream(client, generation);
            }
            else
            {
                TcpClient connected = new TcpClient();
                connected.Connect(remoteAddress, port);
                if (!IsCurrentConnection(generation))
                {
                    connected.Close();
                    return;
                }
                client = connected;
                SetupStream(client, generation);
            }

            if (!IsCurrentConnection(generation))
                return;

            outgoing.Enqueue(JsonUtility.ToJson(new BattleMessage
            {
                type = "hello",
                playerName = LocalPlayerName,
                protocol = ProtocolVersion,
                clientVersion = Application.version
            }));
            incoming.Enqueue(JsonUtility.ToJson(new BattleMessage { type = "connected" }));

            byte[] receiveBytes = new byte[4096];
            char[] receiveChars = new char[4096];
            Decoder decoder = Encoding.UTF8.GetDecoder();
            StringBuilder pendingMessage = new StringBuilder();

            while (IsCurrentConnection(generation))
            {
                FlushOutgoing(generation);
                if (stream == null || !stream.CanRead)
                    break;

                if (stream.DataAvailable)
                {
                    int count = stream.Read(receiveBytes, 0, receiveBytes.Length);
                    if (count == 0)
                        break;
                    int characters = decoder.GetChars(receiveBytes, 0, count, receiveChars, 0);
                    for (int index = 0; index < characters; index++)
                    {
                        char character = receiveChars[index];
                        if (character == '\n')
                        {
                            string message = pendingMessage.ToString().TrimEnd('\r');
                            pendingMessage.Length = 0;
                            if (message.Length > 0)
                                incoming.Enqueue(message);
                        }
                        else
                        {
                            pendingMessage.Append(character);
                            if (pendingMessage.Length > 65536)
                                throw new InvalidDataException("网络消息过长");
                        }
                    }
                }
                else if (client.Client.Poll(0, SelectMode.SelectRead))
                    break;

                Thread.Sleep(8);
            }
        }
        catch (Exception ex)
        {
            if (IsCurrentConnection(generation))
                incoming.Enqueue(JsonUtility.ToJson(new BattleMessage
                {
                    type = "error",
                    result = ConnectionErrorText(ex, IsHost ? "本机" : remoteAddress, port)
                }));
        }
        finally
        {
            if (IsCurrentConnection(generation))
                incoming.Enqueue(JsonUtility.ToJson(new BattleMessage { type = "disconnected" }));
        }
    }

    private void SetupStream(TcpClient connected, int generation)
    {
        lock (clientLock)
        {
            if (!workerRunning || generation != connectionGeneration)
            {
                connected.Close();
                return;
            }
            stream = connected.GetStream();
            connected.NoDelay = true;
            writer = new StreamWriter(stream) { AutoFlush = true, NewLine = "\n" };
        }
    }

    private void FlushOutgoing(int generation)
    {
        if (writer == null || !IsCurrentConnection(generation))
            return;

        string line;
        while (outgoing.TryDequeue(out line))
        {
            writer.WriteLine(line);
        }
    }

    private void DrainIncoming()
    {
        string line;
        while (incoming.TryDequeue(out line))
        {
            BattleMessage message;
            try
            {
                message = JsonUtility.FromJson<BattleMessage>(line);
            }
            catch (Exception ex)
            {
                LastError = "消息解析失败：" + ex.Message;
                continue;
            }

            if (message == null || string.IsNullOrEmpty(message.type))
                continue;

            switch (message.type)
            {
                case "created":
                case "joined":
                    RoomCode = message.roomCode;
                    State = BattleState.Waiting;
                    break;
                case "relay_error":
                    if (State != BattleState.Error && State != BattleState.Finished)
                        Fail(RelayErrorText(message.result));
                    break;
                case "connected":
                    State = BattleState.Waiting;
                    break;
                case "hello":
                    if (!CompatiblePeer(message))
                        break;
                    RemotePlayerName = NormalizeName(message.playerName);
                    State = BattleState.Waiting;
                    Send(new BattleMessage { type = "room", playerName = LocalPlayerName, level = SelectedLevelId,
                        protocol = ProtocolVersion, clientVersion = Application.version });
                    break;
                case "room":
                    if (!CompatiblePeer(message))
                        break;
                    RemotePlayerName = NormalizeName(message.playerName);
                    if (!IsHost && message.level > 0)
                    {
                        SelectedLevelId = Mathf.Clamp(message.level, 1, GameDataMgr.MaxLevelId);
                        GameDataMgr.Instance.SetLevel(SelectedLevelId);
                    }
                    State = BattleState.Waiting;
                    break;
                case "level":
                    if (!IsHost && message.level > 0)
                    {
                        SelectedLevelId = Mathf.Clamp(message.level, 1, GameDataMgr.MaxLevelId);
                        GameDataMgr.Instance.SetLevel(SelectedLevelId);
                    }
                    break;
                case "ready":
                    // Lobby readiness and character confirmation are separate barriers.
                    if (message.phase == "character")
                    {
                        if (State != BattleState.SelectingCharacter)
                            break;
                    }
                    else if (State != BattleState.Waiting && State != BattleState.Ready)
                        break;
                    remoteReady = message.ready;
                    if (localReady && State != BattleState.SelectingCharacter)
                        State = BattleState.Ready;
                    TryStartMatch();
                    break;
                case "rematch":
                    if (State == BattleState.Finished)
                    {
                        remoteReady = message.ready;
                        TryStartMatch();
                    }
                    break;
                case "start":
                    StartMatchFromNetwork(message);
                    break;
                case "state":
                case "dead":
                    if (State != BattleState.Playing)
                        break;
                    RemoteScore = message.score;
                    RemoteTime = Mathf.Max(0f, message.time);
                    RemoteHp = Mathf.Max(0, message.hp);
                    RemoteAlive = message.alive && message.type != "dead";
                    if (!RemoteAlive)
                        resultWaitDeadline = 0f;
                    if (IsHost && !LocalAlive && !RemoteAlive)
                        FinishMatch();
                    break;
                case "finish":
                    if (IsHost || State != BattleState.Playing)
                        break;
                    // 房主发送的 score 是房主成绩，hp 字段复用为加入者成绩；
                    // 加入者收到后需要交换本机和对手的显示顺序。
                    LocalScore = message.hp;
                    RemoteScore = message.score;
                    RemoteAlive = message.alive;
                    FinishLocalResult(ResultForClient(message.result));
                    break;
                case "error":
                    Fail(message.result);
                    break;
                case "disconnected":
                    HandleDisconnect();
                    break;
            }
        }
    }

    private bool CompatiblePeer(BattleMessage message)
    {
        if (message == null)
            return false;

        if (message.protocol != ProtocolVersion)
        {
            Fail("双方联网协议版本不一致，请使用同一份游戏程序。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.clientVersion) ||
            !string.Equals(message.clientVersion.Trim(), Application.version, StringComparison.OrdinalIgnoreCase))
        {
            Fail("双方游戏版本不一致：本机 " + Application.version + "，对方 " + message.clientVersion.Trim() + "。");
            return false;
        }

        return true;
    }

    private void TryStartMatch()
    {
        if (!IsHost || startRequested || !localReady || !remoteReady ||
            (State != BattleState.Waiting && State != BattleState.Ready &&
             State != BattleState.Finished && State != BattleState.SelectingCharacter))
            return;

        startRequested = true;
        if (State == BattleState.SelectingCharacter)
        {
            Send(new BattleMessage { type = "start", phase = "battle", level = SelectedLevelId });
            BeginMatch();
        }
        else
        {
            Send(new BattleMessage { type = "start", phase = "character", level = SelectedLevelId });
            BeginCharacterSelection();
        }
    }

    private void StartMatchFromNetwork(BattleMessage message)
    {
        if (IsHost)
            return;
        if (message.phase == "character")
        {
            if (State != BattleState.Waiting && State != BattleState.Ready && State != BattleState.Finished)
                return;
        }
        else if (message.phase != "battle" || State != BattleState.SelectingCharacter || !localReady)
            return;

        if (message.level > 0)
        {
            SelectedLevelId = Mathf.Clamp(message.level, 1, GameDataMgr.MaxLevelId);
            GameDataMgr.Instance.SetLevel(SelectedLevelId);
        }

        if (message.phase == "character")
            BeginCharacterSelection();
        else
            BeginMatch();
    }

    private void BeginCharacterSelection()
    {
        State = BattleState.SelectingCharacter;
        localReady = false;
        remoteReady = false;
        startRequested = false;
        Time.timeScale = 1f;
        if (SceneManager.GetActiveScene().name != "BeginScene")
            SceneManager.LoadScene("BeginScene");
    }

    private void BeginMatch()
    {
        State = BattleState.Playing;
        GameDataMgr.Instance.SetLevel(SelectedLevelId);
        LocalAlive = true;
        RemoteAlive = true;
        localReportedDead = false;
        nextStateSend = 0f;
        localReady = false;
        remoteReady = false;
        ResultText = string.Empty;
        LocalScore = RemoteScore = 0;
        LocalTime = RemoteTime = 0f;
        resultRecorded = false;
        resultWaitDeadline = 0f;
        Time.timeScale = 1f;

        if (!sceneLoadRequested)
        {
            sceneLoadRequested = true;
            SceneManager.LoadScene("GameScene");
        }
    }

    private void SendLocalState(string type)
    {
        ScoreManager score = ScoreManager.Instance;
        GameFlowManager flow = GameFlowManager.Instance;
        PlayerObject player = PlayerObject.Instance;

        LocalScore = score != null ? score.TotalScore : LocalScore;
        LocalTime = flow != null ? flow.RunTime : LocalTime;
        LocalHp = player != null ? Mathf.Max(0, player.nowHp) : LocalHp;
        LocalAlive = player == null || !player.isDead;

        Send(new BattleMessage
        {
            type = type,
            playerName = LocalPlayerName,
            score = LocalScore,
            time = LocalTime,
            hp = LocalHp,
            alive = LocalAlive
        });
    }

    private void FinishMatch(string forcedResult = null)
    {
        if (!IsHost || State != BattleState.Playing)
            return;

        SendLocalState("state");
        string result;
        if (!string.IsNullOrEmpty(forcedResult))
        {
            result = forcedResult;
        }
        else if (!LocalAlive && RemoteAlive)
        {
            result = "你获胜（对手已出局）";
        }
        else if (LocalAlive && !RemoteAlive)
        {
            result = "对手获胜";
        }
        else if (LocalScore == RemoteScore)
        {
            if (Mathf.Approximately(LocalTime, RemoteTime))
                result = "平局";
            else
                result = LocalTime > RemoteTime ? "你获胜（生存时间更长）" : "对手获胜（生存时间更长）";
        }
        else
        {
            result = LocalScore > RemoteScore ? "你获胜" : "对手获胜";
        }

        ResultText = result;
        State = BattleState.Finished;
        localReady = false;
        remoteReady = false;
        startRequested = false;
        resultWaitDeadline = 0f;
        RecordResult();
        Send(new BattleMessage
        {
            type = "finish",
            result = result,
            score = LocalScore,
            hp = RemoteScore,
            alive = LocalAlive
        });
        Time.timeScale = 0f;
    }

    private void FinishLocalResult(string result)
    {
        if (State != BattleState.Playing)
            return;
        ResultText = string.IsNullOrWhiteSpace(result) ? "对局结束" : result;
        State = BattleState.Finished;
        localReady = false;
        remoteReady = false;
        startRequested = false;
        resultWaitDeadline = 0f;
        RecordResult();
        Time.timeScale = 0f;
    }

    private void HandleDisconnect()
    {
        if (State == BattleState.Finished || State == BattleState.Idle || State == BattleState.Error)
            return;
        if (State == BattleState.Playing)
        {
            RemoteAlive = false;
            string result = IsHost ? "你获胜（对手已断线）" : "你获胜（房主已断线）";
            if (IsHost)
                FinishMatch(result);
            else
                FinishLocalResult(result);
            return;
        }
        Fail("对手已断开连接");
    }

    private void RecordResult()
    {
        if (resultRecorded)
            return;
        resultRecorded = true;
        if (ResultText.Contains("你获胜"))
        {
            CampaignProgress.RecordNetworkResult(true, false, LocalScore);
            return;
        }
        if (ResultText.Contains("对手获胜"))
        {
            CampaignProgress.RecordNetworkResult(false, false, LocalScore);
            return;
        }
        if (ResultText.Contains("平局"))
        {
            CampaignProgress.RecordNetworkResult(false, true, LocalScore);
            return;
        }
        bool draw = ResultText == "平局";
        bool won = !draw && !string.IsNullOrEmpty(ResultText) && ResultText.StartsWith("你获胜");
        CampaignProgress.RecordNetworkResult(won, draw, LocalScore);
    }

    private string ResultForClient(string hostResult)
    {
        if (hostResult == "平局")
            return hostResult;
        if (hostResult.StartsWith("你获胜"))
            return hostResult.Replace("你获胜", "对手获胜");
        if (hostResult.StartsWith("对手获胜"))
            return hostResult.Replace("对手获胜", "你获胜");
        return hostResult;
    }

    public void ReportClientError(string message)
    {
        Fail(message);
    }

    private void Fail(string message)
    {
        string detail = string.IsNullOrWhiteSpace(message) ? "联网连接失败" : message.Trim();
        LastError = detail.StartsWith("[NET-", StringComparison.Ordinal)
            ? detail
            : "[" + ErrorCodeFor(detail) + "] " + detail;
        GameLog.Warning(GameLogCategory.Network, "ERROR", LastError);
        State = BattleState.Error;
        workerRunning = false;
        if (relay != null) relay.Dispose();
    }

    private static string ErrorCodeFor(string message)
    {
        string value = (message ?? string.Empty).ToLowerInvariant();
        if (value.Contains("房间") || value.Contains("room")) return "NET-101";
        if (value.Contains("版本") || value.Contains("协议") || value.Contains("version") || value.Contains("protocol")) return "NET-103";
        if (value.Contains("断开") || value.Contains("掉线") || value.Contains("disconnect")) return "NET-104";
        if (value.Contains("防火墙") || value.Contains("ip") || value.Contains("端口") || value.Contains("地址")) return "NET-105";
        if (value.Contains("服务器") || value.Contains("中转") || value.Contains("server")) return "NET-102";
        return "NET-100";
    }

    private static string RelayErrorText(string code)
    {
        switch (code)
        {
            case "room_not_found": return "找不到这个房间。请核对房间码，并确认房主没有退出房间。";
            case "room_full": return "房间已有两名玩家，请创建另一个房间。";
            case "version_mismatch": return "游戏版本不一致，请双方使用同一个新版测试包。";
            case "server_full": return "中转服务已满，请稍后重试。";
            case "rate_limit": return "操作太频繁，请一分钟后再试。";
            case "invalid_message": return "联网消息不兼容，请双方使用同一个新版测试包。";
            default: return code;
        }
    }

    private static string ConnectionErrorText(Exception exception, string address, int targetPort)
    {
        SocketException socket = exception as SocketException;
        if (socket == null)
            return exception.Message;

        string target = address + ":" + targetPort;
        switch (socket.SocketErrorCode)
        {
            case SocketError.ConnectionRefused:
                return "连接被拒绝：" + target + "\n请先让房主创建房间，并核对 IP、端口和防火墙设置。（" + socket.ErrorCode + "）";
            case SocketError.TimedOut:
            case SocketError.HostUnreachable:
            case SocketError.NetworkUnreachable:
                return "无法访问房主：" + target + "\n请连接同一手机热点，核对房主当前 IP，并允许 U2 通过防火墙。（" + socket.ErrorCode + "）";
            case SocketError.AddressAlreadyInUse:
                return "端口 " + targetPort + " 已被占用。\n请退出旧房间，或让双方改用同一个新端口。";
            case SocketError.AccessDenied:
                return "系统拒绝网络访问。\n请检查防火墙是否阻止 U2。（" + socket.ErrorCode + "）";
            default:
                return target + "：" + socket.Message + "（" + socket.ErrorCode + "）";
        }
    }

    public static string GetPreferredLocalAddress()
    {
        string fallback = string.Empty;
        try
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                IPInterfaceProperties properties = adapter.GetIPProperties();
                bool hasGateway = false;
                foreach (GatewayIPAddressInformation gateway in properties.GatewayAddresses)
                {
                    if (gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !gateway.Address.Equals(IPAddress.Any))
                        hasGateway = true;
                }

                foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
                {
                    IPAddress local = unicast.Address;
                    if (local.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(local) ||
                        local.ToString().StartsWith("169.254."))
                        continue;
                    if (hasGateway)
                        return local.ToString();
                    if (fallback.Length == 0)
                        fallback = local.ToString();
                }
            }
        }
        catch (NetworkInformationException) { }
        return fallback;
    }

    private void Send(BattleMessage message)
    {
        if (!workerRunning || message == null)
            return;

        string json = JsonUtility.ToJson(message);
        if (IsRelayRoom && relay != null) relay.Send(json);
        else outgoing.Enqueue(json);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        sceneLoadRequested = false;
        if (scene.name == "GameScene" && State == BattleState.Playing)
            Time.timeScale = 1f;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Player";

        return value.Trim().Substring(0, Mathf.Min(12, value.Trim().Length));
    }
}

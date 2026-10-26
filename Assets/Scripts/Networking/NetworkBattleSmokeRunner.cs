using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives two built players through the public-room flow when explicitly enabled
/// by Tools/VerifyNetworkPlayer.ps1. Normal game launches never create this object.
/// </summary>
public sealed class NetworkBattleSmokeRunner : MonoBehaviour
{
    private const float TestTimeoutSeconds = 75f;
    private const float FirstDeathDelaySeconds = 0.75f;
    private const float SecondDeathDelaySeconds = 2f;
    private const float HostResultFlushSeconds = 2f;
    private const float ClientResultFlushSeconds = 0.5f;

    private static bool started;

    private string role;
    private string relayUrl;
    private string roomCode;
    private string firstDeathRole;
    private float startedAt;
    private float battleStartedAt = -1f;
    private float resultSeenAt = -1f;
    private bool roomLogged;
    private bool lobbyReadySent;
    private bool characterReadySent;
    private bool deathSent;
    private bool resultLogged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (started)
            return;

        string smokeRole = ArgumentValue("-u2-network-smoke");
        if (smokeRole != "host" && smokeRole != "client")
            return;

        started = true;
        GameObject runnerObject = new GameObject("NetworkBattleSmokeRunner");
        DontDestroyOnLoad(runnerObject);
        NetworkBattleSmokeRunner runner = runnerObject.AddComponent<NetworkBattleSmokeRunner>();
        runner.role = smokeRole;
        runner.relayUrl = ArgumentValue("-u2-relay-smoke");
        runner.roomCode = ArgumentValue("-u2-room-code");
        runner.firstDeathRole = ArgumentValue("-u2-first-death");
        if (runner.firstDeathRole != "host" && runner.firstDeathRole != "client")
            runner.firstDeathRole = "client";
    }

    private void Start()
    {
        Application.runInBackground = true;
        startedAt = Time.realtimeSinceStartup;

        NetworkBattleSession session = NetworkBattleSession.Ensure();
        if (!string.IsNullOrWhiteSpace(relayUrl))
        {
            if (role == "host")
                session.CreatePublicRoom(relayUrl, "SmokeHost");
            else if (string.IsNullOrWhiteSpace(roomCode))
                Fail("client room code is missing");
            else
                session.JoinPublicRoom(relayUrl, roomCode, "SmokeClient");
        }
        else if (role == "host")
        {
            session.Host(7777, "SmokeHost");
            if (session.State != NetworkBattleSession.BattleState.Error)
                Debug.Log("U2_NETWORK_SMOKE_LISTENING");
        }
        else
        {
            session.Join("127.0.0.1", 7777, "SmokeClient");
        }
    }

    private void Update()
    {
        NetworkBattleSession session = NetworkBattleSession.Instance;
        if (session == null)
        {
            Fail("network session was not created");
            return;
        }

        if (Time.realtimeSinceStartup - startedAt > TestTimeoutSeconds)
        {
            Fail("timed out in state " + session.State);
            return;
        }

        if (session.State == NetworkBattleSession.BattleState.Error)
        {
            Fail("session error: " + session.LastError);
            return;
        }

        LogRelayRoomWhenReady(session);
        ReadyInLobby(session);
        ConfirmCharacter(session);
        DriveBattle(session);
        FinishWhenResultArrives(session);
    }

    private void LogRelayRoomWhenReady(NetworkBattleSession session)
    {
        if (roomLogged || role != "host" || string.IsNullOrWhiteSpace(relayUrl) ||
            string.IsNullOrWhiteSpace(session.RoomCode))
            return;

        roomLogged = true;
        Debug.Log("U2_RELAY_SMOKE_ROOM " + session.RoomCode);
    }

    private void ReadyInLobby(NetworkBattleSession session)
    {
        if (lobbyReadySent || string.IsNullOrWhiteSpace(session.RemotePlayerName) ||
            (session.State != NetworkBattleSession.BattleState.Waiting &&
             session.State != NetworkBattleSession.BattleState.Ready))
            return;

        lobbyReadySent = true;
        session.SetReady();
    }

    private void ConfirmCharacter(NetworkBattleSession session)
    {
        if (characterReadySent || session.State != NetworkBattleSession.BattleState.SelectingCharacter)
            return;

        characterReadySent = true;
        GameDataMgr.Instance.heroIndex = role == "host" ? 0 : 1;
        session.ConfirmCharacter();
    }

    private void DriveBattle(NetworkBattleSession session)
    {
        if (session.State != NetworkBattleSession.BattleState.Playing)
            return;

        if (battleStartedAt < 0f)
            battleStartedAt = Time.realtimeSinceStartup;

        if (deathSent || SceneManager.GetActiveScene().name != "GameScene" || PlayerObject.Instance == null)
            return;

        bool diesFirst = role == firstDeathRole;
        float deathDelay = diesFirst ? FirstDeathDelaySeconds : SecondDeathDelaySeconds;
        if (Time.realtimeSinceStartup - battleStartedAt < deathDelay)
            return;

        deathSent = true;
        PlayerObject.Instance.nowHp = 0;
        PlayerObject.Instance.Dead();
    }

    private void FinishWhenResultArrives(NetworkBattleSession session)
    {
        if (session.State != NetworkBattleSession.BattleState.Finished ||
            string.IsNullOrWhiteSpace(session.ResultText))
            return;

        if (!resultLogged)
        {
            resultLogged = true;
            resultSeenAt = Time.realtimeSinceStartup;
            Debug.Log("U2_NETWORK_SMOKE_PASS role=" + role +
                " result=" + session.ResultText.Replace('\n', ' ') +
                " localScore=" + session.LocalScore +
                " remoteScore=" + session.RemoteScore);
        }

        float flushDelay = role == "host" ? HostResultFlushSeconds : ClientResultFlushSeconds;
        if (Time.realtimeSinceStartup - resultSeenAt >= flushDelay)
            Application.Quit(0);
    }

    private void Fail(string reason)
    {
        if (!enabled)
            return;

        enabled = false;
        Debug.LogError("U2_NETWORK_SMOKE_FAIL role=" + role + " reason=" + reason);
        Application.Quit(2);
    }

    private static string ArgumentValue(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index + 1 < arguments.Length; index++)
        {
            if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                return arguments[index + 1].Trim();
        }

        return string.Empty;
    }
}

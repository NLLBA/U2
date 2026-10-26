using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class NetworkBattleConnectionTests
{
    [TestCase("wss://example.com/relay", true)]
    [TestCase("ws://127.0.0.1:8787/relay", true)]
    [TestCase("ws://example.com/relay", false)]
    [TestCase("https://example.com/relay", false)]
    [TestCase("wss://user:password@example.com/relay", false)]
    [TestCase("", false)]
    public void PublicRelayRequiresEncryptedEndpointExceptLoopback(string value, bool valid)
    {
        Uri endpoint;
        Assert.AreEqual(valid, NetworkRelaySettings.TryEndpoint(value, out endpoint));
    }

    [Test]
    public void TemporaryRelayMustBeExplicitlyAllowed()
    {
        var settings = new NetworkRelaySettings
        {
            serverUrl = "wss://temporary.trycloudflare.com/relay",
            requiredClientVersion = Application.version,
            allowQuickTunnel = false
        };
        string error;
        Assert.IsFalse(settings.TryValidate(out error));
        StringAssert.Contains("Quick Tunnel", error);

        settings.allowQuickTunnel = true;
        Assert.IsTrue(settings.TryValidate(out error), error);
    }

    [Test]
    public void PeerWithDifferentGameVersionIsRejected()
    {
        NetworkBattleSession session = NetworkBattleSession.Ensure();
        try
        {
            var queue = (ConcurrentQueue<string>)typeof(NetworkBattleSession)
                .GetField("incoming", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
            queue.Enqueue("{\"type\":\"hello\",\"playerName\":\"OldClient\",\"protocol\":1,\"clientVersion\":\"0.9.0\"}");
            typeof(NetworkBattleSession).GetMethod("DrainIncoming", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(session, null);
            Assert.AreEqual(NetworkBattleSession.BattleState.Error, session.State);
            StringAssert.Contains("版本不一致", session.LastError);
        }
        finally { UnityEngine.Object.DestroyImmediate(session.gameObject); }
    }

    [Test]
    public void PublicJoinRequiresRoomCodeAndLeaveDiscardsQueuedMessages()
    {
        NetworkBattleSession session = NetworkBattleSession.Ensure();
        try
        {
            session.JoinPublicRoom("wss://example.com/relay", "12", "Test");
            Assert.AreEqual(NetworkBattleSession.BattleState.Error, session.State);
            StringAssert.Contains("6 位", session.LastError);
            var queue = (ConcurrentQueue<string>)typeof(NetworkBattleSession)
                .GetField("incoming", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
            queue.Enqueue("{\"type\":\"relay_error\",\"result\":\"room_not_found\"}");
            session.Leave();
            typeof(NetworkBattleSession).GetMethod("DrainIncoming", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(session, null);
            Assert.AreEqual(NetworkBattleSession.BattleState.Idle, session.State);
            Assert.IsFalse(session.IsRelayRoom);
        }
        finally { UnityEngine.Object.DestroyImmediate(session.gameObject); }
    }

    [Test]
    public void EmptyAddressDoesNotSilentlyJoinThisComputer()
    {
        NetworkBattleSession session = NetworkBattleSession.Ensure();
        try
        {
            session.Join("", 7777, "Test");
            Assert.AreEqual(NetworkBattleSession.BattleState.Error, session.State);
            StringAssert.Contains("IPv4", session.LastError);
        }
        finally { UnityEngine.Object.DestroyImmediate(session.gameObject); }
    }

    [TestCase("error", "disconnected")]
    [TestCase("relay_error", "relay_error")]
    public void DisconnectNoticePreservesTheOriginalConnectionError(string firstType, string secondType)
    {
        NetworkBattleSession session = NetworkBattleSession.Ensure();
        try
        {
            var queue = (ConcurrentQueue<string>)typeof(NetworkBattleSession)
                .GetField("incoming", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
            queue.Enqueue("{\"type\":\"" + firstType + "\",\"result\":\"Connection refused: 10061\"}");
            queue.Enqueue("{\"type\":\"" + secondType + "\"}");
            typeof(NetworkBattleSession).GetMethod("DrainIncoming", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(session, null);
            Assert.AreEqual(NetworkBattleSession.BattleState.Error, session.State);
            StringAssert.StartsWith("[NET-", session.LastError);
            StringAssert.Contains("Connection refused: 10061", session.LastError);
        }
        finally { UnityEngine.Object.DestroyImmediate(session.gameObject); }
    }

    [UnityTest]
    public IEnumerator RefusedTcpConnectionReportsAddressAndReason()
    {
        var temporaryListener = new TcpListener(IPAddress.Loopback, 0);
        temporaryListener.Start();
        int unusedPort = ((IPEndPoint)temporaryListener.LocalEndpoint).Port;
        temporaryListener.Stop();
        NetworkBattleSession session = NetworkBattleSession.Ensure();
        try
        {
            session.Join("127.0.0.1", unusedPort, "Test");
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            MethodInfo drain = typeof(NetworkBattleSession).GetMethod("DrainIncoming", BindingFlags.NonPublic | BindingFlags.Instance);
            while (session.State != NetworkBattleSession.BattleState.Error && DateTime.UtcNow < deadline)
            {
                drain.Invoke(session, null);
                yield return null;
            }
            Assert.AreEqual(NetworkBattleSession.BattleState.Error, session.State);
            StringAssert.Contains("127.0.0.1:" + unusedPort, session.LastError);
            StringAssert.Contains("连接被拒绝", session.LastError);
        }
        finally { UnityEngine.Object.DestroyImmediate(session.gameObject); }
    }
}

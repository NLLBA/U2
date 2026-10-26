using System;
using System.IO;
using UnityEngine;

[Serializable]
public class NetworkRelaySettings
{
    public int configVersion = 1;
    public string environment = "production";
    public string serverUrl = "";
    public string notice = "";
    public bool allowQuickTunnel = false;
    public string requiredClientVersion = "";

    public static NetworkRelaySettings Load()
    {
        // The file next to U2.exe can be updated without rebuilding the game.
        string playerDirectory = Path.GetDirectoryName(Application.dataPath);
        string path = Path.Combine(playerDirectory, "U2-Network.json");
        try
        {
            if (File.Exists(path))
            {
                NetworkRelaySettings loaded = JsonUtility.FromJson<NetworkRelaySettings>(File.ReadAllText(path));
                return loaded ?? new NetworkRelaySettings();
            }
        }
        catch (Exception ex) { GameLog.Warning(GameLogCategory.Network, "CONFIG_READ", "读取联网配置失败：" + ex.Message); }
        return new NetworkRelaySettings();
    }

    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (configVersion != 1)
        {
            error = "联网配置版本不受支持，请使用项目附带的 U2-Network.json。";
            return false;
        }

        Uri endpoint;
        if (!TryEndpoint(serverUrl, out endpoint))
        {
            error = "联网服务器地址无效，应填写 wss://域名/relay。";
            return false;
        }

        if (IsTemporaryEndpoint(endpoint) && !allowQuickTunnel)
        {
            error = "当前配置仍是临时 Quick Tunnel，请换成正式 WSS 地址。";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(requiredClientVersion) &&
            !string.Equals(requiredClientVersion.Trim(), Application.version, StringComparison.OrdinalIgnoreCase))
        {
            error = "当前游戏版本为 " + Application.version + "，服务器要求 " + requiredClientVersion.Trim() + "。";
            return false;
        }

        return true;
    }

    public bool IsTemporaryServer()
    {
        Uri endpoint;
        return Uri.TryCreate(serverUrl == null ? "" : serverUrl.Trim(), UriKind.Absolute, out endpoint) &&
            IsTemporaryEndpoint(endpoint);
    }

    private static bool IsTemporaryEndpoint(Uri endpoint)
    {
        return endpoint != null && endpoint.Host.EndsWith("trycloudflare.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryEndpoint(string value, out Uri endpoint)
    {
        if (!Uri.TryCreate(value == null ? "" : value.Trim(), UriKind.Absolute, out endpoint))
            return false;
        // Unencrypted WebSockets are only allowed for the local verification server.
        return (endpoint.Scheme == "wss" || (endpoint.Scheme == "ws" && endpoint.IsLoopback)) &&
            string.IsNullOrEmpty(endpoint.UserInfo) && string.IsNullOrEmpty(endpoint.Fragment);
    }

    public static bool TryHealthEndpoint(string value, out Uri healthEndpoint)
    {
        healthEndpoint = null;
        Uri endpoint;
        if (!TryEndpoint(value, out endpoint))
            return false;

        UriBuilder builder = new UriBuilder(endpoint)
        {
            Scheme = endpoint.Scheme == "wss" ? "https" : "http",
            Path = "/health",
            Query = string.Empty,
            Fragment = string.Empty
        };
        healthEndpoint = builder.Uri;
        return true;
    }
}

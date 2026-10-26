using UnityEngine;

/// <summary>
/// 保证从任意场景启动时都能打开联网大厅。
/// </summary>
public static class NetworkBattleBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        NetworkBattleSession.Ensure();
        NetworkBattleUI.Ensure();
    }
}
